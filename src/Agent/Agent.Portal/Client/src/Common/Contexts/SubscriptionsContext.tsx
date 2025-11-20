import { ReactNode, createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { SubscriptionClient } from '../Clients/SubscriptionClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { Subscription } from '../Contracts/Arm';
import { LogLevel } from '../Contracts/Telemetry';
import { useLocalStorage } from '../Hooks/useLocalStorage';
import { useTelemetry } from '../Hooks/useTelemetry';
import { getArmErrorMessage } from '../Utilities/Client';
import { useAuth } from './AuthContext';
import { useNotifications } from './NotificationContext';

export interface SubscriptionFilter {
    state?: string[];
    /** Search term to filter by name or ID */
    searchTerm?: string;
    tenantIds?: string[];
    /** Custom filter function */
    customFilter?: (sub: Subscription) => boolean;
}

interface SubscriptionsContextValue {
    // State
    subscriptions: Subscription[];
    selectedSubscriptions: Subscription[];
    isLoading: boolean;
    error: string | null;

    // Actions
    setSelectedSubscriptions: (ids: string[]) => void;
    toggleSubscription: (id: string) => void;
    selectAll: () => void;
    clearSelection: () => void;
    refresh: () => Promise<void>;

    // Queries
    filterSubscriptions: (filter: SubscriptionFilter) => Subscription[];
    searchSubscriptions: (term: string) => Subscription[];
    isSelected: (id: string) => boolean;
    getSubscriptionById: (id: string) => Subscription | undefined;

    // Metadata
    totalCount: number;
    selectedCount: number;
    isAllSelected: boolean;
}

const SubscriptionsContext = createContext<SubscriptionsContextValue | undefined>(undefined);

const MAX_SELECTED_SUBSCRIPTIONS = 100;
const STORAGE_KEY_PREFIX = 'sre-agent-portal-subscriptions';
const defaultSelectedSubscriptionsValue: string[] = [];

export const SubscriptionsProvider = ({ children }: { children: ReactNode }) => {
    const { isAuthenticated, user } = useAuth();
    const { error: notifyError } = useNotifications();
    const { logEvent } = useTelemetry(TelemetrySource.SubscriptionsManager, undefined);

    // Tenant-scoped storage key
    const storageKey = useMemo(() => `${STORAGE_KEY_PREFIX}-${user?.tenantId || 'default'}`, [user?.tenantId]);

    const { value: storedSubscriptionIds, setValue: setStoredSubscriptionIds } = useLocalStorage<string[]>(
        storageKey,
        defaultSelectedSubscriptionsValue,
        TelemetrySource.SubscriptionsManager
    );

    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<Set<string>>(new Set());
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const subscriptionClient = useMemo(() => SubscriptionClient.getInstance(TelemetrySource.SubscriptionsManager), []);

    const fetchSubscriptions = useCallback(async (): Promise<{ subscriptions?: Subscription[]; error?: string }> => {
        if (!isAuthenticated) {
            return { error: 'User is not authenticated' };
        }

        logEvent({
            action: 'fetch-subscriptions',
            actionModifier: 'start',
            logLevel: LogLevel.Info,
        });

        const response = await subscriptionClient.getSubscriptions();

        if (!response.isSuccessful || !response.content) {
            const errorMessage = getArmErrorMessage(response.error);

            logEvent({
                action: 'fetch-subscriptions',
                actionModifier: 'failed',
                logLevel: LogLevel.Error,
                additionalData: { error: errorMessage },
            });

            return { error: errorMessage };
        }

        logEvent({
            action: 'fetch-subscriptions',
            actionModifier: 'success',
            logLevel: LogLevel.Info,
            additionalData: { count: response.content.length },
        });

        return { subscriptions: response.content };
    }, [isAuthenticated, subscriptionClient, logEvent]);

    // Initialize subscriptions on mount
    useEffect(() => {
        const loadSubscriptions = async () => {
            if (!isAuthenticated) {
                return;
            }

            setIsLoading(true);
            setError(null);

            const result = await fetchSubscriptions();

            if (result.error || !result.subscriptions) {
                setError(result.error ?? '');
                notifyError('Failed to load subscriptions', result.error);
                setIsLoading(false);
                return;
            }

            const subs = result.subscriptions;
            subs.sort((a, b) => a.displayName.localeCompare(b.displayName));
            setSubscriptions(subs);

            // Restore selected subscriptions from tenant-scoped localStorage
            const validIds = storedSubscriptionIds.filter(id => subs.some(sub => sub.subscriptionId === id));

            setSelectedSubscriptionIds(new Set(validIds));

            // Persist initial selection if it changed
            if (validIds.length !== storedSubscriptionIds.length || !validIds.every(id => storedSubscriptionIds.includes(id))) {
                setStoredSubscriptionIds(validIds);
            }

            setIsLoading(false);
        };

        loadSubscriptions();
    }, [isAuthenticated, fetchSubscriptions, storedSubscriptionIds, setStoredSubscriptionIds, notifyError]);

    // Get selected subscription objects
    const selectedSubscriptions = useMemo(
        () => subscriptions.filter(sub => selectedSubscriptionIds.has(sub.subscriptionId)),
        [subscriptions, selectedSubscriptionIds]
    );

    // Set selected subscriptions
    const setSelectedSubscriptions = useCallback(
        (ids: string[]) => {
            // Validate subscription IDs
            const validIds = ids.filter(id => subscriptions.some(sub => sub.subscriptionId === id));

            // Enforce max selection limit
            if (validIds.length > MAX_SELECTED_SUBSCRIPTIONS) {
                logEvent({
                    action: 'set-selected-subscriptions',
                    actionModifier: 'truncated',
                    logLevel: LogLevel.Warning,
                    additionalData: { requested: validIds.length, max: MAX_SELECTED_SUBSCRIPTIONS },
                });
                validIds.length = MAX_SELECTED_SUBSCRIPTIONS;
            }

            setSelectedSubscriptionIds(new Set(validIds));
            setStoredSubscriptionIds(validIds);

            logEvent({
                action: 'set-selected-subscriptions',
                actionModifier: 'success',
                logLevel: LogLevel.Info,
                additionalData: { count: validIds.length },
            });
        },
        [subscriptions, setStoredSubscriptionIds, logEvent]
    );

    // Toggle subscription selection
    const toggleSubscription = useCallback(
        (subscriptionId: string) => {
            const ids = Array.from(selectedSubscriptionIds);

            if (selectedSubscriptionIds.has(subscriptionId)) {
                const index = ids.indexOf(subscriptionId);
                if (index > -1) {
                    ids.splice(index, 1);
                }
            } else {
                if (ids.length >= MAX_SELECTED_SUBSCRIPTIONS) {
                    notifyError('Selection limit reached', `Cannot select more than ${MAX_SELECTED_SUBSCRIPTIONS} subscriptions`);
                    return;
                }
                ids.push(subscriptionId);
            }

            setSelectedSubscriptions(ids);
        },
        [selectedSubscriptionIds, setSelectedSubscriptions, notifyError]
    );

    const selectAll = useCallback(() => {
        const ids = subscriptions.slice(0, MAX_SELECTED_SUBSCRIPTIONS).map(sub => sub.subscriptionId);
        setSelectedSubscriptions(ids);
    }, [subscriptions, setSelectedSubscriptions]);

    const clearSelection = useCallback(() => {
        setSelectedSubscriptions([]);
    }, [setSelectedSubscriptions]);

    const refresh = useCallback(async () => {
        if (!isAuthenticated) {
            return;
        }

        setIsLoading(true);
        setError(null);

        const result = await fetchSubscriptions();

        if (result.error) {
            setError(result.error);
            notifyError('Failed to refresh subscriptions', result.error);
            setIsLoading(false);
            return;
        }

        const subs = result.subscriptions!;
        subs.sort((a, b) => a.displayName.localeCompare(b.displayName));
        setSubscriptions(subs);

        // Validate current selection against new subscription list
        const currentIds = Array.from(selectedSubscriptionIds);
        const validIds = currentIds.filter(id => subs.some(sub => sub.subscriptionId === id));

        if (validIds.length !== currentIds.length) {
            setSelectedSubscriptionIds(new Set(validIds));
            setStoredSubscriptionIds(validIds);
        }

        logEvent({
            action: 'refresh-subscriptions',
            actionModifier: 'success',
            logLevel: LogLevel.Info,
            additionalData: { count: subs.length },
        });

        setIsLoading(false);
    }, [isAuthenticated, fetchSubscriptions, selectedSubscriptionIds, setStoredSubscriptionIds, logEvent, notifyError]);

    const filterSubscriptions = useCallback(
        (filter: SubscriptionFilter): Subscription[] => {
            let filtered = [...subscriptions];

            if (filter.state && filter.state.length > 0) {
                filtered = filtered.filter(sub => sub.state && filter.state!.includes(sub.state));
            }

            if (filter.searchTerm) {
                const term = filter.searchTerm.toLowerCase();
                filtered = filtered.filter(
                    sub => sub.displayName.toLowerCase().includes(term) || sub.subscriptionId.toLowerCase().includes(term)
                );
            }

            if (filter.tenantIds && filter.tenantIds.length > 0) {
                filtered = filtered.filter(sub => sub.tenantId && filter.tenantIds!.includes(sub.tenantId));
            }

            if (filter.customFilter) {
                filtered = filtered.filter(filter.customFilter);
            }

            return filtered;
        },
        [subscriptions]
    );

    const searchSubscriptions = useCallback(
        (searchTerm: string): Subscription[] => {
            return filterSubscriptions({ searchTerm });
        },
        [filterSubscriptions]
    );

    const isSelected = useCallback(
        (subscriptionId: string): boolean => {
            return selectedSubscriptionIds.has(subscriptionId);
        },
        [selectedSubscriptionIds]
    );

    const getSubscriptionById = useCallback(
        (subscriptionId: string): Subscription | undefined => {
            return subscriptions.find(sub => sub.subscriptionId === subscriptionId);
        },
        [subscriptions]
    );

    // Metadata
    const totalCount = subscriptions.length;
    const selectedCount = selectedSubscriptionIds.size;
    const isAllSelected = useMemo(() => {
        const maxPossible = Math.min(subscriptions.length, MAX_SELECTED_SUBSCRIPTIONS);
        return selectedSubscriptionIds.size === maxPossible && maxPossible > 0;
    }, [subscriptions.length, selectedSubscriptionIds.size]);

    const value = useMemo<SubscriptionsContextValue>(
        () => ({
            subscriptions,
            selectedSubscriptions,
            isLoading,
            error,
            setSelectedSubscriptions,
            toggleSubscription,
            selectAll,
            clearSelection,
            refresh,
            filterSubscriptions,
            searchSubscriptions,
            isSelected,
            getSubscriptionById,
            totalCount,
            selectedCount,
            isAllSelected,
        }),
        [
            subscriptions,
            selectedSubscriptions,
            isLoading,
            error,
            setSelectedSubscriptions,
            toggleSubscription,
            selectAll,
            clearSelection,
            refresh,
            filterSubscriptions,
            searchSubscriptions,
            isSelected,
            getSubscriptionById,
            totalCount,
            selectedCount,
            isAllSelected,
        ]
    );

    return <SubscriptionsContext.Provider value={value}>{children}</SubscriptionsContext.Provider>;
};

export const useSubscriptions = () => {
    const context = useContext(SubscriptionsContext);

    if (!context) {
        throw new Error('useSubscriptions must be used within a SubscriptionsProvider');
    }

    return context;
};

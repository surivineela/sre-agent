import { useCallback, useContext, useMemo, useState } from 'react';
import { useSubscriptions } from '../../../Space/Settings/Hooks/useSubscriptions';
import AzPortalProxy from '../../AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../../Helpers/ResourceDescriptors';
import { useSubscriptionsWithRoles } from '../AzureResourcePicker/Hooks/useSubscriptionsWithRoles';

export interface UseInfrastructureScopePickerResult {
    // Subscription picker
    isSubscriptionPickerOpen: boolean;
    setIsSubscriptionPickerOpen: (open: boolean) => void;
    selectableSubscriptions: ReturnType<typeof useSubscriptionsWithRoles>['selectableSubscriptions'];
    disabledSubscriptions: ReturnType<typeof useSubscriptionsWithRoles>['disabledSubscriptions'];
    subscriptionsWithRolesLoading: boolean;
    selectedSubscriptionIds: string[];
    handleSubscriptionsApplied: (selectedSubscriptionIds: string[]) => void;

    // Resource group picker
    isResourceGroupPickerOpen: boolean;
    setIsResourceGroupPickerOpen: (open: boolean) => void;
    selectedResourceGroupIds: string[];
    resourceGroupLocations: Record<string, string>;
    handleResourceGroupsApplied: (selectedResourceGroupIds: string[], locations: Record<string, string>) => void;

    // Common
    allSubscriptions: { subscriptionId: string; displayName: string }[];
    subscriptionsLoading: boolean;
    defaultSubscriptionId: string;
}

interface UseInfrastructureScopePickerProps {
    initialSubscriptionIds?: string[];
    initialResourceGroupIds?: string[];
    initialResourceGroupLocations?: Record<string, string>;
    onSubscriptionsChange?: (subscriptionIds: string[]) => void;
    onResourceGroupsChange?: (resourceGroupIds: string[], locations: Record<string, string>) => void;
}

export const useInfrastructureScopePicker = (props?: UseInfrastructureScopePickerProps): UseInfrastructureScopePickerResult => {
    const {
        initialSubscriptionIds = [],
        initialResourceGroupIds = [],
        initialResourceGroupLocations = {},
        onSubscriptionsChange,
        onResourceGroupsChange,
    } = props ?? {};

    const portalContext = useContext(AzPortalContext) as AzPortalProxy;
    const { resourceId } = useContext(EnvironmentContext);
    const { subscriptionsList, subscriptionsLoading } = useSubscriptions();

    const [isSubscriptionPickerOpen, setIsSubscriptionPickerOpen] = useState(false);

    // Update hasOpened when dialog opens (but never reset to false)
    const handleSetSubscriptionPickerOpen = useCallback((open: boolean) => {
        setIsSubscriptionPickerOpen(open);
    }, []);

    const {
        selectableSubscriptions,
        disabledSubscriptions,
        isLoading: subscriptionsWithRolesLoading,
    } = useSubscriptionsWithRoles(portalContext);
    const [isResourceGroupPickerOpen, setIsResourceGroupPickerOpen] = useState(false);
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>(initialSubscriptionIds);
    const [selectedResourceGroupIds, setSelectedResourceGroupIds] = useState<string[]>(initialResourceGroupIds);
    const [resourceGroupLocations, setResourceGroupLocations] = useState<Record<string, string>>(initialResourceGroupLocations);

    const defaultSubscriptionId = useMemo(() => {
        const descriptor = new ArmResourceDescriptor(resourceId);
        return descriptor.subscription;
    }, [resourceId]);

    const allSubscriptions = useMemo(
        () =>
            subscriptionsList?.map((s: { subscriptionId: string; displayName: string }) => ({
                subscriptionId: s.subscriptionId,
                displayName: s.displayName,
            })) ?? [],
        [subscriptionsList]
    );

    const handleSubscriptionsApplied = useCallback(
        (newSelectedIds: string[]) => {
            setSelectedSubscriptionIds(newSelectedIds);
            setIsSubscriptionPickerOpen(false);
            onSubscriptionsChange?.(newSelectedIds);
        },
        [onSubscriptionsChange]
    );

    const handleResourceGroupsApplied = useCallback(
        (newSelectedIds: string[], locations: Record<string, string>) => {
            setSelectedResourceGroupIds(newSelectedIds);
            setResourceGroupLocations(prev => ({
                ...prev,
                ...locations,
            }));
            setIsResourceGroupPickerOpen(false);
            onResourceGroupsChange?.(newSelectedIds, { ...resourceGroupLocations, ...locations });
        },
        [onResourceGroupsChange, resourceGroupLocations]
    );

    return {
        // Subscription picker
        isSubscriptionPickerOpen,
        setIsSubscriptionPickerOpen: handleSetSubscriptionPickerOpen,
        selectableSubscriptions,
        disabledSubscriptions,
        subscriptionsWithRolesLoading,
        selectedSubscriptionIds,
        handleSubscriptionsApplied,

        // Resource group picker
        isResourceGroupPickerOpen,
        setIsResourceGroupPickerOpen,
        selectedResourceGroupIds,
        resourceGroupLocations,
        handleResourceGroupsApplied,

        // Common
        allSubscriptions,
        subscriptionsLoading,
        defaultSubscriptionId,
    };
};

import { createContext, ReactNode, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';
import { Notification, NotificationStatus, PollingConfig, PromiseTrackingOptions } from '../Contracts/Notification';
import { newShortGuid } from '../Utilities/Guid';

interface NotificationContextValue {
    notifications: Notification[];
    unreadCount: number;

    // Explicit API (primary)
    start: (title: string, description?: string) => string;
    succeed: (id: string, title?: string, description?: string) => void;
    fail: (id: string, title?: string, description?: string) => void;

    // One-off
    info: (title: string, description?: string) => string;
    warning: (title: string, description?: string) => string;
    error: (title: string, description?: string) => string;

    // Advanced (optional usage)
    trackPromise: <T = unknown>(title: string, promise: Promise<T>, options?: PromiseTrackingOptions<T>) => string;
    startWithPolling: (title: string, config: PollingConfig) => string;

    // Management
    dismiss: (id: string) => void;
    dismissAll: () => void;
    dismissCompleted: () => void;
    markAllAsRead: () => void;
}

const NotificationContext = createContext<NotificationContextValue | undefined>(undefined);

const generateId = () => `notif_${newShortGuid()}`;

export const NotificationProvider = ({ children }: { children: ReactNode }) => {
    const intl = useIntl();

    const [notifications, setNotifications] = useState<Notification[]>([]);
    const [unreadCount, setUnreadCount] = useState(0);

    const addNotification = useCallback((notification: Notification) => {
        setNotifications(prev => [notification, ...prev]);
        setUnreadCount(prev => prev + 1);
        return notification.id;
    }, []);

    /**
     * Replaces an in-progress notification with a completion notification.
     * Intentionally does not increment unread count (just replacing original notification)
     */
    const replaceWithCompletionNotification = useCallback(
        (id: string, status: NotificationStatus, title?: string, description?: string) => {
            setNotifications(prev => {
                const notification = prev.find(n => n.id === id);
                if (notification?._tracking) {
                    notification._tracking.cleanup();
                }

                const filtered = prev.filter(n => n.id !== id);
                return [
                    {
                        id: generateId(),
                        title: title ?? notification?.title ?? (status === 'success' ? 'Success' : 'Error'),
                        description,
                        status,
                        timestamp: new Date(),
                    },
                    ...filtered,
                ];
            });
        },
        []
    );

    // Explicit API
    const start = useCallback(
        (title: string, description?: string): string => {
            return addNotification({
                id: generateId(),
                title,
                description,
                status: 'in-progress',
                timestamp: new Date(),
            });
        },
        [addNotification]
    );

    const succeed = useCallback(
        (id: string, title?: string, description?: string) => {
            replaceWithCompletionNotification(id, 'success', title, description);
        },
        [replaceWithCompletionNotification]
    );

    const fail = useCallback(
        (id: string, title?: string, description?: string) => {
            replaceWithCompletionNotification(id, 'error', title, description);
        },
        [replaceWithCompletionNotification]
    );

    // One-off notifications
    const createOneOff = useCallback(
        (status: NotificationStatus) =>
            (title: string, description?: string): string => {
                return addNotification({
                    id: generateId(),
                    title,
                    description,
                    status,
                    timestamp: new Date(),
                });
            },
        [addNotification]
    );

    const info = useMemo(() => createOneOff('info'), [createOneOff]);
    const warning = useMemo(() => createOneOff('warning'), [createOneOff]);
    const errorNotif = useMemo(() => createOneOff('error'), [createOneOff]);

    // Promise tracking
    const trackPromise = useCallback(
        <T,>(title: string, promise: Promise<T>, options?: PromiseTrackingOptions<T>): string => {
            const id = generateId();
            let cancelled = false;

            const notification: Notification = {
                id,
                title,
                status: 'in-progress',
                timestamp: new Date(),
                _tracking: {
                    type: 'promise',
                    cleanup: () => {
                        cancelled = true;
                    },
                },
            };

            addNotification(notification);

            promise
                .then(result => {
                    if (cancelled) return;

                    const updates = options?.onSuccess?.(result);
                    replaceWithCompletionNotification(id, 'success', updates?.title ?? title, updates?.description);
                })
                .catch(error => {
                    if (cancelled) return;

                    const updates = options?.onError?.(error);
                    replaceWithCompletionNotification(id, 'error', updates?.title ?? title, updates?.description ?? error.message);
                });

            return id;
        },
        [addNotification, replaceWithCompletionNotification]
    );

    // Polling
    const startWithPolling = useCallback(
        (title: string, config: PollingConfig): string => {
            const id = generateId();
            let attempts = 0;
            let timeoutId: NodeJS.Timeout | null = null;
            let cancelled = false;

            const poll = async () => {
                if (cancelled) return;

                try {
                    const result = await config.pollFn();

                    if (cancelled) return;

                    if (result.complete) {
                        replaceWithCompletionNotification(
                            id,
                            result.success === false ? 'error' : 'success',
                            result.title ?? title,
                            result.description
                        );
                        return;
                    }

                    attempts++;
                    if (config.maxAttempts && attempts >= config.maxAttempts) {
                        replaceWithCompletionNotification(
                            id,
                            'error',
                            result.title ?? title,
                            result.description ?? intl.formatMessage(PortalResources.requestError)
                        );
                        return;
                    }

                    timeoutId = setTimeout(poll, config.interval);
                } catch (error) {
                    if (!cancelled) {
                        replaceWithCompletionNotification(
                            id,
                            'error',
                            title,
                            error instanceof Error ? error.message : intl.formatMessage(PortalResources.requestError)
                        );
                    }
                }
            };

            const notification: Notification = {
                id,
                title,
                status: 'in-progress',
                timestamp: new Date(),
                _tracking: {
                    type: 'polling',
                    cleanup: () => {
                        cancelled = true;
                        if (timeoutId) {
                            clearTimeout(timeoutId);
                        }
                    },
                },
            };

            addNotification(notification);
            poll();

            return id;
        },
        [addNotification, intl, replaceWithCompletionNotification]
    );

    // Management
    const dismiss = useCallback((id: string) => {
        setNotifications(prev => {
            const notification = prev.find(n => n.id === id);
            if (notification?._tracking) {
                notification._tracking.cleanup();
            }
            return prev.filter(n => n.id !== id);
        });
    }, []);

    const dismissAll = useCallback(() => {
        setNotifications(prev => {
            prev.forEach(n => {
                if (n._tracking) {
                    n._tracking.cleanup();
                }
            });
            return [];
        });
    }, []);

    const dismissCompleted = useCallback(() => {
        setNotifications(prev => {
            const toRemove = prev.filter(n => n.status !== 'in-progress');
            toRemove.forEach(n => {
                if (n._tracking) {
                    n._tracking.cleanup();
                }
            });
            return prev.filter(n => n.status === 'in-progress');
        });
    }, []);

    const markAllAsRead = useCallback(() => {
        setUnreadCount(0);
    }, []);

    const value = useMemo<NotificationContextValue>(
        () => ({
            notifications,
            unreadCount,
            start,
            succeed,
            fail,
            info,
            warning,
            error: errorNotif,
            trackPromise,
            startWithPolling,
            dismiss,
            dismissAll,
            dismissCompleted,
            markAllAsRead,
        }),
        [
            notifications,
            unreadCount,
            start,
            succeed,
            fail,
            info,
            warning,
            errorNotif,
            trackPromise,
            startWithPolling,
            dismiss,
            dismissAll,
            dismissCompleted,
            markAllAsRead,
        ]
    );

    return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
};

export const useNotifications = () => {
    const context = useContext(NotificationContext);

    if (!context) {
        throw new Error('useNotifications must be used within a NotificationProvider');
    }

    return context;
};

export type NotificationStatus = 'in-progress' | 'success' | 'error' | 'warning' | 'info';

export interface Notification {
    id: string;
    title: string;
    description?: string;
    status: NotificationStatus;
    timestamp: Date;
    // For internal tracking (Promise/polling cleanup)
    _tracking?: {
        type: 'promise' | 'polling';
        cleanup: () => void;
    };
}

export interface NotificationSuccessHandler<T = unknown> {
    (result: T): { title?: string; description?: string } | void;
}

export interface NotificationErrorHandler {
    (error: Error): { title?: string; description?: string } | void;
}

export interface PromiseTrackingOptions<T = unknown> {
    onSuccess?: NotificationSuccessHandler<T>;
    onError?: NotificationErrorHandler;
}

export interface PollingResult {
    complete: boolean;
    success?: boolean;
    title?: string;
    description?: string;
}

export interface PollingConfig {
    pollFn: () => Promise<PollingResult>;
    interval: number;
    maxAttempts?: number;
}

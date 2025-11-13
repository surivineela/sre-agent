// Simple user activity reporter for playground session management
// This is a lightweight solution that uses existing postMessage infrastructure

import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';

/**
 * Static class for managing user activity reporting to support playground session management.
 * Tracks user interactions (clicks, keydown, input, scroll) and reports them via the portal proxy.
 */
export class UserActivityReporter {
    private static portalProxy: AzPortalProxy | null = null;
    private static isEnabled = false;
    private static lastUserActivityTime = 0;
    private static readonly THROTTLE_INTERVAL = 2000; // Only report activity every 2 seconds max

    // Store event handlers for cleanup
    private static clickHandler: ((event: Event) => void) | null = null;
    private static keydownHandler: ((event: Event) => void) | null = null;
    private static inputHandler: ((event: Event) => void) | null = null;
    private static scrollHandler: ((event: Event) => void) | null = null;

    /**
     * Extracts meaningful identifier from an event target element
     * Prioritizes: id > aria-label > data-testid > data-activity-id > element type
     */
    private static getElementIdentifier(target: EventTarget | null): string {
        if (!target || !(target instanceof Element)) {
            return 'unknown';
        }

        const element = target as HTMLElement;

        // Try to get a meaningful identifier in order of preference
        if (element.id) {
            return `#${element.id}`;
        }

        const ariaLabel = element.getAttribute('aria-label');
        if (ariaLabel) {
            return `[aria-label="${ariaLabel}"]`;
        }

        const testId = element.getAttribute('data-testid');
        if (testId) {
            return `[data-testid="${testId}"]`;
        }

        const activityId = element.getAttribute('data-activity-id');
        if (activityId) {
            return `[data-activity-id="${activityId}"]`;
        }

        // Fallback to element type with any available class
        const className = element.className ? `.${element.className.split(' ')[0]}` : '';
        return `${element.tagName.toLowerCase()}${className}`;
    }

    /**
     * Creates a throttled event reporter for a specific event type
     */
    private static createThrottledReporter(eventType: string): (event: Event) => void {
        return (event: Event) => {
            const now = Date.now();
            if (now - UserActivityReporter.lastUserActivityTime > UserActivityReporter.THROTTLE_INTERVAL) {
                const elementId = UserActivityReporter.getElementIdentifier(event.target);
                UserActivityReporter.reportActivity(`${eventType}:${elementId}`);
                UserActivityReporter.lastUserActivityTime = now;
            }
        };
    }

    /**
     * Initializes the user activity reporter with the portal proxy.
     * Sets up event listeners for user interactions.
     */
    public static initialize(proxy: AzPortalProxy): void {
        // Clean up any existing listeners first
        UserActivityReporter.cleanup();

        UserActivityReporter.portalProxy = proxy;
        UserActivityReporter.isEnabled = true;

        // Add global event listeners for common user interactions
        if (typeof window !== 'undefined') {
            // Create and store handlers
            UserActivityReporter.clickHandler = UserActivityReporter.createThrottledReporter('click');
            UserActivityReporter.keydownHandler = UserActivityReporter.createThrottledReporter('keydown');
            UserActivityReporter.inputHandler = UserActivityReporter.createThrottledReporter('input');
            UserActivityReporter.scrollHandler = UserActivityReporter.createThrottledReporter('scroll');

            // Listen for various user interactions
            const options = { passive: true } as AddEventListenerOptions;
            document.addEventListener('click', UserActivityReporter.clickHandler, options);
            document.addEventListener('keydown', UserActivityReporter.keydownHandler, options);
            document.addEventListener('input', UserActivityReporter.inputHandler, options);
            document.addEventListener('scroll', UserActivityReporter.scrollHandler, options);
        }
    }

    /**
     * Cleans up the user activity reporter by removing all event listeners
     * and resetting internal state.
     */
    public static cleanup(): void {
        // Remove all event listeners
        if (typeof window !== 'undefined') {
            if (UserActivityReporter.clickHandler) {
                document.removeEventListener('click', UserActivityReporter.clickHandler);
                UserActivityReporter.clickHandler = null;
            }
            if (UserActivityReporter.keydownHandler) {
                document.removeEventListener('keydown', UserActivityReporter.keydownHandler);
                UserActivityReporter.keydownHandler = null;
            }
            if (UserActivityReporter.inputHandler) {
                document.removeEventListener('input', UserActivityReporter.inputHandler);
                UserActivityReporter.inputHandler = null;
            }
            if (UserActivityReporter.scrollHandler) {
                document.removeEventListener('scroll', UserActivityReporter.scrollHandler);
                UserActivityReporter.scrollHandler = null;
            }
        }

        UserActivityReporter.isEnabled = false;
        UserActivityReporter.portalProxy = null;
    }

    /**
     * Reports user activity to the portal proxy.
     * @param activityType - Type of activity to report (e.g., 'click:button#submit')
     */
    public static reportActivity(activityType: string = 'general'): void {
        if (!UserActivityReporter.isEnabled || !UserActivityReporter.portalProxy) {
            return;
        }

        try {
            UserActivityReporter.portalProxy.reportUserActivity(activityType);
        } catch (error) {
            console.warn('Failed to report user activity:', error);
        }
    }
}

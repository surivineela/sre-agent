import { useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../Strings/Resources';

/**
 * Hook that formats a timestamp as relative time (e.g., "2 minutes ago")
 * Auto-refreshes every minute to keep the display current
 */
export const useRelativeTime = (timestamp: Date): string => {
    const intl = useIntl();
    const [, setTick] = useState(0);

    // Refresh every minute to update relative times
    useEffect(() => {
        const interval = setInterval(() => {
            setTick(prev => prev + 1);
        }, 60000); // 60 seconds

        return () => clearInterval(interval);
    }, []);

    const now = new Date();
    const diffMs = now.getTime() - timestamp.getTime();
    const diffSec = Math.floor(diffMs / 1000);
    const diffMin = Math.floor(diffSec / 60);
    const diffHour = Math.floor(diffMin / 60);
    const diffDay = Math.floor(diffHour / 24);

    if (diffMin < 1) {
        return intl.formatMessage(PortalResources.justNow);
    }

    if (diffMin < 60) {
        return intl.formatMessage(PortalResources.minutesAgo, { count: diffMin });
    }

    if (diffHour < 24) {
        return intl.formatMessage(PortalResources.hoursAgo, { count: diffHour });
    }

    return intl.formatMessage(PortalResources.daysAgo, { count: diffDay });
};

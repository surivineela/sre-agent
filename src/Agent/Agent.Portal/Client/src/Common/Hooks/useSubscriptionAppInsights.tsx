import { useCallback, useEffect, useMemo, useState } from 'react';
import { AppInsightsClient } from '../Clients/AppInsightsClient';
import { TelemetrySource } from '../Constants/Telemetry';

export interface ApplicationInsightsResource {
    id: string;
    name: string;
    resourceGroup: string;
    location: string;
}

export const useSubscriptionAppInsights = (subscriptionId: string, telemetrySource: TelemetrySource) => {
    const [appInsights, setAppInsights] = useState<ApplicationInsightsResource[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);

    const appInsightsClient = useMemo(() => AppInsightsClient.getInstance(telemetrySource), [telemetrySource]);

    const fetchAppInsights = useCallback(async () => {
        if (!subscriptionId) {
            setAppInsights([]);
            return;
        }

        setIsLoading(true);
        const response = await appInsightsClient.getApplicationInsightsBySubscription(subscriptionId);
        setAppInsights(response.isSuccessful && response.content ? response.content : []);
        setIsLoading(false);
    }, [subscriptionId, appInsightsClient]);

    useEffect(() => {
        fetchAppInsights();
    }, [fetchAppInsights]);

    return {
        appInsights,
        isLoading,
        refresh: fetchAppInsights,
    };
};

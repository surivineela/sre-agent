import { useCallback, useState } from 'react';
import { AppInsightsClient } from '../../../Common/Clients/AppInsightsClient';

export interface ApplicationInsightsResource {
    id: string;
    name: string;
    resourceGroup: string;
    location: string;
}

interface UseApplicationInsightsResult {
    appInsights: ApplicationInsightsResource[];
    isLoading: boolean;
    refresh: (subscriptionId: string) => Promise<void>;
}

export const useApplicationInsights = (): UseApplicationInsightsResult => {
    const [appInsights, setAppInsights] = useState<ApplicationInsightsResource[]>([]);
    const [isLoading, setIsLoading] = useState(false);

    const refresh = useCallback(async (subscriptionId: string) => {
        if (!subscriptionId) {
            setAppInsights([]);
            return;
        }

        setIsLoading(true);

        const response = await AppInsightsClient.getApplicationInsightsBySubscription(subscriptionId);

        setIsLoading(false);

        if (response.isSuccessful && response.data) {
            setAppInsights(response.data);
        } else {
            setAppInsights([]);
        }
    }, []);

    return { appInsights, isLoading, refresh };
};

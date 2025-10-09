import { useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../Helpers/headers';

interface FeatureFlags {
    scheduledTasks: boolean;
    agentMemory: boolean;
    extendedAgentsGraph: boolean;
}

interface FeatureStatusResponse {
    features: FeatureFlags;
}

export const useFeatureFlags = () => {
    const [features, setFeatures] = useState<FeatureFlags>({
        scheduledTasks: false,
        agentMemory: false,
        extendedAgentsGraph: false,
    });
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<Error | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    useEffect(() => {
        const fetchFeatureFlags = async () => {
            try {
                setLoading(true);
                const response = await fetch(`${sreAgentEndpoint}/api/v1/feature/status`, {
                    headers: getAgentHeaders(),
                });

                if (!response.ok) {
                    throw new Error(`Failed to fetch feature flags: ${response.status}`);
                }

                const data: FeatureStatusResponse = await response.json();
                setFeatures(data.features);
                setError(null);
            } catch (err) {
                console.error('Error fetching feature flags:', err);
                setError(err as Error);
                // Set default values on error
                setFeatures({
                    scheduledTasks: false,
                    agentMemory: false,
                    extendedAgentsGraph: false,
                });
            } finally {
                setLoading(false);
            }
        };

        fetchFeatureFlags();
    }, []);

    return { features, loading, error };
};

import { useEffect, useState } from 'react';

interface FeatureFlags {
    scheduledTasks: boolean;
    agentMemory: boolean;
}

interface FeatureStatusResponse {
    features: FeatureFlags;
}

export const useFeatureFlags = () => {
    const [features, setFeatures] = useState<FeatureFlags>({
        scheduledTasks: false,
        agentMemory: false,
    });
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<Error | null>(null);

    useEffect(() => {
        const fetchFeatureFlags = async () => {
            try {
                setLoading(true);
                const response = await fetch('/api/v1/feature/status');

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
                });
            } finally {
                setLoading(false);
            }
        };

        fetchFeatureFlags();
    }, []);

    return { features, loading, error };
};

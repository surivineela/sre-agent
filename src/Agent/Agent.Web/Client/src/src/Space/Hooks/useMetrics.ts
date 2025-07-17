import axios from 'axios';
import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';

export interface IncidentMetrics {
    activeCount: number;
    mitigatedCount: number;
    resolvedCount: number;
}

export const useMetrics = (oldestThreadModifiedTimestamp?: string) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [incidentMetrics, setIncidentMetrics] = useState<IncidentMetrics>();

    const getIncidentSeverityMetrics = useCallback(
        async (startTime: string, endTime: string): Promise<IncidentMetrics> => {
            const { data } = await axios.get(
                `${sreAgentEndpoint}/api/v1/metrics/incidentStatus?startTime=${startTime}&endTime=${endTime}`,
                {
                    headers: getAgentHeaders(),
                }
            );
            return data;
        },
        [sreAgentEndpoint]
    );

    useEffect(() => {
        let isSubscribed = true;
        let timer: NodeJS.Timeout | undefined = undefined;

        const pollMetrics = async () => {
            if (oldestThreadModifiedTimestamp) {
                const start = getSafeDateTime(oldestThreadModifiedTimestamp).toISOString();
                const end = new Date().toISOString();

                const metrics = await getIncidentSeverityMetrics(start, end);

                if (isSubscribed) {
                    setIncidentMetrics(metrics);

                    timer = setTimeout(pollMetrics, 10000);
                }
            }
        };

        // Delay the first call in case the oldestThreadModifiedTimestamp is change more frequently than the polling interval
        timer = setTimeout(pollMetrics, 10000);

        return () => {
            clearTimeout(timer);
            isSubscribed = false;
        };
    }, [oldestThreadModifiedTimestamp, getIncidentSeverityMetrics]);

    return { incidentMetrics };
};

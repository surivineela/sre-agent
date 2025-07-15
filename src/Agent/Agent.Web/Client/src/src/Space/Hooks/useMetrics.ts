import axios from 'axios';
import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SelectedTimes } from '../Activities/TimeDropdown';
export interface IncidentMetrics {
    activeCount: number;
    mitigatedCount: number;
    resolvedCount: number;
}

export function getTimeRange(selectedTime: SelectedTimes): { start: string; end: string } {
    const now = new Date();
    const end = new Date(now);
    let start: Date;

    switch (selectedTime) {
        case SelectedTimes.OneDay:
            start = new Date(now.getTime() - 24 * 60 * 60 * 1000);
            break;
        case SelectedTimes.SevenDays:
            start = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
            break;
        case SelectedTimes.ThirtyDays:
            start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
            break;
        default:
            throw new Error(`Invalid time range: ${selectedTime}`);
    }

    return {
        start: start.toISOString(),
        end: end.toISOString(),
    };
}

export const useMetrics = (selectedTime: SelectedTimes) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [incidentMetrics, setIncidentMetrics] = useState<IncidentMetrics>();
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isInitialized, setIsInitialized] = useState<boolean>(false);

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

        const pollMetrics = async () => {
            if (selectedTime && isInitialized) {
                const { start, end } = getTimeRange(selectedTime);
                const metrics = await getIncidentSeverityMetrics(start, end);

                if (isSubscribed) {
                    setIncidentMetrics(metrics);
                }
            }
        };

        const timer = setInterval(pollMetrics, 20000);

        return () => {
            clearInterval(timer);
            isSubscribed = false;
        };
    }, [selectedTime, isInitialized, getIncidentSeverityMetrics]);

    useEffect(() => {
        let isSubscribed = true;

        const getInitialMetrics = async () => {
            if (selectedTime) {
                setIsLoading(true);
                const { start, end } = getTimeRange(selectedTime);
                const metrics = await getIncidentSeverityMetrics(start, end);
                if (isSubscribed) {
                    setIncidentMetrics(metrics);
                    setIsLoading(false);
                    setIsInitialized(true);
                }
            }
        };

        getInitialMetrics();

        return () => {
            isSubscribed = false;
        };
    }, [getIncidentSeverityMetrics, selectedTime]);

    return { incidentMetrics, isLoading };
};

import axios from 'axios';
import { useEffect, useState } from 'react';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SelectedTimes } from '../Activities/TimeDropdown';

const getActionSeverityMetrics = async (startTime: string, endTime: string): Promise<ActionSeverityMetrics> => {
    const { data } = await axios.get(`../api/v1/metrics/actionSeverity?startTime=${startTime}&endTime=${endTime}`, {
        headers: getAgentHeaders(),
    });
    return data;
};

const getActionStatusMetrics = async (startTime: string, endTime: string): Promise<ActionStatusMetrics> => {
    const { data } = await axios.get(`../api/v1/metrics/actionStatus?startTime=${startTime}&endTime=${endTime}`, {
        headers: getAgentHeaders(),
    });
    return data;
};

const getIncidentSeverityMetrics = async (startTime: string, endTime: string): Promise<IncidentMetrics> => {
    const { data } = await axios.get(`../api/v1/metrics/incidentStatus?startTime=${startTime}&endTime=${endTime}`, {
        headers: getAgentHeaders(),
    });
    return data;
};

export interface ActionSeverityMetrics {
    criticalActionsCount: number;
    warningActionsCount: number;
}

export interface ActionStatusMetrics {
    completedActionsCount: number;
    failedActionsCount: number;
    pendingActionsCount: number;
}

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
    const [actionSeverityMetrics, setActionSeverityMetrics] = useState<ActionSeverityMetrics>();
    const [actionStatusMetrics, setActionStatusMetrics] = useState<ActionStatusMetrics>();
    const [incidentMetrics, setIncidentMetrics] = useState<IncidentMetrics>();
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isInitialized, setIsInitialized] = useState<boolean>(false);

    useEffect(() => {
        let isSubscribed = true;

        const pollMetrics = async () => {
            if (selectedTime && isInitialized) {
                const { start, end } = getTimeRange(selectedTime);
                const metrics = await Promise.all([
                    getActionSeverityMetrics(start, end),
                    getActionStatusMetrics(start, end),
                    getIncidentSeverityMetrics(start, end),
                ]);
                if (isSubscribed) {
                    setActionSeverityMetrics(metrics[0]);
                    setActionStatusMetrics(metrics[1]);
                    setIncidentMetrics(metrics[2]);
                }
            }
        };

        const timer = setInterval(pollMetrics, 10000);

        return () => {
            clearInterval(timer);
            isSubscribed = false;
        };
    }, [selectedTime, isInitialized]);

    useEffect(() => {
        let isSubscribed = true;

        const getInitialMetrics = async () => {
            if (selectedTime) {
                setIsLoading(true);
                const { start, end } = getTimeRange(selectedTime);
                const metrics = await Promise.all([
                    getActionSeverityMetrics(start, end),
                    getActionStatusMetrics(start, end),
                    getIncidentSeverityMetrics(start, end),
                ]);

                if (isSubscribed) {
                    setActionSeverityMetrics(metrics[0]);
                    setActionStatusMetrics(metrics[1]);
                    setIncidentMetrics(metrics[2]);
                    setIsLoading(false);
                    setIsInitialized(true);
                }
            }
        };

        getInitialMetrics();

        return () => {
            isSubscribed = false;
        };
    }, [selectedTime]);

    return { actionSeverityMetrics, actionStatusMetrics, incidentMetrics, isLoading };
};

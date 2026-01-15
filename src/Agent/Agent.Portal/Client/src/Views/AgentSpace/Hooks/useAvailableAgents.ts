import { useCallback, useEffect, useMemo, useState } from 'react';
import { SreAgentClient } from '../../../Common/Clients/SreAgentClient';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { SreAgentArgItem } from '../../../Common/Contracts/SreAgent';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';

interface UseAvailableAgentsProps {
    spaceLocation: string;
    subscriptionIds: string[];
    isOpen: boolean;
}

interface UseAvailableAgentsResult {
    availableAgents: SreAgentArgItem[];
    isLoading: boolean;
    error: string | null;
    refresh: () => Promise<void>;
}

export const useAvailableAgents = ({ spaceLocation, subscriptionIds, isOpen }: UseAvailableAgentsProps): UseAvailableAgentsResult => {
    const { logEvent } = useTelemetry(TelemetrySource.AgentSpaceView, undefined);

    const [availableAgents, setAvailableAgents] = useState<SreAgentArgItem[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const fetchAvailableAgents = useCallback(async () => {
        if (!isOpen || !spaceLocation || subscriptionIds.length === 0) {
            return;
        }

        setIsLoading(true);
        setError(null);

        const response = await sreAgentClient.getAgentsFromArg(subscriptionIds, undefined, spaceLocation, true);

        if (!response.isSuccessful) {
            const errorMessage = getArmErrorMessage(response.error);
            setError(errorMessage);
            logEvent({
                action: 'fetch-available-agents',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: { error: errorMessage },
            });
            setAvailableAgents([]);
        } else {
            const agents = response.content || [];
            const filteredAgents = agents.filter(
                agent => agent.location.toLowerCase() === spaceLocation.toLowerCase() && (!agent.agentSpaceId || agent.agentSpaceId === '')
            );
            setAvailableAgents(filteredAgents);
        }

        setIsLoading(false);
    }, [sreAgentClient, spaceLocation, subscriptionIds, isOpen, logEvent]);

    const refresh = useCallback(async () => {
        await fetchAvailableAgents();
    }, [fetchAvailableAgents]);

    useEffect(() => {
        if (isOpen) {
            fetchAvailableAgents();
        }
    }, [isOpen, fetchAvailableAgents]);

    return {
        availableAgents,
        isLoading,
        error,
        refresh,
    };
};

import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { IAgentModeInfo } from '../Contracts/Activities';

export interface AgentModeValidationResult {
    isValid: boolean;
    availableModes: string[];
    errorMessage?: string;
}

export const useAgentMode = () => {
    const [availableAgentModes, setAvailableAgentModes] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [error, setError] = useState<string | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    // Fetch available agent modes from the server
    const fetchAvailableAgentModes = useCallback(async () => {
        setIsLoading(true);
        setError(null);

        try {
            const response = await threadClient.getAvailableAgentModes();
            if (response.isSuccessful && response.content) {
                setAvailableAgentModes(response.content);
            } else {
                setError('Failed to fetch available agent modes');
            }
        } catch (e: any) {
            setError(e?.message || 'Unknown error occurred');
        } finally {
            setIsLoading(false);
        }
    }, [threadClient]);

    // Update thread agent mode
    const updateThreadAgentMode = useCallback(
        async (threadId: string, agentMode: string) => {
            setIsLoading(true);
            setError(null);

            try {
                const response = await threadClient.updateThreadAgentMode(threadId, agentMode);
                if (response.isSuccessful) {
                    return response.content;
                } else {
                    throw new Error('Failed to update agent mode');
                }
            } catch (e: any) {
                const errorMessage = e?.response?.data?.error || e?.message || 'Failed to update agent mode';
                setError(errorMessage);
                throw new Error(errorMessage);
            } finally {
                setIsLoading(false);
            }
        },
        [threadClient]
    );

    // Get agent mode information with display names and descriptions
    const getAgentModeInfo = useCallback((mode: string): IAgentModeInfo => {
        const agentModeDescriptions: Record<string, IAgentModeInfo> = {
            ReadOnly: {
                mode: 'ReadOnly',
                displayName: 'Read Only',
                description: 'Agent can only view and analyze information without taking any actions',
            },
            Review: {
                mode: 'Review',
                displayName: 'Review Mode',
                description: 'Agent can propose actions but requires approval before execution',
            },
            Autonomous: {
                mode: 'Autonomous',
                displayName: 'Autonomous',
                description: 'Agent can execute actions automatically without approval',
            },
        };

        return (
            agentModeDescriptions[mode] || {
                mode,
                displayName: mode,
                description: 'Unknown agent mode',
            }
        );
    }, []);

    // Validate if mode change is allowed based on global restrictions
    const validateAgentModeChange = useCallback((availableModes: string[]): AgentModeValidationResult => {
        if (!availableModes || availableModes.length === 0) {
            return {
                isValid: false,
                availableModes: [],
                errorMessage: 'No agent modes available',
            };
        }

        // If only one mode is available (ReadOnly), button should be disabled
        if (availableModes.length === 1 && availableModes[0] === 'ReadOnly') {
            return {
                isValid: false,
                availableModes,
                errorMessage: 'Agent mode is restricted to Read Only by global configuration',
            };
        }

        return {
            isValid: true,
            availableModes,
        };
    }, []);

    // Get the effective current mode (thread-specific or global default)
    const getEffectiveAgentMode = useCallback((threadAgentMode?: string, globalDefault: string = 'Review'): string => {
        return threadAgentMode || globalDefault;
    }, []);

    // Fetch available modes on component mount
    useEffect(() => {
        fetchAvailableAgentModes();
    }, [fetchAvailableAgentModes]);

    return {
        availableAgentModes,
        isLoading,
        error,
        updateThreadAgentMode,
        getAgentModeInfo,
        validateAgentModeChange,
        getEffectiveAgentMode,
        refetchAvailableAgentModes: fetchAvailableAgentModes,
    };
};

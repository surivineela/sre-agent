import { useCallback, useEffect, useState } from 'react';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent } from '../../../Common/Contracts/Azure/SreAgent';

export function useSreAgent(resourceId: string) {
    const [agent, setAgent] = useState<ArmObj<Agent>>();
    const [agentLoading, setAgentLoading] = useState(false);
    const [agentLoaded, setAgentLoaded] = useState(false);
    const [agentLoadFailure, setAgentLoadFailure] = useState('');

    const getAgent = useCallback(() => {
        setAgent(undefined);
        setAgentLoading(true);
        setAgentLoaded(false);
        setAgentLoadFailure('');

        SreAgentClient.getAgent(resourceId).then(response => {
            setAgentLoading(false);
            if (response?.metadata?.success && response.data) {
                setAgent(response.data);
                setAgentLoaded(true);
            } else {
                setAgentLoadFailure(response?.metadata?.error || 'Failed to load agent');
            }
        });
    }, [resourceId]);

    useEffect(() => {
        if (resourceId) {
            getAgent();
        }
    }, [resourceId]);

    return {
        agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        refresh: getAgent,
    };
}

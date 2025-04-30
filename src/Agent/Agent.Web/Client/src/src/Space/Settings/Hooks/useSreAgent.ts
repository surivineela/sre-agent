import { useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent } from '../../../Common/Contracts/Azure/SreAgent';

export function useSreAgent(resourceId: string) {
    const [agent, setAgent] = useState<ArmObj<Agent>>();
    const [agentLoading, setAgentLoading] = useState(false);
    const [agentLoaded, setAgentLoaded] = useState(false);
    const [agentLoadFailure, setAgentLoadFailure] = useState('');
    const azPortalContext = useContext(AzPortalContext);

    const getAgent = useCallback(() => {
        azPortalContext.log({
            action: 'fetch-agent',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });
        setAgent(undefined);
        setAgentLoading(true);
        setAgentLoaded(false);
        setAgentLoadFailure('');

        SreAgentClient.getAgent(resourceId).then(response => {
            setAgentLoading(false);
            if (response?.metadata?.success && response.data) {
                azPortalContext.log({
                    action: 'fetch-agent',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId,
                });
                setAgent(response.data);
                setAgentLoaded(true);
            } else {
                azPortalContext.log({
                    action: 'fetch-agent',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId,
                    data: { error: response.metadata.error },
                });
                setAgentLoadFailure(response?.metadata?.error || 'Failed to load agent');
            }
        });
    }, [resourceId]);

    useEffect(() => {
        if (resourceId) {
            getAgent();
        }
    }, [resourceId, getAgent]);

    return {
        agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        refresh: getAgent,
    };
}

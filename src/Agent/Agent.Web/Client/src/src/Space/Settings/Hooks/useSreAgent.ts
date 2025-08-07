import { useCallback, useContext, useEffect, useState } from 'react';
import { HttpResponseObject } from '../../../Common/ArmHelper.types';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent } from '../../../Common/Contracts/Azure/SreAgent';

export interface SreAgentHook {
    agent: ArmObj<Agent> | undefined;
    agentLoading: boolean;
    agentLoaded: boolean;
    agentLoadFailure: string;
    agentPatching: boolean;
    agentPatched: boolean;
    agentPatchFailure: string;
    patchAgent: (agentPayload: Partial<ArmObj<Partial<Agent>>>) => Promise<HttpResponseObject<ArmObj<Agent>>>;
    refresh: () => void;
}

export function useSreAgent(resourceId: string): SreAgentHook {
    const [agent, setAgent] = useState<ArmObj<Agent>>();
    const [agentLoading, setAgentLoading] = useState(false);
    const [agentLoaded, setAgentLoaded] = useState(false);
    const [agentLoadFailure, setAgentLoadFailure] = useState('');

    const [agentPatching, setAgentPatching] = useState(false);
    const [agentPatched, setAgentPatched] = useState(false);
    const [agentPatchFailure, setAgentPatchFailure] = useState('');
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
                const error = getErrorMessage(response?.metadata?.error);
                azPortalContext.log({
                    action: 'fetch-agent',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId,
                    data: { error },
                });
                setAgentLoadFailure(error || 'Failed to load agent');
            }
        });
    }, [resourceId, azPortalContext]);

    const patchAgent = useCallback(
        (agentPayload: Partial<ArmObj<Partial<Agent>>>) => {
            azPortalContext.log({
                action: 'patch-agent',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId,
            });
            setAgentPatching(true);
            setAgentPatched(false);
            setAgentPatchFailure('');

            return SreAgentClient.patchAgent(resourceId, agentPayload).then(response => {
                setAgentPatching(false);
                if (response?.metadata?.success && response.data) {
                    azPortalContext.log({
                        action: 'patch-agent',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId,
                    });
                    setAgent(response.data);
                    setAgentPatched(true);
                } else {
                    const error = getErrorMessage(response?.metadata?.error);
                    azPortalContext.log({
                        action: 'patch-agent',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: { error },
                    });
                    setAgentPatchFailure(error || 'Failed to patch agent');
                }
                return response;
            });
        },
        [resourceId, azPortalContext]
    );

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
        agentPatching,
        agentPatched,
        agentPatchFailure,
        patchAgent,
        refresh: getAgent,
    };
}

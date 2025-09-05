import { useCallback, useContext, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { SubAgentClient } from '../../../Common/Clients/SubAgentClient';
import { SubAgent } from '../../../Common/Contracts/SubAgent';

export function useSubAgents(resourceId: string) {
    const [subAgents, setSubAgents] = useState<SubAgent[]>();
    const [subAgentsLoading, setSubAgentsLoading] = useState(false);
    const [subAgentsLoaded, setSubAgentsLoaded] = useState(false);
    const [subAgentsLoadFailure, setSubAgentsLoadFailure] = useState('');
    const azPortalContext = useContext(AzPortalContext);

    const getAgent = useCallback(
        (agentEndpoint: string) => {
            azPortalContext.log({
                action: 'fetch-sub-agents',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId,
            });
            setSubAgents([]);
            setSubAgentsLoading(true);
            setSubAgentsLoaded(false);
            setSubAgentsLoadFailure('');

            SubAgentClient.getInstance(agentEndpoint)
                .getSubAgents()
                .then(response => {
                    setSubAgentsLoading(false);
                    if (response.isSuccessful && response.content) {
                        azPortalContext.log({
                            action: 'fetch-sub-agents',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId,
                        });
                        setSubAgents(response.content);
                        setSubAgentsLoaded(true);
                    } else {
                        azPortalContext.log({
                            action: 'fetch-sub-agents',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId,
                            data: { error: response.error },
                        });
                        setSubAgentsLoadFailure(response.error || 'Failed to load agent');
                    }
                });
        },
        [azPortalContext, resourceId]
    );

    return {
        subAgents,
        subAgentsLoading,
        subAgentsLoaded,
        subAgentsLoadFailure,
        initialize: getAgent,
        refresh: getAgent,
    };
}

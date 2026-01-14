import { useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { HttpResponseObject } from '../../../Common/ArmHelper.types';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

export interface SreAgentHook {
    agent: ArmObj<Agent> | undefined;
    agentLoading: boolean;
    agentLoaded: boolean;
    agentLoadFailure: string;
    agentPatching: boolean;
    agentPatched: boolean;
    agentPatchFailure: string;
    agentLastUpdatedTime: number | undefined;
    patchAgent: (agentPayload: Partial<ArmObj<Partial<Agent>>>) => Promise<HttpResponseObject<ArmObj<Agent>>>;
    refresh: () => void;
    startAgent: () => Promise<HttpResponseObject<ArmObj<Agent>>>;
    stopAgent: () => Promise<HttpResponseObject<ArmObj<Agent>>>;
}

export function useSreAgent(resourceId: string): SreAgentHook {
    const [agent, setAgent] = useState<ArmObj<Agent>>();
    const [agentLoading, setAgentLoading] = useState(false);
    const [agentLoaded, setAgentLoaded] = useState(false);
    const [agentLoadFailure, setAgentLoadFailure] = useState('');

    const [agentPatching, setAgentPatching] = useState(false);
    const [agentPatched, setAgentPatched] = useState(false);
    const [agentPatchFailure, setAgentPatchFailure] = useState('');
    const [agentLastUpdatedTime, setAgentLastUpdatedTime] = useState<number>();
    const azPortalContext = useContext(AzPortalContext);

    const intl = useIntl();

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
                    setAgentLastUpdatedTime(Date.now());
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

    const startAgent = useCallback(async () => {
        azPortalContext.log({
            action: 'start-agent',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });

        const agentName = new ArmResourceDescriptor(resourceId)?.resourceName || '';
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(SreAgentResources.startSreAgentNotificationTitle),
            intl.formatMessage(SreAgentResources.startSreAgentNotificationInProgress, { name: agentName })
        );

        const response = await SreAgentClient.startAgent(resourceId);
        if (response.metadata.success) {
            azPortalContext.log({
                action: 'startAgent',
                actionModifier: 'succeeded',
                resourceId: resourceId,
                logLevel: 'info',
            });
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(SreAgentResources.startSreAgentNotificationSuccess, { name: agentName })
            );
            setAgent(response.data);
        } else {
            const errorMessage = getErrorMessage(response.metadata.error) || '';
            azPortalContext.log({
                action: 'startAgent',
                actionModifier: 'failed',
                resourceId: resourceId,
                logLevel: 'error',
                data: {
                    error: errorMessage,
                },
            });
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(SreAgentResources.startSreAgentNotificationFailure, { name: agentName, errorMessage })
            );
        }

        return response;
    }, [azPortalContext, resourceId, intl]);

    const stopAgent = useCallback(async () => {
        azPortalContext.log({
            action: 'stop-agent',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });

        const agentName = new ArmResourceDescriptor(resourceId)?.resourceName || '';
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(SreAgentResources.stopSreAgentNotificationTitle),
            intl.formatMessage(SreAgentResources.stopSreAgentNotificationInProgress, { name: agentName })
        );

        const response = await SreAgentClient.stopAgent(resourceId);
        if (response.metadata.success) {
            azPortalContext.log({
                action: 'stopAgent',
                actionModifier: 'succeeded',
                resourceId: resourceId,
                logLevel: 'info',
            });

            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(SreAgentResources.stopSreAgentNotificationSuccess, { name: agentName })
            );

            setAgent(response.data);
        } else {
            const errorMessage = getErrorMessage(response.metadata.error) || '';
            azPortalContext.log({
                action: 'stopAgent',
                actionModifier: 'failed',
                resourceId: resourceId,
                logLevel: 'error',
                data: {
                    error: errorMessage,
                },
            });
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(SreAgentResources.stopSreAgentNotificationFailure, {
                    name: agentName,
                    errorMessage,
                })
            );
        }

        return response;
    }, [azPortalContext, resourceId, intl]);

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
        agentLastUpdatedTime,
        patchAgent,
        refresh: getAgent,
        startAgent,
        stopAgent,
    };
}

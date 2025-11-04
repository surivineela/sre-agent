import { useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { SreAgentResources } from '../../../Strings/SREAgentResources';

export interface AgentPowerStateHook {
    startAgent: (agentResourceId: string) => Promise<boolean>;
    stopAgent: (agentResourceId: string) => Promise<boolean>;
}

export function useAgentPowerState(onSuccess?: () => void): AgentPowerStateHook {
    const intl = useIntl();
    const az = useContext(AzPortalContext);

    const startAgent = useCallback(
        async (agentResourceId: string) => {
            if (!agentResourceId) return false;

            const agentName = new ArmResourceDescriptor(agentResourceId)?.resourceName || '';
            const notificationId = az.startNotification(
                intl.formatMessage(SreAgentResources.startingSreAgentTitle),
                intl.formatMessage(SreAgentResources.startingSreAgentInProgress, { name: agentName })
            );

            const response = await SreAgentClient.startAgent(agentResourceId);
            if (response.metadata.success) {
                az.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SreAgentResources.startingSreAgentSuccess, { name: agentName })
                );
                az.log({
                    action: 'startAgent',
                    actionModifier: 'succeeded',
                    resourceId: agentResourceId,
                    logLevel: 'info',
                    data: {
                        agentName,
                    },
                });
                onSuccess?.();
                return true;
            } else {
                const errorMessage = getErrorMessage(response.metadata.error) || '';
                az.stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(SreAgentResources.startingSreAgentFailedWithError, { name: agentName, error: errorMessage })
                        : intl.formatMessage(SreAgentResources.startingSreAgentFailed, { name: agentName })
                );
                az.log({
                    action: 'startAgent',
                    actionModifier: 'failed',
                    resourceId: agentResourceId,
                    logLevel: 'error',
                    data: {
                        message: 'Failed to start SRE Agent',
                        agentName,
                        error: response.metadata.error,
                    },
                });
                return false;
            }
        },
        [az, intl, onSuccess]
    );

    const stopAgent = useCallback(
        async (agentResourceId: string) => {
            if (!agentResourceId) return false;

            const agentName = new ArmResourceDescriptor(agentResourceId)?.resourceName || '';
            const notificationId = az.startNotification(
                intl.formatMessage(SreAgentResources.stoppingSreAgentTitle),
                intl.formatMessage(SreAgentResources.stoppingSreAgentInProgress, { name: agentName })
            );

            const response = await SreAgentClient.stopAgent(agentResourceId);
            if (response.metadata.success) {
                az.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SreAgentResources.stoppingSreAgentSuccess, { name: agentName })
                );
                az.log({
                    action: 'stopAgent',
                    actionModifier: 'succeeded',
                    resourceId: agentResourceId,
                    logLevel: 'info',
                    data: {
                        agentName,
                    },
                });
                onSuccess?.();
                return true;
            } else {
                const errorMessage = getErrorMessage(response.metadata.error) || '';
                az.stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(SreAgentResources.stoppingSreAgentFailedWithError, { name: agentName, error: errorMessage })
                        : intl.formatMessage(SreAgentResources.stoppingSreAgentFailed, { name: agentName })
                );
                az.log({
                    action: 'stopAgent',
                    actionModifier: 'failed',
                    resourceId: agentResourceId,
                    logLevel: 'error',
                    data: {
                        message: 'Failed to stop SRE Agent',
                        agentName,
                        error: response.metadata.error,
                    },
                });
                return false;
            }
        },
        [az, intl, onSuccess]
    );

    return {
        startAgent,
        stopAgent,
    };
}

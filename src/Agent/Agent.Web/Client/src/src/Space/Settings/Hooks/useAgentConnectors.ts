import { useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';

export const useAgentConnectors = (agentResourceId: string) => {
    const [connectors, setConnectors] = useState<Connector[]>([]);
    const [isConnectorsLoading, setIsConnectorsLoading] = useState(true);
    const [connectorsFailure, setConnectorsFailure] = useState('');

    const [isConnectorsUpdating, setIsConnectorsUpdating] = useState(false);
    const [connectorsUpdateFailure, setConnectorsUpdateFailure] = useState('');
    const azPortalContext = useContext(AzPortalContext);

    const getConnectors = useCallback(async () => {
        azPortalContext.log({
            action: 'fetch-agent-connectors',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: agentResourceId,
        });
        setIsConnectorsLoading(true);
        setConnectorsUpdateFailure('');

        const listConnectorsPromise = SreAgentClient.listDataConnectors(agentResourceId);
        const listConnectorsSecretsPromise = SreAgentClient.listConnectorsSecrets(agentResourceId);

        const [connectorsResponse, connectorsSecretsResponse] = await Promise.all([listConnectorsPromise, listConnectorsSecretsPromise]);

        setIsConnectorsLoading(false);
        if (connectorsResponse?.metadata?.success && connectorsResponse.data) {
            azPortalContext.log({
                action: 'fetch-agent-connectors',
                actionModifier: 'success',
                logLevel: 'info',
                resourceId: agentResourceId,
            });

            const connectorsArray = connectorsResponse.data.value.map(armObj => armObj.properties);

            if (connectorsSecretsResponse?.metadata?.success && connectorsSecretsResponse.data) {
                azPortalContext.log({
                    action: 'fetch-agent-connectors-secrets',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: agentResourceId,
                });
                const connectorsWithSecretsArray = connectorsSecretsResponse.data.value.map(armObj => armObj.properties);
                connectorsArray.forEach(connector => {
                    const matchingSecret = connectorsWithSecretsArray.find(dc => dc.name === connector.name);
                    if (matchingSecret) {
                        connector.dataSource = matchingSecret.dataSource;
                    }
                });
            } else {
                const error = getErrorMessage(connectorsSecretsResponse?.metadata?.error);
                azPortalContext.log({
                    action: 'fetch-agent-connectors-secrets',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId: agentResourceId,
                    data: { error },
                });
            }

            setConnectors(connectorsArray);
        } else {
            const error = getErrorMessage(connectorsResponse?.metadata?.error);
            azPortalContext.log({
                action: 'fetch-agent-connectors',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId: agentResourceId,
                data: { error },
            });
            setConnectorsFailure(error || 'Failed to get agent connectors');
        }
    }, [agentResourceId, azPortalContext]);

    const putConnector = useCallback(
        (connectorPayload: Connector) => {
            azPortalContext.log({
                action: 'put-agent-connector',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: agentResourceId,
            });
            setIsConnectorsUpdating(true);
            setConnectorsUpdateFailure('');

            return SreAgentClient.putDataConnector(`${agentResourceId}/DataConnectors/${connectorPayload.name}`, connectorPayload).then(
                response => {
                    setIsConnectorsUpdating(false);
                    if (response?.metadata?.success && response.data) {
                        azPortalContext.log({
                            action: 'put-agent-connector',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId: agentResourceId,
                        });
                    } else {
                        const error = getErrorMessage(response?.metadata?.error);
                        azPortalContext.log({
                            action: 'put-agent-connector',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId: agentResourceId,
                            data: { error },
                        });
                        setConnectorsUpdateFailure(error || 'Failed to update agent connectors');
                    }
                    return response;
                }
            );
        },
        [agentResourceId, azPortalContext]
    );

    const deleteConnector = useCallback(
        (connectorName: string) => {
            azPortalContext.log({
                action: 'delete-agent-connector',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: agentResourceId,
            });
            setIsConnectorsUpdating(true);
            setConnectorsUpdateFailure('');

            return SreAgentClient.deleteDataConnector(`${agentResourceId}/DataConnectors/${connectorName}`).then(response => {
                setIsConnectorsUpdating(false);
                if (response?.metadata?.success) {
                    azPortalContext.log({
                        action: 'delete-agent-connector',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: agentResourceId,
                    });
                } else {
                    const error = getErrorMessage(response?.metadata?.error);
                    azPortalContext.log({
                        action: 'delete-agent-connector',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: agentResourceId,
                        data: { error },
                    });
                    setConnectorsUpdateFailure(error || 'Failed to delete agent connectors');
                }
                return response;
            });
        },
        [agentResourceId, azPortalContext]
    );

    useEffect(() => {
        if (agentResourceId) {
            getConnectors();
        }
    }, [agentResourceId, getConnectors]);

    return {
        connectors,
        isConnectorsLoading,
        connectorsFailure,
        isConnectorsUpdating,
        connectorsUpdateFailure,
        putConnector,
        deleteConnector,
        refreshConnectors: getConnectors,
    };
};

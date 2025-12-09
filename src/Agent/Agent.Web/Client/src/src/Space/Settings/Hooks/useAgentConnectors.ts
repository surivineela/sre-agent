import { useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { ExtendedAgentClient } from '../../../Common/Clients/ExtendedAgentClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { Connector, ConnectorStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources } from '../../../Strings/SREAgentResources';

export const useAgentConnectors = (agentResourceId: string) => {
    const intl = useIntl();
    const [connectors, setConnectors] = useState<Connector[]>([]);
    const [isConnectorsLoading, setIsConnectorsLoading] = useState(true);
    const [connectorsFailure, setConnectorsFailure] = useState('');

    const [isConnectorsUpdating, setIsConnectorsUpdating] = useState(false);
    const [connectorsUpdateFailure, setConnectorsUpdateFailure] = useState('');
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [connectionMap, setConnectionMap] = useState<Record<string, ConnectorStatus>>({});
    const [loadingStatusMap, setLoadingStatusMap] = useState<Record<string, boolean>>({});

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
            setIsConnectorsLoading(false);
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
            setIsConnectorsLoading(false);
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

    useEffect(() => {
        if (!sreAgentEndpoint) {
            return;
        }

        if (!connectors || connectors.length === 0) {
            setConnectionMap({});
            setLoadingStatusMap({});
            return;
        }

        setConnectionMap({});
        const loadingMap: Record<string, boolean> = {};
        connectors.forEach(c => {
            loadingMap[c.name] = true;
        });
        setLoadingStatusMap(loadingMap);

        const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

        connectors.forEach(c => {
            azPortalContext.log({
                action: 'fetch-connector-status',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: agentResourceId,
                data: { connectorName: c.name },
            });

            const timeoutPromise = new Promise<never>((_, reject) => {
                setTimeout(() => reject(new Error('Request timeout')), 20000);
            });

            Promise.race([extendedAgentClient.getConnectorStatus(c.name), timeoutPromise])
                .then(response => {
                    if (response.isSuccessful && response.content) {
                        setConnectionMap(prev => ({
                            ...prev,
                            [c.name]: response.content!,
                        }));
                    } else {
                        azPortalContext.log({
                            action: 'fetch-connector-status',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId: agentResourceId,
                            data: { connectorName: c.name, error: response.error },
                        });
                        setConnectionMap(prev => ({
                            ...prev,
                            [c.name]: {
                                name: c.name,
                                type: c.dataConnectorType,
                                healthy: false,
                                message: intl.formatMessage(ConnectorsResources.failedToFetchStatus),
                                status: 'Error',
                                executionTimeMs: 0,
                            },
                        }));
                    }
                })
                .catch(error => {
                    const isTimeout = error.message === 'Request timeout';
                    azPortalContext.log({
                        action: 'fetch-connector-status',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: agentResourceId,
                        data: { connectorName: c.name, error: isTimeout ? 'Request timeout after 20s' : error },
                    });
                    setConnectionMap(prev => ({
                        ...prev,
                        [c.name]: {
                            name: c.name,
                            type: c.dataConnectorType,
                            healthy: false,
                            message: isTimeout
                                ? intl.formatMessage(ConnectorsResources.requestTimeout)
                                : intl.formatMessage(ConnectorsResources.failedToFetchStatus),
                            status: 'Error',
                            executionTimeMs: 0,
                        },
                    }));
                })
                .finally(() => {
                    setLoadingStatusMap(prev => ({
                        ...prev,
                        [c.name]: false,
                    }));
                });
        });
    }, [connectors, sreAgentEndpoint, agentResourceId, azPortalContext, intl]);

    return {
        connectors,
        isConnectorsLoading,
        connectorsFailure,
        isConnectorsUpdating,
        connectorsUpdateFailure,
        putConnector,
        deleteConnector,
        refreshConnectors: getConnectors,
        connectionMap,
        loadingStatusMap,
    };
};

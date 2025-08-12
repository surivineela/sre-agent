import { useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { DataConnector } from '../../../Common/Contracts/Azure/SreAgent';

export const useAgentDataConnectors = (agentResourceId: string) => {
    const [dataConnectors, setDataConnectors] = useState<DataConnector[]>([]);
    const [isDataConnectorsLoading, setIsDataConnectorsLoading] = useState(false);
    const [getDataConnectorsFailure, setGetDataConnectorsFailure] = useState('');

    const [isDataConnectorsUpdating, setIsDataConnectorsUpdating] = useState(false);
    const [dataConnectorsUpdateFailure, setDataConnectorsUpdateFailure] = useState('');
    const azPortalContext = useContext(AzPortalContext);

    const getDataConnectors = useCallback(async () => {
        azPortalContext.log({
            action: 'fetch-agent-data-connectors',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: agentResourceId,
        });
        setIsDataConnectorsLoading(true);
        setDataConnectorsUpdateFailure('');

        const listDataConnectorsPromise = SreAgentClient.listDataConnectors(agentResourceId);
        const listDataConnectorsSecretsPromise = SreAgentClient.listDataConnectorsSecrets(agentResourceId);

        const [dataConnectorsResponse, dataConnectorsSecretsResponse] = await Promise.all([
            listDataConnectorsPromise,
            listDataConnectorsSecretsPromise,
        ]);

        setIsDataConnectorsLoading(false);
        if (dataConnectorsResponse?.metadata?.success && dataConnectorsResponse.data) {
            azPortalContext.log({
                action: 'fetch-agent-data-connectors',
                actionModifier: 'success',
                logLevel: 'info',
                resourceId: agentResourceId,
            });

            const dataConnectorsArray = dataConnectorsResponse.data.value.map(armObj => armObj.properties);

            if (dataConnectorsSecretsResponse?.metadata?.success && dataConnectorsSecretsResponse.data) {
                azPortalContext.log({
                    action: 'fetch-agent-data-connectors-secrets',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: agentResourceId,
                });
                const dataConnectorsWithSecretsArray = dataConnectorsSecretsResponse.data.value.map(armObj => armObj.properties);
                dataConnectorsArray.forEach(connector => {
                    const matchingSecret = dataConnectorsWithSecretsArray.find(dc => dc.name === connector.name);
                    if (matchingSecret) {
                        connector.dataSource = matchingSecret.dataSource;
                    }
                });
            } else {
                const error = getErrorMessage(dataConnectorsSecretsResponse?.metadata?.error);
                azPortalContext.log({
                    action: 'fetch-agent-data-connectors-secrets',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId: agentResourceId,
                    data: { error },
                });
            }

            setDataConnectors(dataConnectorsArray);
        } else {
            const error = getErrorMessage(dataConnectorsResponse?.metadata?.error);
            azPortalContext.log({
                action: 'fetch-agent-data-connectors',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId: agentResourceId,
                data: { error },
            });
            setGetDataConnectorsFailure(error || 'Failed to get agent data connectors');
        }
    }, [agentResourceId, azPortalContext]);

    const putDataConnector = useCallback(
        (dataConnectorPayload: DataConnector) => {
            azPortalContext.log({
                action: 'put-agent-data-connector',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: agentResourceId,
            });
            setIsDataConnectorsUpdating(true);
            setDataConnectorsUpdateFailure('');

            return SreAgentClient.putDataConnector(
                `${agentResourceId}/DataConnectors/${dataConnectorPayload.name}`,
                dataConnectorPayload
            ).then(response => {
                setIsDataConnectorsUpdating(false);
                if (response?.metadata?.success && response.data) {
                    azPortalContext.log({
                        action: 'put-agent-data-connector',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: agentResourceId,
                    });
                } else {
                    const error = getErrorMessage(response?.metadata?.error);
                    azPortalContext.log({
                        action: 'put-agent-data-connector',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: agentResourceId,
                        data: { error },
                    });
                    setDataConnectorsUpdateFailure(error || 'Failed to update agent data connectors');
                }
                return response;
            });
        },
        [agentResourceId, azPortalContext]
    );

    const deleteDataConnector = useCallback(
        (dataConnectorName: string) => {
            azPortalContext.log({
                action: 'delete-agent-data-connector',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: agentResourceId,
            });
            setIsDataConnectorsUpdating(true);
            setDataConnectorsUpdateFailure('');

            return SreAgentClient.deleteDataConnector(`${agentResourceId}/DataConnectors/${dataConnectorName}`).then(response => {
                setIsDataConnectorsUpdating(false);
                if (response?.metadata?.success) {
                    azPortalContext.log({
                        action: 'delete-agent-data-connector',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: agentResourceId,
                    });
                } else {
                    const error = getErrorMessage(response?.metadata?.error);
                    azPortalContext.log({
                        action: 'delete-agent-data-connector',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId: agentResourceId,
                        data: { error },
                    });
                    setDataConnectorsUpdateFailure(error || 'Failed to delete agent data connectors');
                }
                return response;
            });
        },
        [agentResourceId, azPortalContext]
    );

    useEffect(() => {
        if (agentResourceId) {
            getDataConnectors();
        }
    }, [agentResourceId, getDataConnectors]);

    return {
        dataConnectors,
        isDataConnectorsLoading,
        getDataConnectorsFailure,
        isDataConnectorsUpdating,
        dataConnectorsUpdateFailure,
        putDataConnector,
        deleteDataConnector,
        refreshDataConnectors: getDataConnectors,
    };
};

import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { ConnectorStatus } from '../../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';

interface UseKustoToolConnectorStatusResult {
    connectionMap: Record<string, ConnectorStatus>;
    loadingStatusMap: Record<string, boolean>;
    refreshStatuses: () => void;
}

export const useKustoToolConnectorStatus = (kustoTools: ExtendedTool[]): UseKustoToolConnectorStatusResult => {
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [connectionMap, setConnectionMap] = useState<Record<string, ConnectorStatus>>({});
    const [loadingStatusMap, setLoadingStatusMap] = useState<Record<string, boolean>>({});

    const uniqueConnectorNames = useMemo(() => {
        const connectorNames = kustoTools.map(tool => tool.connector).filter((connector): connector is string => !!connector);
        return Array.from(new Set(connectorNames));
    }, [kustoTools]);

    const fetchConnectorStatuses = useCallback(() => {
        if (!sreAgentEndpoint || uniqueConnectorNames.length === 0) {
            setConnectionMap({});
            setLoadingStatusMap({});
            return;
        }

        setConnectionMap({});
        const loadingMap: Record<string, boolean> = {};
        uniqueConnectorNames.forEach(connectorName => {
            loadingMap[connectorName] = true;
        });
        setLoadingStatusMap(loadingMap);

        const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

        uniqueConnectorNames.forEach(connectorName => {
            azPortalContext.log({
                action: 'fetch-kusto-tool-connector-status',
                actionModifier: 'start',
                logLevel: 'info',
                data: { connectorName },
            });

            const timeoutPromise = new Promise<never>((_, reject) => {
                setTimeout(() => reject(new Error('Request timeout')), 20000);
            });

            Promise.race([extendedAgentClient.getConnectorStatus(connectorName), timeoutPromise])
                .then(response => {
                    if (response.isSuccessful && response.content) {
                        setConnectionMap(prev => ({
                            ...prev,
                            [connectorName]: response.content!,
                        }));
                    } else {
                        azPortalContext.log({
                            action: 'fetch-kusto-tool-connector-status',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            data: { connectorName, error: response.error },
                        });
                        setConnectionMap(prev => ({
                            ...prev,
                            [connectorName]: {
                                name: connectorName,
                                type: 'kusto',
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
                        action: 'fetch-kusto-tool-connector-status',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        data: { connectorName, error: isTimeout ? 'Request timeout after 20s' : error },
                    });
                    setConnectionMap(prev => ({
                        ...prev,
                        [connectorName]: {
                            name: connectorName,
                            type: 'kusto',
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
                        [connectorName]: false,
                    }));
                });
        });
    }, [sreAgentEndpoint, uniqueConnectorNames, azPortalContext, intl]);

    useEffect(() => {
        fetchConnectorStatuses();
    }, [fetchConnectorStatuses]);

    return {
        connectionMap,
        loadingStatusMap,
        refreshStatuses: fetchConnectorStatuses,
    };
};

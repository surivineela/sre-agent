import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentSpaceClient } from '../../../Common/Clients/AgentSpaceClient';
import { SreAgentClient } from '../../../Common/Clients/SreAgentClient';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useNotifications } from '../../../Common/Contexts/NotificationContext';
import { AgentSpace, AgentSpaceConnector } from '../../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../../Common/Contracts/Arm';
import { SreAgentArgItem } from '../../../Common/Contracts/SreAgent';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';
import { PortalResources } from '../../../Strings/Resources';

interface UseAgentSpaceResult {
    agentSpace: ArmObj<AgentSpace> | null;
    memberAgents: SreAgentArgItem[];
    connectors: AgentSpaceConnector[];
    isLoading: boolean;
    isLoadingAgents: boolean;
    isLoadingConnectors: boolean;
    error: string | null;
    refresh: () => Promise<void>;
    refreshAgents: () => Promise<void>;
    refreshConnectors: () => Promise<void>;
    startAgents: (agentIds: string[]) => Promise<void>;
    stopAgents: (agentIds: string[]) => Promise<void>;
    removeAgentsFromSpace: (agentIds: string[]) => Promise<void>;
    createConnector: (connector: AgentSpaceConnector) => Promise<boolean>;
    updateConnector: (connector: AgentSpaceConnector) => Promise<boolean>;
    deleteConnectors: (connectorNames: string[]) => Promise<boolean>;
}

export const useAgentSpace = (resourceId: string): UseAgentSpaceResult => {
    const intl = useIntl();
    const { logEvent } = useTelemetry(TelemetrySource.AgentSpaceView, resourceId);
    const { start, succeed, fail } = useNotifications();

    const [agentSpace, setAgentSpace] = useState<ArmObj<AgentSpace> | null>(null);
    const [memberAgents, setMemberAgents] = useState<SreAgentArgItem[]>([]);
    const [connectors, setConnectors] = useState<AgentSpaceConnector[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [isLoadingAgents, setIsLoadingAgents] = useState(true);
    const [isLoadingConnectors, setIsLoadingConnectors] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const agentSpaceClient = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.AgentSpaceView), []);
    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.AgentSpaceView), []);

    const fetchAgentSpace = useCallback(async () => {
        if (!resourceId) {
            setIsLoading(false);
            return;
        }

        setIsLoading(true);
        setError(null);

        const response = await agentSpaceClient.getAgentSpace(resourceId);

        if (!response.isSuccessful) {
            const errorMessage = getArmErrorMessage(response.error);
            setError(errorMessage);
            logEvent({
                action: 'fetch-agent-space',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: { error: errorMessage },
            });
        } else {
            setAgentSpace(response.content || null);
        }

        setIsLoading(false);
    }, [agentSpaceClient, resourceId, logEvent]);

    const fetchMemberAgents = useCallback(async () => {
        if (!resourceId) {
            setIsLoadingAgents(false);
            return;
        }

        setIsLoadingAgents(true);

        const response = await agentSpaceClient.getAgentsInSpace(resourceId);

        if (!response.isSuccessful) {
            logEvent({
                action: 'fetch-member-agents',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: {
                    error: response.error instanceof Error ? response.error.message : String(response.error),
                },
            });
            setMemberAgents([]);
        } else {
            setMemberAgents(response.content || []);
        }

        setIsLoadingAgents(false);
    }, [agentSpaceClient, resourceId, logEvent]);

    const fetchConnectors = useCallback(async () => {
        if (!resourceId) {
            setIsLoadingConnectors(false);
            return;
        }

        setIsLoadingConnectors(true);

        // Fetch both connectors and secrets in parallel
        const [connectorsResponse, secretsResponse] = await Promise.all([
            agentSpaceClient.getConnectors(resourceId),
            agentSpaceClient.listConnectorSecrets(resourceId),
        ]);

        if (!connectorsResponse.isSuccessful) {
            logEvent({
                action: 'fetch-connectors',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: {
                    error: connectorsResponse.error instanceof Error ? connectorsResponse.error.message : String(connectorsResponse.error),
                },
            });
            setConnectors([]);
        } else {
            // Extract properties from ARM objects
            const connectorsList = (connectorsResponse.content?.value || []).map(armObj => ({
                ...armObj.properties,
                name: armObj.name,
            }));

            // Merge secrets into connectors by name
            if (secretsResponse.isSuccessful && secretsResponse.content) {
                const secretsMap = new Map((secretsResponse.content.value || []).map(s => [s.name, s.properties.dataSource]));
                connectorsList.forEach(connector => {
                    connector.dataSource = secretsMap.get(connector.name) || connector.dataSource;
                });
            } else {
                logEvent({
                    action: 'fetch-connector-secrets',
                    actionModifier: 'error',
                    logLevel: LogLevel.Warning,
                    additionalData: {
                        error: secretsResponse.error instanceof Error ? secretsResponse.error.message : String(secretsResponse.error),
                    },
                });
            }

            setConnectors(connectorsList);
        }

        setIsLoadingConnectors(false);
    }, [agentSpaceClient, resourceId, logEvent]);

    const refresh = useCallback(async () => {
        await fetchAgentSpace();
    }, [fetchAgentSpace]);

    const refreshAgents = useCallback(async () => {
        await fetchMemberAgents();
    }, [fetchMemberAgents]);

    const refreshConnectors = useCallback(async () => {
        await fetchConnectors();
    }, [fetchConnectors]);

    const getAgentName = useCallback(
        (agentId: string): string => {
            const agent = memberAgents.find(a => a.id === agentId);
            return agent?.name || agentId;
        },
        [memberAgents]
    );

    const startAgents = useCallback(
        async (agentIds: string[]): Promise<void> => {
            if (agentIds.length === 0) return;

            const agentNames = agentIds.map(getAgentName);
            const notificationId = start(
                intl.formatMessage(PortalResources.startingAgent),
                intl.formatMessage(PortalResources.startingAgentInProgress)
            );

            const results = await Promise.all(agentIds.map(id => sreAgentClient.startAgent(id)));
            const failures = results.filter(result => !result.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.startingAgent),
                    agentIds.length === 1
                        ? intl.formatMessage(PortalResources.startAgentSuccess, { name: agentNames[0] })
                        : intl.formatMessage(PortalResources.startAgentSuccess, { name: `${agentIds.length} agents` })
                );
                await refreshAgents();
            } else {
                const errorDetail = getArmErrorMessage(failures[0]?.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.startAgentError),
                    errorDetail
                        ? intl.formatMessage(PortalResources.startAgentErrorDetail, { error: errorDetail })
                        : intl.formatMessage(PortalResources.startAgentError)
                );
                logEvent({
                    action: 'start-agents',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { failedCount: failures.length, totalCount: agentIds.length },
                });
            }
        },
        [getAgentName, intl, logEvent, refreshAgents, sreAgentClient, start, succeed, fail]
    );

    const stopAgents = useCallback(
        async (agentIds: string[]): Promise<void> => {
            if (agentIds.length === 0) return;

            const agentNames = agentIds.map(getAgentName);
            const notificationId = start(
                intl.formatMessage(PortalResources.stoppingAgent),
                intl.formatMessage(PortalResources.stoppingAgentInProgress)
            );

            const results = await Promise.all(agentIds.map(id => sreAgentClient.stopAgent(id)));
            const failures = results.filter(result => !result.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.stoppingAgent),
                    agentIds.length === 1
                        ? intl.formatMessage(PortalResources.stopAgentSuccess, { name: agentNames[0] })
                        : intl.formatMessage(PortalResources.stopAgentSuccess, { name: `${agentIds.length} agents` })
                );
                await refreshAgents();
            } else {
                const errorDetail = getArmErrorMessage(failures[0]?.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.stopAgentError),
                    errorDetail
                        ? intl.formatMessage(PortalResources.stopAgentErrorDetail, { error: errorDetail })
                        : intl.formatMessage(PortalResources.stopAgentError)
                );
                logEvent({
                    action: 'stop-agents',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { failedCount: failures.length, totalCount: agentIds.length },
                });
            }
        },
        [getAgentName, intl, logEvent, refreshAgents, sreAgentClient, start, succeed, fail]
    );

    const removeAgentsFromSpace = useCallback(
        async (agentIds: string[]): Promise<void> => {
            if (agentIds.length === 0) return;

            const agentNames = agentIds.map(getAgentName);
            const notificationId = start(
                intl.formatMessage(PortalResources.removingAgentFromSpace),
                intl.formatMessage(PortalResources.removingAgentFromSpaceInProgress)
            );

            const results = await Promise.all(agentIds.map(id => sreAgentClient.updateAgent(id, { agentSpaceId: null })));
            const failures = results.filter(result => !result.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.removingAgentFromSpace),
                    agentIds.length === 1
                        ? intl.formatMessage(PortalResources.removeAgentFromSpaceSuccess, { name: agentNames[0] })
                        : intl.formatMessage(PortalResources.removeAgentFromSpaceSuccess, { name: `${agentIds.length} agents` })
                );
                await refreshAgents();
            } else {
                const errorDetail = getArmErrorMessage(failures[0]?.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.removeAgentFromSpaceError),
                    errorDetail
                        ? intl.formatMessage(PortalResources.removeAgentFromSpaceErrorDetail, { error: errorDetail })
                        : intl.formatMessage(PortalResources.removeAgentFromSpaceError)
                );
                logEvent({
                    action: 'remove-agents-from-space',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { failedCount: failures.length, totalCount: agentIds.length },
                });
            }
        },
        [getAgentName, intl, logEvent, refreshAgents, sreAgentClient, start, succeed, fail]
    );

    const createConnector = useCallback(
        async (connector: AgentSpaceConnector): Promise<boolean> => {
            const notificationId = start(
                intl.formatMessage(PortalResources.createConnector),
                intl.formatMessage(PortalResources.creatingConnector)
            );

            const response = await agentSpaceClient.createOrUpdateConnector(resourceId, connector.name, connector);

            if (response.isSuccessful) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.createConnector),
                    intl.formatMessage(PortalResources.createConnectorSuccess, { name: connector.name })
                );
                await refreshConnectors();
                return true;
            } else {
                const errorDetail = getArmErrorMessage(response.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.createConnector),
                    errorDetail
                        ? intl.formatMessage(PortalResources.createConnectorErrorDetail, {
                              name: connector.name,
                              error: errorDetail,
                          })
                        : intl.formatMessage(PortalResources.createConnectorError)
                );
                logEvent({
                    action: 'create-connector',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { error: errorDetail },
                });
                return false;
            }
        },
        [agentSpaceClient, resourceId, intl, start, succeed, fail, refreshConnectors, logEvent]
    );

    const updateConnector = useCallback(
        async (connector: AgentSpaceConnector): Promise<boolean> => {
            const notificationId = start(
                intl.formatMessage(PortalResources.updateConnector),
                intl.formatMessage(PortalResources.updatingConnector)
            );

            const response = await agentSpaceClient.createOrUpdateConnector(resourceId, connector.name, connector);

            if (response.isSuccessful) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.updateConnector),
                    intl.formatMessage(PortalResources.updateConnectorSuccess, { name: connector.name })
                );
                await refreshConnectors();
                return true;
            } else {
                const errorDetail = getArmErrorMessage(response.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.updateConnector),
                    errorDetail
                        ? intl.formatMessage(PortalResources.updateConnectorErrorDetail, {
                              name: connector.name,
                              error: errorDetail,
                          })
                        : intl.formatMessage(PortalResources.updateConnectorError)
                );
                logEvent({
                    action: 'update-connector',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { error: errorDetail },
                });
                return false;
            }
        },
        [agentSpaceClient, resourceId, intl, start, succeed, fail, refreshConnectors, logEvent]
    );

    const deleteConnectors = useCallback(
        async (connectorNames: string[]): Promise<boolean> => {
            if (connectorNames.length === 0) return true;

            const notificationId = start(
                intl.formatMessage(PortalResources.deleteConnector),
                intl.formatMessage(PortalResources.deletingConnector)
            );

            const results = await Promise.all(connectorNames.map(name => agentSpaceClient.deleteConnector(resourceId, name)));
            const failures = results.filter(result => !result.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.deleteConnector),
                    connectorNames.length === 1
                        ? intl.formatMessage(PortalResources.deleteConnectorSuccess, { name: connectorNames[0] })
                        : intl.formatMessage(PortalResources.deleteConnectorsSuccess, { count: connectorNames.length })
                );
                await refreshConnectors();
                return true;
            } else {
                const isSingleConnector = connectorNames.length === 1;
                const errorDetail = getArmErrorMessage(failures[0]?.error);
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.deleteConnector),
                    isSingleConnector && errorDetail
                        ? intl.formatMessage(PortalResources.deleteConnectorErrorDetail, {
                              name: connectorNames[0],
                              error: errorDetail,
                          })
                        : intl.formatMessage(PortalResources.deleteConnectorError)
                );
                logEvent({
                    action: 'delete-connectors',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: { failedCount: failures.length, totalCount: connectorNames.length },
                });
                return false;
            }
        },
        [agentSpaceClient, resourceId, intl, start, succeed, fail, refreshConnectors, logEvent]
    );

    useEffect(() => {
        fetchAgentSpace();
        fetchMemberAgents();
        fetchConnectors();
    }, [fetchAgentSpace, fetchMemberAgents, fetchConnectors]);

    return {
        agentSpace,
        memberAgents,
        connectors,
        isLoading,
        isLoadingAgents,
        isLoadingConnectors,
        error,
        refresh,
        refreshAgents,
        refreshConnectors,
        startAgents,
        stopAgents,
        removeAgentsFromSpace,
        createConnector,
        updateConnector,
        deleteConnectors,
    };
};

import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import DeleteConfirmationDialog from '../DataKnowledgeSpaceComponents.tsx/DeleteConfirmationDialog';
import { useAgentConnectors } from '../Hooks/useAgentConnectors';
import { useSreAgent } from '../Hooks/useSreAgent';
import { useConnectorsStyles } from './Connectors.styles';
import { ConnectorsDataGrid } from './ConnectorsDataGrid';
import ConnectorsToolbar from './ConnectorsToolbar';
import { ConnectorType } from './Wizard/Common/ConnectorType';
import { ConnectorWizardFormik } from './Wizard/ConnectorWizardFormik';

export const Connectors = () => {
    const intl = useIntl();
    const styles = useConnectorsStyles();

    const { resourceId } = useContext(EnvironmentContext);
    const { log, startNotification, stopNotification } = useContext(AzPortalContext);

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [isOperationInProgress, setIsOperationInProgress] = useState(false);
    const [searchTerm, setSearchTerm] = useState<string>('');
    const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
    const [selectedConnector, setSelectedConnector] = useState<Connector | undefined>();

    const { agent, refresh: refreshAgent } = useSreAgent(resourceId);
    const { connectors, isConnectorsLoading, putConnector, deleteConnector, refreshConnectors } = useAgentConnectors(resourceId);

    const refresh = useCallback(async () => {
        setIsRefreshing(true);
        await Promise.all([refreshAgent(), refreshConnectors()]);
        setIsRefreshing(false);
    }, [refreshAgent, refreshConnectors]);

    const addNewConnector = useCallback(() => {
        setSelectedConnector(undefined);
        setSelectedKeys(new Set());
        setIsDialogOpen(true);
    }, []);

    const onBulkDelete = useCallback(() => {
        setIsDeleteConfirmOpen(true);
    }, []);

    const createDataConnection = useCallback(
        async (connector: Connector) => {
            setIsOperationInProgress(true);
            const notificationId = startNotification(
                intl.formatMessage(ConnectorsResources.creatingConnector),
                intl.formatMessage(ConnectorsResources.creatingConnectorDescription, { name: connector.name })
            );

            const response = await putConnector(connector);
            if (response.metadata.success) {
                refresh();
                stopNotification(notificationId, true, intl.formatMessage(ConnectorsResources.connectorCreated, { name: connector.name }));
            } else {
                const errorMessage = getErrorMessage(response.metadata.error);

                log({
                    action: 'createDataConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to create data connector: ${errorMessage}`,
                    },
                });

                stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(ConnectorsResources.createConnectorWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(ConnectorsResources.createConnectorFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [startNotification, intl, putConnector, refresh, stopNotification, log, resourceId]
    );

    const updateDataConnection = useCallback(
        async (connector: Connector) => {
            setIsOperationInProgress(true);
            const notificationId = startNotification(
                intl.formatMessage(ConnectorsResources.updatingConnector),
                intl.formatMessage(ConnectorsResources.updatingConnectorDescription, { name: connector.name })
            );

            const response = await putConnector(connector);
            if (response.metadata.success) {
                refresh();
                stopNotification(notificationId, true, intl.formatMessage(ConnectorsResources.connectorUpdated, { name: connector.name }));
            } else {
                const errorMessage = getErrorMessage(response.metadata.error);

                log({
                    action: 'updateConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to update connector: ${errorMessage}`,
                    },
                });

                stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(ConnectorsResources.updateConnectorWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(ConnectorsResources.updateConnectorFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [startNotification, intl, putConnector, refresh, stopNotification, log, resourceId]
    );

    const onSubmit = useCallback(
        (dataConnector: Connector) => {
            if (selectedConnector) {
                updateDataConnection(dataConnector);
            } else {
                createDataConnection(dataConnector);
            }
        },
        [selectedConnector, updateDataConnection, createDataConnection]
    );

    const bulkDeleteDataConnectors = useCallback(async () => {
        if (selectedKeys.size === 0) {
            return;
        }

        setIsOperationInProgress(true);
        const selectedNames = Array.from(selectedKeys);
        const isPlural = selectedNames.length > 1;

        const notificationId = startNotification(
            isPlural
                ? intl.formatMessage(ConnectorsResources.deletingMultipleConnectors, { count: selectedNames.length })
                : intl.formatMessage(ConnectorsResources.deletingConnector),
            isPlural
                ? intl.formatMessage(ConnectorsResources.deletingMultipleConnectors, { count: selectedNames.length })
                : intl.formatMessage(ConnectorsResources.deletingConnectorDescription, { name: selectedNames[0] })
        );

        let successCount = 0;
        let failedCount = 0;
        const failedItems: string[] = [];

        for (const name of selectedNames) {
            try {
                const response = await deleteConnector(name);
                if (response.metadata.success) {
                    successCount++;
                } else {
                    failedCount++;
                    failedItems.push(name);
                    log({
                        action: 'deleteConnector',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            message: `Failed to delete connector ${name}: ${getErrorMessage(response.metadata.error)}`,
                        },
                    });
                }
            } catch (error) {
                failedCount++;
                failedItems.push(name);
                log({
                    action: 'deleteConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to delete connector ${name}: ${error}`,
                    },
                });
            }
        }

        setSelectedKeys(new Set());
        setSelectedConnector(undefined);
        refresh();

        if (failedCount === 0) {
            stopNotification(
                notificationId,
                true,
                isPlural
                    ? intl.formatMessage(ConnectorsResources.successfullyDeletedMultiple, { count: successCount })
                    : intl.formatMessage(ConnectorsResources.connectorDeleted, { name: selectedNames[0] })
            );
        } else if (successCount === 0) {
            stopNotification(
                notificationId,
                false,
                isPlural
                    ? intl.formatMessage(ConnectorsResources.failedToDeleteAll, { count: failedCount })
                    : intl.formatMessage(ConnectorsResources.deleteConnectorFailed)
            );
        } else {
            stopNotification(
                notificationId,
                true,
                intl.formatMessage(ConnectorsResources.partialDeleteSuccess, {
                    successCount,
                    failedCount,
                    failedItems: failedItems.join(', '),
                })
            );
        }

        setIsOperationInProgress(false);
    }, [selectedKeys, startNotification, intl, refresh, deleteConnector, log, resourceId, stopNotification]);

    const onConfirmDelete = useCallback(async () => {
        setIsDeleteConfirmOpen(false);
        await bulkDeleteDataConnectors();
    }, [bulkDeleteDataConnectors]);

    const onCancelDelete = useCallback(() => {
        setIsDeleteConfirmOpen(false);
    }, []);

    const filteredConnectors = useMemo(() => {
        if (!searchTerm) {
            return connectors;
        }
        const searchLower = searchTerm.toLowerCase();
        return connectors.filter(
            connector =>
                connector.name.toLowerCase().includes(searchLower) ||
                connector.dataConnectorType.toLowerCase().includes(searchLower) ||
                (connector.source && connector.source.toLowerCase().includes(searchLower))
        );
    }, [connectors, searchTerm]);

    const selectedConnectorTypes = useMemo(() => {
        const itemsToCheck = Array.from(selectedKeys);
        return itemsToCheck.map(connectorName => {
            const connector = connectors.find(dc => dc.name === connectorName);
            return connector?.dataConnectorType ? (connector.dataConnectorType as ConnectorType) : '';
        });
    }, [selectedKeys, connectors]);

    const onEditConnector = useCallback((dataConnector: Connector) => {
        setSelectedConnector(dataConnector);
        setIsDialogOpen(true);
    }, []);

    const onDeleteConnector = useCallback((connectorName: string) => {
        setSelectedKeys(new Set([connectorName]));
        setIsDeleteConfirmOpen(true);
    }, []);

    return (
        <div className={styles.container}>
            <div className={styles.titleContainer}>
                <h3 className={styles.title}>{intl.formatMessage(ConnectorsResources.addAConnector)}</h3>
                <TextWithLink
                    text={intl.formatMessage(ConnectorsResources.connectorsDescription)}
                    linkText={intl.formatMessage(ConnectorsResources.connectorsDescriptionLearnMore)}
                    linkUrl={SreAgentFwLinks.connectors}
                />
            </div>
            <ConnectorsToolbar
                onRefreshClick={refresh}
                onNewConnectorClick={addNewConnector}
                onDeleteConnectorClick={onBulkDelete}
                isConnectorSelected={!!selectedConnector || selectedKeys.size > 0}
                selectedCount={selectedKeys.size}
                isOperationInProgress={isOperationInProgress || isRefreshing}
                setSearchTerm={setSearchTerm}
            />
            <ConnectorsDataGrid
                connectors={filteredConnectors}
                selectedKeys={selectedKeys}
                isEmpty={connectors.length === 0}
                isLoading={isConnectorsLoading}
                isRefreshing={isRefreshing}
                isOperationInProgress={isOperationInProgress}
                setSelectedKeys={setSelectedKeys}
                addNewConnector={addNewConnector}
                onEditConnector={onEditConnector}
                onDeleteConnector={onDeleteConnector}
            />
            <DeleteConfirmationDialog
                isOpen={isDeleteConfirmOpen}
                onOpenChange={setIsDeleteConfirmOpen}
                onConfirmDelete={onConfirmDelete}
                onCancelDelete={onCancelDelete}
                isOperationInProgress={isOperationInProgress}
                itemType={intl.formatMessage(ConnectorsResources.connector)}
                actionVerb={intl.formatMessage(ConnectorsResources.remove)}
                actionPositive={intl.formatMessage(SreAgentResources.yes)}
                actionNegative={intl.formatMessage(SreAgentResources.no)}
                selectedItems={Array.from(selectedKeys)}
                connectorTypes={selectedConnectorTypes}
            />
            <ConnectorWizardFormik
                agentName={agent?.name}
                agentLocation={agent?.location}
                agentIdentity={agent?.identity}
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                onSubmit={onSubmit}
                refreshAgent={refreshAgent}
                selectedConnector={selectedConnector}
                existingConnectors={connectors}
            />
        </div>
    );
};

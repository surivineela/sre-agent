import { Link } from '@fluentui/react-components';
import {
    CheckboxVisibility,
    ConstrainMode,
    DetailsListLayoutMode,
    IColumn,
    Selection,
    SelectionMode,
} from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { FC, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SecretValue } from '../../Common/Components/SecretValue';
import { DataConnector } from '../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { DataConnectionsResources, SettingsTabResources } from '../../Strings/SREAgentResources';
import { CreateOrUpdateDataConnectorDialog, DataConnectorFormProps } from './AddEditDataConnections';
import DataConnectionsToolbar from './DataConnectionsToolbar';
import { useAgentDataConnectors } from './Hooks/useAgentDataConnectors';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSettingsStyles } from './Styles/Settings.styles';

enum DataConnectionColumnKey {
    name = 'name',
    dataConnectionType = 'dataConnectorType',
    dataSource = 'dataSource',
    keyVaultUri = 'keyVaultUri',
    identity = 'identity',
}

const DataConnections: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const { agent, refresh: refreshAgent } = useSreAgent(resourceId);
    const { dataConnectors, isDataConnectorsLoading, putDataConnector, deleteDataConnector, refreshDataConnectors } =
        useAgentDataConnectors(resourceId);

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [selectedDataConnection, setSelectedDataConnection] = useState<DataConnector | undefined>();
    const [isEditMode, setIsEditMode] = useState(false);
    const [isOperationInProgress, setIsOperationInProgress] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);

    const selection = useRef(
        new Selection({
            onSelectionChanged: () => {
                const items = (selection.current.getSelection() as DataConnector[]) ?? [];
                setSelectedDataConnection(items.length > 0 ? items[0] : undefined);
            },
        })
    );

    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        await Promise.all([refreshAgent(), refreshDataConnectors()]);
        setIsRefreshing(false);
    }, [refreshAgent, refreshDataConnectors]);

    const handleEditDataConnection = useCallback((dataConnector: DataConnector) => {
        setSelectedDataConnection(dataConnector);
        setIsEditMode(true);
        setIsDialogOpen(true);
    }, []);

    const handleNewDataConnection = useCallback(() => {
        setSelectedDataConnection(undefined);
        setIsEditMode(false);
        setIsDialogOpen(true);
        selection.current.setAllSelected(false);
    }, [selection]);

    const openManagedIdentity = useCallback(
        (identityId: string) => {
            portalContext.openBlade({
                detailBlade: 'ResourceMenuBlade',
                detailBladeInputs: { id: identityId },
                extension: 'HubsExtension',
            });
        },
        [portalContext]
    );

    const deleteDataConnection = useCallback(async () => {
        if (!selectedDataConnection) {
            return;
        }

        setIsOperationInProgress(true);
        const notificationId = portalContext.startNotification(
            intl.formatMessage(DataConnectionsResources.deletingDataConnection),
            intl.formatMessage(DataConnectionsResources.deletingDataConnectionDescription, { name: selectedDataConnection.name })
        );

        const response = await deleteDataConnector(selectedDataConnection.name);
        if (response.metadata.success) {
            setSelectedDataConnection(undefined);
            selection.current.setAllSelected(false);
            handleRefresh();

            portalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(DataConnectionsResources.dataConnectionDeleted, { name: selectedDataConnection.name })
            );
        } else {
            portalContext.log({
                action: 'deleteDataConnector',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    message: `Failed to delete data connector: ${response.metadata.error?.error?.message}`,
                },
            });

            const errorMessage = response.metadata.error?.error?.message;
            portalContext.stopNotification(
                notificationId,
                false,
                errorMessage
                    ? intl.formatMessage(DataConnectionsResources.deleteDataConnectionWithMessageFailed, { error: errorMessage })
                    : intl.formatMessage(DataConnectionsResources.deleteDataConnectionFailed)
            );
        }
        setIsOperationInProgress(false);
    }, [selectedDataConnection, deleteDataConnector, resourceId, selection, handleRefresh, portalContext, intl]);

    const getFormValuesFromDataConnector = useCallback((dataConnector: DataConnector): DataConnectorFormProps => {
        return {
            name: dataConnector.name,
            dataConnectorType: dataConnector.dataConnectorType,
            dataSource: dataConnector.dataSource ?? '-',
            identity: dataConnector.identity,
        };
    }, []);

    const formInitialValues = useMemo(() => {
        return selectedDataConnection ? getFormValuesFromDataConnector(selectedDataConnection) : undefined;
    }, [selectedDataConnection, getFormValuesFromDataConnector]);

    const createDataConnection = useCallback(
        async (dataConnector: DataConnector) => {
            setIsOperationInProgress(true);
            const notificationId = portalContext.startNotification(
                intl.formatMessage(DataConnectionsResources.creatingDataConnection),
                intl.formatMessage(DataConnectionsResources.creatingDataConnectionDescription, { name: dataConnector.name })
            );

            const response = await putDataConnector(dataConnector);
            if (response.metadata.success) {
                handleRefresh();
                portalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(DataConnectionsResources.dataConnectionCreated, { name: dataConnector.name })
                );
            } else {
                portalContext.log({
                    action: 'createDataConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to create data connector: ${response.metadata.error?.error?.message}`,
                    },
                });

                const errorMessage = response.metadata.error?.error?.message;
                portalContext.stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(DataConnectionsResources.createDataConnectionWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(DataConnectionsResources.createDataConnectionFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [resourceId, handleRefresh, portalContext, intl, putDataConnector]
    );

    const updateDataConnection = useCallback(
        async (dataConnector: DataConnector) => {
            setIsOperationInProgress(true);
            const notificationId = portalContext.startNotification(
                intl.formatMessage(DataConnectionsResources.updatingDataConnection),
                intl.formatMessage(DataConnectionsResources.updatingDataConnectionDescription, { name: dataConnector.name })
            );

            const response = await putDataConnector(dataConnector);
            if (response.metadata.success) {
                handleRefresh();
                portalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(DataConnectionsResources.dataConnectionUpdated, { name: dataConnector.name })
                );
            } else {
                portalContext.log({
                    action: 'updateDataConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to update data connector: ${response.metadata.error?.error?.message}`,
                    },
                });

                const errorMessage = response.metadata.error?.error?.message;
                portalContext.stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(DataConnectionsResources.updateDataConnectionWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(DataConnectionsResources.updateDataConnectionFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [resourceId, handleRefresh, portalContext, intl, putDataConnector]
    );

    const columns = useMemo<IColumn[]>(() => {
        return [
            {
                key: DataConnectionColumnKey.name,
                name: intl.formatMessage(DataConnectionsResources.name),
                fieldName: DataConnectionColumnKey.name,
                minWidth: 150,
                maxWidth: 200,
                isResizable: true,
                onRender: (item: DataConnector) => item.name,
            },
            {
                key: DataConnectionColumnKey.dataConnectionType,
                name: intl.formatMessage(DataConnectionsResources.dataConnectionType),
                fieldName: DataConnectionColumnKey.dataConnectionType,
                minWidth: 120,
                maxWidth: 150,
                isResizable: true,
                onRender: (item: DataConnector) => item.dataConnectorType,
            },
            {
                key: DataConnectionColumnKey.dataSource,
                name: intl.formatMessage(DataConnectionsResources.dataSource),
                fieldName: DataConnectionColumnKey.dataSource,
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
                onRender: (item: DataConnector) =>
                    item.dataSource ? (
                        <span data-selection-disabled={true} data-is-focusable={true}>
                            <SecretValue value={item.dataSource} />
                        </span>
                    ) : (
                        '-'
                    ),
            },
            {
                key: DataConnectionColumnKey.identity,
                name: intl.formatMessage(DataConnectionsResources.identity),
                fieldName: DataConnectionColumnKey.identity,
                minWidth: 150,
                maxWidth: 250,
                isResizable: true,
                onRender: (item: DataConnector) => {
                    if (typeof item.identity === 'string') {
                        const identityResourceDescriptor = new ArmResourceDescriptor(item.identity);
                        const identityName = identityResourceDescriptor.resourceName;
                        return <Link onClick={() => openManagedIdentity(item.identity as string)}>{identityName}</Link>;
                    }
                    return JSON.stringify(item.identity);
                },
            },
        ];
    }, [intl, openManagedIdentity]);

    const identities = useMemo(() => {
        return agent?.identity?.userAssignedIdentities ? Object.keys(agent.identity.userAssignedIdentities) : [];
    }, [agent?.identity?.userAssignedIdentities]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.dataConnections)}</div>
            <div style={styles.accessControlSettingsContainer}>
                <DataConnectionsToolbar
                    onRefreshClick={handleRefresh}
                    onNewDataConnectorClick={handleNewDataConnection}
                    onDeleteDataConnectorClick={deleteDataConnection}
                    isConnectorSelected={!!selectedDataConnection}
                    isOperationInProgress={isOperationInProgress || isRefreshing}
                />
                <div data-is-scrollable="true">
                    <ShimmeredDetailsList
                        compact={true}
                        selection={selection.current}
                        selectionMode={SelectionMode.single}
                        columns={columns}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        items={dataConnectors}
                        layoutMode={DetailsListLayoutMode.justified}
                        enableShimmer={isDataConnectorsLoading || isRefreshing}
                        checkboxVisibility={CheckboxVisibility.always}
                        onItemInvoked={isOperationInProgress ? undefined : handleEditDataConnection}
                        selectionPreservedOnEmptyClick={true}
                    />
                    {!isDataConnectorsLoading && dataConnectors.length === 0 && (
                        <div style={{ padding: '20px', textAlign: 'center', color: '#666' }}>
                            {intl.formatMessage(DataConnectionsResources.noDataConnections)}
                        </div>
                    )}
                </div>
            </div>

            <CreateOrUpdateDataConnectorDialog
                identities={identities}
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                createDataConnector={createDataConnection}
                updateDataConnector={updateDataConnection}
                initialValues={formInitialValues}
                isEditMode={isEditMode}
                isOperationInProgress={isOperationInProgress}
                existingDataConnectors={dataConnectors}
            />
        </>
    );
};

export default DataConnections;

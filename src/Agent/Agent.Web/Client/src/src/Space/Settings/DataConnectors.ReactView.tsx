import { Link, tokens } from '@fluentui/react-components';
import { ConstrainMode, DetailsListLayoutMode, IColumn } from '@fluentui/react/lib/DetailsList';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SecretValue } from '../../Common/Components/SecretValue';
import ShimmeredDetailsListWithSelection, { OnUpdateSelectionArgs } from '../../Common/Components/ShimmeredDetailsListWithSelection';
import { Connector } from '../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { DataConnectorsResources, SettingsTabResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IdentityKeys } from '../Contracts/Identity';
import { CreateOrUpdateDataConnectorDialog, DataConnectorFormProps } from './AddEditDataConnectors';
import DataConnectionsToolbar from './DataConnectorsToolbar';
import { useAgentConnectors } from './Hooks/useAgentConnectors';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSettingsStyles } from './Styles/Settings.styles';

enum DataConnectorColumnKey {
    name = 'name',
    dataConnectorType = 'dataConnectorType',
    dataSource = 'dataSource',
    keyVaultUri = 'keyVaultUri',
    identity = 'identity',
    source = 'source',
}

const DataConnectors: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const { agent, refresh: refreshAgent } = useSreAgent(resourceId);
    const {
        connectors: dataConnectors,
        isConnectorsLoading: isDataConnectorsLoading,
        putConnector: putDataConnector,
        deleteConnector: deleteDataConnector,
        refreshConnectors: refreshDataConnectors,
    } = useAgentConnectors(resourceId);

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [selectedDataConnection, setSelectedDataConnection] = useState<Connector | undefined>();
    const [isEditMode, setIsEditMode] = useState(false);
    const [isOperationInProgress, setIsOperationInProgress] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [selectedKeys, setSelectedKeys] = useState<string[]>([]);

    const onUpdateSelection = useCallback(({ selectedItems, selectedKeys }: OnUpdateSelectionArgs<Connector>) => {
        setSelectedDataConnection(selectedItems.length > 0 ? selectedItems[0] : undefined);
        setSelectedKeys(selectedKeys);
    }, []);

    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        await Promise.all([refreshAgent(), refreshDataConnectors()]);
        setIsRefreshing(false);
    }, [refreshAgent, refreshDataConnectors]);

    const handleEditDataConnection = useCallback((dataConnector: Connector) => {
        setSelectedDataConnection(dataConnector);
        setIsEditMode(true);
        setIsDialogOpen(true);
    }, []);

    const handleNewDataConnection = useCallback(() => {
        setSelectedDataConnection(undefined);
        setSelectedKeys([]);
        setIsEditMode(false);
        setIsDialogOpen(true);
    }, []);

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
            intl.formatMessage(DataConnectorsResources.deletingDataConnector),
            intl.formatMessage(DataConnectorsResources.deletingDataConnectorDescription, { name: selectedDataConnection.name })
        );

        const response = await deleteDataConnector(selectedDataConnection.name);
        if (response.metadata.success) {
            setSelectedDataConnection(undefined);
            setSelectedKeys([]);
            handleRefresh();

            portalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(DataConnectorsResources.dataConnectorDeleted, { name: selectedDataConnection.name })
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
                    ? intl.formatMessage(DataConnectorsResources.deleteDataConnectorWithMessageFailed, { error: errorMessage })
                    : intl.formatMessage(DataConnectorsResources.deleteDataConnectorFailed)
            );
        }
        setIsOperationInProgress(false);
    }, [selectedDataConnection, deleteDataConnector, resourceId, handleRefresh, portalContext, intl]);

    const getFormValuesFromDataConnector = useCallback((dataConnector: Connector): DataConnectorFormProps => {
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
        async (dataConnector: Connector) => {
            setIsOperationInProgress(true);
            const notificationId = portalContext.startNotification(
                intl.formatMessage(DataConnectorsResources.creatingDataConnector),
                intl.formatMessage(DataConnectorsResources.creatingDataConnectorDescription, { name: dataConnector.name })
            );

            const response = await putDataConnector(dataConnector);
            if (response.metadata.success) {
                handleRefresh();
                portalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(DataConnectorsResources.dataConnectorCreated, { name: dataConnector.name })
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
                        ? intl.formatMessage(DataConnectorsResources.createDataConnectorWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(DataConnectorsResources.createDataConnectorFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [resourceId, handleRefresh, portalContext, intl, putDataConnector]
    );

    const updateDataConnection = useCallback(
        async (dataConnector: Connector) => {
            setIsOperationInProgress(true);
            const notificationId = portalContext.startNotification(
                intl.formatMessage(DataConnectorsResources.updatingDataConnector),
                intl.formatMessage(DataConnectorsResources.updatingDataConnectorDescription, { name: dataConnector.name })
            );

            const response = await putDataConnector(dataConnector);
            if (response.metadata.success) {
                handleRefresh();
                portalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(DataConnectorsResources.dataConnectorUpdated, { name: dataConnector.name })
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
                        ? intl.formatMessage(DataConnectorsResources.updateDataConnectorWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(DataConnectorsResources.updateDataConnectorFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [resourceId, handleRefresh, portalContext, intl, putDataConnector]
    );

    const columns = useMemo<IColumn[]>(() => {
        return [
            {
                key: DataConnectorColumnKey.name,
                name: intl.formatMessage(DataConnectorsResources.name),
                minWidth: 150,
                maxWidth: 200,
                isResizable: true,
                onRender: (item: Connector) => (
                    <span data-selection-disabled={true} data-is-focusable={true}>
                        <Link disabled={isOperationInProgress} onClick={() => handleEditDataConnection(item)}>
                            {item.name}
                        </Link>
                    </span>
                ),
            },
            {
                key: DataConnectorColumnKey.dataConnectorType,
                name: intl.formatMessage(DataConnectorsResources.dataConnectorType),
                minWidth: 120,
                maxWidth: 150,
                isResizable: true,
                onRender: (item: Connector) => item.dataConnectorType,
            },
            {
                key: DataConnectorColumnKey.dataSource,
                name: intl.formatMessage(DataConnectorsResources.dataSource),
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
                onRender: (item: Connector) =>
                    item.dataSource ? (
                        <span data-selection-disabled={true} data-is-focusable={true}>
                            <SecretValue value={item.dataSource} />
                        </span>
                    ) : (
                        '-'
                    ),
            },
            {
                key: DataConnectorColumnKey.identity,
                name: intl.formatMessage(DataConnectorsResources.identity),
                minWidth: 150,
                maxWidth: 250,
                isResizable: true,
                onRender: (item: Connector) => {
                    if (typeof item.identity === 'string') {
                        if (item.identity.toLowerCase() === IdentityKeys.system) {
                            return intl.formatMessage(SreAgentResources.systemAssigned);
                        }

                        const identityResourceDescriptor = new ArmResourceDescriptor(item.identity);
                        const identityName = identityResourceDescriptor.resourceName;
                        return <Link onClick={() => openManagedIdentity(item.identity as string)}>{identityName}</Link>;
                    }
                    return JSON.stringify(item.identity);
                },
            },
            {
                key: DataConnectorColumnKey.source,
                name: intl.formatMessage(DataConnectorsResources.source),
                minWidth: 100,
                maxWidth: 150,
                isResizable: true,
                onRender: (item: Connector) => item.source ?? '-',
            },
        ];
    }, [intl, openManagedIdentity, handleEditDataConnection, isOperationInProgress]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.dataConnectors)}</div>
            <div style={styles.accessControlSettingsContainer}>
                <DataConnectionsToolbar
                    onRefreshClick={handleRefresh}
                    onNewDataConnectorClick={handleNewDataConnection}
                    onDeleteDataConnectorClick={deleteDataConnection}
                    isConnectorSelected={!!selectedDataConnection}
                    isOperationInProgress={isOperationInProgress || isRefreshing}
                />
                <div data-is-scrollable="true">
                    <ShimmeredDetailsListWithSelection<Connector>
                        items={dataConnectors}
                        getKey={dc => dc.name}
                        columns={columns}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        layoutMode={DetailsListLayoutMode.justified}
                        enableShimmer={isDataConnectorsLoading || isRefreshing}
                        multiSelect={false}
                        hideSelectAll
                        selectedKeys={selectedKeys}
                        onUpdateSelection={onUpdateSelection}
                    />
                    {!isDataConnectorsLoading && dataConnectors.length === 0 && (
                        <div style={{ padding: '20px', textAlign: 'center', color: tokens.colorNeutralForeground3 }}>
                            {intl.formatMessage(DataConnectorsResources.noDataConnectors)}
                        </div>
                    )}
                </div>
            </div>

            <CreateOrUpdateDataConnectorDialog
                agentIdentity={agent?.identity}
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                createDataConnector={createDataConnection}
                updateDataConnector={updateDataConnection}
                initialValues={formInitialValues}
                isEditMode={isEditMode}
                isOperationInProgress={isOperationInProgress}
                existingDataConnectors={dataConnectors}
                refreshAgent={refreshAgent}
            />
        </>
    );
};

export default DataConnectors;

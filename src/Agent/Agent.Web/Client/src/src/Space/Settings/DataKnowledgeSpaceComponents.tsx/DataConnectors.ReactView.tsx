import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridProps,
    DataGridRow,
    InputOnChangeData,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    SearchBoxChangeEvent,
    Skeleton,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    Text,
} from '@fluentui/react-components';
import { CheckmarkCircle16Regular, Delete16Regular, Edit16Regular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { debounce } from 'lodash';
import * as React from 'react';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { DataConnector } from '../../../Common/Contracts/Azure/SreAgent';
import { DataConnectorsResources } from '../../../Strings/SREAgentResources';
import DataConnectionsToolbar from '../DataConnectorsToolbar';
import { useAgentDataConnectors } from '../Hooks/useAgentDataConnectors';
import { useSreAgent } from '../Hooks/useSreAgent';
import { useDataConnectorsStyles } from '../Styles/DataKnowledgeSpace.styles';
import { useSettingsStyles } from '../Styles/Settings.styles';
import {
    ConnectorType,
    CreateOrUpdateDataConnectorDialog,
    DataConnectorFormProps,
    getConnectorTypeOptions,
} from './AddEditDataConnectorsNew';
import { DeleteConfirmationDialog } from './DeleteConfirmationDialog';
import { EmptyState } from './EmptyState';

// Constants
const SHIMMER_ITEMS_COUNT = 3;
const DEBOUNCE_DELAY = 200;
const DEFAULT_SORT_COLUMN = 'name';
const DEFAULT_SORT_DIRECTION = 'ascending' as const;

const DataConnectors: FC = () => {
    const intl = useIntl();
    const settingsStyles = useSettingsStyles();
    const styles = useDataConnectorsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const { agent, refresh: refreshAgent } = useSreAgent(resourceId);
    const { dataConnectors, isDataConnectorsLoading, putDataConnector, deleteDataConnector, refreshDataConnectors } =
        useAgentDataConnectors(resourceId);

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [selectedDataConnection, setSelectedDataConnection] = useState<DataConnector | undefined>();
    const [connectorToDelete, setConnectorToDelete] = useState<string | null>(null);
    const [isEditMode, setIsEditMode] = useState(false);
    const [isOperationInProgress, setIsOperationInProgress] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [selectedKeys, setSelectedKeys] = useState<string[]>([]);
    const [searchText, setSearchText] = useState<string>('');
    const [debouncedSearchText, setDebouncedSearchText] = useState<string>('');
    const [isDeleteConfirmOpen, setIsDeleteConfirmOpen] = useState(false);
    const selectedRowsSet = useMemo(() => new Set(selectedKeys), [selectedKeys]);

    const filteredDataConnectors = useMemo(() => {
        if (!debouncedSearchText) {
            return dataConnectors;
        }
        const searchLower = debouncedSearchText.toLowerCase();
        return dataConnectors.filter(
            connector =>
                connector.name.toLowerCase().includes(searchLower) ||
                connector.dataConnectorType.toLowerCase().includes(searchLower) ||
                (connector.source && connector.source.toLowerCase().includes(searchLower))
        );
    }, [dataConnectors, debouncedSearchText]);

    const debouncedSetSearchText = useMemo(() => {
        const debouncedFn = debounce((value: string) => {
            setDebouncedSearchText(value);
        }, DEBOUNCE_DELAY);

        return debouncedFn;
    }, []);

    useEffect(() => {
        return () => {
            debouncedSetSearchText.cancel();
        };
    }, [debouncedSetSearchText]);

    const handleSearchChange = useCallback(
        (_: SearchBoxChangeEvent, data: InputOnChangeData) => {
            const value = data.value || '';
            setSearchText(value);
            debouncedSetSearchText(value);
        },
        [debouncedSetSearchText]
    );

    const createShimmerCell = (skeletonItems: { width: string; height: string; marginBottom?: string }[]) => (
        <TableCellLayout>
            <div className={styles.shimmerContainer}>
                <Skeleton>
                    {skeletonItems.map((item, index) => (
                        <SkeletonItem
                            key={index}
                            style={{
                                width: item.width,
                                height: item.height,
                                ...(item.marginBottom && { marginBottom: item.marginBottom }),
                            }}
                        />
                    ))}
                </Skeleton>
            </div>
        </TableCellLayout>
    );

    const renderCellWithShimmer = (
        item: DataConnector,
        shimmerConfig: { width: string; height: string; marginBottom?: string }[],
        renderContent: (item: DataConnector) => React.ReactNode
    ) => {
        const shimmerItem = item as DataConnector & { isShimmer?: boolean };
        if (shimmerItem.isShimmer) {
            return createShimmerCell(shimmerConfig);
        }
        return renderContent(item);
    };

    const columns: TableColumnDefinition<DataConnector>[] = useMemo(
        () => [
            createTableColumn<DataConnector>({
                columnId: 'name',
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => intl.formatMessage(DataConnectorsResources.name),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '200px', height: '16px' }], item => (
                        <TableCellLayout>
                            <div className={styles.nameCellContainer}>
                                <span className={styles.nameText}>{item.name}</span>
                                <div className={styles.nameMenuContainer}>
                                    <Menu>
                                        <MenuTrigger disableButtonEnhancement>
                                            <Button
                                                appearance="transparent"
                                                size="small"
                                                icon={<MoreHorizontal20Regular />}
                                                onClick={e => e.stopPropagation()}
                                            />
                                        </MenuTrigger>
                                        <MenuPopover>
                                            <MenuList>
                                                <MenuItem
                                                    icon={<Edit16Regular />}
                                                    onClick={e => {
                                                        e.stopPropagation();
                                                        handleEditDataConnection(item);
                                                    }}
                                                >
                                                    {intl.formatMessage(DataConnectorsResources.edit)}
                                                </MenuItem>
                                                <MenuItem
                                                    icon={<Delete16Regular />}
                                                    onClick={e => {
                                                        e.stopPropagation();
                                                        handleSingleConnectorDelete(item.name);
                                                    }}
                                                >
                                                    {intl.formatMessage(DataConnectorsResources.delete)}
                                                </MenuItem>
                                            </MenuList>
                                        </MenuPopover>
                                    </Menu>
                                </div>
                            </div>
                        </TableCellLayout>
                    )),
            }),
            createTableColumn<DataConnector>({
                columnId: 'dataConnectorType',
                compare: (a, b) => a.dataConnectorType.localeCompare(b.dataConnectorType),
                renderHeaderCell: () => intl.formatMessage(DataConnectorsResources.connectorType),
                renderCell: item =>
                    renderCellWithShimmer(
                        item,
                        [
                            { width: '100px', height: '14px', marginBottom: '4px' },
                            { width: '80px', height: '12px' },
                        ],
                        item => {
                            const frontendType = item.dataConnectorType as ConnectorType;
                            const connectorOption = getConnectorTypeOptions(intl).find(option => option.id === frontendType);
                            if (connectorOption) {
                                return (
                                    <TableCellLayout>
                                        <div className={styles.connectorTypeContainer}>
                                            <div className={styles.connectorTypeName}>{connectorOption.name}</div>
                                            <div className={styles.connectorTypeService}>{connectorOption.service}</div>
                                        </div>
                                    </TableCellLayout>
                                );
                            }
                            return <TableCellLayout>{item.dataConnectorType}</TableCellLayout>;
                        }
                    ),
            }),
            createTableColumn<DataConnector>({
                columnId: 'lastModified',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(DataConnectorsResources.lastModified),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '80px', height: '16px' }], () => <TableCellLayout>-</TableCellLayout>),
            }),
            createTableColumn<DataConnector>({
                columnId: 'lastSynced',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(DataConnectorsResources.lastSynced),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '80px', height: '16px' }], () => <TableCellLayout>-</TableCellLayout>),
            }),
            createTableColumn<DataConnector>({
                columnId: 'status',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(DataConnectorsResources.status),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '90px', height: '16px' }], () => (
                        <TableCellLayout>
                            <div className={styles.statusContainer}>
                                <CheckmarkCircle16Regular className={styles.statusIcon} />
                                <Text>{intl.formatMessage(DataConnectorsResources.connected)}</Text>
                            </div>
                        </TableCellLayout>
                    )),
            }),
        ],
        [intl, isOperationInProgress]
    );

    const createShimmerData = (count: number): DataConnector[] => {
        return Array.from(
            { length: count },
            (_, index) =>
                ({
                    name: `shimmer-${index}`,
                    dataConnectorType: 'shimmer',
                    dataSource: undefined,
                    identity: '',
                    isShimmer: true,
                }) as unknown as DataConnector & { isShimmer: boolean }
        );
    };

    const displayData = isDataConnectorsLoading || isRefreshing ? createShimmerData(SHIMMER_ITEMS_COUNT) : filteredDataConnectors;

    const [sortState, setSortState] = useState<{
        sortColumn: string;
        sortDirection: 'ascending' | 'descending';
    }>({
        sortColumn: DEFAULT_SORT_COLUMN,
        sortDirection: DEFAULT_SORT_DIRECTION,
    });

    const selectedItemsForDataGrid = useMemo(() => {
        if (isDataConnectorsLoading || isRefreshing) {
            return new Set();
        }
        const indices = selectedKeys.map(name => filteredDataConnectors.findIndex(dc => dc.name === name)).filter(index => index >= 0);
        return new Set(indices);
    }, [selectedKeys, filteredDataConnectors, isDataConnectorsLoading, isRefreshing]);

    const onSortChange: DataGridProps['onSortChange'] = (_, nextSortState) => {
        setSortState({
            sortColumn: nextSortState.sortColumn?.toString() || DEFAULT_SORT_COLUMN,
            sortDirection: nextSortState.sortDirection || DEFAULT_SORT_DIRECTION,
        });
    };

    const onSelectionChange: DataGridProps['onSelectionChange'] = (_, data) => {
        if (isDataConnectorsLoading || isRefreshing) {
            return;
        }

        const selectedArray = Array.from(data.selectedItems)
            .map(index => {
                const rowIndex = typeof index === 'number' ? index : parseInt(index.toString());
                return filteredDataConnectors[rowIndex]?.name;
            })
            .filter(Boolean) as string[];

        setSelectedKeys(selectedArray);

        const selectedDataConnector = selectedArray.length > 0 ? dataConnectors.find(dc => dc.name === selectedArray[0]) : undefined;
        setSelectedDataConnection(selectedDataConnector);
    };

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
        setSelectedKeys([]);
        setIsEditMode(false);
        setIsDialogOpen(true);
    }, []);

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

    const bulkDeleteDataConnectors = useCallback(async () => {
        if (selectedRowsSet.size === 0) {
            return;
        }

        setIsOperationInProgress(true);
        const selectedNames = Array.from(selectedRowsSet);
        const isPlural = selectedNames.length > 1;

        const notificationId = portalContext.startNotification(
            isPlural
                ? intl.formatMessage(DataConnectorsResources.deletingMultipleDataConnectors, { count: selectedNames.length })
                : intl.formatMessage(DataConnectorsResources.deletingDataConnector),
            isPlural
                ? intl.formatMessage(DataConnectorsResources.deletingMultipleDataConnectors, { count: selectedNames.length })
                : intl.formatMessage(DataConnectorsResources.deletingDataConnectorDescription, { name: selectedNames[0] })
        );

        let successCount = 0;
        let failedCount = 0;
        const failedItems: string[] = [];

        for (const name of selectedNames) {
            try {
                const response = await deleteDataConnector(name);
                if (response.metadata.success) {
                    successCount++;
                } else {
                    failedCount++;
                    failedItems.push(name);
                    portalContext.log({
                        action: 'deleteDataConnector',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            message: `Failed to delete data connector ${name}: ${response.metadata.error?.error?.message}`,
                        },
                    });
                }
            } catch (error) {
                failedCount++;
                failedItems.push(name);
                portalContext.log({
                    action: 'deleteDataConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to delete data connector ${name}: ${error}`,
                    },
                });
            }
        }

        setSelectedKeys([]);
        setSelectedDataConnection(undefined);
        handleRefresh();

        if (failedCount === 0) {
            portalContext.stopNotification(
                notificationId,
                true,
                isPlural
                    ? intl.formatMessage(DataConnectorsResources.successfullyDeletedMultiple, { count: successCount })
                    : intl.formatMessage(DataConnectorsResources.dataConnectorDeleted, { name: selectedNames[0] })
            );
        } else if (successCount === 0) {
            portalContext.stopNotification(
                notificationId,
                false,
                isPlural
                    ? intl.formatMessage(DataConnectorsResources.failedToDeleteAll, { count: failedCount })
                    : intl.formatMessage(DataConnectorsResources.deleteDataConnectorFailed)
            );
        } else {
            portalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(DataConnectorsResources.partialDeleteSuccess, {
                    successCount,
                    failedCount,
                    failedItems: failedItems.join(', '),
                })
            );
        }

        setIsOperationInProgress(false);
    }, [selectedRowsSet, deleteDataConnector, resourceId, handleRefresh, portalContext, intl]);

    const handleConfirmDelete = useCallback(async () => {
        setIsDeleteConfirmOpen(false);

        if (connectorToDelete) {
            setSelectedDataConnection(dataConnectors.find(dc => dc.name === connectorToDelete));
            await deleteDataConnection();
        } else {
            await bulkDeleteDataConnectors();
        }
        setConnectorToDelete(null);
    }, [connectorToDelete, selectedRowsSet.size, dataConnectors, bulkDeleteDataConnectors, deleteDataConnection]);

    const handleCancelDelete = useCallback(() => {
        setIsDeleteConfirmOpen(false);
        setConnectorToDelete(null);
    }, []);

    const handleSingleConnectorDelete = useCallback((connectorName: string) => {
        setConnectorToDelete(connectorName);
        setIsDeleteConfirmOpen(true);
    }, []);

    const handleBulkDeleteStart = useCallback(() => {
        setConnectorToDelete(null);
        setIsDeleteConfirmOpen(true);
    }, []);

    const getSelectedConnectorTypes = useCallback(() => {
        const itemsToCheck = connectorToDelete ? [connectorToDelete] : Array.from(selectedRowsSet);
        return itemsToCheck.map(connectorName => {
            const connector = dataConnectors.find(dc => dc.name === connectorName);
            return connector?.dataConnectorType ? (connector.dataConnectorType as ConnectorType) : '';
        });
    }, [connectorToDelete, selectedRowsSet, dataConnectors]);

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
        async (dataConnector: DataConnector) => {
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

    const handleConnectorSubmit = useCallback(
        (dataConnector: DataConnector) => {
            if (isEditMode) {
                updateDataConnection(dataConnector);
            } else {
                createDataConnection(dataConnector);
            }
        },
        [isEditMode, updateDataConnection, createDataConnection]
    );

    return (
        <>
            <div style={settingsStyles.accessControlSettingsContainer}>
                <DataConnectionsToolbar
                    onRefreshClick={handleRefresh}
                    onNewDataConnectorClick={handleNewDataConnection}
                    onDeleteDataConnectorClick={handleBulkDeleteStart}
                    isConnectorSelected={!!selectedDataConnection || selectedRowsSet.size > 0}
                    selectedCount={selectedRowsSet.size}
                    isOperationInProgress={isOperationInProgress || isRefreshing}
                    searchText={searchText}
                    onSearchChange={handleSearchChange}
                />
                <div data-is-scrollable="true">
                    <DataGrid
                        items={displayData}
                        columns={columns}
                        sortable={!isDataConnectorsLoading && !isRefreshing}
                        sortState={sortState}
                        onSortChange={onSortChange}
                        selectionMode="multiselect"
                        selectedItems={selectedItemsForDataGrid as Set<any>}
                        onSelectionChange={onSelectionChange}
                        className={styles.dataGrid}
                    >
                        <DataGridHeader>
                            <DataGridRow
                                selectionCell={{
                                    checkboxIndicator: { 'aria-label': 'Select all rows' },
                                }}
                            >
                                {({ renderHeaderCell }) => (
                                    <DataGridHeaderCell className={styles.headerCell}>{renderHeaderCell()}</DataGridHeaderCell>
                                )}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<DataConnector>>
                            {({ item, rowId }) => (
                                <DataGridRow<DataConnector>
                                    key={rowId}
                                    selectionCell={{
                                        checkboxIndicator: { 'aria-label': 'Select row' },
                                    }}
                                >
                                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>

                    {!isDataConnectorsLoading && !isRefreshing && filteredDataConnectors.length === 0 && (
                        <div className={styles.emptyStateContainer}>
                            <EmptyState
                                type="dataConnectors"
                                variant={dataConnectors.length === 0 ? 'noItems' : 'noSearchResults'}
                                onPrimaryAction={dataConnectors.length === 0 ? handleNewDataConnection : () => {}}
                                isActionDisabled={isDataConnectorsLoading || isOperationInProgress}
                            />
                        </div>
                    )}
                </div>
            </div>

            <DeleteConfirmationDialog
                isOpen={isDeleteConfirmOpen}
                onOpenChange={setIsDeleteConfirmOpen}
                onConfirmDelete={handleConfirmDelete}
                onCancelDelete={handleCancelDelete}
                isOperationInProgress={isOperationInProgress}
                itemType={intl.formatMessage(DataConnectorsResources.dataConnectorItemType)}
                actionVerb={intl.formatMessage(DataConnectorsResources.disconnect)}
                selectedItems={connectorToDelete ? [connectorToDelete] : Array.from(selectedRowsSet)}
                title={
                    connectorToDelete
                        ? intl.formatMessage(DataConnectorsResources.disconnectDataConnectorTitle)
                        : selectedRowsSet.size > 1
                          ? intl.formatMessage(DataConnectorsResources.disconnectMultipleDataConnectorsTitle, {
                                count: selectedRowsSet.size,
                            })
                          : intl.formatMessage(DataConnectorsResources.disconnectDataConnectorTitle)
                }
                message={
                    connectorToDelete
                        ? intl.formatMessage(DataConnectorsResources.disconnectDataConnectorMessage)
                        : selectedRowsSet.size > 1
                          ? intl.formatMessage(DataConnectorsResources.disconnectMultipleDataConnectorsMessage, {
                                count: selectedRowsSet.size,
                            })
                          : intl.formatMessage(DataConnectorsResources.disconnectDataConnectorMessage)
                }
                connectorTypes={getSelectedConnectorTypes()}
            />

            <CreateOrUpdateDataConnectorDialog
                agentIdentity={agent?.identity}
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                onSubmit={handleConnectorSubmit}
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

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
    Link,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    Skeleton,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    Text,
} from '@fluentui/react-components';
import { Delete16Regular, Edit16Regular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { Connector, ConnectorStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { McpConnectorStatus } from './Connectors';
import { useConnectorsStyles } from './Connectors.styles';
import { ConnectorStatusDialog } from './ConnectorStatusDialog';
import { getStatusIcon } from './ConnectorStatusUtils';
import EmptyState, { EmptyStateType } from './EmptyState';
import { ConnectorType, getConnectorIcon, getConnectorName, getConnectorService } from './Wizard/Common/ConnectorType';

type ConnectorWithService = Connector & { service: string };

export interface ConnectorsDataGridProps {
    connectors: Connector[];
    selectedKeys: Set<string>;
    isEmpty: boolean;
    isLoading: boolean;
    isRefreshing: boolean;
    isOperationInProgress: boolean;
    setSelectedKeys: React.Dispatch<React.SetStateAction<Set<string>>>;
    addNewConnector: () => void;
    onEditConnector: (connector: Connector) => void;
    onDeleteConnector: (connectorName: string) => void;
    connectionMap: Record<string, ConnectorStatus>;
    loadingStatusMap: Record<string, boolean>;
}

export const ConnectorsDataGrid = ({
    connectors,
    selectedKeys,
    isEmpty,
    isLoading,
    isRefreshing,
    isOperationInProgress,
    addNewConnector,
    onEditConnector,
    onDeleteConnector,
    setSelectedKeys,
    connectionMap,
    loadingStatusMap,
}: ConnectorsDataGridProps) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();

    const [isStatusDialogOpen, setIsStatusDialogOpen] = useState(false);
    const [selectedStatus, setSelectedStatus] = useState<ConnectorStatus | null>(null);

    const [sortState, setSortState] = useState<{
        sortColumn: string;
        sortDirection: 'ascending' | 'descending';
    }>({
        sortColumn: DEFAULT_SORT_COLUMN,
        sortDirection: DEFAULT_SORT_DIRECTION,
    });

    const onSortChange: DataGridProps['onSortChange'] = (_, nextSortState) => {
        setSortState({
            sortColumn: nextSortState.sortColumn?.toString() || DEFAULT_SORT_COLUMN,
            sortDirection: nextSortState.sortDirection || DEFAULT_SORT_DIRECTION,
        });
    };

    const connectorsToDisplay = useMemo(() => {
        if (isLoading || isRefreshing) {
            return createShimmerData(SHIMMER_ITEMS_COUNT);
        }
        return connectors;
    }, [isLoading, isRefreshing, connectors]);

    const createShimmerCell = useCallback(
        (skeletonItems: { width: string; height: string; marginBottom?: string }[]) => (
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
        ),
        [styles]
    );

    const renderCellWithShimmer = useCallback(
        (
            item: ConnectorWithService,
            shimmerConfig: { width: string; height: string; marginBottom?: string }[],
            renderContent: (item: ConnectorWithService) => React.ReactNode
        ) => {
            const shimmerItem = item as ConnectorWithService & { isShimmer?: boolean };
            if (shimmerItem.isShimmer) {
                return createShimmerCell(shimmerConfig);
            }
            return renderContent(item);
        },
        [createShimmerCell]
    );

    const handleStatusClick = useCallback((connectorStatus: ConnectorStatus) => {
        setSelectedStatus(connectorStatus);
        setIsStatusDialogOpen(true);
    }, []);

    const renderStatusIcon = useCallback(
        (status: string, onSeeDetailsClick?: () => void) => {
            const { icon } = getStatusIcon(status);
            const isErrorStatus = status === McpConnectorStatus.Error || status === McpConnectorStatus.Failed;

            return (
                <div className={styles.iconAndTextContainer}>
                    {icon}
                    <Text>{status}</Text>
                    {isErrorStatus && onSeeDetailsClick && (
                        <Link
                            className={styles.seeDetailsLink}
                            onClick={e => {
                                e.stopPropagation();
                                onSeeDetailsClick();
                            }}
                        >
                            {intl.formatMessage(ConnectorsResources.seeDetails)}
                        </Link>
                    )}
                </div>
            );
        },
        [intl, styles.iconAndTextContainer, styles.seeDetailsLink]
    );

    const columns: TableColumnDefinition<ConnectorWithService>[] = useMemo(
        () => [
            createTableColumn<ConnectorWithService>({
                columnId: 'name',
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(SreAgentResources.name)}</Text>,
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '200px', height: '16px' }], item => (
                        <TableCellLayout>
                            <div className={styles.nameCellContainer}>
                                <span className={styles.nameText}>{item.name}</span>
                                <div className={styles.nameMenuContainer}>
                                    <ItemActionsMenu
                                        connector={item}
                                        onEdit={() => onEditConnector(item)}
                                        onDelete={() => onDeleteConnector(item.name)}
                                    />
                                </div>
                            </div>
                        </TableCellLayout>
                    )),
            }),
            createTableColumn<ConnectorWithService>({
                columnId: 'service',
                compare: (a, b) => a.service.localeCompare(b.service),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ConnectorsResources.service)}</Text>,
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '100px', height: '14px' }], item => {
                        const connectorType = item.dataConnectorType as ConnectorType;
                        if (connectorType) {
                            return (
                                <TableCellLayout>
                                    <div className={styles.iconAndTextContainer}>
                                        <img
                                            className={styles.connectorIcon}
                                            src={getConnectorIcon(connectorType, intl)}
                                            alt={getConnectorName(connectorType, intl)}
                                        />
                                        <Text>
                                            {connectorType !== ConnectorType.McpServer
                                                ? getConnectorService(connectorType, intl)
                                                : intl.formatMessage(ConnectorsResources.mcpServer)}
                                        </Text>
                                    </div>
                                </TableCellLayout>
                            );
                        }
                        return <TableCellLayout>{item.dataConnectorType}</TableCellLayout>;
                    }),
            }),
            createTableColumn<ConnectorWithService>({
                columnId: 'status',
                compare: (a, b) => {
                    const statusA = connectionMap[a.name]?.status || '-';
                    const statusB = connectionMap[b.name]?.status || '-';
                    return statusA.localeCompare(statusB);
                },
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ConnectorsResources.status)}</Text>,
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '120px', height: '20px' }], item => {
                        const isLoadingThisStatus = loadingStatusMap[item.name] ?? true;
                        if (isLoadingThisStatus) {
                            return (
                                <TableCellLayout>
                                    <Skeleton>
                                        <SkeletonItem style={{ width: '120px', height: '20px' }} />
                                    </Skeleton>
                                </TableCellLayout>
                            );
                        }
                        const connectorStatus = connectionMap[item.name];
                        const status = connectorStatus?.status || '-';
                        return (
                            <TableCellLayout>
                                {status !== '-' ? (
                                    renderStatusIcon(status, () => handleStatusClick(connectorStatus))
                                ) : (
                                    <Text>{status}</Text>
                                )}
                            </TableCellLayout>
                        );
                    }),
            }),
            createTableColumn<ConnectorWithService>({
                columnId: 'source',
                compare: (a, b) => (a.source || '').localeCompare(b.source || ''),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ConnectorsResources.source)}</Text>,
                renderCell: item => renderCellWithShimmer(item, [{ width: '90px', height: '16px' }], () => <>{item.source}</>),
            }),
        ],
        [
            connectionMap,
            handleStatusClick,
            intl,
            loadingStatusMap,
            onDeleteConnector,
            onEditConnector,
            renderCellWithShimmer,
            renderStatusIcon,
            styles.connectorIcon,
            styles.iconAndTextContainer,
            styles.nameCellContainer,
            styles.nameMenuContainer,
            styles.nameText,
        ]
    );

    const selectedItemsForDataGrid = useMemo(() => {
        if (isLoading || isRefreshing) {
            return new Set();
        }
        const indices = Array.from(selectedKeys)
            .map(name => connectors.findIndex(dc => dc.name === name))
            .filter(index => index >= 0);
        return new Set(indices);
    }, [selectedKeys, connectors, isLoading, isRefreshing]);

    const onSelectionChange: DataGridProps['onSelectionChange'] = (_, data) => {
        if (isLoading || isRefreshing) {
            return;
        }

        const selectedArray = Array.from(data.selectedItems)
            .map(index => {
                const rowIndex = typeof index === 'number' ? index : parseInt(index.toString());
                return connectors[rowIndex]?.name;
            })
            .filter(Boolean) as string[];

        setSelectedKeys(new Set(selectedArray));
    };

    const columnSizingOptions = useMemo(
        () => ({
            name: {
                minWidth: 150,
                idealWidth: 300,
                defaultWidth: 300,
            },
            service: {
                minWidth: 150,
                idealWidth: 300,
                defaultWidth: 300,
            },
            status: {
                minWidth: 150,
                idealWidth: 300,
                defaultWidth: 300,
            },
            source: {
                minWidth: 100,
                idealWidth: 100,
                defaultWidth: 300,
            },
        }),
        []
    );

    return (
        <>
            <DataGrid
                items={connectorsToDisplay}
                columns={columns}
                sortable={!isLoading && !isRefreshing}
                sortState={sortState}
                onSortChange={onSortChange}
                selectionMode="multiselect"
                selectedItems={selectedItemsForDataGrid as Set<any>}
                onSelectionChange={onSelectionChange}
                columnSizingOptions={columnSizingOptions}
                resizableColumns
                className={styles.dataGrid}
            >
                <DataGridHeader>
                    <DataGridRow
                        selectionCell={{
                            checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel) },
                        }}
                    >
                        {({ renderHeaderCell }) => (
                            <DataGridHeaderCell className={styles.headerCell}>{renderHeaderCell()}</DataGridHeaderCell>
                        )}
                    </DataGridRow>
                </DataGridHeader>
                <DataGridBody<Connector>>
                    {({ item, rowId }) => (
                        <DataGridRow<Connector>
                            key={rowId}
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) },
                            }}
                        >
                            {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                        </DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>
            {!isLoading && !isRefreshing && !isOperationInProgress && connectors.length === 0 && (
                <div className={styles.emptyStateContainer}>
                    <EmptyState
                        variant={isEmpty ? EmptyStateType.NoItems : EmptyStateType.NoSearchResults}
                        onPrimaryAction={isEmpty ? addNewConnector : () => {}}
                        isActionDisabled={isLoading || isOperationInProgress}
                    />
                </div>
            )}
            <ConnectorStatusDialog isOpen={isStatusDialogOpen} onOpenChange={setIsStatusDialogOpen} connectorStatus={selectedStatus} />
        </>
    );
};

const SHIMMER_ITEMS_COUNT = 3;
const DEFAULT_SORT_COLUMN = 'name';
const DEFAULT_SORT_DIRECTION = 'ascending' as const;

const createShimmerData = (count: number): Connector[] => {
    return Array.from(
        { length: count },
        (_, index) =>
            ({
                name: `shimmer-${index}`,
                dataConnectorType: 'shimmer',
                dataSource: undefined,
                identity: '',
                isShimmer: true,
            }) as unknown as Connector & { isShimmer: boolean }
    );
};

interface ItemActionsMenuProps {
    connector: Connector;
    onEdit: (connector: Connector) => void;
    onDelete: (connectorName: string) => void;
}

const ItemActionsMenu = ({ connector, onEdit, onDelete }: ItemActionsMenuProps) => {
    const intl = useIntl();

    return (
        <Menu>
            <MenuTrigger disableButtonEnhancement>
                <Button appearance="transparent" size="small" icon={<MoreHorizontal20Regular />} onClick={e => e.stopPropagation()} />
            </MenuTrigger>
            <MenuPopover>
                <MenuList>
                    <MenuItem
                        icon={<Edit16Regular />}
                        onClick={e => {
                            e.stopPropagation();
                            onEdit(connector);
                        }}
                    >
                        {intl.formatMessage(ConnectorsResources.editConnector)}
                    </MenuItem>
                    <MenuItem
                        icon={<Delete16Regular />}
                        onClick={e => {
                            e.stopPropagation();
                            onDelete(connector.name);
                        }}
                    >
                        {intl.formatMessage(ConnectorsResources.deleteConnector)}
                    </MenuItem>
                </MenuList>
            </MenuPopover>
        </Menu>
    );
};

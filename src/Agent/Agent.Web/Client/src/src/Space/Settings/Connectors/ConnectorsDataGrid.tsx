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
import { CheckmarkCircle16Regular, Delete16Regular, Edit16Regular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { Connector } from '../../../Common/Contracts/Azure/SreAgent';
import { ConnectorsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useConnectorsStyles } from './Connectors.styles';
import { ConnectorType, getConnectionName, getConnectorService } from './ConnectorType';
import EmptyState, { EmptyStateType } from './EmptyState';

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
}: ConnectorsDataGridProps) => {
    const intl = useIntl();
    const styles = useConnectorsStyles();

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

    const connectorsToDisplay = isLoading || isRefreshing ? createShimmerData(SHIMMER_ITEMS_COUNT) : connectors;

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
            item: Connector,
            shimmerConfig: { width: string; height: string; marginBottom?: string }[],
            renderContent: (item: Connector) => React.ReactNode
        ) => {
            const shimmerItem = item as Connector & { isShimmer?: boolean };
            if (shimmerItem.isShimmer) {
                return createShimmerCell(shimmerConfig);
            }
            return renderContent(item);
        },
        [createShimmerCell]
    );

    const columns: TableColumnDefinition<Connector>[] = useMemo(
        () => [
            createTableColumn<Connector>({
                columnId: 'name',
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => intl.formatMessage(SreAgentResources.name),
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
            createTableColumn<Connector>({
                columnId: 'dataConnectorType',
                compare: (a, b) => a.dataConnectorType.localeCompare(b.dataConnectorType),
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.type),
                renderCell: item =>
                    renderCellWithShimmer(
                        item,
                        [
                            { width: '100px', height: '14px', marginBottom: '4px' },
                            { width: '80px', height: '12px' },
                        ],
                        item => {
                            const connectorType = item.dataConnectorType as ConnectorType;
                            if (connectorType) {
                                return (
                                    <TableCellLayout>
                                        <div className={styles.connectorTypeContainer}>
                                            <div className={styles.connectorTypeName}>{getConnectionName(connectorType, intl)}</div>
                                            <div className={styles.connectorTypeService}>{getConnectorService(connectorType, intl)}</div>
                                        </div>
                                    </TableCellLayout>
                                );
                            }
                            return <TableCellLayout>{item.dataConnectorType}</TableCellLayout>;
                        }
                    ),
            }),
            createTableColumn<Connector>({
                columnId: 'lastModified',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.lastModified),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '80px', height: '16px' }], () => <TableCellLayout>-</TableCellLayout>),
            }),
            createTableColumn<Connector>({
                columnId: 'lastSynced',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.lastSynced),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '80px', height: '16px' }], () => <TableCellLayout>-</TableCellLayout>),
            }),
            createTableColumn<Connector>({
                columnId: 'status',
                compare: (_a, _b) => 0, // No sorting for now since data not available
                renderHeaderCell: () => intl.formatMessage(ConnectorsResources.status),
                renderCell: item =>
                    renderCellWithShimmer(item, [{ width: '90px', height: '16px' }], () => (
                        <TableCellLayout>
                            <div className={styles.statusContainer}>
                                <CheckmarkCircle16Regular className={styles.statusIcon} />
                                <Text>{intl.formatMessage(ConnectorsResources.connected)}</Text>
                            </div>
                        </TableCellLayout>
                    )),
            }),
        ],
        [intl, onDeleteConnector, onEditConnector, renderCellWithShimmer, styles]
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
                <DataGridBody<Connector>>
                    {({ item, rowId }) => (
                        <DataGridRow<Connector>
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
            {!isLoading && !isRefreshing && connectors.length === 0 && (
                <div className={styles.emptyStateContainer}>
                    <EmptyState
                        variant={isEmpty ? EmptyStateType.NoItems : EmptyStateType.NoSearchResults}
                        onPrimaryAction={isEmpty ? addNewConnector : () => {}}
                        isActionDisabled={isLoading || isOperationInProgress}
                    />
                </div>
            )}
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
                        {intl.formatMessage(SreAgentResources.edit)}
                    </MenuItem>
                    <MenuItem
                        icon={<Delete16Regular />}
                        onClick={e => {
                            e.stopPropagation();
                            onDelete(connector.name);
                        }}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </MenuItem>
                </MenuList>
            </MenuPopover>
        </Menu>
    );
};

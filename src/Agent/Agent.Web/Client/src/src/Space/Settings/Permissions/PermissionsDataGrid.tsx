import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Skeleton,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    Text,
} from '@fluentui/react-components';
import { PeopleCommunityAddFilled } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { Permission } from '../../../Common/Contracts/Azure/SreAgent';
import { AgentPermissionsResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { getRoleDisplayName } from './Permissions';
import { usePermissionsStyles } from './Permissions.styles';

interface PermissionsDataGridProps {
    permissions: Permission[];
    selectedItems: Set<string>;
    onSelectionChange: (selectedItems: Set<string>) => void;
    isLoading: boolean;
    isEmpty: boolean;
    onEmptyStateAction: () => void;
}

interface PermissionWithId extends Permission {
    id: string;
    isShimmer?: boolean;
}

const SHIMMER_ITEMS_COUNT = 5;

const KNOWN_TENANT_NAMES: Record<string, string> = {
    '124edf19-b350-4797-aefc-3206115ffdb3': 'GME',
    '33e01921-4d64-4f8c-a055-5bdaffd5e33d': 'AME',
    '72f988bf-86f1-41af-91ab-2d7cd011db47': 'Microsoft',
    '975f013f-7f24-47e8-a7d3-abc4752bf346': 'PME',
    'cdc5aeea-15c5-4db6-b079-fcadd2505dc2': 'Torus',
};

const getTenantDisplayName = (tenantId: string): string => {
    const knownName = KNOWN_TENANT_NAMES[tenantId.toLowerCase()];
    return knownName || tenantId;
};

export const PermissionsDataGrid: FC<PermissionsDataGridProps> = ({
    permissions,
    selectedItems,
    onSelectionChange,
    isLoading,
    isEmpty,
    onEmptyStateAction,
}) => {
    const intl = useIntl();
    const styles = usePermissionsStyles();

    const createShimmerData = useCallback((count: number): PermissionWithId[] => {
        return Array.from({ length: count }, (_, index) => ({
            id: `shimmer-${index}`,
            displayName: '',
            role: '',
            objectId: '',
            tenantId: '',
            isShimmer: true,
        }));
    }, []);

    const permissionsWithId: PermissionWithId[] = useMemo(() => {
        if (isLoading) {
            return createShimmerData(SHIMMER_ITEMS_COUNT);
        }
        return permissions.map(permission => ({
            ...permission,
            id: `${permission.objectId}-${permission.role}-${permission.tenantId}`,
        }));
    }, [permissions, isLoading, createShimmerData]);

    const createShimmerCell = useCallback(
        (width: string) => (
            <TableCellLayout>
                <div className={styles.shimmerContainer}>
                    <Skeleton>
                        <SkeletonItem style={{ width, height: '16px' }} />
                    </Skeleton>
                </div>
            </TableCellLayout>
        ),
        [styles.shimmerContainer]
    );

    const columns: TableColumnDefinition<PermissionWithId>[] = useMemo(
        () => [
            createTableColumn<PermissionWithId>({
                columnId: 'displayName',
                compare: (a, b) => a.displayName.localeCompare(b.displayName),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(AgentPermissionsResources.displayName)}</Text>,
                renderCell: item => {
                    if (item.isShimmer) {
                        return createShimmerCell('120px');
                    }
                    return <TableCellLayout>{item.displayName}</TableCellLayout>;
                },
            }),
            createTableColumn<PermissionWithId>({
                columnId: 'role',
                compare: (a, b) => a.role.localeCompare(b.role),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(AgentPermissionsResources.role)}</Text>,
                renderCell: item => {
                    if (item.isShimmer) {
                        return createShimmerCell('80px');
                    }
                    return <TableCellLayout>{getRoleDisplayName(item.role, intl)}</TableCellLayout>;
                },
            }),
            createTableColumn<PermissionWithId>({
                columnId: 'objectId',
                compare: (a, b) => a.objectId.localeCompare(b.objectId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(AgentPermissionsResources.objectId)}</Text>,
                renderCell: item => {
                    if (item.isShimmer) {
                        return createShimmerCell('200px');
                    }
                    return <TableCellLayout>{item.objectId}</TableCellLayout>;
                },
            }),
            createTableColumn<PermissionWithId>({
                columnId: 'tenantId',
                compare: (a, b) => a.tenantId.localeCompare(b.tenantId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(AgentPermissionsResources.tenantId)}</Text>,
                renderCell: item => {
                    if (item.isShimmer) {
                        return createShimmerCell('200px');
                    }
                    return <TableCellLayout>{getTenantDisplayName(item.tenantId)}</TableCellLayout>;
                },
            }),
        ],
        [intl, createShimmerCell]
    );

    const columnSizingOptions = useMemo(
        () => ({
            displayName: {
                defaultWidth: 200,
            },
            role: {
                defaultWidth: 200,
            },
            objectId: {
                defaultWidth: 300,
            },
            tenantId: {
                defaultWidth: 250,
            },
        }),
        []
    );

    const handleSelectionChange = useCallback(
        (_: unknown, data: { selectedItems: Set<string | number> }) => {
            const newSelectedItems = new Set<string>();
            data.selectedItems.forEach(item => {
                if (typeof item === 'string') {
                    newSelectedItems.add(item);
                }
            });
            onSelectionChange(newSelectedItems);
        },
        [onSelectionChange]
    );

    return (
        <>
            <div className={styles.dataGridWrapper}>
                <DataGrid
                    items={permissionsWithId}
                    columns={columns}
                    sortable={!isLoading && !isEmpty}
                    selectionMode="multiselect"
                    selectedItems={selectedItems as Set<string | number>}
                    onSelectionChange={handleSelectionChange}
                    getRowId={item => item.id}
                    resizableColumns
                    columnSizingOptions={columnSizingOptions}
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
                    {(isLoading || !isEmpty) && (
                        <DataGridBody<PermissionWithId>>
                            {({ item, rowId }) => (
                                <DataGridRow<PermissionWithId>
                                    key={rowId}
                                    selectionCell={{
                                        checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) },
                                    }}
                                >
                                    {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    )}
                </DataGrid>
            </div>
            {!isLoading && isEmpty && (
                <div className={styles.emptyStateContainer}>
                    <div className={styles.emptyStateContent}>
                        <PeopleCommunityAddFilled className={styles.emptyStateIcon} />
                        <Text className={styles.emptyStateTitle}>{intl.formatMessage(AgentPermissionsResources.emptyStateTitle)}</Text>
                        <Text className={styles.emptyStateDescription}>
                            {intl.formatMessage(AgentPermissionsResources.emptyStateDescription)}
                        </Text>
                        <Button appearance="primary" onClick={onEmptyStateAction}>
                            {intl.formatMessage(AgentPermissionsResources.addPermission)}
                        </Button>
                    </div>
                </div>
            )}
        </>
    );
};

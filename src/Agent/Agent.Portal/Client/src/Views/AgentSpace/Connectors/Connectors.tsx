import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Link,
    makeStyles,
    Spinner,
    Switch,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SecretValue } from '../../../Common/Components/SecretValue/SecretValue';
import { AgentSpace, AgentSpaceConnector } from '../../../Common/Contracts/AgentSpace';
import { ArmObj } from '../../../Common/Contracts/Arm';
import { IdentityKeys } from '../../../Common/Contracts/Identity';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { safeCompare } from '../../../Common/Utilities/String';
import { PortalResources } from '../../../Strings/Resources';
import { ConnectorFormValues, CreateConnectorDialog } from './CreateConnectorDialog';
import { DeleteConnectorDialog } from './DeleteConnectorDialog';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalL,
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
    toolbarActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
    },
    toolbarRight: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: tokens.spacingVerticalXXXL,
        gap: tokens.spacingVerticalM,
        color: tokens.colorNeutralForeground3,
    },
    dataGrid: {
        maxHeight: '400px',
        overflowY: 'auto',
    },
});

interface ConnectorsProps {
    spaceResourceId: string;
    agentSpace: ArmObj<AgentSpace> | null;
    connectors: AgentSpaceConnector[];
    isLoading: boolean;
    refresh: () => Promise<void>;
    createConnector: (connector: AgentSpaceConnector) => Promise<boolean>;
    updateConnector: (connector: AgentSpaceConnector) => Promise<boolean>;
    deleteConnectors: (connectorNames: string[]) => Promise<boolean>;
}

export const Connectors = ({
    spaceResourceId: _spaceResourceId,
    agentSpace,
    connectors,
    isLoading,
    refresh,
    createConnector,
    updateConnector,
    deleteConnectors,
}: ConnectorsProps) => {
    const intl = useIntl();
    const styles = useStyles();

    const [selectedConnectorNames, setSelectedConnectorNames] = useState<Set<string>>(new Set());
    const [revealAll, setRevealAll] = useState(false);
    const [showCreateDialog, setShowCreateDialog] = useState(false);
    const [showDeleteDialog, setShowDeleteDialog] = useState(false);
    const [editingConnector, setEditingConnector] = useState<AgentSpaceConnector | null>(null);
    const [isDeleting, setIsDeleting] = useState(false);

    const handleEdit = useCallback((connector: AgentSpaceConnector) => {
        setEditingConnector(connector);
        setShowCreateDialog(true);
    }, []);

    const handleCloseCreateDialog = useCallback(() => {
        setShowCreateDialog(false);
        setEditingConnector(null);
    }, []);

    const handleCreateOrUpdate = useCallback(
        async (connector: AgentSpaceConnector): Promise<boolean> => {
            if (editingConnector) {
                return updateConnector(connector);
            }
            return createConnector(connector);
        },
        [editingConnector, createConnector, updateConnector]
    );

    const handleDeleteConfirm = useCallback(async () => {
        setIsDeleting(true);
        const success = await deleteConnectors(Array.from(selectedConnectorNames));
        setIsDeleting(false);
        if (success) {
            setShowDeleteDialog(false);
            setSelectedConnectorNames(new Set());
        }
    }, [deleteConnectors, selectedConnectorNames]);

    const getIdentityDisplayName = useCallback(
        (identity: string) => {
            if (identity === IdentityKeys.system) {
                return intl.formatMessage(PortalResources.systemAssigned);
            }
            const parsed = parseArmId(identity);
            return parsed.resourceName || identity;
        },
        [intl]
    );

    const columns: TableColumnDefinition<AgentSpaceConnector>[] = useMemo(
        () => [
            createTableColumn<AgentSpaceConnector>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link
                            onClick={e => {
                                e.stopPropagation();
                                handleEdit(item);
                            }}
                        >
                            {item.name}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<AgentSpaceConnector>({
                columnId: 'type',
                compare: (a, b) => safeCompare(a.dataConnectorType, b.dataConnectorType),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.type)}</Text>,
                renderCell: item => <TableCellLayout>{item.dataConnectorType}</TableCellLayout>,
            }),
            createTableColumn<AgentSpaceConnector>({
                columnId: 'dataSource',
                compare: (a, b) => safeCompare(a.dataSource, b.dataSource),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.dataSource)}</Text>,
                renderCell: item => {
                    if (!item.dataSource) {
                        return <TableCellLayout>-</TableCellLayout>;
                    }
                    return (
                        <TableCellLayout>
                            <SecretValue value={item.dataSource} forceRevealed={revealAll} />
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<AgentSpaceConnector>({
                columnId: 'identity',
                compare: (a, b) => safeCompare(a.identity, b.identity),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.identity)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Tooltip content={item.identity} relationship="label">
                            <Text>{getIdentityDisplayName(item.identity)}</Text>
                        </Tooltip>
                    </TableCellLayout>
                ),
            }),
        ],
        [intl, revealAll, getIdentityDisplayName, handleEdit]
    );

    const onSelectionChange = useCallback((_: unknown, data: { selectedItems: Set<string | number> }) => {
        setSelectedConnectorNames(data.selectedItems as Set<string>);
    }, []);

    const editInitialValues: ConnectorFormValues | undefined = useMemo(() => {
        if (!editingConnector) return undefined;
        return {
            name: editingConnector.name,
            dataConnectorType: editingConnector.dataConnectorType,
            dataSource: editingConnector.dataSource || '',
            identity: editingConnector.identity,
        };
    }, [editingConnector]);

    return (
        <div className={styles.container}>
            <div className={styles.toolbar}>
                <div className={styles.toolbarActions}>
                    <Button icon={<Add16Regular />} appearance="primary" onClick={() => setShowCreateDialog(true)}>
                        {intl.formatMessage(PortalResources.createConnector)}
                    </Button>
                    <Button icon={<ArrowClockwise16Regular />} onClick={refresh} disabled={isLoading}>
                        {intl.formatMessage(PortalResources.refresh)}
                    </Button>
                    <Button
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteDialog(true)}
                        disabled={selectedConnectorNames.size === 0 || isLoading}
                    >
                        {intl.formatMessage(PortalResources.delete)}
                    </Button>
                </div>
                <div className={styles.toolbarRight}>
                    <Switch
                        checked={revealAll}
                        onChange={(_, data) => setRevealAll(data.checked)}
                        label={intl.formatMessage(PortalResources.revealAll)}
                    />
                </div>
            </div>

            {isLoading ? (
                <div className={styles.emptyState}>
                    <Spinner size="medium" />
                </div>
            ) : connectors.length === 0 ? (
                <div className={styles.emptyState}>
                    <Text>{intl.formatMessage(PortalResources.noConnectors)}</Text>
                </div>
            ) : (
                <DataGrid
                    items={connectors}
                    columns={columns}
                    sortable
                    getRowId={item => item.name}
                    className={styles.dataGrid}
                    selectionMode="multiselect"
                    selectedItems={selectedConnectorNames}
                    onSelectionChange={onSelectionChange}
                >
                    <DataGridHeader>
                        <DataGridRow
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': intl.formatMessage(PortalResources.selectAll) },
                            }}
                        >
                            {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                        </DataGridRow>
                    </DataGridHeader>
                    <DataGridBody<AgentSpaceConnector>>
                        {({ item, rowId }) => (
                            <DataGridRow<AgentSpaceConnector>
                                key={rowId}
                                selectionCell={{
                                    checkboxIndicator: { 'aria-label': `Select ${item.name}` },
                                }}
                            >
                                {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                            </DataGridRow>
                        )}
                    </DataGridBody>
                </DataGrid>
            )}

            <CreateConnectorDialog
                isOpen={showCreateDialog}
                onClose={handleCloseCreateDialog}
                agentSpace={agentSpace}
                existingConnectors={connectors}
                onSubmit={handleCreateOrUpdate}
                initialValues={editInitialValues}
                isEditMode={!!editingConnector}
            />

            <DeleteConnectorDialog
                isOpen={showDeleteDialog}
                onClose={() => setShowDeleteDialog(false)}
                connectorNames={Array.from(selectedConnectorNames)}
                onConfirm={handleDeleteConfirm}
                isDeleting={isDeleting}
            />
        </div>
    );
};

import {
    Button,
    Card,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Image,
    Link,
    makeStyles,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    SearchBox,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    useTableFeatures,
    useTableSort,
} from '@fluentui/react-components';
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular } from '@fluentui/react-icons';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentSpaceClient } from '../../../Common/Clients/AgentSpaceClient';
import { ResourceGroupPill } from '../../../Common/Components/ResourceGroupPill/ResourceGroupPill';
import { SubscriptionPill } from '../../../Common/Components/SubscriptionPill/SubscriptionPill';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useAuth } from '../../../Common/Contexts/AuthContext';
import { useNotifications } from '../../../Common/Contexts/NotificationContext';
import { useSubscriptions } from '../../../Common/Contexts/SubscriptionsContext';
import { AgentSpaceArgItem } from '../../../Common/Contracts/AgentSpace';
import { SpecialControlValue } from '../../../Common/Contracts/Amplitude';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useAmplitudeTelemetry } from '../../../Common/Hooks/useAmplitudeTelemetry';
import { usePersistentNavigate } from '../../../Common/Hooks/usePersistentNavigate';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';
import { getUserFriendlyLocation } from '../../../Common/Utilities/Location';
import { safeCompare } from '../../../Common/Utilities/String';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../../Common/Utilities/Url';
import { PortalResources } from '../../../Strings/Resources';
import { CreateAgentSpaceDialog } from '../CreateAgentSpace/CreateAgentSpaceDialog';
import { AgentSpaceListSkeleton } from './AgentSpaceListSkeleton';
import { DeleteAgentSpaceDialog } from './DeleteAgentSpaceDialog';
import { NoAgentSpaces } from './NoAgentSpaces';

const useStyles = makeStyles({
    errorContainer: {
        maxWidth: '1000px',
    },
    controlsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    actionButtons: {
        display: 'flex',
        gap: '12px',
    },
    controlsRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '12px',
    },
    searchControls: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
        flexWrap: 'wrap',
    },
    searchBox: {
        width: '250px',
    },
    card: {
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        flex: '1',
        minHeight: '0',
    },
    dataGrid: {
        flex: '1',
        overflowY: 'auto',
    },
});

export const AgentSpacesGrid = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { isAuthenticated } = useAuth();
    const { start, succeed, fail } = useNotifications();
    const { logEvent } = useTelemetry(TelemetrySource.HomeBrowseView, undefined);
    const { logControlEvent } = useAmplitudeTelemetry();
    const navigate = usePersistentNavigate();
    const { selectedSubscriptions, subscriptions } = useSubscriptions();

    const agentSpaceClient = useMemo(() => AgentSpaceClient.getInstance(TelemetrySource.HomeBrowseView), []);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [agentSpaces, setAgentSpaces] = useState<AgentSpaceArgItem[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [selectedSpaceIds, setSelectedSpaceIds] = useState<Set<string>>(new Set());
    const [showDeleteConfirmDialog, setShowDeleteConfirmDialog] = useState<boolean>(false);
    const [showCreateAgentSpaceDialog, setShowCreateAgentSpaceDialog] = useState<boolean>(false);

    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>([]);
    const [selectedResourceGroupNames, setSelectedResourceGroupNames] = useState<string[]>([]);

    const selectedSpaces = useMemo(() => {
        return agentSpaces.filter(space => selectedSpaceIds.has(space.id));
    }, [agentSpaces, selectedSpaceIds]);

    const subscriptionDisplayNameMap = useMemo(() => {
        return new Map(subscriptions.map(sub => [sub.subscriptionId, sub.displayName]));
    }, [subscriptions]);

    const columns: TableColumnDefinition<AgentSpaceArgItem>[] = useMemo(
        () => [
            createTableColumn<AgentSpaceArgItem>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => (
                    <TableCellLayout
                        media={
                            <Image src="SreAgentSpace.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.agentSpace)} />
                        }
                    >
                        <Link onClick={() => navigate(`/spaces/${encodeURIComponent(item.id)}`)}>{item.name}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<AgentSpaceArgItem>({
                columnId: 'subscription',
                compare: (a, b) => safeCompare(a.subscriptionId, b.subscriptionId),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.subscription)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openSubscriptionOverviewInNewTab(item.subscriptionId)}>
                            {subscriptionDisplayNameMap.get(item.subscriptionId) || item.subscriptionId}
                        </Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<AgentSpaceArgItem>({
                columnId: 'resourceGroup',
                compare: (a, b) => safeCompare(a.resourceGroup, b.resourceGroup),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openResourceGroupOverviewInNewTab(item.id)}>{item.resourceGroup}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<AgentSpaceArgItem>({
                columnId: 'region',
                compare: (a, b) => safeCompare(a.location, b.location),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.region)}</Text>,
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ],
        [intl, navigate, subscriptionDisplayNameMap]
    );

    const filteredSpaces = useMemo(() => {
        if (!searchQuery) return agentSpaces;

        const lowerQuery = searchQuery.toLowerCase();
        return agentSpaces.filter(
            space =>
                space.name.toLowerCase().includes(lowerQuery) ||
                space.subscriptionId.toLowerCase().includes(lowerQuery) ||
                space.resourceGroup.toLowerCase().includes(lowerQuery) ||
                space.location.toLowerCase().includes(lowerQuery)
        );
    }, [agentSpaces, searchQuery]);

    const {
        getRows,
        sort: { getSortDirection, toggleColumnSort, sort },
    } = useTableFeatures(
        {
            columns,
            items: filteredSpaces,
        },
        [
            useTableSort({
                defaultSortState: { sortColumn: 'name', sortDirection: 'ascending' },
            }),
        ]
    );

    const headerSortProps = (columnId: string | number) => ({
        onClick: (e: React.MouseEvent) => {
            toggleColumnSort(e, columnId);
        },
        sortDirection: getSortDirection(columnId),
    });

    const rows = sort(getRows());

    const onSelectionChange = useCallback((_: unknown, data: { selectedItems: Set<string | number> }) => {
        setSelectedSpaceIds(data.selectedItems as Set<string>);
    }, []);

    const fetchAgentSpaces = useCallback(async () => {
        if (!isAuthenticated) {
            setIsLoading(false);
            return;
        }

        setIsLoading(true);
        setError(null);

        const subIds = selectedSubscriptionIds.length > 0 ? selectedSubscriptionIds : undefined;
        const rgNames = selectedResourceGroupNames.length > 0 ? selectedResourceGroupNames : undefined;

        const response = await agentSpaceClient.getAgentSpacesFromArg(subIds, rgNames);

        if (!response.isSuccessful) {
            const errorMessage = response.error instanceof Error ? response.error.message : 'Unknown error occurred';
            setError(errorMessage);
            logEvent({
                action: 'fetch-agent-spaces',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: {
                    error: errorMessage,
                },
            });
        } else {
            setAgentSpaces(response.content || []);
        }

        setIsLoading(false);
    }, [agentSpaceClient, isAuthenticated, logEvent, selectedSubscriptionIds, selectedResourceGroupNames]);

    const deleteAgentSpaces = useCallback(
        async (spacesToDelete: AgentSpaceArgItem[]) => {
            const isSingleSpace = spacesToDelete.length === 1;
            const notificationId = start(
                intl.formatMessage(PortalResources.deleteAgentSpace),
                intl.formatMessage(PortalResources.deleteAgentSpaceInProgress)
            );

            const results = await Promise.all(
                spacesToDelete.map(space =>
                    agentSpaceClient.deleteAgentSpace(space.id).then(response => ({
                        space,
                        response,
                    }))
                )
            );

            const successCount = results.filter(r => r.response.isSuccessful).length;
            const failures = results.filter(r => !r.response.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.deleteAgentSpace),
                    isSingleSpace
                        ? intl.formatMessage(PortalResources.deleteAgentSpaceSuccess, { name: spacesToDelete[0].name })
                        : intl.formatMessage(PortalResources.agentSpaceDeleted)
                );
                fetchAgentSpaces();
                setSelectedSpaceIds(new Set());
            } else {
                const errorDetail = isSingleSpace ? getArmErrorMessage(failures[0].response.error) : '';
                fail(
                    notificationId,
                    intl.formatMessage(PortalResources.deleteAgentSpace),
                    isSingleSpace && errorDetail
                        ? intl.formatMessage(PortalResources.deleteAgentSpaceErrorDetail, {
                              name: failures[0].space.name,
                              error: errorDetail,
                          })
                        : intl.formatMessage(PortalResources.deleteAgentSpaceError)
                );

                logEvent({
                    action: 'delete-agent-spaces',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        failureCount: failures.length,
                        errors: failures.map(f => ({
                            spaceId: f.space.id,
                            error: getArmErrorMessage(f.response.error) || String(f.response.error),
                        })),
                    },
                });

                if (successCount > 0) {
                    fetchAgentSpaces();
                    setSelectedSpaceIds(new Set());
                }
            }
        },
        [agentSpaceClient, intl, start, succeed, fail, fetchAgentSpaces, logEvent]
    );

    const handleDeleteConfirm = useCallback(() => {
        logControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'deleteAgentSpaces',
            targetFriendlyName: 'Yes',
            valueObjectName: SpecialControlValue.DoAction,
            valueObjectFriendlyName: SpecialControlValue.DoAction,
        });
        deleteAgentSpaces(selectedSpaces);
    }, [logControlEvent, deleteAgentSpaces, selectedSpaces]);

    useEffect(() => {
        setSelectedSubscriptionIds(selectedSubscriptions.map(sub => sub.subscriptionId));
    }, [selectedSubscriptions]);

    useEffect(() => {
        fetchAgentSpaces();
    }, [fetchAgentSpaces]);

    return (
        <>
            {!isLoading && error && (
                <div className={styles.errorContainer}>
                    <MessageBar intent="error">
                        <MessageBarBody>
                            <MessageBarTitle>{intl.formatMessage(PortalResources.requestError)}</MessageBarTitle>
                            <Text>{error}</Text>
                        </MessageBarBody>
                    </MessageBar>
                </div>
            )}

            <div className={styles.controlsContainer}>
                <div className={styles.actionButtons}>
                    <Button appearance="primary" icon={<Add16Regular />} onClick={() => setShowCreateAgentSpaceDialog(true)}>
                        {intl.formatMessage(PortalResources.createAgentSpace)}
                    </Button>
                    <Button
                        icon={<ArrowClockwise16Regular />}
                        onClick={fetchAgentSpaces}
                        disabled={isLoading}
                        title={intl.formatMessage(PortalResources.refresh)}
                    >
                        {intl.formatMessage(PortalResources.refresh)}
                    </Button>
                    <Button
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmDialog(true)}
                        disabled={selectedSpaces.length === 0 || isLoading}
                    >
                        {intl.formatMessage(PortalResources.delete)}
                    </Button>
                </div>

                <div className={styles.controlsRow}>
                    <div className={styles.searchControls}>
                        <SearchBox
                            placeholder={intl.formatMessage(PortalResources.searchAgentSpaces)}
                            className={styles.searchBox}
                            value={searchQuery}
                            onChange={(_, data) => setSearchQuery(data.value)}
                            disabled={isLoading}
                        />
                        <SubscriptionPill
                            selectedSubscriptionIds={selectedSubscriptionIds}
                            onSelectedSubscriptionIdsChange={setSelectedSubscriptionIds}
                            disabled={isLoading}
                        />
                        <ResourceGroupPill
                            selectedSubscriptionIds={selectedSubscriptionIds}
                            selectedResourceGroupNames={selectedResourceGroupNames}
                            onSelectedResourceGroupNamesChange={setSelectedResourceGroupNames}
                            disabled={isLoading}
                        />
                    </div>
                </div>
            </div>

            <Card className={styles.card}>
                {!isLoading && agentSpaces.length === 0 ? (
                    <NoAgentSpaces onClickCreate={() => setShowCreateAgentSpaceDialog(true)} />
                ) : isLoading ? (
                    <div className={styles.dataGrid}>
                        <AgentSpaceListSkeleton rowCount={6} />
                    </div>
                ) : (
                    <DataGrid
                        items={rows}
                        columns={columns}
                        sortable
                        getRowId={item => (item as any).item.id}
                        className={styles.dataGrid}
                        selectionMode="multiselect"
                        selectedItems={selectedSpaceIds}
                        onSelectionChange={onSelectionChange}
                    >
                        <DataGridHeader>
                            <DataGridRow
                                selectionCell={{
                                    checkboxIndicator: { 'aria-label': intl.formatMessage(PortalResources.selectAll) },
                                }}
                            >
                                {({ renderHeaderCell, columnId }) => (
                                    <DataGridHeaderCell {...headerSortProps(columnId)}>{renderHeaderCell()}</DataGridHeaderCell>
                                )}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<AgentSpaceArgItem>>
                            {({ item, rowId }) => (
                                <DataGridRow<AgentSpaceArgItem>
                                    key={rowId}
                                    selectionCell={{
                                        checkboxIndicator: {
                                            'aria-label': `Select ${(item as any).item.name}`,
                                        },
                                    }}
                                >
                                    {({ renderCell }) => <DataGridCell>{renderCell((item as any).item)}</DataGridCell>}
                                </DataGridRow>
                            )}
                        </DataGridBody>
                    </DataGrid>
                )}
            </Card>

            <DeleteAgentSpaceDialog
                open={showDeleteConfirmDialog}
                selectedSpaces={selectedSpaces}
                onClose={() => setShowDeleteConfirmDialog(false)}
                onConfirm={handleDeleteConfirm}
            />

            {/* Fully dismount create components on close to fully reset state */}
            {showCreateAgentSpaceDialog && (
                <CreateAgentSpaceDialog
                    isDialogOpen={showCreateAgentSpaceDialog}
                    setIsDialogOpen={setShowCreateAgentSpaceDialog}
                    onCreated={fetchAgentSpaces}
                />
            )}
        </>
    );
};

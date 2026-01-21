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
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation } from 'react-router-dom';
import { SreAgentClient } from '../../Common/Clients/SreAgentClient';
import { ResourceGroupPill } from '../../Common/Components/ResourceGroupPill/ResourceGroupPill';
import { SubscriptionPill } from '../../Common/Components/SubscriptionPill/SubscriptionPill';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { useSubscriptions } from '../../Common/Contexts/SubscriptionsContext';
import { SpecialControlValue } from '../../Common/Contracts/Amplitude';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { LogLevel } from '../../Common/Contracts/Telemetry';
import { useAmplitudeTelemetry } from '../../Common/Hooks/useAmplitudeTelemetry';
import { usePersistentNavigate } from '../../Common/Hooks/usePersistentNavigate';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { getUserFriendlyLocation } from '../../Common/Utilities/Location';
import { safeCompare } from '../../Common/Utilities/String';
import { openResourceGroupOverviewInNewTab, openSubscriptionOverviewInNewTab } from '../../Common/Utilities/Url';
import { PortalResources } from '../../Strings/Resources';
import { AgentListSkeleton } from './AgentListSkeleton';
import { CreateAgentDialog } from './Create/CreateAgentDialog';
import { CreateFirstAgent } from './CreateFirstAgent';
import { DeleteAgentDialog } from './DeleteAgentDialog';

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

export const AgentsGrid = () => {
    const intl = useIntl();
    const styles = useStyles();
    const location = useLocation();
    const { isAuthenticated } = useAuth();
    const { start, succeed, fail } = useNotifications();
    const { logEvent } = useTelemetry(TelemetrySource.HomeBrowseView, undefined);
    const { logNavigationEvent, logControlEvent } = useAmplitudeTelemetry();
    const navigate = usePersistentNavigate();
    const { selectedSubscriptions, subscriptions, isLoading: isSubscriptionsLoading } = useSubscriptions();

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.HomeBrowseView), []);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [agents, setAgents] = useState<SreAgentArgItem[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);
    const [selectedAgentIds, setSelectedAgentIds] = useState<Set<string>>(new Set());
    const [showDeleteConfirmDialog, setShowDeleteConfirmDialog] = useState<boolean>(false);

    // Initialize selected subscriptions from context. null = not yet initialized, [] = "All subscriptions"
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[] | null>(null);
    const [selectedResourceGroupNames, setSelectedResourceGroupNames] = useState<string[]>([]);

    // Track fetch request ID to prevent stale responses from overwriting newer data
    // (technically properly tracking the initialized/sync state of the selectedSubscriptionIds
    // fixes the issue, but this is a known and cool pattern to add to our toolbelt)
    const fetchCallIdRef = useRef(0);

    // Track if auto-open from URL param has been attempted (prevents re-triggering on re-renders)
    const hasAutoOpenedCreateRef = useRef(false);

    const selectedAgents = useMemo(() => {
        return agents.filter(agent => selectedAgentIds.has(agent.id));
    }, [agents, selectedAgentIds]);

    const subscriptionDisplayNameMap = useMemo(() => {
        return new Map(subscriptions.map(sub => [sub.subscriptionId, sub.displayName]));
    }, [subscriptions]);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = useMemo(
        () => [
            createTableColumn<SreAgentArgItem>({
                columnId: 'name',
                compare: (a, b) => safeCompare(a.name, b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
                renderCell: item => (
                    <TableCellLayout
                        media={<Image src="SreAgent.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.azureSreAgent)} />}
                    >
                        <Link onClick={() => navigate(`/agents${item.id}`)}>{item.name}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
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
            createTableColumn<SreAgentArgItem>({
                columnId: 'resourceGroup',
                compare: (a, b) => safeCompare(a.resourceGroup, b.resourceGroup),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
                renderCell: item => (
                    <TableCellLayout>
                        <Link onClick={() => openResourceGroupOverviewInNewTab(item.id)}>{item.resourceGroup}</Link>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<SreAgentArgItem>({
                columnId: 'region',
                compare: (a, b) => safeCompare(a.location, b.location),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.region)}</Text>,
                renderCell: item => <TableCellLayout>{getUserFriendlyLocation(item.location)}</TableCellLayout>,
            }),
        ],
        [intl, navigate, subscriptionDisplayNameMap]
    );

    const filteredAgents = useMemo(() => {
        if (!searchQuery) return agents;

        const lowerQuery = searchQuery.toLowerCase();
        return agents.filter(
            agent =>
                agent.name.toLowerCase().includes(lowerQuery) ||
                agent.subscriptionId.toLowerCase().includes(lowerQuery) ||
                agent.resourceGroup.toLowerCase().includes(lowerQuery) ||
                agent.location.toLowerCase().includes(lowerQuery)
        );
    }, [agents, searchQuery]);

    const {
        getRows,
        sort: { getSortDirection, toggleColumnSort, sort },
    } = useTableFeatures(
        {
            columns,
            items: filteredAgents,
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
        setSelectedAgentIds(data.selectedItems as Set<string>);
    }, []);

    const fetchAgents = useCallback(async () => {
        // Don't fetch until filters are initialized from context
        if (!isAuthenticated || isSubscriptionsLoading || selectedSubscriptionIds === null) {
            return;
        }

        // Increment call ID to track this specific request
        const currentCallId = ++fetchCallIdRef.current;

        setIsLoading(true);
        setError(null);

        // Pass filters to ARG query. Empty arrays mean "All"
        const subIds = selectedSubscriptionIds.length > 0 ? selectedSubscriptionIds : undefined;
        const rgNames = selectedResourceGroupNames.length > 0 ? selectedResourceGroupNames : undefined;

        const response = await sreAgentClient.getAgentsFromArg(subIds, rgNames);

        // Ignore stale responses - a newer request has been initiated
        if (currentCallId !== fetchCallIdRef.current) {
            return;
        }

        if (!response.isSuccessful) {
            const errorMessage = response.error instanceof Error ? response.error.message : 'Unknown error occurred';
            setError(errorMessage);
            logEvent({
                action: 'fetch-agents',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                additionalData: {
                    error: errorMessage,
                },
            });
        } else {
            setAgents(response.content || []);
        }

        setIsLoading(false);
    }, [sreAgentClient, isAuthenticated, isSubscriptionsLoading, logEvent, selectedSubscriptionIds, selectedResourceGroupNames]);

    const deleteAgents = useCallback(
        async (selectedAgents: SreAgentArgItem[]) => {
            const isSingleAgent = selectedAgents.length === 1;
            const notificationId = start(
                intl.formatMessage(PortalResources.deleteAgentTitle),
                isSingleAgent
                    ? intl.formatMessage(PortalResources.deleteAgentInProgress, { name: selectedAgents[0].name })
                    : intl.formatMessage(PortalResources.deleteAgentsInProgress, { count: selectedAgents.length })
            );

            const results = await Promise.all(
                selectedAgents.map(agent =>
                    sreAgentClient.deleteAgent(agent.id).then(response => ({
                        agent,
                        response,
                    }))
                )
            );

            const successCount = results.filter(r => r.response.isSuccessful).length;
            const failures = results.filter(r => !r.response.isSuccessful);

            if (failures.length === 0) {
                succeed(
                    notificationId,
                    intl.formatMessage(PortalResources.deleteAgentTitle),
                    isSingleAgent
                        ? intl.formatMessage(PortalResources.deleteAgentSuccess, { name: selectedAgents[0].name })
                        : intl.formatMessage(PortalResources.deleteAgentsSuccess, { count: successCount })
                );
                fetchAgents();
                setSelectedAgentIds(new Set());
            } else {
                const errorMessage = isSingleAgent
                    ? intl.formatMessage(PortalResources.deleteAgentErrorDetail, {
                          name: failures[0].agent.name,
                          error:
                              failures[0].response.error instanceof Error
                                  ? failures[0].response.error.message
                                  : String(failures[0].response.error),
                      })
                    : intl.formatMessage(PortalResources.deleteAgentsError);

                fail(notificationId, intl.formatMessage(PortalResources.deleteAgentTitle), errorMessage);

                logEvent({
                    action: 'delete-agents',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        failureCount: failures.length,
                        errors: failures.map(f => ({
                            agentId: f.agent.id,
                            error: f.response.error instanceof Error ? f.response.error.message : String(f.response.error),
                        })),
                    },
                });

                if (successCount > 0) {
                    fetchAgents();
                    setSelectedAgentIds(new Set());
                }
            }
        },
        [sreAgentClient, intl, start, succeed, fail, fetchAgents, logEvent]
    );

    const handleCreateClick = useCallback(() => {
        logNavigationEvent({
            targetType: 'button',
            targetAction: 'openContextPane',
            targetName: 'SreAgentCreate',
            targetFriendlyName: 'Create',
        });
        setIsCreateDialogOpen(true);
    }, [logNavigationEvent]);

    const handleDeleteConfirm = useCallback(() => {
        logControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'deleteAgents',
            targetFriendlyName: 'Yes',
            valueObjectName: SpecialControlValue.DoAction,
            valueObjectFriendlyName: SpecialControlValue.DoAction,
        });
        deleteAgents(selectedAgents);
    }, [logControlEvent, deleteAgents, selectedAgents]);

    useEffect(() => {
        setSelectedSubscriptionIds(selectedSubscriptions.map(sub => sub.subscriptionId));
    }, [selectedSubscriptions]);

    useEffect(() => {
        fetchAgents();
    }, [fetchAgents]);

    // Auto-open Create Agent dialog if `?create=true` is present in URL (once per load)
    useEffect(() => {
        if (hasAutoOpenedCreateRef.current) {
            return;
        }

        const searchParams = new URLSearchParams(location.search.toLowerCase());
        const createParam = searchParams.get('create');

        if (createParam === 'true' || createParam === '1') {
            hasAutoOpenedCreateRef.current = true;
            setIsCreateDialogOpen(true);
        }
    }, [location.search]);

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
                    <Button icon={<Add16Regular />} appearance="primary" onClick={handleCreateClick} disabled={isLoading}>
                        {intl.formatMessage(PortalResources.createAgent)}
                    </Button>
                    <Button
                        icon={<ArrowClockwise16Regular />}
                        onClick={fetchAgents}
                        disabled={isLoading}
                        title={intl.formatMessage(PortalResources.refresh)}
                    >
                        {intl.formatMessage(PortalResources.refresh)}
                    </Button>
                    <Button
                        icon={<Delete16Regular />}
                        onClick={() => setShowDeleteConfirmDialog(true)}
                        disabled={selectedAgents.length === 0 || isLoading}
                    >
                        {intl.formatMessage(PortalResources.delete)}
                    </Button>
                </div>

                <div className={styles.controlsRow}>
                    <div className={styles.searchControls}>
                        <SearchBox
                            placeholder={intl.formatMessage(PortalResources.search)}
                            className={styles.searchBox}
                            value={searchQuery}
                            onChange={(_, data) => setSearchQuery(data.value)}
                            disabled={isLoading}
                        />
                        <SubscriptionPill
                            selectedSubscriptionIds={selectedSubscriptionIds ?? []}
                            onSelectedSubscriptionIdsChange={setSelectedSubscriptionIds}
                            disabled={isLoading}
                        />
                        <ResourceGroupPill
                            selectedSubscriptionIds={selectedSubscriptionIds ?? []}
                            selectedResourceGroupNames={selectedResourceGroupNames}
                            onSelectedResourceGroupNamesChange={setSelectedResourceGroupNames}
                            disabled={isLoading}
                        />
                    </div>
                </div>
            </div>

            <Card className={styles.card}>
                {!isLoading && agents.length === 0 ? (
                    <CreateFirstAgent onClickCreate={() => setIsCreateDialogOpen(true)} />
                ) : isLoading ? (
                    <div className={styles.dataGrid}>
                        <AgentListSkeleton rowCount={6} />
                    </div>
                ) : (
                    <DataGrid
                        items={rows}
                        columns={columns}
                        sortable
                        getRowId={item => (item as any).item.id}
                        className={styles.dataGrid}
                        selectionMode="multiselect"
                        selectedItems={selectedAgentIds}
                        onSelectionChange={onSelectionChange}
                    >
                        <DataGridHeader>
                            <DataGridRow
                                selectionCell={{
                                    checkboxIndicator: { 'aria-label': intl.formatMessage(PortalResources.selectAllAgents) },
                                }}
                            >
                                {({ renderHeaderCell, columnId }) => (
                                    <DataGridHeaderCell {...headerSortProps(columnId)}>{renderHeaderCell()}</DataGridHeaderCell>
                                )}
                            </DataGridRow>
                        </DataGridHeader>
                        <DataGridBody<SreAgentArgItem>>
                            {({ item, rowId }) => (
                                <DataGridRow<SreAgentArgItem>
                                    key={rowId}
                                    selectionCell={{
                                        checkboxIndicator: {
                                            'aria-label': intl.formatMessage(PortalResources.selectAgent, {
                                                name: (item as any).item.name,
                                            }),
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

            {/* Fully dismount create components on close to fully reset state */}
            {isCreateDialogOpen && <CreateAgentDialog isDialogOpen={isCreateDialogOpen} setIsDialogOpen={setIsCreateDialogOpen} />}

            <DeleteAgentDialog
                open={showDeleteConfirmDialog}
                selectedAgents={selectedAgents}
                onClose={() => setShowDeleteConfirmDialog(false)}
                onConfirm={handleDeleteConfirm}
            />
        </>
    );
};

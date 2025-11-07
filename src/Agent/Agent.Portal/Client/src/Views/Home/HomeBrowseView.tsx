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
    Subtitle1,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    useTableFeatures,
    useTableSort,
} from '@fluentui/react-components';
import { Add16Regular } from '@fluentui/react-icons';
import { useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { SreAgentClient } from '../../Common/Clients/SreAgentClient';
import { ResourceGroupPill } from '../../Common/Components/ResourceGroupPill/ResourceGroupPill';
import { SubscriptionPill } from '../../Common/Components/SubscriptionPill/SubscriptionPill';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useSubscriptions } from '../../Common/Contexts/SubscriptionsContext';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { LogLevel } from '../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { PortalResources } from '../../Strings/Resources';
import { AgentListSkeleton } from './AgentListSkeleton';
import { CreateAgentDialog } from './Create/CreateAgentDialog';
import { CreateFirstAgent } from './CreateFirstAgent';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        minHeight: '600px',
        flex: 'auto',
        flexDirection: 'column',
        gap: '40px',
        alignItems: 'center',
        padding: '32px',
    },
    title: {
        margin: 0,
    },
    errorContainer: {
        maxWidth: '1000px',
    },
    agentListContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        width: '100%',
        maxWidth: '1200px',
    },
    controlsRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    searchControls: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    searchBox: {
        width: '250px',
    },
    card: {
        padding: '20px',
    },
    dataGrid: {
        height: '585px',
        overflowY: 'auto',
    },
});

export const HomeBrowseView = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(TelemetrySource.HomeBrowseView, undefined);
    const navigate = useNavigate();
    const { selectedSubscriptions } = useSubscriptions();

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.HomeBrowseView), []);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [agents, setAgents] = useState<SreAgentArgItem[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);

    // Initialize selected subscriptions from context, default to empty array (meaning "All")
    const [selectedSubscriptionIds, setSelectedSubscriptionIds] = useState<string[]>(selectedSubscriptions.map(sub => sub.subscriptionId));
    const [selectedResourceGroupNames, setSelectedResourceGroupNames] = useState<string[]>([]);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = [
        createTableColumn<SreAgentArgItem>({
            columnId: 'name',
            compare: (a, b) => a.name.localeCompare(b.name),
            renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
            renderCell: item => (
                <TableCellLayout
                    media={<Image src="SreAgent.svg" width={16} height={16} alt={intl.formatMessage(PortalResources.azureSreAgent)} />}
                >
                    <Link onClick={() => navigate(`/agents/${encodeURIComponent(item.id)}`)}>{item.name}</Link>
                </TableCellLayout>
            ),
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'subscription',
            compare: (a, b) => a.subscriptionId.localeCompare(b.subscriptionId),
            renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.subscription)}</Text>,
            // TODO: Subscription displayName
            renderCell: item => <TableCellLayout>{item.subscriptionId}</TableCellLayout>,
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'resourceGroup',
            compare: (a, b) => a.resourceGroup.localeCompare(b.resourceGroup),
            renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.resourceGroup)}</Text>,
            renderCell: item => <TableCellLayout>{item.resourceGroup}</TableCellLayout>,
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'region',
            compare: (a, b) => a.location.localeCompare(b.location),
            renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.region)}</Text>,
            renderCell: item => <TableCellLayout>{item.location}</TableCellLayout>,
        }),
    ];

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

    useEffect(() => {
        const fetchAgents = async () => {
            if (!isAuthenticated) {
                setIsLoading(false);
                return;
            }

            setIsLoading(true);
            setError(null);

            // Pass filters to ARG query. Empty arrays mean "All"
            const subIds = selectedSubscriptionIds.length > 0 ? selectedSubscriptionIds : undefined;
            const rgNames = selectedResourceGroupNames.length > 0 ? selectedResourceGroupNames : undefined;

            const response = await sreAgentClient.getAgentsFromArg(subIds, rgNames);

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
        };

        fetchAgents();
    }, [sreAgentClient, isAuthenticated, logEvent, selectedSubscriptionIds, selectedResourceGroupNames]);

    return (
        <div className={styles.container}>
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

            <div className={styles.agentListContainer}>
                <Subtitle1 as="h1" block className={styles.title}>
                    {intl.formatMessage(PortalResources.agents)}
                </Subtitle1>

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

                    <div>
                        <Button
                            icon={<Add16Regular />}
                            appearance="primary"
                            onClick={() => setIsCreateDialogOpen(true)}
                            disabled={isLoading}
                        >
                            {intl.formatMessage(PortalResources.createAgent)}
                        </Button>
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
                        <DataGrid items={rows} columns={columns} sortable getRowId={item => item.rowId} className={styles.dataGrid}>
                            <DataGridHeader>
                                <DataGridRow>
                                    {({ renderHeaderCell, columnId }) => (
                                        <DataGridHeaderCell {...headerSortProps(columnId)}>{renderHeaderCell()}</DataGridHeaderCell>
                                    )}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<SreAgentArgItem>>
                                {({ item, rowId }) => (
                                    <DataGridRow<SreAgentArgItem> key={rowId}>
                                        {({ renderCell }) => <DataGridCell>{renderCell((item as any).item)}</DataGridCell>}
                                    </DataGridRow>
                                )}
                            </DataGridBody>
                        </DataGrid>
                    )}
                </Card>
            </div>

            {/* Fully dismount create components on close to fully reset state */}
            {isCreateDialogOpen && <CreateAgentDialog isDialogOpen={isCreateDialogOpen} setIsDialogOpen={setIsCreateDialogOpen} />}
        </div>
    );
};

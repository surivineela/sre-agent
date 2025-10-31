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
    Dropdown,
    Link,
    makeStyles,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    SearchBox,
    Spinner,
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
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { LogLevel } from '../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { PortalResources } from '../../Strings/Resources';
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

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.HomeBrowseView), []);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [agents, setAgents] = useState<SreAgentArgItem[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState(false);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = [
        createTableColumn<SreAgentArgItem>({
            columnId: 'name',
            compare: (a, b) => a.name.localeCompare(b.name),
            renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(PortalResources.name)}</Text>,
            renderCell: item => (
                <TableCellLayout>
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

            const response = await sreAgentClient.getAgentsFromArg();

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
    }, [sreAgentClient, isAuthenticated, logEvent]);

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

            {isLoading ? (
                <div>
                    <Spinner size="extra-large" />
                </div>
            ) : agents.length === 0 ? (
                <CreateFirstAgent onClickCreate={() => setIsCreateDialogOpen(true)} />
            ) : (
                <div className={styles.agentListContainer}>
                    <Subtitle1 block>{intl.formatMessage(PortalResources.agents)}</Subtitle1>

                    <div className={styles.controlsRow}>
                        <div className={styles.searchControls}>
                            <SearchBox
                                placeholder={intl.formatMessage(PortalResources.search)}
                                className={styles.searchBox}
                                value={searchQuery}
                                onChange={(_, data) => setSearchQuery(data.value)}
                            />
                            <Dropdown placeholder={intl.formatMessage(PortalResources.allSubscriptions)} />
                            <Dropdown placeholder={intl.formatMessage(PortalResources.allResourceGroups)} />
                        </div>

                        <div>
                            <Button icon={<Add16Regular />} appearance="primary" onClick={() => setIsCreateDialogOpen(true)}>
                                {intl.formatMessage(PortalResources.createAgent)}
                            </Button>
                        </div>
                    </div>

                    <Card className={styles.card}>
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
                    </Card>
                </div>
            )}

            <CreateAgentDialog isDialogOpen={isCreateDialogOpen} setIsDialogOpen={setIsCreateDialogOpen} />
        </div>
    );
};

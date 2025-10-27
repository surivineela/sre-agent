import { useMsal } from '@azure/msal-react';
import {
    Button,
    Card,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dropdown,
    Link,
    SearchBox,
    Spinner,
    TableCellLayout,
    TableColumnDefinition,
    Text,
    createTableColumn,
} from '@fluentui/react-components';
import { Add16Regular } from '@fluentui/react-icons';
import { useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentClient } from '../../Common/Clients/SreAgentClient';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { SreAgentArgItem } from '../../Common/Contracts/SreAgent';
import { LogLevel } from '../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { PortalResources } from '../../Strings/Resources';
import { CreateFirstAgent } from './CreateFirstAgent';

export const HomeBrowseView = () => {
    const intl = useIntl();
    const { instance } = useMsal();
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(TelemetrySource.HomeBrowseView, undefined);

    const sreAgentClient = useMemo(() => SreAgentClient.getInstance(instance, TelemetrySource.HomeBrowseView), [instance]);

    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [agents, setAgents] = useState<SreAgentArgItem[]>([]);

    const columns: TableColumnDefinition<SreAgentArgItem>[] = [
        createTableColumn<SreAgentArgItem>({
            columnId: 'name',
            renderHeaderCell: () => intl.formatMessage(PortalResources.name),
            renderCell: item => (
                <TableCellLayout>
                    <Link href={`/agents${item.id}`}>{item.name}</Link>
                </TableCellLayout>
            ),
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'subscription',
            renderHeaderCell: () => intl.formatMessage(PortalResources.subscription),
            renderCell: item => <TableCellLayout>{item.subscriptionId}</TableCellLayout>,
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'resourceGroup',
            renderHeaderCell: () => intl.formatMessage(PortalResources.resourceGroup),
            renderCell: item => <TableCellLayout>{item.resourceGroup}</TableCellLayout>,
        }),
        createTableColumn<SreAgentArgItem>({
            columnId: 'region',
            renderHeaderCell: () => intl.formatMessage(PortalResources.region),
            renderCell: item => <TableCellLayout>{item.location}</TableCellLayout>,
        }),
    ];

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
                const errorMessage =
                    response.error instanceof Error ? response.error.message : 'Unknown error occurred';
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
        <div>
            <Text block>{intl.formatMessage(PortalResources.agents)}</Text>

            {isLoading && (
                <div style={{ padding: '20px' }}>
                    <Spinner />
                </div>
            )}

            {error && <div style={{ padding: '20px', color: 'red' }}>Error: {error}</div>}

            {agents.length === 0 ? (
                <CreateFirstAgent />
            ) : (
                <>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
                        <div>
                            <SearchBox placeholder="Search agents..." style={{ marginBottom: '10px', width: '300px' }} />
                            <Dropdown placeholder="All subscriptions" />
                            <Dropdown placeholder="All resource groups" />
                            <Dropdown placeholder="All regions" />
                        </div>

                        <div>
                            <Button icon={<Add16Regular />} appearance="primary">
                                {intl.formatMessage(PortalResources.createAgent)}
                            </Button>
                        </div>
                    </div>

                    <Card>
                        <DataGrid
                            items={agents}
                            columns={columns}
                            sortable
                            getRowId={item => item.id}
                            style={{ height: '585px', overflowY: 'auto' }}
                        >
                            <DataGridHeader>
                                <DataGridRow>
                                    {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                                </DataGridRow>
                            </DataGridHeader>
                            <DataGridBody<SreAgentArgItem>>
                                {({ item, rowId }) => (
                                    <DataGridRow<SreAgentArgItem> key={rowId}>
                                        {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                    </DataGridRow>
                                )}
                            </DataGridBody>
                        </DataGrid>
                    </Card>
                </>
            )}
        </div>
    );
};

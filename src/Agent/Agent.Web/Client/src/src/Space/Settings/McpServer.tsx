import {
    Body1,
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Image,
    SearchBox,
    TableColumnDefinition,
    Title1,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { AddRegular, ArrowCounterclockwiseRegular, DeleteRegular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { PillFilterSet } from '../../Common/Components/PillFilter/PillFilterSet';
import { TextWithLink } from '../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { McpServerResources } from '../../Strings/SREAgentResources';
import { useSreAgent } from './Hooks/useSreAgent';
import { useMcpServerStyles } from './McpServer.styles';

enum McpServersColumnKey {
    name = 'name',
    serviceType = 'serviceType',
    status = 'status',
}

export interface ApiKeyAuth {
    type: 'ApiKey';
    apiKey: string;
    apiKeyHeader?: string; // Default: "X-API-Key"
    apiKeyPrefix?: string | null; // Optional prefix (e.g., "Bearer")
}

export interface BearerAuth {
    type: 'Bearer';
    bearerToken: string;
}

export interface CustomHeadersAuth {
    type: 'CustomHeaders';
    customHeaders: Record<string, string>;
}

export interface NoAuth {
    type: 'None';
}

export type Authentication = ApiKeyAuth | BearerAuth | CustomHeadersAuth | NoAuth;

interface McpServer {
    name: string;
    type: string;
    endpoint: string;
    authentication: Authentication;
    status: string;
}

const McpServer: FC = () => {
    const intl = useIntl();
    const styles = useMcpServerStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const { /*agent, agentLoading, */ refresh: refreshAgent } = useSreAgent(resourceId);

    // TODO (evtheodo): Get MCP servers when backend supports it
    // const { mcpServers, mcpServersLoading, initialize: initializeMcpServers, refresh: refreshMcpServers } = useMcpServers(resourceId);

    const [_, setIsDialogOpen] = useState(false);
    const [isOperationInProgress, __] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);
    const [searchQuery, setSearchQuery] = useState('');
    // const [selectedServiceType, setSelectedServiceType] = useState<string>('All');

    // Mock data - replace with actual data from backend
    const mcpServers: any[] = [];
    const mcpServersLoading = false;

    // const agentEndpoint = useMemo(() => {
    //     return agent?.properties?.agentEndpoint || '';
    // }, [agent?.properties?.agentEndpoint]);

    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        await refreshAgent();
        // await refreshMcpServers(agentEndpoint);
        setIsRefreshing(false);
    }, [refreshAgent]);

    const handleConnectMcpServer = useCallback(() => {
        setIsDialogOpen(true);
    }, []);

    const handleDisconnect = useCallback(() => {
        // TODO(evtheodo): Implement disconnect logic
    }, []);

    const columns: TableColumnDefinition<McpServer>[] = [
        createTableColumn<McpServer>({
            columnId: McpServersColumnKey.name,
            compare: (a, b) => {
                return a.name.localeCompare(b.name);
            },
            renderHeaderCell: () => {
                return <span className={styles.dataGridHeader}>{intl.formatMessage(McpServerResources.name)}</span>;
            },
            renderCell: item => {
                return item.name;
            },
        }),
        createTableColumn<McpServer>({
            columnId: McpServersColumnKey.serviceType,
            compare: (a, b) => {
                return a.type.localeCompare(b.type);
            },
            renderHeaderCell: () => {
                return <span className={styles.dataGridHeader}>{intl.formatMessage(McpServerResources.serviceType)}</span>;
            },
            renderCell: item => {
                return item.type;
            },
        }),
        createTableColumn<McpServer>({
            columnId: McpServersColumnKey.status,
            compare: (a, b) => {
                return a.status.localeCompare(b.status);
            },
            renderHeaderCell: () => {
                return <span className={styles.dataGridHeader}>{intl.formatMessage(McpServerResources.status)}</span>;
            },
            renderCell: item => {
                return item.status;
            },
        }),
    ];

    return (
        <>
            <div className={styles.headerContainer}>
                <h3 className={styles.headerTitle}>{intl.formatMessage(McpServerResources.title)}</h3>
                <TextWithLink
                    text={intl.formatMessage(McpServerResources.description)}
                    linkUrl={SreAgentFwLinks.sreAgentMcpServers}
                    linkText={intl.formatMessage(McpServerResources.learnMore)}
                    textClassName={styles.headerDescription}
                    linkClassName={styles.headerDescription}
                />
            </div>
            <Toolbar className={styles.toolbar}>
                <ToolbarButton
                    aria-label={intl.formatMessage(McpServerResources.connectServer)}
                    icon={<AddRegular />}
                    disabled={isOperationInProgress || isRefreshing}
                    onClick={handleConnectMcpServer}
                    className={styles.toolbarButton}
                >
                    {intl.formatMessage(McpServerResources.connectServer)}
                </ToolbarButton>
                <ToolbarButton
                    aria-label={intl.formatMessage(McpServerResources.refresh)}
                    icon={<ArrowCounterclockwiseRegular />}
                    disabled={isRefreshing}
                    onClick={handleRefresh}
                    className={styles.toolbarButton}
                >
                    {intl.formatMessage(McpServerResources.refresh)}
                </ToolbarButton>
                <ToolbarButton
                    aria-label={intl.formatMessage(McpServerResources.disconnect)}
                    icon={<DeleteRegular />}
                    disabled={isOperationInProgress || isRefreshing || mcpServers.length === 0}
                    onClick={handleDisconnect}
                    className={styles.toolbarButton}
                >
                    {intl.formatMessage(McpServerResources.disconnect)}
                </ToolbarButton>
                <ToolbarDivider />
                <SearchBox
                    placeholder={intl.formatMessage(McpServerResources.searchPlaceholder)}
                    value={searchQuery}
                    onChange={(_, data) => setSearchQuery(data?.value || '')}
                />
                <PillFilterSet
                    staticFilters={[]} /* TODO(evtheodo): what is the list of possible service types? */
                    disabled={isOperationInProgress || isRefreshing}
                />
            </Toolbar>
            <DataGrid
                items={mcpServers}
                columns={columns}
                sortable
                selectionMode="multiselect"
                focusMode="composite"
                aria-label={intl.formatMessage(McpServerResources.mcpServersTableAriaLabel)}
            >
                <DataGridHeader>
                    <DataGridRow
                        selectionCell={{
                            checkboxIndicator: { 'aria-label': 'Select all rows' },
                        }}
                    >
                        {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                    </DataGridRow>
                </DataGridHeader>
                <DataGridBody<McpServer>>
                    {({ item: mcpServer, rowId }) => (
                        <DataGridRow<McpServer>
                            key={rowId}
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': 'Select row' },
                            }}
                        >
                            {({ renderCell }) => <DataGridCell>{renderCell(mcpServer)}</DataGridCell>}
                        </DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>
            {!mcpServersLoading && mcpServers.length === 0 && (
                <section className={styles.emptyStateContainer}>
                    <Image src="./McpServerAdd.svg" alt="Mcp Server Add" />
                    <div className={styles.emptyStateTextContainer}>
                        <Title1 block className={styles.headerTitle}>
                            {intl.formatMessage(McpServerResources.emptyStateTitle)}
                        </Title1>
                        <Body1 block>{intl.formatMessage(McpServerResources.emptyStateDescription)}</Body1>
                    </div>
                    <Button appearance="primary" disabled={isOperationInProgress || isRefreshing}>
                        {intl.formatMessage(McpServerResources.title)}
                    </Button>
                </section>
            )}
            {/* TODO: Add CreateMcpServerDialog component */}
            {/* <CreateMcpServerDialog
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isOperationInProgress={isOperationInProgress}
            /> */}
        </>
    );
};

export default McpServer;

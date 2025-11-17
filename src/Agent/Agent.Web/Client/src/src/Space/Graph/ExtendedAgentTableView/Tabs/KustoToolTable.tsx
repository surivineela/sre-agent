import {
    Button,
    InputOnChangeData,
    SearchBox,
    TableCell,
    TableHeaderCell,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { ArrowClockwise20Regular, CheckmarkCircle20Regular, Delete16Regular, ErrorCircle20Regular } from '@fluentui/react-icons';
import { SearchBoxChangeEvent } from '@fluentui/react-search';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../../../Common/Clients/ArmClient';
import { getDataPlaneErrorMessage } from '../../../../Common/Clients/DataPlaneClient';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { PillFilter } from '../../../../Common/Components/PillFilter/PillFilter';
import {
    ExtendedAgentsGraphResources,
    GenericErrorResources,
    ScheduledTasksResources,
    SreAgentResources,
} from '../../../../Strings/SREAgentResources';
import { ExtendedConnector, ExtendedTool, ExtendedTrigger } from '../../../Contracts/ExtendedAgentGraph';
import { EntityDeleteConfirmDialog } from '../Common/EntityDeleteConfirmDialog';
import { EntityTable } from '../Common/EntityTable';
import {
    ALL_FILTER_KEY,
    BaseTableItem,
    CONNECTOR_STATUS,
    EntityTableProps,
    EntityToolbarProps,
    KustoToolItem,
} from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface KustoToolTableProps extends EntityTableProps {
    kustoTools: ExtendedTool[];
    connectors: ExtendedConnector[];
}

export const KustoToolTable: FC<KustoToolTableProps> = ({ kustoTools, connectors, openInfoPanel, refresh, lastUpdated, isLoading }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [connectorFilter, setConnectorFilter] = useState<string>(ALL_FILTER_KEY);
    const [connectorStatusFilter, setConnectorStatusFilter] = useState<string>(ALL_FILTER_KEY);
    const [selectedTools, setSelectedTools] = useState<ExtendedTool[]>([]);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);
    const connectorMap = useMemo(() => new Map(connectors.map(connector => [connector.name, connector])), [connectors]);

    const kustoToolItems = useMemo(() => {
        const query = searchText?.trim().toLowerCase();
        let filteredTools = kustoTools;

        if (query) {
            filteredTools = filteredTools.filter(tool => tool.name?.toLowerCase().includes(query));
        }

        if (connectorFilter !== ALL_FILTER_KEY) {
            filteredTools = filteredTools.filter(tool => tool.connector?.toLowerCase().includes(connectorFilter.toLowerCase()));
        }

        if (connectorStatusFilter !== ALL_FILTER_KEY) {
            filteredTools = filteredTools.filter(tool => {
                const connector = tool.connector ? connectorMap.get(tool.connector) : undefined;
                const connectorStatus = connector?.enabled !== false ? CONNECTOR_STATUS.CONNECTED : CONNECTOR_STATUS.NOT_CONNECTED;
                return connectorStatus === connectorStatusFilter;
            });
        }

        return filteredTools.map(tool => {
            const parameterCount = tool.parameters?.length || 0;
            const parametersText = parameterCount > 0 ? `${parameterCount}` : EMPTY_DISPLAY;
            const connector = tool.connector ? connectorMap.get(tool.connector) : undefined;
            const connectorStatus = connector?.enabled !== false ? CONNECTOR_STATUS.CONNECTED : CONNECTOR_STATUS.NOT_CONNECTED;

            return {
                name: tool.name || EMPTY_DISPLAY,
                connector: tool.connector || EMPTY_DISPLAY,
                database: tool.database || EMPTY_DISPLAY,
                parameters: parametersText,
                connectorStatus,
                data: tool,
            };
        });
    }, [searchText, kustoTools, connectorFilter, connectorStatusFilter, EMPTY_DISPLAY, connectorMap]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoToolName)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.connector)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoDatabaseLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.connectorStatus)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const toolItem = item as KustoToolItem;
            return (
                <>
                    <TableCell tabIndex={0} role="gridcell">
                        <Button
                            appearance="transparent"
                            onClick={() => {
                                openInfoPanel?.(toolItem);
                            }}
                            className={styles.transparentButton}
                        >
                            <Text className={styles.clickableText}>{toolItem.name}</Text>
                        </Button>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.connector}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.database}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <div className={styles.flexRowMedium}>
                            {toolItem.connectorStatus === CONNECTOR_STATUS.CONNECTED ? (
                                <>
                                    <CheckmarkCircle20Regular className={styles.greenIcon} />
                                    <Text>{intl.formatMessage(ExtendedAgentsGraphResources.connectedStatus)}</Text>
                                </>
                            ) : (
                                <>
                                    <ErrorCircle20Regular className={styles.redIcon} />
                                    <Text>{intl.formatMessage(ExtendedAgentsGraphResources.disconnectedStatus)}</Text>
                                </>
                            )}
                        </div>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{toolItem.parameters}</Text>
                    </TableCell>
                </>
            );
        },
        [styles, intl, openInfoPanel]
    );

    return (
        <div className={styles.entityTable}>
            <KustoToolTableToolbar
                searchText={searchText}
                setSearchText={setSearchText}
                connectorFilter={connectorFilter}
                setConnectorFilter={setConnectorFilter}
                connectorStatusFilter={connectorStatusFilter}
                setConnectorStatusFilter={setConnectorStatusFilter}
                tools={kustoTools}
                selectedTools={selectedTools}
                refresh={refresh}
                lastUpdated={lastUpdated}
            />
            <EntityTable
                activeTab="scheduledTasks"
                searchText={searchText}
                items={kustoToolItems}
                setSelectedItems={(items: BaseTableItem[]) => setSelectedTools(items as ExtendedTrigger[])}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};

interface KustoToolTableToolbarProps extends EntityToolbarProps {
    tools: ExtendedTool[];
    selectedTools: ExtendedTool[];
    connectorFilter: string;
    setConnectorFilter: (filter: string) => void;
    connectorStatusFilter: string;
    setConnectorStatusFilter: (statusFilter: string) => void;
}

const KustoToolTableToolbar = memo<KustoToolTableToolbarProps>(
    ({
        tools = [],
        selectedTools = [],
        searchText,
        setSearchText,
        connectorFilter,
        setConnectorFilter,
        connectorStatusFilter,
        setConnectorStatusFilter,
        refresh,
        lastUpdated,
    }) => {
        const intl = useIntl();
        const styles = useListViewStyles();
        const { sreAgentEndpoint } = useContext(EnvironmentContext);
        const azPortalContext = useContext(AzPortalContext);
        const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);
        const agentClient = useMemo(() => ExtendedAgentClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

        const connectorFilterOptions = useMemo(() => {
            const uniqueConnectorNames = Array.from(new Set(tools.map(tool => tool.connector)));

            return [
                {
                    key: ALL_FILTER_KEY,
                    label: intl.formatMessage(SreAgentResources.all),
                },
                ...uniqueConnectorNames.map(connector => ({
                    key: connector ?? '',
                    label: connector ?? '',
                })),
            ];
        }, [tools, intl]);

        const connectorStatusFilterOptions = useMemo(
            () => [
                { key: ALL_FILTER_KEY, label: intl.formatMessage(SreAgentResources.all) },
                { key: CONNECTOR_STATUS.CONNECTED, label: intl.formatMessage(ExtendedAgentsGraphResources.connectedStatus) },
                { key: CONNECTOR_STATUS.NOT_CONNECTED, label: intl.formatMessage(ExtendedAgentsGraphResources.disconnectedStatus) },
            ],
            [intl]
        );

        const isDeleteDisabled = useMemo(() => selectedTools.length === 0 || isDeleting, [isDeleting, selectedTools.length]);

        const handleDelete = useCallback(async () => {
            setIsDeleting(true);
            setShowDeleteConfirmationDialog(false);
            const toolNames = selectedTools.map(tool => tool.name);
            const toolCount = selectedTools.length;

            azPortalContext.log({
                action: 'delete-tools',
                actionModifier: 'start',
                logLevel: 'info',
                data: { toolNames },
            });

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(SreAgentResources.deleteKustoToolNotificationTitle, { count: toolCount }),
                intl.formatMessage(SreAgentResources.deleteKustoToolNotificationDescription, {
                    count: toolCount,
                    name: toolCount === 1 ? toolNames[0] : undefined,
                })
            );

            try {
                const responses = await Promise.all(
                    selectedTools.map(async toolItem => {
                        const response = await agentClient.deleteKustoTool(toolItem.name);
                        return { toolName: toolItem.name, response };
                    })
                );

                const failures = responses.filter(({ response }) => !response.isSuccessful);

                if (failures.length === 0) {
                    azPortalContext.log({
                        action: 'delete-tools',
                        actionModifier: 'success',
                        logLevel: 'info',
                        data: { toolNames },
                    });

                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(SreAgentResources.deleteKustoToolNotificationSuccess, {
                            count: toolCount,
                            name: toolCount === 1 ? toolNames[0] : undefined,
                        })
                    );

                    refresh();
                } else {
                    const failedTools = failures.map(f => f.toolName);
                    const failedCount = failedTools.length;
                    const errorMessages = failures
                        .map(f => getDataPlaneErrorMessage(f.response.error) || getErrorMessageOrStringify(f.response.error))
                        .join('; ');

                    azPortalContext.log({
                        action: 'delete-tools',
                        actionModifier: 'failure',
                        logLevel: 'error',
                        data: {
                            failedAgents: failedTools,
                            error: errorMessages,
                        },
                    });

                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(SreAgentResources.deleteKustoToolNotificationError, {
                            count: failedCount,
                            name: failedCount === 1 ? failedTools[0] : undefined,
                            errorMessage: errorMessages || undefined,
                        })
                    );
                    if (failures.length < selectedTools.length) {
                        refresh();
                    }
                }
            } catch (error) {
                const errorMessage = error instanceof Error ? error.message : intl.formatMessage(GenericErrorResources.unexpectedError);

                azPortalContext.log({
                    action: 'delete-tools',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    data: {
                        agentNames: toolNames,
                        error: errorMessage,
                    },
                });

                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(SreAgentResources.deleteKustoToolNotificationError, {
                        count: toolCount,
                        name: toolCount === 1 ? toolNames[0] : undefined,
                        errorMessage: errorMessage || undefined,
                    })
                );
            } finally {
                setIsDeleting(false);
            }
        }, [agentClient, azPortalContext, intl, refresh, selectedTools]);

        return (
            <div className={styles.toolbar}>
                <div className={styles.searchAndToolbar}>
                    <Toolbar className={styles.toolbarButtons}>
                        <ToolbarButton
                            appearance="subtle"
                            className={styles.toolbarButton}
                            icon={<Delete16Regular />}
                            onClick={() => setShowDeleteConfirmationDialog(true)}
                            disabled={isDeleteDisabled}
                        >
                            {intl.formatMessage(SreAgentResources.delete)}
                        </ToolbarButton>
                        <ToolbarDivider />
                    </Toolbar>
                    <div className={styles.searchBoxAndFilters}>
                        <SearchBox
                            className={styles.searchBox}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchByTool)}
                            value={searchText}
                            onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? '')}
                        />
                        <PillFilter
                            label={`${intl.formatMessage(ExtendedAgentsGraphResources.connector)}`}
                            filterType="combobox"
                            options={connectorFilterOptions}
                            selectedKeys={[connectorFilter]}
                            onApply={keys => {
                                setConnectorFilter(keys[0]);
                            }}
                        />
                        <PillFilter
                            label={`${intl.formatMessage(ExtendedAgentsGraphResources.connectorStatus)}`}
                            filterType="combobox"
                            options={connectorStatusFilterOptions}
                            selectedKeys={[connectorStatusFilter]}
                            onApply={keys => {
                                setConnectorStatusFilter(keys[0]);
                            }}
                        />
                    </div>
                    <EntityDeleteConfirmDialog
                        showDialog={showDeleteConfirmationDialog}
                        setShowDialog={setShowDeleteConfirmationDialog}
                        handleDelete={handleDelete}
                        numItems={selectedTools.length}
                    />
                </div>
                {lastUpdated && (
                    <div className={styles.lastUpdated}>
                        <ArrowClockwise20Regular />
                        <Text>{`${intl.formatMessage(ScheduledTasksResources.lastUpdated)}: ${lastUpdated}`}</Text>
                    </div>
                )}
            </div>
        );
    }
);

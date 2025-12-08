import {
    Button,
    InputOnChangeData,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    SearchBox,
    SearchBoxChangeEvent,
    TableCell,
    TableHeaderCell,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import {
    ArrowClockwise20Regular,
    Delete16Regular,
    Edit20Regular,
    MoreHorizontal16Regular,
    Whiteboard16Regular,
} from '@fluentui/react-icons';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../../../Common/Clients/ArmClient';
import { getDataPlaneErrorMessage } from '../../../../Common/Clients/DataPlaneClient';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import {
    ExtendedAgentsGraphResources,
    GenericErrorResources,
    ScheduledTasksResources,
    SreAgentResources,
} from '../../../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentGraphView,
    ExtendedAgentNodeType,
    ExtendedTool,
    ExtendedTrigger,
    SystemTool,
} from '../../../Contracts/ExtendedAgentGraph';
import { EntityIcon } from '../../EntityIcon';
import { EntityDeleteConfirmDialog } from '../Common/EntityDeleteConfirmDialog';
import { EntityTable } from '../Common/EntityTable';
import { AgentItem, BaseTableItem, EntityTableProps, EntityToolbarProps } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface AgentTableProps extends EntityTableProps {
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    triggers: ExtendedTrigger[];
    systemTools?: SystemTool[];
}

export const AgentTable: FC<AgentTableProps> = ({
    agents,
    triggers,
    tools,
    systemTools,
    openInfoPanel,
    refresh,
    lastUpdated,
    isLoading,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const extendedAgentGraphContext = useContext(ExtendedAgentGraphContext);
    const [searchText, setSearchText] = useState<string>();
    const [selectedAgents, setSelectedAgents] = useState<ExtendedAgent[]>([]);

    const toolMap = useMemo(() => new Map(tools.map(tool => [tool.name, tool])), [tools]);
    const systemToolMap = useMemo(() => new Map((systemTools || []).map(tool => [tool.name, tool])), [systemTools]);

    const filteredAgents = useMemo(() => {
        const query = searchText?.trim().toLowerCase();
        if (!query) return agents;
        return agents.filter(agent => agent.name?.toLowerCase().includes(query));
    }, [agents, searchText]);

    const getAgentTriggers = useCallback(
        (agentName: string, triggers: ExtendedTrigger[]) => triggers.filter(trigger => trigger.agentName === agentName),
        []
    );

    const getAgentIncidentTriggers = useCallback(
        (agentTriggers: ExtendedTrigger[]) => agentTriggers.filter(trigger => trigger.type === 'incident'),
        []
    );

    const getAgentScheduledTaskTriggers = useCallback(
        (agentTriggers: ExtendedTrigger[]) => agentTriggers.filter(trigger => trigger.type === 'scheduled'),
        []
    );

    const agentItems = useMemo<AgentItem[]>(() => {
        return filteredAgents.map(agent => {
            const agentTriggers = getAgentTriggers(agent.name, triggers);
            let triggerDisplay = '';

            if (agentTriggers.length > 0) {
                triggerDisplay = agentTriggers.length.toString();
            }

            let systemToolsCount = 0;
            let kustoToolsCount = 0;

            if (agent.tools) {
                agent.tools.forEach(toolName => {
                    const tool = toolMap.get(toolName);
                    const systemTool = systemToolMap.has(toolName);

                    if (systemTool) {
                        systemToolsCount++;
                    } else if (tool?.type === 'KustoTool') {
                        kustoToolsCount++;
                    }
                });
            }

            const explicitSystemTools = agent.systemTools?.filter(toolName => !agent.tools?.includes(toolName)) || [];
            systemToolsCount += explicitSystemTools.length;

            const totalToolsCount = systemToolsCount + kustoToolsCount;
            const toolsDisplay = totalToolsCount > 0 ? totalToolsCount.toString() : '0';

            return {
                name: agent.name,
                trigger: triggerDisplay,
                tools: toolsDisplay,
                systemToolsCount,
                kustoToolsCount,
                handoff:
                    agent.handoffs && agent.handoffs.length > 0 ? agent.handoffs.join(', ') : intl.formatMessage(SreAgentResources.none),
                data: agent,
            };
        });
    }, [filteredAgents, getAgentTriggers, triggers, intl, toolMap, systemToolMap]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.subagentNameColumn)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.triggersColumn)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>{intl.formatMessage(ExtendedAgentsGraphResources.tools)}</TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.handoffColumn)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const agentItem = item as AgentItem;
            return (
                <>
                    <TableCell role="gridcell">
                        <div className={styles.tableCellContent}>
                            <Button
                                appearance="transparent"
                                onClick={() => openInfoPanel?.(agentItem.name, ExtendedAgentNodeType.Agent)}
                                className={styles.transparentButton}
                            >
                                <Text className={styles.clickableText}>{agentItem.name}</Text>
                            </Button>
                            <div className={styles.tableCellActionsWrapper}>
                                <MenuButton
                                    appearance="subtle"
                                    size="small"
                                    icon={<Whiteboard16Regular />}
                                    aria-label={intl.formatMessage(ExtendedAgentsGraphResources.openInVisualView)}
                                    onClick={() => {
                                        extendedAgentGraphContext.onEntitySelect({ entityType: 'Agent', entityName: agentItem.name });
                                        extendedAgentGraphContext.onViewChange(ExtendedAgentGraphView.Visual);
                                    }}
                                />
                                <Menu>
                                    <MenuTrigger disableButtonEnhancement>
                                        <MenuButton
                                            appearance="subtle"
                                            size="small"
                                            icon={<MoreHorizontal16Regular />}
                                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.openInVisualView)}
                                        />
                                    </MenuTrigger>
                                    <MenuPopover>
                                        <MenuList>
                                            <MenuItem
                                                icon={<Edit20Regular />}
                                                aria-label={intl.formatMessage(SreAgentResources.edit)}
                                                onClick={() =>
                                                    extendedAgentGraphContext.triggerAgentQuickAction(agentItem.name, 'editAgent')
                                                }
                                            >
                                                {intl.formatMessage(SreAgentResources.edit)}
                                            </MenuItem>
                                        </MenuList>
                                    </MenuPopover>
                                </Menu>
                            </div>
                        </div>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {(() => {
                            const agentTriggers = getAgentTriggers(agentItem.name, triggers);
                            const agentIncidentTriggers = getAgentIncidentTriggers(agentTriggers);
                            const agentScheduledTasks = getAgentScheduledTaskTriggers(agentTriggers);

                            return (
                                <div className={styles.flexRow}>
                                    {agentScheduledTasks.length > 0 && (
                                        <div className={styles.flexRowSmall}>
                                            <EntityIcon
                                                type="scheduledTask"
                                                shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                            />
                                            <Text>{agentScheduledTasks.length}</Text>
                                        </div>
                                    )}
                                    {agentIncidentTriggers.length > 0 && (
                                        <div className={styles.flexRowSmall}>
                                            <EntityIcon
                                                type="incidentTrigger"
                                                shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                            />
                                            <Text>{agentIncidentTriggers.length}</Text>
                                        </div>
                                    )}
                                    {agentIncidentTriggers.length === 0 && agentScheduledTasks.length === 0 && (
                                        <span>{intl.formatMessage(SreAgentResources.none)}</span>
                                    )}
                                </div>
                            );
                        })()}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {(() => {
                            const totalToolsCount = parseInt(agentItem.tools) || 0;
                            const systemToolsCount = agentItem.systemToolsCount || 0;
                            const kustoToolsCount = agentItem.kustoToolsCount || 0;

                            return (
                                <div className={styles.flexRow}>
                                    {systemToolsCount > 0 && (
                                        <div className={styles.flexRowSmall}>
                                            <EntityIcon type="tool" shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }} />
                                            <Text>{systemToolsCount}</Text>
                                        </div>
                                    )}
                                    {kustoToolsCount > 0 && (
                                        <div className={styles.flexRowSmall}>
                                            <EntityIcon
                                                type="toolWithGear"
                                                shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                            />
                                            <Text>{kustoToolsCount}</Text>
                                        </div>
                                    )}
                                    {totalToolsCount === 0 && <span>{intl.formatMessage(SreAgentResources.none)}</span>}
                                </div>
                            );
                        })()}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {(() => {
                            const handoffCount = agentItem.handoff ? agentItem.handoff.split(', ').length : 0;

                            return handoffCount > 0 ? (
                                <div className={styles.flexRowSmall}>
                                    <EntityIcon type="agent" shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }} />
                                    <Text>{handoffCount}</Text>
                                </div>
                            ) : (
                                <span>{intl.formatMessage(SreAgentResources.none)}</span>
                            );
                        })()}
                    </TableCell>
                </>
            );
        },
        [
            styles,
            intl,
            openInfoPanel,
            extendedAgentGraphContext,
            getAgentTriggers,
            triggers,
            getAgentIncidentTriggers,
            getAgentScheduledTaskTriggers,
        ]
    );

    return (
        <div className={styles.entityTable}>
            <AgentTableToolbar
                searchText={searchText}
                setSearchText={setSearchText}
                selectedAgents={selectedAgents}
                refresh={refresh}
                lastUpdated={lastUpdated}
            />
            <EntityTable
                activeTab="agents"
                searchText={searchText}
                items={agentItems}
                setSelectedItems={(items: BaseTableItem[]) => setSelectedAgents(items as ExtendedAgent[])}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};

interface AgentTableToolbarProps extends EntityToolbarProps {
    selectedAgents: ExtendedAgent[];
}

const AgentTableToolbar = memo<AgentTableToolbarProps>(({ selectedAgents = [], searchText, setSearchText, refresh, lastUpdated }) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const agentClient = useMemo(() => ExtendedAgentClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const isDeleteDisabled = useMemo(() => selectedAgents.length === 0 || isDeleting, [isDeleting, selectedAgents.length]);

    const handleDelete = useCallback(async () => {
        setIsDeleting(true);
        setShowDeleteConfirmationDialog(false);
        const agentNames = selectedAgents.map(agent => agent.name);

        azPortalContext.log({
            action: 'delete-agents',
            actionModifier: 'start',
            logLevel: 'info',
            data: { agentNames },
        });

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(SreAgentResources.deleteAgentNotificationTitle, { count: selectedAgents.length }),
            intl.formatMessage(SreAgentResources.deleteAgentNotificationDescription, {
                count: selectedAgents.length,
                name:
                    agentNames.length === 1
                        ? agentNames[0]
                        : `${agentNames.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
            })
        );

        try {
            const responses = await Promise.all(
                selectedAgents.map(async agentItem => {
                    const response = await agentClient.deleteExtendedAgent(agentItem.name);
                    return { agentName: agentItem.name, response };
                })
            );

            const failures = responses.filter(({ response }) => !response.isSuccessful);

            if (failures.length === 0) {
                azPortalContext.log({
                    action: 'delete-agents',
                    actionModifier: 'success',
                    logLevel: 'info',
                    data: { agentNames },
                });

                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SreAgentResources.deleteAgentNotificationSuccess, {
                        count: selectedAgents.length,
                        name:
                            agentNames.length === 1
                                ? agentNames[0]
                                : `${agentNames.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                    })
                );

                refresh();
            } else {
                const failedAgents = failures.map(f => f.agentName);
                const errorMessages = failures
                    .map(f => getDataPlaneErrorMessage(f.response.error) || getErrorMessageOrStringify(f.response.error))
                    .join('; ');

                azPortalContext.log({
                    action: 'delete-agents',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    data: {
                        failedAgents,
                        error: errorMessages,
                    },
                });

                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(SreAgentResources.deleteAgentNotificationError, {
                        count: failedAgents.length,
                        name:
                            failedAgents.length === 1
                                ? failedAgents[0]
                                : `${failedAgents.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                        errorMessage: errorMessages || undefined,
                    })
                );
                if (failures.length < selectedAgents.length) {
                    refresh();
                }
            }
        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : intl.formatMessage(GenericErrorResources.unknownError);
            azPortalContext.log({
                action: 'delete-agents',
                actionModifier: 'failure',
                logLevel: 'error',
                data: {
                    agentNames,
                    error: errorMessage,
                },
            });

            azPortalContext.stopNotification(
                notificationId,
                false,
                `${intl.formatMessage(SreAgentResources.deleteAgentNotificationError, {
                    count: selectedAgents.length,
                    name:
                        agentNames.length === 1
                            ? agentNames[0]
                            : `${agentNames.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                    errorMessage: errorMessage || undefined,
                })}`
            );
        } finally {
            setIsDeleting(false);
        }
    }, [selectedAgents, azPortalContext, intl, agentClient, refresh]);

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
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchBySubagent)}
                        value={searchText}
                        onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? '')}
                    />
                </Toolbar>
                <EntityDeleteConfirmDialog
                    showDialog={showDeleteConfirmationDialog}
                    setShowDialog={setShowDeleteConfirmationDialog}
                    handleDelete={handleDelete}
                    numItems={selectedAgents.length}
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
});

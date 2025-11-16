import {
    Badge,
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    InputOnChangeData,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
    SearchBox,
    SearchBoxChangeEvent,
    Spinner,
    Tab,
    TabList,
    Table,
    TableBody,
    TableCell,
    TableColumnDefinition,
    TableHeader,
    TableHeaderCell,
    TableRow,
    TableSelectionCell,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
    createTableColumn,
    makeStyles,
    mergeClasses,
    tokens,
    useArrowNavigationGroup,
    useTableFeatures,
    useTableSelection,
} from '@fluentui/react-components';
import {
    ArrowClockwise16Regular,
    CheckmarkCircle20Regular,
    Delete16Regular,
    Edit20Regular,
    ErrorCircle20Regular,
    MoreHorizontal16Regular,
    Whiteboard16Regular,
} from '@fluentui/react-icons';
import debounce from 'lodash/debounce';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getDataPlaneErrorMessage } from '../../Common/Clients/DataPlaneClient';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import {
    ComponentResources,
    ExtendedAgentsGraphResources,
    GenericErrorResources,
    ScheduledTasksResources,
    SettingsTabResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentGraphView,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    INFO_PANEL_DEFAULT_WIDTH,
    INFO_PANEL_MAX_WIDTH,
    INFO_PANEL_MIN_WIDTH,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import PlaygroundModal, { PlaygroundTarget } from '../Playground/PlaygroundModal';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { ExtendedAgentInfoPanel } from './ExtendedAgentInfoPanel';
import { parseCronExpression } from './Utility';

const useListViewStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingTop: '16px',
        paddingBottom: '16px',
        paddingLeft: '21px',
    },
    cardsContainer: {
        display: 'flex',
        gap: '20px',
        marginBottom: '24px',
    },
    cardHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '20px',
    },
    cardTitleSection: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    cardTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
    },
    cardCount: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
    },
    searchAndToolbar: {
        display: 'flex',
        flexDirection: 'column',
    },
    tableHeader: {
        fontWeight: '600',
    },
    emptyState: {
        padding: '40px',
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
    },
    errorBar: {
        marginBottom: '8px',
    },
    dangerButton: {
        backgroundColor: tokens.colorPaletteRedBackground3,
        color: tokens.colorNeutralForegroundOnBrand,
        ':hover': {
            backgroundColor: tokens.colorPaletteRedBackground2,
        },
        ':active': {
            backgroundColor: tokens.colorPaletteRedBackground1,
        },
    },
    card: {
        minWidth: '220px',
        padding: '14px',
    },
    tableCellContent: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        width: '100%',
    },
    tableCellActionsWrapper: {
        display: 'flex',
        gap: '8px',
    },
    transparentButton: {
        padding: 0,
        minWidth: 'auto',
        justifyContent: 'flex-start',
    },
    clickableText: {
        color: tokens.colorBrandForeground1,
        cursor: 'pointer',
    },
    containerWrapper: {
        display: 'flex',
        height: '100%',
        position: 'relative',
    },
    containerFlex: {
        flex: '1',
    },
    clickableCard: {
        cursor: 'pointer',
    },
    flexRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    flexRowSmall: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    flexRowMedium: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    greenIcon: {
        color: tokens.colorPaletteGreenForeground1,
    },
    redIcon: {
        color: tokens.colorPaletteRedForeground1,
    },
    minWidthTable: {
        minWidth: '800px',
    },
    infoPanelAbsolute: {
        position: 'absolute',
        right: 0,
        top: 0,
        height: '100%',
        zIndex: 1000,
    },
});

interface ExtendedAgentListViewProps {
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    triggers: ExtendedTrigger[];
    connectors: ExtendedConnector[];
    isLoading: boolean;
    onRefresh: () => void;
    systemTools?: SystemTool[];
}

type AgentItem = {
    name: string;
    trigger: string;
    tools: string;
    systemToolsCount: number;
    kustoToolsCount: number;
    handoff: string;
    data: ExtendedAgent;
};

type IncidentTriggerItem = {
    name: string;
    status: string;
    subAgent: string;
    severity: string;
    incidentType: string;
    impactedService: string;
    description: string;
    titleContains: string;
    data: ExtendedTrigger;
};

type ScheduledTaskItem = {
    name: string;
    status: string;
    schedule: string;
    completedRuns: string;
    data: ExtendedTrigger;
};

type KustoToolItem = {
    name: string;
    connector: string;
    database: string;
    parameters: string;
    connectorStatus: (typeof CONNECTOR_STATUS)[keyof typeof CONNECTOR_STATUS];
    data: ExtendedTool;
};

type TabValue = 'agents' | 'incidentTriggers' | 'scheduledTasks' | 'kustoTools';
const STATUS = {
    ACTIVE: 'active',
    DISABLED: 'disabled',
    COMPLETED: 'completed',
} as const;

const CONNECTOR_STATUS = {
    CONNECTED: 'connected',
    NOT_CONNECTED: 'not-connected',
} as const;

const EMPTY_DISPLAY = '-' as const;

export const ExtendedAgentListView: FC<ExtendedAgentListViewProps> = ({
    agents,
    tools,
    triggers,
    systemTools,
    connectors,
    isLoading,
    onRefresh,
}) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const extendedAgentGraphContext = useContext(ExtendedAgentGraphContext);
    const intl = useIntl();
    const { infoPanelContainer, infoPanelFloating } = useExtendedAgentGraphStyles();

    const [activeTab, setActiveTab] = useState<TabValue>('agents');
    const [searchText, setSearchText] = useState<string>('');

    const [selectedDrawerItem, setSelectedDrawerItem] = useState<any>(undefined);

    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [isDeleting, setIsDeleting] = useState(false);
    const [errorMessage, setErrorMessage] = useState<string | undefined>();

    const [isPlaygroundOpen, setIsPlaygroundOpen] = useState(false);
    const [playgroundTarget, setPlaygroundTarget] = useState<PlaygroundTarget | undefined>(undefined);

    const [isInfoPanelFloating, setIsInfoPanelFloating] = useState(false);
    const [infoPanelPosition, setInfoPanelPosition] = useState({ x: 0, y: 0 });
    const [infoPanelWidth, setInfoPanelWidth] = useState(INFO_PANEL_DEFAULT_WIDTH);
    const infoPanelRef = useRef<HTMLDivElement>(null);
    const containerRef = useRef<HTMLDivElement>(null);
    const infoPanelDragStateRef = useRef<{ pointerId: number; offsetX: number; offsetY: number } | null>(null);
    const infoPanelResizeStateRef = useRef<{ pointerId: number; startX: number; startWidth: number } | null>(null);

    const debouncedSetSearchText = useMemo(() => debounce((value: string) => setSearchText(value), 150), []);

    const toolMap = useMemo(() => new Map(tools.map(tool => [tool.name, tool])), [tools]);
    const systemToolMap = useMemo(() => new Map((systemTools || []).map(tool => [tool.name, tool])), [systemTools]);
    const connectorMap = useMemo(() => new Map(connectors.map(connector => [connector.name, connector])), [connectors]);

    const filteredAgents = useMemo(() => {
        const query = searchText.trim().toLowerCase();
        if (!query) return agents;

        return agents.filter(agent => {
            const name = agent.name.toLowerCase();
            const instructions = agent.instructions?.toLowerCase() || '';
            return name.includes(query) || instructions.includes(query);
        });
    }, [agents, searchText]);

    const getAgentTriggers = useCallback(
        (agentName: string) => {
            return triggers.filter(trigger => trigger.agentName === agentName);
        },
        [triggers]
    );

    const incidentTriggers = useMemo(() => {
        return triggers.filter(trigger => trigger.type === 'incident');
    }, [triggers]);

    const scheduledTasks = useMemo(() => {
        return triggers.filter(trigger => trigger.type === 'scheduled');
    }, [triggers]);

    const agentItems = useMemo<AgentItem[]>(() => {
        return filteredAgents.map(agent => {
            const agentTriggers = getAgentTriggers(agent.name);
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
                handoff: agent.handoffs && agent.handoffs.length > 0 ? agent.handoffs.join(', ') : EMPTY_DISPLAY,
                data: agent,
            };
        });
    }, [filteredAgents, getAgentTriggers, toolMap, systemToolMap]);

    const incidentTriggerItems = useMemo<IncidentTriggerItem[]>(() => {
        const query = searchText.trim().toLowerCase();
        let filtered = incidentTriggers;

        if (query) {
            filtered = incidentTriggers.filter(
                trigger =>
                    trigger.name?.toLowerCase().includes(query) ||
                    trigger.status?.toLowerCase().includes(query) ||
                    trigger.agentName?.toLowerCase().includes(query) ||
                    trigger.incidentType?.toLowerCase().includes(query) ||
                    trigger.description?.toLowerCase().includes(query)
            );
        }

        return filtered.map(trigger => ({
            name: trigger.name || EMPTY_DISPLAY,
            status: trigger.status || EMPTY_DISPLAY,
            subAgent: trigger.agentName || EMPTY_DISPLAY,
            severity: trigger.priority || EMPTY_DISPLAY,
            incidentType: trigger.incidentType || EMPTY_DISPLAY,
            impactedService: trigger.service || EMPTY_DISPLAY,
            description: trigger.description || EMPTY_DISPLAY,
            titleContains: trigger.titleContains || EMPTY_DISPLAY,
            data: trigger,
        }));
    }, [incidentTriggers, searchText]);

    const scheduledTaskItems = useMemo<ScheduledTaskItem[]>(() => {
        const query = searchText.trim().toLowerCase();
        let filtered = scheduledTasks;

        if (query) {
            filtered = scheduledTasks.filter(
                trigger =>
                    trigger.name?.toLowerCase().includes(query) ||
                    trigger.status?.toLowerCase().includes(query) ||
                    trigger.schedule?.toLowerCase().includes(query) ||
                    trigger.cronExpression?.toLowerCase().includes(query)
            );
        }

        return filtered.map(trigger => ({
            name: trigger.name || EMPTY_DISPLAY,
            status: trigger.status || EMPTY_DISPLAY,
            schedule: parseCronExpression(trigger.schedule || trigger.cronExpression || EMPTY_DISPLAY),
            completedRuns: '-',
            data: trigger,
        }));
    }, [scheduledTasks, searchText]);

    type BaseTableItem = {
        name: string;
        [key: string]: any;
    };

    const kustoToolItems = useMemo(() => {
        const query = searchText.trim().toLowerCase();
        let filteredTools = tools.filter(tool => tool.type === 'KustoTool');

        if (query) {
            filteredTools = filteredTools.filter(
                tool =>
                    tool.name?.toLowerCase().includes(query) ||
                    tool.connector?.toLowerCase().includes(query) ||
                    tool.database?.toLowerCase().includes(query)
            );
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
    }, [tools, connectorMap, searchText]);

    const allItems = useMemo((): BaseTableItem[] => {
        switch (activeTab) {
            case 'incidentTriggers':
                return incidentTriggerItems.map(item => ({ ...item, type: 'incident' }));
            case 'scheduledTasks':
                return scheduledTaskItems.map(item => ({ ...item, type: 'scheduled' }));
            case 'kustoTools':
                return kustoToolItems.map(item => ({ ...item, type: 'tool' }));
            default:
                return agentItems.map(item => ({ ...item, type: 'agent' }));
        }
    }, [activeTab, agentItems, incidentTriggerItems, scheduledTaskItems, kustoToolItems]);

    const genericColumns: TableColumnDefinition<BaseTableItem>[] = [
        createTableColumn<BaseTableItem>({
            columnId: 'name',
            compare: (a, b) => a.name.localeCompare(b.name),
        }),
    ];

    const {
        getRows,
        selection: { allRowsSelected, someRowsSelected, toggleAllRows, toggleRow, isRowSelected },
    } = useTableFeatures(
        {
            columns: genericColumns,
            items: allItems,
        },
        [
            useTableSelection({
                selectionMode: 'multiselect',
                defaultSelectedItems: new Set(),
            }),
        ]
    );

    const rows = getRows(row => {
        const selected = isRowSelected(row.rowId);
        return {
            ...row,
            selected,
            appearance: selected ? ('brand' as const) : ('none' as const),
        };
    });

    const keyboardNavAttr = useArrowNavigationGroup({ axis: 'grid' });

    const selectedAgents = useMemo(() => {
        const selectedRowIds = Array.from(getRows().entries())
            .filter(([, row]) => isRowSelected(row.rowId))
            .map(([, row]) => row.item);
        return selectedRowIds;
    }, [getRows, isRowSelected]);

    const isDeleteDisabled = useMemo(() => {
        return selectedAgents.length === 0 || isDeleting;
    }, [isDeleting, selectedAgents]);

    const handleDelete = useCallback(async () => {
        setIsDeleting(true);
        setShowDeleteConfirmationDialog(false);
        setErrorMessage(undefined);

        const agentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);
        const agentNames = selectedAgents.map(agent => agent.name);

        azPortalContext.log({
            action: 'delete-agents',
            actionModifier: 'start',
            logLevel: 'info',
            data: { agentNames },
        });

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(SreAgentResources.deleteAgentNotificationTitle),
            intl.formatMessage(SreAgentResources.deleteAgentNotificationDescription, {
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
                        name:
                            agentNames.length === 1
                                ? agentNames[0]
                                : `${agentNames.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                    })
                );

                onRefresh();
            } else {
                const failedAgents = failures.map(f => f.agentName);
                const errorMessages = failures.map(f => getDataPlaneErrorMessage(f.response.error)).join('; ');

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
                        name:
                            failedAgents.length === 1
                                ? failedAgents[0]
                                : `${failedAgents.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                    })
                );
                if (failures.length < selectedAgents.length) {
                    onRefresh();
                }

                setErrorMessage(errorMessages);
            }
        } catch (error) {
            azPortalContext.log({
                action: 'delete-agents',
                actionModifier: 'failure',
                logLevel: 'error',
                data: {
                    agentNames,
                    error: error instanceof Error ? error.message : GenericErrorResources.unknownError,
                },
            });

            const errorMessage = error instanceof Error ? error.message : intl.formatMessage(GenericErrorResources.unexpectedError);

            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(SreAgentResources.deleteAgentNotificationError, {
                    name:
                        agentNames.length === 1
                            ? agentNames[0]
                            : `${agentNames.length} ${intl.formatMessage(SreAgentResources.agents).toLowerCase()}`,
                })
            );

            setErrorMessage(errorMessage);
        } finally {
            setIsDeleting(false);
        }
    }, [selectedAgents, sreAgentEndpoint, azPortalContext, intl, onRefresh]);

    const styles = useListViewStyles();

    const handleOpenInfoPanel = useCallback(
        (item: any) => {
            if (activeTab === 'agents') {
                const fullAgent = agents.find(agent => agent.name === item.name);
                setSelectedDrawerItem(fullAgent);
                extendedAgentGraphContext.setSelectedNode({
                    id: fullAgent?.name || item.name,
                    name: fullAgent?.name || item.name,
                    type: ExtendedAgentNodeType.Agent,
                    data: fullAgent || item,
                });
            } else if (activeTab === 'kustoTools') {
                const toolData = item.data;
                setSelectedDrawerItem(toolData);
                extendedAgentGraphContext.setSelectedNode({
                    id: toolData?.name || item.name,
                    name: toolData?.name || item.name,
                    type: ExtendedAgentNodeType.Tool,
                    data: toolData || item,
                });
            } else {
                setSelectedDrawerItem(item);
                let nodeType: ExtendedAgentNodeType;
                switch (activeTab) {
                    case 'incidentTriggers':
                    case 'scheduledTasks':
                        nodeType = ExtendedAgentNodeType.Trigger;
                        break;
                    default:
                        nodeType = ExtendedAgentNodeType.Agent;
                }
                extendedAgentGraphContext.setSelectedNode({
                    id: item.name,
                    name: item.name,
                    type: nodeType,
                    data: item,
                });
            }
        },
        [activeTab, agents, extendedAgentGraphContext.setSelectedNode]
    );

    const handleCloseInfoPanel = useCallback(() => {
        setSelectedDrawerItem(undefined);
        extendedAgentGraphContext.setSelectedNode(undefined);
    }, [extendedAgentGraphContext.setSelectedNode]);

    useEffect(() => {
        if (!selectedDrawerItem || activeTab !== 'agents') {
            return;
        }

        const updatedAgent = agents.find(agent => agent.name === selectedDrawerItem?.name);

        if (!updatedAgent) {
            handleCloseInfoPanel();
            return;
        }

        if (updatedAgent !== selectedDrawerItem) {
            // Keep the info panel in sync with the latest agent payload after edits.
            setSelectedDrawerItem(updatedAgent);
            extendedAgentGraphContext.setSelectedNode({
                id: updatedAgent.name,
                name: updatedAgent.name,
                type: ExtendedAgentNodeType.Agent,
                data: updatedAgent,
            });
        }
    }, [activeTab, agents, extendedAgentGraphContext, handleCloseInfoPanel, selectedDrawerItem]);

    const handleCardClick = useCallback(
        (cardType: TabValue) => {
            setActiveTab(cardType);
            setSelectedDrawerItem(undefined);
            extendedAgentGraphContext.setSelectedNode(undefined);
        },
        [extendedAgentGraphContext.setSelectedNode]
    );

    const handleOpenPlayground = useCallback((target: PlaygroundTarget) => {
        setPlaygroundTarget(target);
        setIsPlaygroundOpen(true);
    }, []);

    const handleDismissPlayground = useCallback(() => {
        setIsPlaygroundOpen(false);
        setPlaygroundTarget(undefined);
    }, []);

    const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

    const handleInfoPanelResizePointerMove = useCallback((event: PointerEvent) => {
        const resizeState = infoPanelResizeStateRef.current;
        if (!resizeState || resizeState.pointerId !== event.pointerId) {
            return;
        }

        const deltaX = event.clientX - resizeState.startX;
        const nextWidth = clamp(resizeState.startWidth - deltaX, INFO_PANEL_MIN_WIDTH, INFO_PANEL_MAX_WIDTH);
        setInfoPanelWidth(nextWidth);
    }, []);

    const handleInfoPanelResizePointerUp = useCallback(
        (event: PointerEvent) => {
            const resizeState = infoPanelResizeStateRef.current;
            if (!resizeState || resizeState.pointerId !== event.pointerId) {
                return;
            }

            infoPanelResizeStateRef.current = null;
            window.removeEventListener('pointermove', handleInfoPanelResizePointerMove);
            window.removeEventListener('pointerup', handleInfoPanelResizePointerUp);
            window.removeEventListener('pointercancel', handleInfoPanelResizePointerUp);
        },
        [handleInfoPanelResizePointerMove]
    );

    const handleInfoPanelResizePointerDown = useCallback(
        (event: React.PointerEvent<HTMLDivElement>) => {
            event.preventDefault();

            infoPanelResizeStateRef.current = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startWidth: infoPanelWidth,
            };

            window.addEventListener('pointermove', handleInfoPanelResizePointerMove);
            window.addEventListener('pointerup', handleInfoPanelResizePointerUp);
            window.addEventListener('pointercancel', handleInfoPanelResizePointerUp);
        },
        [infoPanelWidth, handleInfoPanelResizePointerMove, handleInfoPanelResizePointerUp]
    );

    const handleInfoPanelPointerMove = useCallback((event: PointerEvent) => {
        const dragState = infoPanelDragStateRef.current;
        if (!dragState || dragState.pointerId !== event.pointerId || !containerRef.current || !infoPanelRef.current) {
            return;
        }

        const containerRect = containerRef.current.getBoundingClientRect();
        const panelRect = infoPanelRef.current.getBoundingClientRect();

        const maxX = Math.max(containerRect.width - panelRect.width, 0);
        const maxY = Math.max(containerRect.height - panelRect.height, 0);

        setInfoPanelPosition({
            x: clamp(event.clientX - containerRect.left - dragState.offsetX, 0, maxX),
            y: clamp(event.clientY - containerRect.top - dragState.offsetY, 0, maxY),
        });
    }, []);

    const handleInfoPanelPointerUp = useCallback(
        (event: PointerEvent) => {
            const dragState = infoPanelDragStateRef.current;
            if (!dragState || dragState.pointerId !== event.pointerId) {
                return;
            }

            infoPanelDragStateRef.current = null;

            window.removeEventListener('pointermove', handleInfoPanelPointerMove);
            window.removeEventListener('pointerup', handleInfoPanelPointerUp);
            window.removeEventListener('pointercancel', handleInfoPanelPointerUp);
        },
        [handleInfoPanelPointerMove]
    );

    const handleInfoPanelPointerDown = useCallback(
        (event: React.PointerEvent<HTMLDivElement>) => {
            if (!containerRef.current || !infoPanelRef.current) {
                return;
            }

            event.preventDefault();

            const containerRect = containerRef.current.getBoundingClientRect();
            const panelRect = infoPanelRef.current.getBoundingClientRect();

            infoPanelDragStateRef.current = {
                pointerId: event.pointerId,
                offsetX: event.clientX - panelRect.left,
                offsetY: event.clientY - panelRect.top,
            };

            infoPanelRef.current.setPointerCapture?.(event.pointerId);

            const maxX = Math.max(containerRect.width - panelRect.width, 0);
            const maxY = Math.max(containerRect.height - panelRect.height, 0);
            const initialX = panelRect.left - containerRect.left;
            const initialY = panelRect.top - containerRect.top;

            setIsInfoPanelFloating(true);
            setInfoPanelPosition({
                x: clamp(initialX, 0, maxX),
                y: clamp(initialY, 0, maxY),
            });

            window.addEventListener('pointermove', handleInfoPanelPointerMove);
            window.addEventListener('pointerup', handleInfoPanelPointerUp);
            window.addEventListener('pointercancel', handleInfoPanelPointerUp);
        },
        [handleInfoPanelPointerMove, handleInfoPanelPointerUp]
    );

    const infoPanelStyle: React.CSSProperties = {
        width: `${infoPanelWidth}px`,
    };

    if (isInfoPanelFloating) {
        infoPanelStyle.transform = `translate(${infoPanelPosition.x}px, ${infoPanelPosition.y}px)`;
    }

    const renderTableHeaders = () => {
        switch (activeTab) {
            case 'incidentTriggers':
                return (
                    <>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerNameTitle)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.subagent)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.severityLabel)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.incidentImpactedService)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.incidentTitleContains)}
                        </TableHeaderCell>
                    </>
                );
            case 'scheduledTasks':
                return (
                    <>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.scheduledTriggerNameTitle)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.scheduleTitle)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ScheduledTasksResources.completedRuns)}
                        </TableHeaderCell>
                    </>
                );
            case 'kustoTools':
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
            default:
                return (
                    <>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.nameColumn)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.triggersColumn)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.tools)}
                        </TableHeaderCell>
                        <TableHeaderCell className={styles.tableHeader}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.handoffColumn)}
                        </TableHeaderCell>
                    </>
                );
        }
    };

    const renderTableCells = (item: any) => {
        switch (activeTab) {
            case 'incidentTriggers': {
                const incidentItem = item as IncidentTriggerItem;
                return (
                    <>
                        <TableCell tabIndex={0} role="gridcell">
                            <Button
                                appearance="transparent"
                                onClick={() => handleOpenInfoPanel(incidentItem)}
                                className={styles.transparentButton}
                            >
                                <Text className={styles.clickableText}>{incidentItem.name}</Text>
                            </Button>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            {(() => {
                                const isActive = incidentItem.status?.toLowerCase() === STATUS.ACTIVE;
                                const isDisabled = incidentItem.status?.toLowerCase() === STATUS.DISABLED;

                                if (isActive) {
                                    return (
                                        <Badge appearance="tint" color="success">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.onLabel)}
                                        </Badge>
                                    );
                                } else if (isDisabled) {
                                    return (
                                        <Badge appearance="tint" color="danger">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
                                        </Badge>
                                    );
                                } else {
                                    return <Text>{incidentItem.status}</Text>;
                                }
                            })()}
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{incidentItem.subAgent}</Text>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{incidentItem.severity}</Text>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{incidentItem.incidentType}</Text>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{incidentItem.impactedService}</Text>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{incidentItem.titleContains}</Text>
                        </TableCell>
                    </>
                );
            }
            case 'scheduledTasks': {
                const scheduledItem = item as ScheduledTaskItem;
                return (
                    <>
                        <TableCell tabIndex={0} role="gridcell">
                            <Button
                                appearance="transparent"
                                onClick={() => handleOpenInfoPanel(scheduledItem)}
                                className={styles.transparentButton}
                            >
                                <Text className={styles.clickableText}>{scheduledItem.name}</Text>
                            </Button>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            {(() => {
                                const isActive = scheduledItem.status?.toLowerCase() === STATUS.ACTIVE;
                                const isDisabled = scheduledItem.status?.toLowerCase() === STATUS.DISABLED;
                                const isCompleted = scheduledItem.status?.toLowerCase() === STATUS.COMPLETED;

                                if (isActive) {
                                    return (
                                        <Badge appearance="tint" color="success">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.onLabel)}
                                        </Badge>
                                    );
                                } else if (isDisabled) {
                                    return (
                                        <Badge appearance="tint" color="danger">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
                                        </Badge>
                                    );
                                } else if (isCompleted) {
                                    return (
                                        <Badge appearance="tint" color="subtle">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.completedLabel)}
                                        </Badge>
                                    );
                                } else {
                                    return <Text>{scheduledItem.status}</Text>;
                                }
                            })()}
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{scheduledItem.schedule}</Text>
                        </TableCell>
                        <TableCell tabIndex={0} role="gridcell">
                            <Text>{scheduledItem.completedRuns}</Text>
                        </TableCell>
                    </>
                );
            }
            case 'kustoTools': {
                const toolItem = item as KustoToolItem;
                return (
                    <>
                        <TableCell tabIndex={0} role="gridcell">
                            <Button
                                appearance="transparent"
                                onClick={() => handleOpenInfoPanel(toolItem)}
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
            }
            default: {
                const agentItem = item as AgentItem;
                return (
                    <>
                        <TableCell role="gridcell">
                            <div className={styles.tableCellContent}>
                                <Button
                                    appearance="transparent"
                                    onClick={() => handleOpenInfoPanel(agentItem)}
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
                                const agentIncidentTriggers = incidentTriggers.filter(trigger => trigger.agentName === agentItem.name);
                                const agentScheduledTasks = scheduledTasks.filter(trigger => trigger.agentName === agentItem.name);

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
                                            <span>{EMPTY_DISPLAY}</span>
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
                                                <EntityIcon
                                                    type="tool"
                                                    shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                                />
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
                                        {totalToolsCount === 0 && <span>{EMPTY_DISPLAY}</span>}
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
                                    <span>{EMPTY_DISPLAY}</span>
                                );
                            })()}
                        </TableCell>
                    </>
                );
            }
        }
    };

    return (
        <div ref={containerRef} className={styles.containerWrapper}>
            <div className={mergeClasses(styles.container, styles.containerFlex)}>
                <div className={styles.cardsContainer}>
                    <Card className={mergeClasses(styles.card, styles.clickableCard)} onClick={() => handleCardClick('agents')}>
                        <div className={styles.cardHeader}>
                            <div className={styles.cardTitleSection}>
                                <EntityIcon type="agent" shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />
                                <Text className={styles.cardTitle}>{intl.formatMessage(SettingsTabResources.subAgents)}</Text>
                            </div>
                            <Text className={styles.cardCount}>{agents.length}</Text>
                        </div>
                    </Card>

                    <Card className={mergeClasses(styles.card, styles.clickableCard)} onClick={() => handleCardClick('incidentTriggers')}>
                        <div className={styles.cardHeader}>
                            <div className={styles.cardTitleSection}>
                                <EntityIcon type="incidentTrigger" shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />
                                <Text className={styles.cardTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggers)}
                                </Text>
                            </div>
                            <Text className={styles.cardCount}>{incidentTriggers.length}</Text>
                        </div>
                    </Card>

                    <Card className={mergeClasses(styles.card, styles.clickableCard)} onClick={() => handleCardClick('scheduledTasks')}>
                        <div className={styles.cardHeader}>
                            <div className={styles.cardTitleSection}>
                                <EntityIcon type="scheduledTask" shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />
                                <Text className={styles.cardTitle}>{intl.formatMessage(ScheduledTasksResources.scheduledTasks)}</Text>
                            </div>
                            <Text className={styles.cardCount}>{scheduledTasks.length}</Text>
                        </div>
                    </Card>

                    <Card className={mergeClasses(styles.card, styles.clickableCard)} onClick={() => handleCardClick('kustoTools')}>
                        <div className={styles.cardHeader}>
                            <div className={styles.cardTitleSection}>
                                <EntityIcon type="toolWithGear" shorthandStyle={{ wrapperSize: 36, iconSize: 22, borderRadius: 6 }} />
                                <Text className={styles.cardTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.kustoTools)}</Text>
                            </div>
                            <Text className={styles.cardCount}>{kustoToolItems.length}</Text>
                        </div>
                    </Card>
                </div>

                <TabList
                    selectedValue={activeTab}
                    onTabSelect={(_event, data) => {
                        setActiveTab(data.value as TabValue);
                        setSelectedDrawerItem(undefined);
                        extendedAgentGraphContext.setSelectedNode(undefined);
                    }}
                >
                    <Tab id="agents" value="agents">
                        {intl.formatMessage(SettingsTabResources.subAgents)}
                    </Tab>
                    <Tab id="incidentTriggers" value="incidentTriggers">
                        {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggers)}
                    </Tab>
                    <Tab id="scheduledTasks" value="scheduledTasks">
                        {intl.formatMessage(ScheduledTasksResources.scheduledTasks)}
                    </Tab>
                    <Tab id="kustoTools" value="kustoTools">
                        {intl.formatMessage(ExtendedAgentsGraphResources.kustoTools)}
                    </Tab>
                </TabList>

                <div className={styles.searchAndToolbar}>
                    <Toolbar>
                        <ToolbarButton icon={<ArrowClockwise16Regular />} appearance="subtle" disabled={isLoading} onClick={onRefresh}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.updateList)}
                        </ToolbarButton>
                        <ToolbarButton
                            appearance="subtle"
                            icon={<Delete16Regular />}
                            onClick={() => setShowDeleteConfirmationDialog(true)}
                            disabled={isDeleteDisabled}
                        >
                            {intl.formatMessage(SreAgentResources.delete)}
                        </ToolbarButton>
                        <ToolbarDivider />
                        <SearchBox
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                            value={searchText}
                            onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => debouncedSetSearchText(data.value ?? '')}
                        />
                    </Toolbar>
                </div>

                {errorMessage && (
                    <MessageBar intent="error" className={styles.errorBar} role="alert">
                        <MessageBarBody>
                            <MessageBarTitle>{intl.formatMessage(SreAgentResources.error)}</MessageBarTitle>
                            {errorMessage}
                        </MessageBarBody>
                        <MessageBarActions>
                            <Button appearance="transparent" onClick={() => setErrorMessage(undefined)}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.relationshipDismiss)}
                            </Button>
                        </MessageBarActions>
                    </MessageBar>
                )}

                <div>
                    {isLoading ? (
                        <div className={styles.emptyState}>
                            <Spinner />
                            <Text>{intl.formatMessage(ComponentResources.loading)}</Text>
                        </div>
                    ) : allItems.length === 0 ? (
                        <div className={styles.emptyState}>
                            <Text>
                                {searchText
                                    ? intl.formatMessage(ComponentResources.noResultsFoundFor, { searchString: searchText })
                                    : intl.formatMessage(ExtendedAgentsGraphResources.noActiveTab, { activeTab: activeTab })}
                            </Text>
                        </div>
                    ) : (
                        <Table
                            {...keyboardNavAttr}
                            role="grid"
                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.agentDatagrid)}
                            className={styles.minWidthTable}
                        >
                            <TableHeader>
                                <TableRow>
                                    <TableSelectionCell
                                        checked={allRowsSelected ? true : someRowsSelected ? 'mixed' : false}
                                        aria-checked={allRowsSelected ? true : someRowsSelected ? 'mixed' : false}
                                        role="checkbox"
                                        onClick={toggleAllRows}
                                        checkboxIndicator={{ 'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel) }}
                                    />
                                    {renderTableHeaders()}
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {rows.map(({ item, selected, appearance, rowId }) => (
                                    <TableRow key={item.name} aria-selected={selected} appearance={appearance}>
                                        <TableSelectionCell
                                            role="gridcell"
                                            aria-selected={selected}
                                            checked={selected}
                                            onClick={(e: React.MouseEvent) => toggleRow(e, rowId)}
                                            checkboxIndicator={{ 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) }}
                                        />
                                        {renderTableCells(item)}
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    )}
                </div>

                {/* Delete Confirmation Dialog */}
                <Dialog open={showDeleteConfirmationDialog} onOpenChange={(_, data) => setShowDeleteConfirmationDialog(data.open)}>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>{intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmTitle)}</DialogTitle>
                            <DialogContent>
                                {intl.formatMessage(ExtendedAgentsGraphResources.deleteConfirmMessage, { count: selectedAgents.length })}
                            </DialogContent>
                            <DialogActions>
                                <Button appearance="primary" onClick={handleDelete} disabled={isDeleting} className={styles.dangerButton}>
                                    {intl.formatMessage(SreAgentResources.yes)}
                                </Button>
                                <Button appearance="secondary" onClick={() => setShowDeleteConfirmationDialog(false)}>
                                    {intl.formatMessage(SreAgentResources.no)}
                                </Button>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>

                {/* Playground Modal */}
                <PlaygroundModal
                    open={isPlaygroundOpen}
                    target={playgroundTarget}
                    agents={agents}
                    tools={tools}
                    connectors={connectors}
                    systemTools={systemTools || []}
                    onDismiss={handleDismissPlayground}
                />
            </div>

            {/* Info Panel */}
            {selectedDrawerItem && (
                <div
                    ref={infoPanelRef}
                    className={mergeClasses(infoPanelContainer, isInfoPanelFloating && infoPanelFloating, styles.infoPanelAbsolute)}
                    style={infoPanelStyle}
                >
                    <ExtendedAgentInfoPanel
                        agents={agents}
                        selectedAgent={selectedDrawerItem}
                        tools={tools}
                        connectors={connectors}
                        triggers={triggers}
                        systemTools={systemTools}
                        sreAgentEndpoint={sreAgentEndpoint}
                        onRefresh={onRefresh}
                        onDragHandlePointerDown={handleInfoPanelPointerDown}
                        onResizeHandlePointerDown={handleInfoPanelResizePointerDown}
                        width={infoPanelWidth}
                        minWidth={INFO_PANEL_MIN_WIDTH}
                        maxWidth={INFO_PANEL_MAX_WIDTH}
                        onOpenPlayground={handleOpenPlayground}
                        onClose={handleCloseInfoPanel}
                    />
                </div>
            )}
        </div>
    );
};

export default ExtendedAgentListView;

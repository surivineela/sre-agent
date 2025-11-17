import { Tab, TabList, mergeClasses } from '@fluentui/react-components';
import React, { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { TextWithLink } from '../../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../../Common/Constants/FwLinks';
import { getLocaleTimeHHMM } from '../../../Common/Helpers/Date';
import { ExtendedAgentsGraphResources, ScheduledTasksResources, SettingsTabResources } from '../../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    INFO_PANEL_DEFAULT_WIDTH,
    INFO_PANEL_MAX_WIDTH,
    INFO_PANEL_MIN_WIDTH,
    SystemTool,
} from '../../Contracts/ExtendedAgentGraph';
import PlaygroundModal, { PlaygroundTarget } from '../../Playground/PlaygroundModal';
import { useExtendedAgentGraphStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { ExtendedAgentInfoPanel } from '../ExtendedAgentInfoPanel';
import { EntityCard } from './Common/EntityCard';
import { TableViewTabValue } from './ExtendedAgentTableView.Contracts';
import { useListViewStyles } from './ExtendedAgentTableView.Styles';
import { AgentTable } from './Tabs/AgentTable';
import { IncidentTriggerTable } from './Tabs/IncidentTriggerTable';
import { KustoToolTable } from './Tabs/KustoToolTable';
import { ScheduledTaskTable } from './Tabs/ScheduledTaskTable';

interface ExtendedAgentTableViewProps {
    activeTab: TableViewTabValue;
    setActiveTab: (tab: TableViewTabValue) => void;
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    triggers: ExtendedTrigger[];
    connectors: ExtendedConnector[];
    isLoading: boolean;
    onRefresh: () => void;
    lastUpdated?: string;
    systemTools?: SystemTool[];
    onEditKustoTool?: (tool: ExtendedTool) => void;
}

export const ExtendedAgentTableView: FC<ExtendedAgentTableViewProps> = ({
    activeTab,
    setActiveTab,
    agents,
    tools,
    triggers,
    systemTools,
    connectors,
    isLoading,
    onRefresh,
    onEditKustoTool,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const { infoPanelContainer, infoPanelFloating } = useExtendedAgentGraphStyles();
    const extendedAgentGraphContext = useContext(ExtendedAgentGraphContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [selectedDrawerItem, setSelectedDrawerItem] = useState<any>(undefined);
    const [lastUpdated, setLastUpdated] = useState<string>('');
    const [isPlaygroundOpen, setIsPlaygroundOpen] = useState(false);
    const [playgroundTarget, setPlaygroundTarget] = useState<PlaygroundTarget | undefined>(undefined);
    const [isInfoPanelFloating, setIsInfoPanelFloating] = useState(false);
    const [infoPanelPosition, setInfoPanelPosition] = useState({ x: 0, y: 0 });
    const [infoPanelWidth, setInfoPanelWidth] = useState(INFO_PANEL_DEFAULT_WIDTH);
    const infoPanelRef = useRef<HTMLDivElement>(null);
    const containerRef = useRef<HTMLDivElement>(null);
    const infoPanelDragStateRef = useRef<{ pointerId: number; offsetX: number; offsetY: number } | null>(null);
    const infoPanelResizeStateRef = useRef<{ pointerId: number; startX: number; startWidth: number } | null>(null);

    const allIncidentTriggers = useMemo(() => {
        return triggers.filter(trigger => trigger.type === 'incident');
    }, [triggers]);

    const allScheduledTasks = useMemo(() => {
        return triggers.filter(trigger => trigger.type === 'scheduled');
    }, [triggers]);

    const allKustoTools = useMemo(() => {
        return tools.filter(tool => tool.type === 'KustoTool');
    }, [tools]);

    const handleOpenPlayground = useCallback((target: PlaygroundTarget) => {
        setPlaygroundTarget(target);
        setIsPlaygroundOpen(true);
    }, []);

    const handleDismissPlayground = useCallback(() => {
        setIsPlaygroundOpen(false);
        setPlaygroundTarget(undefined);
    }, []);

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
        [activeTab, agents, extendedAgentGraphContext]
    );

    const handleCloseInfoPanel = useCallback(() => {
        setSelectedDrawerItem(undefined);
        extendedAgentGraphContext.setSelectedNode(undefined);
    }, [extendedAgentGraphContext]);

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

    const infoPanelStyle: React.CSSProperties = useMemo(
        () => ({
            width: `${infoPanelWidth}px`,
            ...(isInfoPanelFloating ? { transform: `translate(${infoPanelPosition.x}px, ${infoPanelPosition.y}px)` } : {}),
        }),
        [infoPanelPosition.x, infoPanelPosition.y, infoPanelWidth, isInfoPanelFloating]
    );

    const handleTabSelect = useCallback(
        (cardType: TableViewTabValue) => {
            setActiveTab(cardType);
            setSelectedDrawerItem(undefined);
            extendedAgentGraphContext.setSelectedNode(undefined);
        },
        [extendedAgentGraphContext, setActiveTab]
    );

    const renderTable = useCallback(() => {
        switch (activeTab) {
            case 'agents':
                return (
                    <AgentTable
                        agents={agents}
                        tools={tools}
                        triggers={triggers}
                        systemTools={systemTools}
                        openInfoPanel={handleOpenInfoPanel}
                        refresh={onRefresh}
                        lastUpdated={lastUpdated}
                        isLoading={isLoading}
                    />
                );
            case 'incidentTriggers':
                return (
                    <IncidentTriggerTable
                        incidentTriggers={allIncidentTriggers}
                        openInfoPanel={handleOpenInfoPanel}
                        refresh={onRefresh}
                        lastUpdated={lastUpdated}
                        isLoading={isLoading}
                    />
                );
            case 'scheduledTasks':
                return (
                    <ScheduledTaskTable
                        scheduledTaskTriggers={allScheduledTasks}
                        openInfoPanel={handleOpenInfoPanel}
                        refresh={onRefresh}
                        lastUpdated={lastUpdated}
                        isLoading={isLoading}
                    />
                );
            case 'kustoTools':
                return (
                    <KustoToolTable
                        kustoTools={allKustoTools}
                        connectors={connectors}
                        openInfoPanel={handleOpenInfoPanel}
                        refresh={onRefresh}
                        lastUpdated={lastUpdated}
                        isLoading={isLoading}
                    />
                );
            default:
                return null;
        }
    }, [
        activeTab,
        agents,
        tools,
        triggers,
        systemTools,
        handleOpenInfoPanel,
        onRefresh,
        lastUpdated,
        isLoading,
        allIncidentTriggers,
        allScheduledTasks,
        allKustoTools,
        connectors,
    ]);

    useEffect(() => {
        if (!isLoading) {
            setLastUpdated(getLocaleTimeHHMM(new Date()));
        }
    }, [isLoading]);

    return (
        <div ref={containerRef} className={styles.containerWrapper}>
            <div className={mergeClasses(styles.container, styles.containerFlex)}>
                <div className={styles.descriptionText}>
                    <TextWithLink
                        text={intl.formatMessage(ExtendedAgentsGraphResources.listViewDescription)}
                        linkText={intl.formatMessage(ExtendedAgentsGraphResources.learnMoreAboutSubagent)}
                        linkUrl={SreAgentFwLinks.learnMoreAboutSubagents}
                    />
                </div>
                <div className={styles.cardsContainer}>
                    <EntityCard type="agents" entityCount={agents.length} handleCardClick={handleTabSelect} />
                    <EntityCard type="incidentTriggers" entityCount={allIncidentTriggers.length} handleCardClick={handleTabSelect} />
                    <EntityCard type="scheduledTasks" entityCount={allScheduledTasks.length} handleCardClick={handleTabSelect} />
                    <EntityCard type="kustoTools" entityCount={allKustoTools.length} handleCardClick={handleTabSelect} />
                </div>

                <TabList
                    selectedValue={activeTab}
                    onTabSelect={(_event, data) => {
                        handleTabSelect(data.value as TableViewTabValue);
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

                {renderTable()}

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
                        onEditKustoTool={onEditKustoTool}
                        onClose={handleCloseInfoPanel}
                    />
                </div>
            )}
        </div>
    );
};

export default ExtendedAgentTableView;

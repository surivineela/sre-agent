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
    ExtendedAgentGraphNode,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    INFO_PANEL_DEFAULT_WIDTH,
    INFO_PANEL_MAX_WIDTH,
    INFO_PANEL_MIN_WIDTH,
    Skill,
    SystemTool,
} from '../../Contracts/ExtendedAgentGraph';
import PlaygroundModal, { PlaygroundTarget } from '../../Playground/PlaygroundModal';
import { useExtendedAgentGraphStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { ExtendedAgentInfoPanel } from '../ExtendedAgentInfoPanel/ExtendedAgentInfoPanel';
import { EntityCard } from './Common/EntityCard';
import { TableViewTabValue } from './ExtendedAgentTableView.Contracts';
import { useListViewStyles } from './ExtendedAgentTableView.Styles';
import { AgentTable } from './Tabs/AgentTable';
import { IncidentTriggerTable } from './Tabs/IncidentTriggerTable';
import { KustoToolTable } from './Tabs/KustoToolTable';
import { ScheduledTaskTable } from './Tabs/ScheduledTaskTable';
import { SkillTable } from './Tabs/SkillTable';

interface ExtendedAgentTableViewProps {
    activeTab: TableViewTabValue;
    setActiveTab: (tab: TableViewTabValue) => void;
    agents: ExtendedAgent[];
    tools: ExtendedTool[];
    triggers: ExtendedTrigger[];
    connectors: ExtendedConnector[];
    skills: Skill[];
    isLoading: boolean;
    onRefresh: () => void;
    lastUpdated?: string;
    systemTools?: SystemTool[];
    onEditKustoTool?: (tool: ExtendedTool) => void;
    onEditSkill?: (skill: Skill) => void;
}

export const ExtendedAgentTableView: FC<ExtendedAgentTableViewProps> = ({
    activeTab,
    setActiveTab,
    agents,
    tools,
    triggers,
    systemTools,
    connectors,
    skills,
    isLoading,
    onRefresh,
    onEditKustoTool,
    onEditSkill,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const { infoPanelContainer, infoPanelFloating } = useExtendedAgentGraphStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [selectedDrawerItem, setSelectedDrawerItem] = useState<ExtendedAgentGraphNode>();
    const [selectedDrawerItemId, setSelectedDrawerItemId] = useState<{ id: string; type: ExtendedAgentNodeType }>();
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

    const handleOpenInfoPanel = useCallback((itemName: string, itemType: ExtendedAgentNodeType) => {
        setSelectedDrawerItemId({ id: itemName, type: itemType });
    }, []);

    const handleCloseInfoPanel = useCallback(() => {
        setSelectedDrawerItemId(undefined);
    }, []);

    useEffect(() => {
        if (!selectedDrawerItemId) {
            setSelectedDrawerItem(undefined);
        } else {
            const { id, type } = selectedDrawerItemId;

            if (type === ExtendedAgentNodeType.Agent) {
                const fullAgent = agents.find(agent => agent.name === id);
                if (fullAgent) {
                    setSelectedDrawerItem({
                        id: `agent_${fullAgent.name}`,
                        name: fullAgent.name,
                        type: ExtendedAgentNodeType.Agent,
                        data: fullAgent,
                    });
                }
            } else if (type === ExtendedAgentNodeType.Tool) {
                const toolData = tools.find(tool => tool.name === id);
                if (toolData) {
                    setSelectedDrawerItem({
                        id: `tool_${toolData.name}`,
                        name: toolData.name,
                        type: ExtendedAgentNodeType.Tool,
                        data: toolData,
                    });
                }
            } else if (type === ExtendedAgentNodeType.Trigger) {
                const triggerData = triggers.find(trigger => trigger.name === id);
                if (triggerData) {
                    setSelectedDrawerItem({
                        id: `trigger_${triggerData.name}`,
                        name: triggerData.name || '',
                        type: ExtendedAgentNodeType.Trigger,
                        data: triggerData,
                    });
                }
            } else if (type === ExtendedAgentNodeType.Skill) {
                const skillData = skills.find(skill => skill.name === id);
                if (skillData) {
                    setSelectedDrawerItem({
                        id: `skill_${skillData.name}`,
                        name: skillData.name,
                        type: ExtendedAgentNodeType.Skill,
                        data: skillData,
                    });
                }
            } else {
                setSelectedDrawerItem(undefined);
                setSelectedDrawerItemId(undefined);
            }
        }
    }, [selectedDrawerItemId, agents, tools, triggers, skills]);

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
            setSelectedDrawerItemId(undefined);
        },
        [setActiveTab]
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
            case 'skills':
                return (
                    <SkillTable
                        skills={skills}
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
        skills,
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
                    <EntityCard type="skills" entityCount={skills.length} handleCardClick={handleTabSelect} />
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
                    <Tab id="skills" value="skills">
                        {intl.formatMessage(ExtendedAgentsGraphResources.skillsLabel)}
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
                        selectedNode={selectedDrawerItem}
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
                        onEditSkill={onEditSkill}
                        onClose={handleCloseInfoPanel}
                    />
                </div>
            )}
        </div>
    );
};

export default ExtendedAgentTableView;

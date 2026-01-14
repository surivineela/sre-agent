import { useTheme } from '@fluentui/react';
import { Button, mergeClasses, MessageBar, MessageBarActions, MessageBarBody, Spinner, Text } from '@fluentui/react-components';
import { BeakerRegular } from '@fluentui/react-icons';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider, useEdgesState, useNodesState, useReactFlow } from '@xyflow/react';
import React, { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { useFeatureFlags } from '../../Common/Hooks/useFeatureFlags';
import { ExtendedAgentsGraphResources, PlaygroundResources } from '../../Strings/SREAgentResources';
import {
    AgentQuickAction,
    ExtendedAgent,
    ExtendedAgentAnchorEntity,
    ExtendedAgentGraphContext,
    ExtendedAgentGraphView,
    ExtendedConnector,
    ExtendedTool,
    INFO_PANEL_DEFAULT_WIDTH,
    INFO_PANEL_MAX_WIDTH,
    INFO_PANEL_MIN_WIDTH,
    PlaygroundEntity,
    Skill,
    TriggerQuickAction,
} from '../Contracts/ExtendedAgentGraph';
import { ScheduledTask } from '../Contracts/ScheduledTasks';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';
import { useExtendedAgentGraph } from '../Hooks/useExtendedAgentGraph';
import { useExtendedAgentGraphLayout } from '../Hooks/useExtendedAgentGraphLayout';
import { HandlerCreateOrEditInfo } from '../IncidentManagement/CreateIncidentHandler/Contracts';
import { ScheduledTaskCreateOrEditDialog, ScheduledTaskDialogMode } from '../ScheduledTasks/Common/ScheduledTaskCreateOrEditDialog';
import { ScheduledTasksContext } from '../ScheduledTasks/Hooks/ScheduledTasksContext';
import { useCommonStyles } from '../Styles/Common.styles';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import {
    AddExistingAgentHandoffDialog,
    AddExistingAgentHandoffDialogProps,
} from './AddExistingAgentHandoffDialog/AddExistingAgentHandoffDialog';
import { AddExistingToolDialog, AddExistingToolDialogProps } from './AddExistingToolDialog/AddExistingToolDialog';
import { AgentCreateDialog } from './AgentCreateDialog/AgentCreateDialog';
import { AgentCreateOrEditInfo } from './AgentCreateDialog/Contracts';
import { ConnectorCard } from './ConnectorCard';
import { ExtendedAgentCard } from './ExtendedAgentCard';
import { EntityType, EntityTypeExt } from './ExtendedAgentCreationDialog/types';
import { ExtendedAgentCreationDialog } from './ExtendedAgentCreationDialogNew';
import { ExtendedAgentEdge } from './ExtendedAgentEdge';
import { ExtendedAgentEmptyState } from './ExtendedAgentEmptyState';
import {
    CONNECTOR_CARD_TYPE,
    doesNodeExistInGraph,
    EXPANDED_SKILL_GROUP_CARD_TYPE,
    EXPANDED_TOOLBOX_CARD_TYPE,
    EXTENDED_AGENT_CARD_TYPE,
    EXTENDED_AGENT_EDGE_TYPE,
    SKILL_CARD_TYPE,
    SKILL_GROUP_CARD_TYPE,
    TOOL_CARD_TYPE,
    TOOLBOX_CARD_TYPE,
    TRIGGER_CARD_TYPE,
} from './ExtendedAgentGraphUtility';
import { ExtendedAgentInfoPanel } from './ExtendedAgentInfoPanel/ExtendedAgentInfoPanel';
import { ExtendedAgentRelationshipDialog } from './ExtendedAgentRelationshipDialog';
import { CreateSkillDialog } from './ExtendedAgents/Skills/CreateSkillDialog';
import { ExpandedSkillGroup } from './ExtendedAgents/Skills/ExpandedSkillGroup';
import { SkillCard } from './ExtendedAgents/Skills/SkillCard';
import { SkillGroupCard } from './ExtendedAgents/Skills/SkillGroupCard';
import { CollapsedToolboxCard, ExpandedToolboxCard } from './ExtendedAgents/Toolbox/ToolboxCard';
import { ExtendedAgentSelector } from './ExtendedAgentSelector';
import ExtendedAgentTableView from './ExtendedAgentTableView/ExtendedAgentTableView';
import { TableViewTabValue } from './ExtendedAgentTableView/ExtendedAgentTableView.Contracts';
import { ExtendedAgentToolbar } from './ExtendedAgentToolbar';
import { buildMetaAgentYaml, convertExtendedEntityToYaml } from './ExtendedAgentYamlUtils';
import { IncidentTriggerCreateDialog } from './IncidentTriggerCreateDialog/IncidentTriggerCreateDialog';
import { KustoToolDialog, KustoToolDialogMode } from './KustoToolDialog/KustoToolDialog';
import { KustoToolPlayground } from './KustoToolDialog/KustoToolPlayground';
import { AgentPlayground } from './Playground/AgentPlayground/AgentPlayground';
import { PlaygroundEntitySelector } from './Playground/PlaygroundEntitySelector';
import { SystemToolPlayground } from './Playground/SystemToolPlayground/SystemToolPlayground';
import { PythonToolDialog, PythonToolDialogMode } from './PythonToolDialog/PythonToolDialog';
import { ToolCard } from './ToolCard';
import { TriggerCard } from './TriggerCard';

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

const ExtendedAgentGraph = () => {
    return (
        <ReactFlowProvider>
            <ExtendedAgentGraphContent />
        </ReactFlowProvider>
    );
};

type OperationResult = {
    success: boolean;
    message: string;
};

type LinkRetryContext = {
    entityType: 'agent' | 'tool';
    entityName: string;
    sourceAgentName: string;
    lastError?: string;
};

type CreationDialogContext =
    | undefined
    | {
          kind: 'linkFromAgent';
          sourceAgentName: string;
          targetType: 'agent' | 'tool';
      };

const ExtendedAgentGraphContent = memo(() => {
    const {
        nodes: graphNodes,
        edges: graphEdges,
        selectedNodeIdRef,
        selectedNodeId,
        setSelectedNodeId,
        selectedNode,
        hoveredNodeId,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        loading,
        error,
        agents,
        tools,
        mcpConnections,
        connectors,
        triggers,
        incidentFiltersHook,
        incidentHandlersHook,
        scheduledTasksHook,
        systemTools,
        skills,
        anchorEntity,
        setAnchorEntity,
        refetch,
        isSkillGroupExpanded,
        toggleSkillGroupExpanded,
        expandedToolboxes,
        toggleToolboxExpanded,
        skipNextFitViewRef,
    } = useExtendedAgentGraph();

    const { features } = useFeatureFlags();

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const {
        rootContainer,
        visualRoot,
        reactFlow,
        spinner,
        container,
        selectorOverlay,
        infoPanelContainer,
        infoPanelFloating,
        statusMessageContainer,
    } = useExtendedAgentGraphStyles();

    const commonStyles = useCommonStyles();

    const theme = useTheme();
    const intl = useIntl();
    const navigate = useNavigate();
    const location = useLocation();
    const agentSiteNavigate = useAgentSiteNavigate();

    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [agentHandoffPickerInfo, setAgentHandoffPickerInfo] = useState<AddExistingAgentHandoffDialogProps['handoffInfo'] | undefined>();
    const [toolPickerInfo, setToolPickerInfo] = useState<AddExistingToolDialogProps['toolPickerInfo'] | undefined>();
    const [agentCreateOrEditInfo, setAgentCreateOrEditInfo] = useState<AgentCreateOrEditInfo>();

    const [isOperationInProgress, setIsOperationInProgress] = useState<boolean>(false);
    const [isScheduledTaskDialogOpen, setIsScheduledTaskDialogOpen] = useState(false);
    const [scheduledTaskDialogMode, setScheduledTaskDialogMode] = useState<ScheduledTaskDialogMode>(ScheduledTaskDialogMode.Create);
    const [scheduledTaskStartingAgent, setScheduledTaskStartingAgent] = useState<string>();
    const [scheduledTaskEditingTask, setScheduledTaskEditingTask] = useState<ScheduledTask>();

    const [isToolDialogOpen, setIsToolDialogOpen] = useState<boolean>(false);
    const [createToolAgent, setCreateToolAgent] = useState<string>();
    const [toolDialogMode, setToolDialogMode] = useState<KustoToolDialogMode>(KustoToolDialogMode.Create);
    const [toolToEdit, setToolToEdit] = useState<ExtendedTool>();

    // Python Tool Dialog state
    const [isPythonToolDialogOpen, setIsPythonToolDialogOpen] = useState<boolean>(false);
    const [pythonToolDialogMode, setPythonToolDialogMode] = useState<PythonToolDialogMode>(PythonToolDialogMode.Create);
    const [pythonToolToEdit, setPythonToolToEdit] = useState<ExtendedTool>();
    const [pythonToolAgentName, setPythonToolAgentName] = useState<string>();

    const [currentView, setCurrentView] = useState<ExtendedAgentGraphView>(ExtendedAgentGraphView.Canvas);
    const [currentTableViewTab, setCurrentTableViewTab] = useState<TableViewTabValue>('agents');
    const [nodes, setNodes, onNodesChange] = useNodesState<any>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<any>([]);
    const [isLayouting, setIsLayouting] = useState(false);
    const [isCreationDialogOpen, setIsCreationDialogOpen] = useState(false);
    const [isRelationshipDialogOpen, setIsRelationshipDialogOpen] = useState(false);
    const [relationshipAgentName, setRelationshipAgentName] = useState<string | undefined>(undefined);
    const [relationshipInitialAction, setRelationshipInitialAction] = useState<'handoff' | 'tool' | undefined>(undefined);
    const [creationDialogContext, setCreationDialogContext] = useState<CreationDialogContext>(undefined);
    const [creationSuccess, setCreationSuccess] = useState<
        { entityType: 'agent' | 'tool' | 'connector'; entityName: string; sourceAgentName?: string } | undefined
    >(undefined);
    const [creationDialogInitialTypeOverride, setCreationDialogInitialTypeOverride] = useState<EntityType | undefined>(undefined);
    const [creationDialogInitialToolType, setCreationDialogInitialToolType] = useState<'KustoTool' | 'PythonFunctionTool' | undefined>(
        undefined
    );
    const [creationDialogTriggerAgentName, setCreationDialogTriggerAgentName] = useState<string | undefined>(undefined);
    const [pendingEntitySelection, setPendingEntitySelection] = useState<ExtendedAgentAnchorEntity | undefined>(undefined);
    const [linkRetryContext, setLinkRetryContext] = useState<LinkRetryContext | undefined>(undefined);
    const [isRetryingLink, setIsRetryingLink] = useState(false);
    const [isInfoPanelFloating, setIsInfoPanelFloating] = useState(false);
    const [infoPanelPosition, setInfoPanelPosition] = useState({ x: 0, y: 0 });
    const [isInfoPanelDragging, setIsInfoPanelDragging] = useState(false);
    const [infoPanelWidth, setInfoPanelWidth] = useState(INFO_PANEL_DEFAULT_WIDTH);
    const [isInfoPanelCollapsed, setIsInfoPanelCollapsed] = useState(true);
    const [isSkillDialogOpen, setIsSkillDialogOpen] = useState(false);
    const [editingSkill, setEditingSkill] = useState<Skill | undefined>(undefined);

    const layoutGraph = useExtendedAgentGraphLayout();

    const previousEntityRef = useRef<ExtendedAgentAnchorEntity | undefined>(undefined);
    const visualRootRef = useRef<HTMLDivElement>(null);
    const infoPanelRef = useRef<HTMLDivElement>(null);
    const infoPanelDragStateRef = useRef<{ pointerId: number; offsetX: number; offsetY: number } | null>(null);
    const infoPanelResizeStateRef = useRef<{ pointerId: number; startX: number; startWidth: number } | null>(null);
    const infoPanelResizeHandleRef = useRef<HTMLDivElement | null>(null);
    const lastFitSignatureRef = useRef<string>('');
    const reactFlowInstance = useReactFlow();

    const [playgroundEntity, setPlaygroundEntity] = useState<PlaygroundEntity | undefined>(undefined);

    const handleInfoPanelResizePointerMove = useCallback((event: PointerEvent) => {
        const resizeState = infoPanelResizeStateRef.current;

        if (!resizeState || resizeState.pointerId !== event.pointerId) {
            return;
        }

        const delta = resizeState.startX - event.clientX;
        const nextWidth = clamp(resizeState.startWidth + delta, INFO_PANEL_MIN_WIDTH, INFO_PANEL_MAX_WIDTH);

        setInfoPanelWidth(nextWidth);
    }, []);

    const handleInfoPanelResizePointerUp = useCallback(
        (event: PointerEvent) => {
            const resizeState = infoPanelResizeStateRef.current;

            if (!resizeState || resizeState.pointerId !== event.pointerId) {
                return;
            }

            infoPanelResizeHandleRef.current?.releasePointerCapture?.(event.pointerId);
            infoPanelResizeStateRef.current = null;
            infoPanelResizeHandleRef.current = null;

            window.removeEventListener('pointermove', handleInfoPanelResizePointerMove);
            window.removeEventListener('pointerup', handleInfoPanelResizePointerUp);
            window.removeEventListener('pointercancel', handleInfoPanelResizePointerUp);
        },
        [handleInfoPanelResizePointerMove]
    );

    const handleInfoPanelResizePointerDown = useCallback(
        (event: React.PointerEvent<HTMLDivElement>) => {
            if (event.button !== 0) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();

            infoPanelResizeStateRef.current = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startWidth: infoPanelWidth,
            };

            infoPanelResizeHandleRef.current = event.currentTarget;
            event.currentTarget.setPointerCapture?.(event.pointerId);

            window.addEventListener('pointermove', handleInfoPanelResizePointerMove);
            window.addEventListener('pointerup', handleInfoPanelResizePointerUp);
            window.addEventListener('pointercancel', handleInfoPanelResizePointerUp);
        },
        [infoPanelWidth, handleInfoPanelResizePointerMove, handleInfoPanelResizePointerUp]
    );

    const handleInfoPanelPointerMove = useCallback((event: PointerEvent) => {
        const dragState = infoPanelDragStateRef.current;
        if (!dragState || dragState.pointerId !== event.pointerId) {
            return;
        }

        if (!visualRootRef.current || !infoPanelRef.current) {
            return;
        }

        const containerRect = visualRootRef.current.getBoundingClientRect();
        const panelWidth = infoPanelRef.current.offsetWidth;
        const panelHeight = infoPanelRef.current.offsetHeight;

        const nextX = event.clientX - containerRect.left - dragState.offsetX;
        const nextY = event.clientY - containerRect.top - dragState.offsetY;

        const maxX = Math.max(containerRect.width - panelWidth, 0);
        const maxY = Math.max(containerRect.height - panelHeight, 0);

        setInfoPanelPosition({
            x: clamp(nextX, 0, maxX),
            y: clamp(nextY, 0, maxY),
        });
    }, []);

    const handleInfoPanelPointerUp = useCallback(
        function onPointerUp(event: PointerEvent) {
            const dragState = infoPanelDragStateRef.current;
            if (!dragState || dragState.pointerId !== event.pointerId) {
                return;
            }

            infoPanelRef.current?.releasePointerCapture?.(event.pointerId);
            infoPanelDragStateRef.current = null;
            setIsInfoPanelDragging(false);

            window.removeEventListener('pointermove', handleInfoPanelPointerMove);
            window.removeEventListener('pointerup', onPointerUp);
            window.removeEventListener('pointercancel', onPointerUp);
        },
        [handleInfoPanelPointerMove]
    );

    const handleInfoPanelPointerDown = useCallback(
        (event: React.PointerEvent<HTMLDivElement>) => {
            if (!visualRootRef.current || !infoPanelRef.current) {
                return;
            }

            event.preventDefault();

            const containerRect = visualRootRef.current.getBoundingClientRect();
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
            setIsInfoPanelDragging(true);
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

    useEffect(() => {
        return () => {
            window.removeEventListener('pointermove', handleInfoPanelPointerMove);
            window.removeEventListener('pointerup', handleInfoPanelPointerUp);
            window.removeEventListener('pointercancel', handleInfoPanelPointerUp);
            window.removeEventListener('pointermove', handleInfoPanelResizePointerMove);
            window.removeEventListener('pointerup', handleInfoPanelResizePointerUp);
            window.removeEventListener('pointercancel', handleInfoPanelResizePointerUp);
        };
    }, [handleInfoPanelPointerMove, handleInfoPanelPointerUp, handleInfoPanelResizePointerMove, handleInfoPanelResizePointerUp]);

    useEffect(() => {
        if (currentView !== ExtendedAgentGraphView.Canvas) {
            setIsInfoPanelFloating(false);
            setInfoPanelPosition({ x: 0, y: 0 });
            setIsInfoPanelDragging(false);
            infoPanelDragStateRef.current = null;
        }
    }, [currentView]);

    // Layout graph when data changes
    useEffect(() => {
        if (graphNodes.length === 0) {
            setNodes([]);
            setEdges([]);
            return;
        }

        const performLayout = async () => {
            setIsLayouting(true);

            try {
                const { nodes: layoutedNodes, edges: layoutedEdges } = await layoutGraph(graphNodes, graphEdges);
                setNodes(layoutedNodes);
                setEdges(layoutedEdges);
            } catch (err) {
                console.error('Error laying out graph:', err);
                setNodes(graphNodes);
                setEdges(graphEdges);
            } finally {
                setIsLayouting(false);
            }
        };

        performLayout();
    }, [graphNodes, graphEdges, layoutGraph, setNodes, setEdges]);

    useEffect(() => {
        if (currentView !== ExtendedAgentGraphView.Canvas) {
            return;
        }

        if (loading || isLayouting || nodes.length === 0) {
            return;
        }

        if (typeof reactFlowInstance?.fitView !== 'function') {
            return;
        }

        const signature = nodes
            .map(node => `${node.id}:${Math.round(node.position?.x ?? 0)}:${Math.round(node.position?.y ?? 0)}`)
            .join('|');

        if (!signature || signature === lastFitSignatureRef.current) {
            return;
        }

        lastFitSignatureRef.current = signature;

        // Skip fitView if this change was from expanding/collapsing a toolbox or skill group
        if (skipNextFitViewRef.current) {
            skipNextFitViewRef.current = false;
            return;
        }

        const raf = window.requestAnimationFrame(() => {
            try {
                reactFlowInstance.fitView({ padding: 0.25, duration: 400, minZoom: 0.1, maxZoom: 1.5 });
            } catch (err) {
                console.warn('Failed to fit view', err);
            }
        });

        return () => {
            window.cancelAnimationFrame(raf);
        };
    }, [currentView, isLayouting, loading, nodes, reactFlowInstance, skipNextFitViewRef]);

    useEffect(() => {
        setAnchorEntity(prevAnchorEntity => {
            if (!loading && !prevAnchorEntity) {
                if (agents.length > 0) {
                    return {
                        entityType: 'Agent',
                        entityName: agents[0].name,
                    };
                }
                if (triggers.length > 0) {
                    return {
                        entityType: 'Trigger',
                        entityName: triggers[0].name,
                    };
                }
            }
            return prevAnchorEntity;
        });
    }, [loading, agents, triggers, setAnchorEntity]);

    // Handle navigation state - set anchor entity from location state if provided
    useEffect(() => {
        if (!loading && location.state?.anchorEntity) {
            const stateEntity = location.state.anchorEntity as ExtendedAgentAnchorEntity;
            setAnchorEntity(stateEntity);
            // Clear the state after using it
            navigate(location.pathname, { replace: true, state: {} });
        }
    }, [loading, location.state, location.pathname, navigate, setAnchorEntity]);

    useEffect(() => {
        if (loading) {
            return;
        }

        if (!anchorEntity) {
            return;
        }

        const hasAgent = anchorEntity.entityType === 'Agent' && agents.some(agent => agent.name === anchorEntity.entityName);
        const hasTrigger = anchorEntity.entityType === 'Trigger' && triggers.some(trigger => trigger.name === anchorEntity.entityName);

        if (hasAgent || hasTrigger) {
            return;
        }

        if (
            pendingEntitySelection &&
            pendingEntitySelection.entityType === anchorEntity.entityType &&
            pendingEntitySelection.entityName === anchorEntity.entityName
        ) {
            return;
        }

        if (agents.length === 0 && triggers.length === 0) {
            setAnchorEntity(undefined);
            return;
        }

        if (agents.length > 0) {
            setAnchorEntity({
                entityType: 'Agent',
                entityName: agents[0].name,
            });
            return;
        }
        if (triggers.length > 0) {
            setAnchorEntity({
                entityType: 'Trigger',
                entityName: triggers[0].name,
            });
            return;
        }
    }, [agents, loading, pendingEntitySelection, setAnchorEntity]);

    useEffect(() => {
        if (!pendingEntitySelection) {
            return;
        }

        if (pendingEntitySelection.entityType === 'Trigger') {
            if (triggers.some(trigger => trigger.name === pendingEntitySelection.entityName)) {
                setAnchorEntity({ entityType: 'Trigger', entityName: pendingEntitySelection.entityName });
                setPendingEntitySelection(undefined);
            }
            return;
        }

        if (pendingEntitySelection.entityType === 'Agent') {
            if (agents.some(agent => agent.name === pendingEntitySelection.entityName)) {
                setAnchorEntity({ entityType: 'Agent', entityName: pendingEntitySelection.entityName });
                setPendingEntitySelection(undefined);
            }
        }
    }, [agents, pendingEntitySelection, setPendingEntitySelection, setAnchorEntity]);

    useEffect(() => {
        const prevSelectedNodeId = selectedNodeIdRef.current;
        const activeEntity = anchorEntity;

        if (!activeEntity) {
            previousEntityRef.current = undefined;
            setSelectedNodeId(prevSelectedNodeId ? undefined : prevSelectedNodeId);
            return;
        }

        const primaryNode = graphNodes.find(node => {
            const searchIdPrefix = activeEntity.entityType === 'Agent' ? 'agent_' : 'trigger_';
            return node.id === `${searchIdPrefix}${activeEntity.entityName}`;
        });

        if (!primaryNode) {
            if (graphNodes.length === 0) {
                // Wait for layout to supply nodes before updating selection
                return;
            }

            previousEntityRef.current = activeEntity;
            setSelectedNodeId(prevSelectedNodeId ? undefined : prevSelectedNodeId);
            return;
        }

        const entityChanged = previousEntityRef.current !== activeEntity;
        const prevSelectedNodeExists = doesNodeExistInGraph(graphNodes, prevSelectedNodeId);

        if (prevSelectedNodeExists) {
            previousEntityRef.current = activeEntity;
            setSelectedNodeId(entityChanged ? primaryNode.data.id : prevSelectedNodeId);
            return;
        }

        const alreadySelectedSameNode = prevSelectedNodeId === primaryNode.data.id;
        if (entityChanged || !alreadySelectedSameNode) {
            previousEntityRef.current = activeEntity;
            setSelectedNodeId(primaryNode.data.id);
            return;
        }
        setSelectedNodeId(prevSelectedNodeId);
    }, [anchorEntity, graphNodes, setSelectedNodeId]);

    const relationshipAgent = useMemo(
        () => (relationshipAgentName ? agents.find(agent => agent.name === relationshipAgentName) : undefined),
        [agents, relationshipAgentName]
    );

    const creationDialogInitialType = useMemo(
        () => (creationDialogContext?.kind === 'linkFromAgent' ? creationDialogContext.targetType : creationDialogInitialTypeOverride),
        [creationDialogContext, creationDialogInitialTypeOverride]
    );

    const creationDialogNotice = useMemo(() => {
        if (!creationDialogContext || creationDialogContext.kind !== 'linkFromAgent') {
            return undefined;
        }

        if (creationDialogContext.targetType === 'agent') {
            return intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateAgentReminder, {
                agentName: creationDialogContext.sourceAgentName,
            });
        }

        return intl.formatMessage(ExtendedAgentsGraphResources.relationshipContextToolSubtext, {
            agentName: creationDialogContext.sourceAgentName,
        });
    }, [creationDialogContext, intl]);

    const creationDialogLinkContext = useMemo(() => {
        if (creationDialogContext?.kind !== 'linkFromAgent') {
            return undefined;
        }

        return {
            sourceAgentName: creationDialogContext.sourceAgentName,
            targetType: creationDialogContext.targetType,
        } as const;
    }, [creationDialogContext]);

    const handleEditKustoTool = useCallback((tool: ExtendedTool) => {
        setCreateToolAgent(undefined);
        setToolDialogMode(KustoToolDialogMode.Edit);
        setToolToEdit(tool);
        setIsToolDialogOpen(true);
    }, []);

    const handleEditPythonTool = useCallback((tool: ExtendedTool) => {
        setPythonToolAgentName(undefined);
        setPythonToolDialogMode(PythonToolDialogMode.Edit);
        setPythonToolToEdit(tool);
        setIsPythonToolDialogOpen(true);
    }, []);

    const handleCreatePythonTool = useCallback((agentName?: string) => {
        setPythonToolAgentName(agentName);
        setPythonToolDialogMode(PythonToolDialogMode.Create);
        setPythonToolToEdit(undefined);
        setIsPythonToolDialogOpen(true);
    }, []);

    const handleSaveSkill = useCallback(
        async (skill: Skill) => {
            const client = ExtendedAgentClient.getInstance(sreAgentEndpoint);

            // If meta_agent override exists and doesn't have vanilla_mode enabled, update it
            const metaAgent = agents.find(a => a.name === 'meta_agent');
            if (metaAgent && !metaAgent.enableVanillaMode) {
                const updatedMetaAgent: ExtendedAgent = {
                    ...metaAgent,
                    enableVanillaMode: true,
                };
                await client.applyEntity(updatedMetaAgent, 'agent');
            }

            const response = await client.createOrUpdateSkill(skill);

            if (response.isSuccessful) {
                await refetch();
                return { success: true };
            } else {
                return { success: false, error: String(response.error) };
            }
        },
        [sreAgentEndpoint, refetch, agents]
    );

    const incidentHandlersCount = useMemo(
        () => (incidentHandlersHook.incidentHandlersLoading ? null : (incidentHandlersHook.incidentHandlers?.length ?? 0)),
        [incidentHandlersHook]
    );

    const scheduledTasksCount = useMemo(
        () => (features.scheduledTasks ? (scheduledTasksHook.loading ? null : scheduledTasksHook.scheduledTasks?.length) : null),
        [features.scheduledTasks, scheduledTasksHook]
    );

    const triggerCardConfig = useMemo(
        () => ({
            isLoading: incidentHandlersHook.incidentHandlersLoading || (features.scheduledTasks && scheduledTasksHook.loading),
            incidentHandlersCount,
            scheduledTasksCount,
            hasScheduledTasksFeature: features.scheduledTasks,
        }),
        [
            features.scheduledTasks,
            incidentHandlersCount,
            incidentHandlersHook.incidentHandlersLoading,
            scheduledTasksCount,
            scheduledTasksHook.loading,
        ]
    );

    const creationSuccessMessage = useMemo(() => {
        if (!creationSuccess) {
            return undefined;
        }

        if (creationSuccess.entityType === 'agent') {
            if (creationSuccess.sourceAgentName) {
                return intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAgentSuccess, {
                    agentName: creationSuccess.entityName,
                    sourceAgentName: creationSuccess.sourceAgentName,
                });
            }

            return intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAgentSuccessNoSource, {
                agentName: creationSuccess.entityName,
            });
        }

        if (creationSuccess.entityType === 'tool' && creationSuccess.sourceAgentName) {
            return intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickCreateToolSuccess, {
                toolName: creationSuccess.entityName,
                agentName: creationSuccess.sourceAgentName,
            });
        }

        return undefined;
    }, [creationSuccess, intl]);

    const handleEntitySelect = useCallback(
        (entity?: ExtendedAgentAnchorEntity) => {
            setAnchorEntity(entity);
            const entityNodeId = !entity
                ? undefined
                : entity.entityType === 'Agent'
                  ? `agent_${entity.entityName}`
                  : `trigger_${entity.entityName}`;
            const targetNode = !entityNodeId ? undefined : nodes.find(node => node.id === entityNodeId);
            if (targetNode) {
                requestAnimationFrame(() => {
                    setIsInfoPanelCollapsed(false);
                    reactFlowInstance.fitView({
                        nodes: [{ id: targetNode.id }],
                        duration: 600,
                        padding: 0.1,
                    });
                });
            }
        },
        [setAnchorEntity, nodes, reactFlowInstance]
    );

    const handleRefresh = useCallback(() => {
        return refetch();
    }, [refetch]);

    const onChangeViewType = useCallback((view: ExtendedAgentGraphView) => {
        if (view !== ExtendedAgentGraphView.Playground) {
            setPlaygroundEntity(undefined);
        }
        setCurrentView(view);
    }, []);

    const applyEntity = useCallback(
        async (
            data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
            type: 'agent' | 'tool' | 'connector',
            options?: { refreshMode?: 'reload' | 'refetch'; suppressAlert?: boolean; refetchDelayMs?: number }
        ) => {
            try {
                // Generate the YAML content (this may include multiple documents for meta agent override)
                let yamlContent = convertExtendedEntityToYaml(data, type);

                // If this is an agent with meta agent override enabled, append the meta agent YAML
                if (type === 'agent' && (data as any)?.metaAgentOverride) {
                    const agentData = data as Partial<ExtendedAgent>;

                    // Create the user's agent first (without meta agent override flag for YAML generation)
                    const userAgentData = { ...agentData };
                    delete (userAgentData as any).metaAgentOverride;
                    const userAgentYaml = convertExtendedEntityToYaml(userAgentData, type);

                    // Get the meta agent YAML (which already starts with ---)
                    const metaAgentYaml = buildMetaAgentYaml();

                    // Combine documents - userAgentYaml doesn't have --- at start, metaAgentYaml does
                    yamlContent = userAgentYaml.trim() + '\n' + metaAgentYaml;
                }

                // Split YAML content on document separators and apply each document
                // Handle both cases: documents that start with --- and those that don't
                let yamlDocuments: string[];
                if (yamlContent.includes('\n---\n')) {
                    // Multi-document YAML with separators
                    yamlDocuments = yamlContent.split(/\n---\n/).filter(doc => doc.trim().length > 0);
                } else {
                    // Single document
                    yamlDocuments = [yamlContent];
                }

                console.log(`Meta agent override: Processing ${yamlDocuments.length} YAML documents`);
                if (type === 'agent' && (data as any)?.metaAgentOverride) {
                    console.log(
                        'Documents found:',
                        yamlDocuments.map((doc, i) => `Doc ${i + 1}: ${doc.substring(0, 100)}...`)
                    );
                }

                for (let i = 0; i < yamlDocuments.length; i++) {
                    const document = yamlDocuments[i].trim();
                    if (!document) continue;

                    // Remove any leading --- from individual documents as API expects clean YAML
                    const cleanDocument = document.replace(/^---\s*\n?/, '').trim();

                    console.log(`Applying document ${i + 1}/${yamlDocuments.length}:`, cleanDocument.substring(0, 200) + '...');

                    const agentHeaders = getAgentHeaders();
                    const { 'Content-Type': _, ...headersWithoutContentType } = agentHeaders;

                    const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/apply`, {
                        method: 'PUT',
                        headers: {
                            ...headersWithoutContentType,
                            'Content-Type': 'application/x-yaml',
                        },
                        body: cleanDocument,
                    });

                    if (!response.ok) {
                        const errorText = await response.text();
                        throw new Error(`Failed to apply document ${i + 1}/${yamlDocuments.length}: ${response.status} - ${errorText}`);
                    }
                }

                if (options?.refreshMode === 'refetch') {
                    await refetch();

                    if (options?.refetchDelayMs) {
                        setTimeout(() => {
                            refetch();
                        }, options.refetchDelayMs);
                    }
                } else {
                    window.location.reload();
                }
            } catch (error) {
                console.error('Error creating entity:', error);

                if (!options?.suppressAlert) {
                    alert(`Error creating ${type}: ${error instanceof Error ? error.message : 'Unknown error'}`);
                }

                throw error;
            }
        },
        [refetch, sreAgentEndpoint]
    );

    const handleCreateEntity = useCallback(
        async (
            data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
            type: 'agent' | 'tool' | 'connector' | 'trigger' | 'skill'
        ) => {
            // Trigger and skill types don't create entities in the graph through this handler
            if (type === 'trigger' || type === 'skill') {
                return;
            }

            const previousAgentName = anchorEntity?.entityType === 'Agent' ? anchorEntity?.entityName : undefined;

            await applyEntity(data, type, { refreshMode: 'refetch', refetchDelayMs: 2000 });

            if (type === 'agent') {
                const agentName = (data as Partial<ExtendedAgent>).name?.trim();

                if (agentName) {
                    setAnchorEntity({ entityType: 'Agent', entityName: agentName });

                    if (!agents.some(agent => agent.name === agentName)) {
                        setPendingEntitySelection({ entityType: 'Agent', entityName: agentName });
                    } else {
                        setPendingEntitySelection(undefined);
                    }

                    setCreationSuccess({ entityType: 'agent', entityName: agentName });
                }
                return;
            }

            if (type === 'tool') {
                const toolName = (data as Partial<ExtendedTool>).name?.trim();

                if (previousAgentName) {
                    setAnchorEntity({ entityType: 'Agent', entityName: previousAgentName });
                }

                if (toolName && previousAgentName) {
                    setCreationSuccess({
                        entityType: 'tool',
                        entityName: toolName,
                        sourceAgentName: previousAgentName,
                    });
                }
                return;
            }

            if (type === 'connector') {
                const connectorName = (data as Partial<ExtendedConnector>).name?.trim();

                if (connectorName) {
                    setCreationSuccess({ entityType: 'connector', entityName: connectorName });
                }
            }
        },
        [agents, applyEntity, anchorEntity, setCreationSuccess, setAnchorEntity, setPendingEntitySelection]
    );

    const addHandoffToAgent = useCallback(
        async (targetAgentName: string, handoffName: string): Promise<OperationResult> => {
            const currentAgent = agents.find(agent => agent.name === targetAgentName);

            if (!currentAgent) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected),
                };
            }

            if (currentAgent.handoffs?.includes(handoffName)) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickAlreadyHandoff, {
                        handoffName,
                        agentName: currentAgent.name,
                    }),
                };
            }

            const updatedAgent: ExtendedAgent = {
                ...currentAgent,
                handoffs: [...(currentAgent.handoffs ?? []), handoffName],
            };

            try {
                await applyEntity(updatedAgent, 'agent', {
                    refreshMode: 'refetch',
                    suppressAlert: true,
                    refetchDelayMs: 2000,
                });

                return {
                    success: true,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickAddHandoffSuccess, {
                        handoffName,
                        agentName: currentAgent.name,
                    }),
                };
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                return { success: false, message };
            }
        },
        [agents, applyEntity, intl]
    );

    const handleAddExistingHandoff = useCallback(
        async (handoffName: string): Promise<OperationResult> => {
            if (!relationshipAgentName) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected),
                };
            }

            return addHandoffToAgent(relationshipAgentName, handoffName);
        },
        [addHandoffToAgent, intl, relationshipAgentName]
    );

    const addToolToAgent = useCallback(
        async (targetAgentName: string, toolName: string): Promise<OperationResult> => {
            const normalizeList = (values?: (string | null | undefined)[]) =>
                (values ?? [])
                    .map(value => (typeof value === 'string' ? value.trim() : ''))
                    .filter((value): value is string => value.length > 0);

            const normalizedToolName = toolName.trim();
            const currentAgent = agents.find(agent => agent.name === targetAgentName);

            if (!currentAgent) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected),
                };
            }

            if (!normalizedToolName) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipNameRequired),
                };
            }

            const toolDefinition = tools.find(tool => tool.name.trim() === normalizedToolName);
            const systemToolDefinition = systemTools.find(tool => tool.name.trim() === normalizedToolName);

            if (!toolDefinition && !systemToolDefinition) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickToolMissing, {
                        toolName: normalizedToolName,
                    }),
                };
            }

            const existingTools = normalizeList(currentAgent.tools);
            const existingSystemTools = normalizeList(currentAgent.systemTools);

            if (existingTools.includes(normalizedToolName) || existingSystemTools.includes(normalizedToolName)) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickAlreadyTool, {
                        toolName: normalizedToolName,
                        agentName: currentAgent.name,
                    }),
                };
            }

            let updatedAgent: ExtendedAgent;

            if (toolDefinition) {
                const sanitizedConnectors = normalizeList(currentAgent.connectors);
                const connectorToAdd = toolDefinition.connector?.trim();
                const updatedConnectors =
                    connectorToAdd && !sanitizedConnectors.includes(connectorToAdd)
                        ? [...sanitizedConnectors, connectorToAdd]
                        : sanitizedConnectors;

                updatedAgent = {
                    ...currentAgent,
                    tools: [...existingTools, normalizedToolName],
                    connectors: updatedConnectors,
                };
            } else {
                const updatedSystemTools = [...existingSystemTools, normalizedToolName];

                // Avoid mutating regular tools list; ensure the system tool isn't duplicated there.
                const filteredTools = existingTools.filter(tool => tool !== normalizedToolName);

                updatedAgent = {
                    ...currentAgent,
                    tools: filteredTools,
                    systemTools: updatedSystemTools,
                };
            }

            try {
                await applyEntity(updatedAgent, 'agent', {
                    refreshMode: 'refetch',
                    suppressAlert: true,
                    refetchDelayMs: 2000,
                });

                return {
                    success: true,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickAddToolSuccess, {
                        toolName: normalizedToolName,
                        agentName: currentAgent.name,
                    }),
                };
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                return { success: false, message };
            }
        },
        [agents, applyEntity, intl, systemTools, tools]
    );

    const addToolsToAgent = useCallback(
        async (targetAgentName: string, nonMcpToolNames: string[], mcpToolNames: string[]): Promise<OperationResult> => {
            const currentAgent = agents.find(agent => agent.name === targetAgentName);

            if (!currentAgent) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected),
                };
            }

            const filteredMcpToolNames = mcpToolNames.filter(name => !currentAgent.mcpTools?.includes(name));

            const filteredNonMcpToolNames = nonMcpToolNames.filter(
                name => !currentAgent.tools?.includes(name) && !currentAgent.systemTools?.includes(name)
            );

            const updatedAgent: ExtendedAgent = {
                ...currentAgent,
                tools: [...(currentAgent.tools ?? []), ...filteredNonMcpToolNames],
                mcpTools: [...(currentAgent.mcpTools ?? []), ...filteredMcpToolNames],
            };

            try {
                await applyEntity(updatedAgent, 'agent', {
                    refreshMode: 'refetch',
                    suppressAlert: true,
                    refetchDelayMs: 2000,
                });

                return {
                    success: true,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickAddToolsSuccess, {
                        agentName: currentAgent.name,
                    }),
                };
            } catch (error) {
                const message = error instanceof Error ? error.message : String(error);
                return { success: false, message };
            }
        },
        [agents, applyEntity, intl, mcpConnections]
    );

    const handleAddExistingTool = useCallback(
        async (toolName: string): Promise<OperationResult> => {
            if (!relationshipAgentName) {
                return {
                    success: false,
                    message: intl.formatMessage(ExtendedAgentsGraphResources.relationshipQuickNoAgentSelected),
                };
            }

            return addToolToAgent(relationshipAgentName, toolName);
        },
        [addToolToAgent, intl, relationshipAgentName]
    );

    const handleCreateAndLinkEntity = useCallback(
        async (
            data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
            type: 'agent' | 'tool' | 'connector' | 'trigger' | 'skill'
        ) => {
            // Trigger and skill types are handled separately, just create without linking
            if (type === 'trigger' || type === 'skill') {
                return;
            }
            if (!creationDialogContext || creationDialogContext.kind !== 'linkFromAgent') {
                await handleCreateEntity(data, type);
                return;
            }

            const { targetType, sourceAgentName } = creationDialogContext;

            if (type !== targetType) {
                await handleCreateEntity(data, type);
                return;
            }

            if (type === 'agent') {
                const agentDraft = data as Partial<ExtendedAgent>;

                if (!agentDraft.name || !agentDraft.instructions) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.relationshipAgentFieldsRequired);
                    throw new Error(message);
                }

                await applyEntity(agentDraft, 'agent', {
                    refreshMode: 'refetch',
                    suppressAlert: true,
                    refetchDelayMs: 2000,
                });

                if (agentDraft.name) {
                    setAnchorEntity({ entityType: 'Agent', entityName: agentDraft.name! });

                    if (!agents.some(agent => agent.name === agentDraft.name)) {
                        setPendingEntitySelection({ entityType: 'Agent', entityName: agentDraft.name });
                    } else {
                        setPendingEntitySelection(undefined);
                    }
                }

                const connectResult = await addHandoffToAgent(sourceAgentName, agentDraft.name);

                if (!connectResult.success) {
                    setCreationSuccess(undefined);
                    setLinkRetryContext({
                        entityType: 'agent',
                        entityName: agentDraft.name,
                        sourceAgentName,
                        lastError: connectResult.message,
                    });
                    return;
                }

                setLinkRetryContext(undefined);
                setCreationSuccess({
                    entityType: 'agent',
                    entityName: agentDraft.name,
                    sourceAgentName,
                });
                return;
            }

            if (type === 'tool') {
                const toolDraft = data as Partial<ExtendedTool>;

                if (!toolDraft.name) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.relationshipNameRequired);
                    throw new Error(message);
                }

                setAnchorEntity({ entityType: 'Agent', entityName: sourceAgentName });

                await applyEntity(toolDraft, 'tool', {
                    refreshMode: 'refetch',
                    suppressAlert: true,
                    refetchDelayMs: 2000,
                });

                const connectResult = await addToolToAgent(sourceAgentName, toolDraft.name);

                if (!connectResult.success) {
                    setCreationSuccess(undefined);
                    setLinkRetryContext({
                        entityType: 'tool',
                        entityName: toolDraft.name,
                        sourceAgentName,
                        lastError: connectResult.message,
                    });
                    return;
                }

                setLinkRetryContext(undefined);
                setCreationSuccess({
                    entityType: 'tool',
                    entityName: toolDraft.name,
                    sourceAgentName,
                });
                return;
            }

            await handleCreateEntity(data, type);
        },
        [
            addHandoffToAgent,
            addToolToAgent,
            agents,
            applyEntity,
            creationDialogContext,
            handleCreateEntity,
            intl,
            setAnchorEntity,
            setPendingEntitySelection,
        ]
    );

    const handleAgentQuickAction = useCallback(
        (agentName: string, action: AgentQuickAction) => {
            if (action === 'addIncidentTrigger') {
                setHandlerCreateOrEditInfo({
                    subAgentTriggerInfo: {
                        agents: agents.map(a => a.name),
                        preSelectedAgent: agentName,
                    },
                });
                return;
            }

            if (action === 'addScheduledTask') {
                setIsScheduledTaskDialogOpen(true);
                setScheduledTaskDialogMode(ScheduledTaskDialogMode.Create);
                setScheduledTaskStartingAgent(agentName);
                setScheduledTaskEditingTask(undefined);
                return;
            }

            if (action === 'addHandoffSourceExistingAgent' || action === 'addHandoffTargetExistingAgent') {
                setAgentHandoffPickerInfo({
                    mode: action === 'addHandoffSourceExistingAgent' ? 'sourcePicker' : 'targetPicker',
                    currentAgent: agents.find(a => a.name === agentName)!,
                });
                return;
            }

            if (action === 'addTool') {
                const agent = agents.find(a => a.name === agentName)!;
                setToolPickerInfo({ agent });
                return;
            }

            if (action === 'createTool') {
                const agent = agents.find(a => a.name === agentName)!;
                setCreateToolAgent(agent.name);
                setToolDialogMode(KustoToolDialogMode.Create);
                setToolToEdit(undefined);
                setIsToolDialogOpen(true);
                return;
            }

            if (action === 'createPythonTool') {
                handleCreatePythonTool(agentName);
                return;
            }

            if (action === 'createHandoffSourceAgent' || action === 'createHandoffTargetAgent' || action === 'editAgent') {
                const agent = agents.find(a => a.name === agentName)!;
                setAgentCreateOrEditInfo({
                    agent: agent,
                    mode:
                        action === 'createHandoffSourceAgent'
                            ? 'createSource'
                            : action === 'createHandoffTargetAgent'
                              ? 'createTarget'
                              : 'edit',
                });
                return;
            }

            if (action === 'createAgent') {
                setCreationDialogContext({
                    kind: 'linkFromAgent',
                    sourceAgentName: agentName,
                    targetType: action === 'createAgent' ? 'agent' : 'tool',
                });
                setIsCreationDialogOpen(true);
                return;
            }

            setRelationshipAgentName(agentName);
            setRelationshipInitialAction(action === 'addHandoff' ? 'handoff' : 'tool');
            setIsRelationshipDialogOpen(true);
        },
        [agents, handleCreatePythonTool]
    );

    const handleTriggerQuickAction = useCallback(
        (triggerName: string, action: TriggerQuickAction) => {
            if (action === 'editTrigger') {
                const trigger = triggers.find(a => a.name === triggerName);
                if (!trigger) {
                    return;
                }

                if (trigger.type === 'incident') {
                    const incidentHandler = incidentHandlersHook.incidentHandlers?.find(
                        handler => handler.name === triggerName || handler.id === triggerName
                    );
                    if (incidentHandler) {
                        const incidentFilter = incidentHandler?.incidentFilterId
                            ? incidentFiltersHook.incidentFilters?.find(filter => filter.id === incidentHandler.incidentFilterId)
                            : undefined;

                        if (incidentFilter) {
                            setHandlerCreateOrEditInfo({
                                filter: incidentFilter,
                                handlerId: incidentHandler.id,
                                subAgentTriggerInfo: {
                                    agents: agents.map(a => a.name),
                                },
                            });
                            return;
                        }
                    }

                    const incidentFilter = incidentFiltersHook.incidentFilters?.find(filter => filter.id === triggerName);
                    if (incidentFilter) {
                        setHandlerCreateOrEditInfo({
                            filter: incidentFilter,
                            subAgentTriggerInfo: {
                                agents: agents.map(a => a.name),
                            },
                        });
                    }
                } else if (trigger.type === 'scheduled') {
                    const scheduledTask = scheduledTasksHook.scheduledTasks?.find(
                        task => task.name === triggerName || task.id === triggerName
                    );
                    if (scheduledTask) {
                        setIsScheduledTaskDialogOpen(true);
                        setScheduledTaskDialogMode(ScheduledTaskDialogMode.Edit);
                        setScheduledTaskStartingAgent(scheduledTask?.agent);
                        setScheduledTaskEditingTask(scheduledTask);
                    }
                }

                return;
            }
        },
        [triggers, agents, incidentHandlersHook, incidentFiltersHook, scheduledTasksHook]
    );

    const handleLaunchLinkedCreation = useCallback((targetType: 'agent' | 'tool', sourceAgentName: string) => {
        setCreationDialogContext({
            kind: 'linkFromAgent',
            sourceAgentName,
            targetType,
        });
        setIsRelationshipDialogOpen(false);
        setIsCreationDialogOpen(true);
    }, []);

    const openRelationshipDialog = useCallback((agentName: string) => {
        setRelationshipAgentName(agentName);
        setRelationshipInitialAction(undefined);
        setIsRelationshipDialogOpen(true);
    }, []);

    const handleRelationshipDialogOpenChange = useCallback((open: boolean) => {
        setIsRelationshipDialogOpen(open);

        if (!open) {
            setRelationshipAgentName(undefined);
            setRelationshipInitialAction(undefined);
        }
    }, []);

    const handleCreationDialogOpenChange = useCallback(
        (open: boolean) => {
            setIsCreationDialogOpen(open);

            if (!open) {
                setCreationDialogContext(undefined);
                setCreationDialogInitialTypeOverride(undefined);
                setCreationDialogInitialToolType(undefined);
                setCreationDialogTriggerAgentName(undefined);
            }
        },
        [
            setIsCreationDialogOpen,
            setCreationDialogContext,
            setCreationDialogInitialTypeOverride,
            setCreationDialogInitialToolType,
            setCreationDialogTriggerAgentName,
        ]
    );

    const handleDismissCreationSuccess = useCallback(() => {
        setCreationSuccess(undefined);
    }, []);

    const handleRetryLink = useCallback(async () => {
        if (!linkRetryContext) {
            return;
        }

        setIsRetryingLink(true);

        try {
            const { entityType, entityName, sourceAgentName } = linkRetryContext;

            const result =
                entityType === 'agent'
                    ? await addHandoffToAgent(sourceAgentName, entityName)
                    : await addToolToAgent(sourceAgentName, entityName);

            if (!result.success) {
                setLinkRetryContext(prev => (prev ? { ...prev, lastError: result.message } : prev));
                return;
            }

            setLinkRetryContext(undefined);
            setCreationSuccess({
                entityType,
                entityName,
                sourceAgentName,
            });
        } finally {
            setIsRetryingLink(false);
        }
    }, [addHandoffToAgent, addToolToAgent, linkRetryContext]);

    const handleDismissLinkError = useCallback(() => {
        setLinkRetryContext(undefined);
    }, []);

    const handleIncidentManagementClick = useCallback(() => {
        agentSiteNavigate({
            primaryNavItemValue: PrimaryNavItemValues.Activities,
            secondaryNavItemValue: SecondaryNavItemValues.IncidentOverview,
        });
        setCreationSuccess(undefined);
    }, [agentSiteNavigate]);

    const handleScheduledTasksClick = useCallback(() => {
        agentSiteNavigate({
            primaryNavItemValue: PrimaryNavItemValues.Builder,
            secondaryNavItemValue: SecondaryNavItemValues.ScheduledTasks,
        });
        setCreationSuccess(undefined);
    }, [agentSiteNavigate]);

    const handleConnectorNavigate = useCallback(() => {
        agentSiteNavigate({
            primaryNavItemValue: PrimaryNavItemValues.Settings,
            secondaryNavItemValue: SecondaryNavItemValues.Connectors,
        });
        setIsCreationDialogOpen(false);
    }, [agentSiteNavigate]);

    const handleTriggerNavigate = useCallback(
        (destination: 'incidentManagement' | 'scheduledTasks') => {
            if (destination === 'incidentManagement') {
                handleIncidentManagementClick();
            } else {
                handleScheduledTasksClick();
            }
        },
        [handleIncidentManagementClick, handleScheduledTasksClick]
    );

    const handleTestAgentClick = useCallback(() => {
        if (creationSuccess?.entityType === 'agent' && creationSuccess?.entityName) {
            // Navigate to threads with extended agent parameter for testing
            navigate(`/views/threads?testAgent=${encodeURIComponent(creationSuccess.entityName)}`);
        }
    }, [navigate, creationSuccess]);

    const handleAddTriggerForAgent = useCallback(() => {
        if (!creationSuccess || creationSuccess.entityType !== 'agent') {
            return;
        }

        const agentName = creationSuccess.entityName?.trim();
        if (!agentName) {
            return;
        }

        setCreationDialogContext(undefined);
        setCreationDialogInitialTypeOverride('trigger');
        setCreationDialogTriggerAgentName(agentName);
        setCreationSuccess(undefined);
        setIsCreationDialogOpen(true);
    }, [
        creationSuccess,
        setCreationDialogContext,
        setCreationDialogInitialTypeOverride,
        setCreationDialogTriggerAgentName,
        setCreationSuccess,
        setIsCreationDialogOpen,
    ]);

    const handleAddToolForAgent = useCallback(() => {
        if (!creationSuccess || creationSuccess.entityType !== 'agent') {
            return;
        }

        const agentName = creationSuccess.entityName?.trim();
        if (!agentName) {
            return;
        }

        setCreationDialogInitialTypeOverride(undefined);
        setCreationDialogTriggerAgentName(undefined);
        setCreationDialogContext({
            kind: 'linkFromAgent',
            sourceAgentName: agentName,
            targetType: 'tool',
        });
        setCreationSuccess(undefined);
        setIsCreationDialogOpen(true);
    }, [
        creationSuccess,
        setCreationDialogContext,
        setCreationDialogInitialTypeOverride,
        setCreationDialogTriggerAgentName,
        setCreationSuccess,
        setIsCreationDialogOpen,
    ]);

    const isLoading = useMemo(() => loading || isLayouting, [loading, isLayouting]);
    const hasSkills = useMemo(() => skills.length > 0, [skills.length]);
    const hasAgents = useMemo(() => agents.length > 0, [agents.length]);
    // Check for subagents excluding the meta_agent override (for skill creation logic)
    const hasSubagents = useMemo(() => agents.some(agent => agent.name !== 'meta_agent'), [agents]);
    const hasTools = useMemo(() => tools.length > 0, [tools.length]);
    const hasConnectors = useMemo(() => connectors.length > 0, [connectors.length]);
    const hasSystemTools = useMemo(() => systemTools.length > 0, [systemTools.length]);
    const hasAnyResources = useMemo(
        () => hasAgents || hasTools || hasConnectors || hasSystemTools || hasSkills,
        [hasAgents, hasTools, hasConnectors, hasSystemTools, hasSkills]
    );
    const hasData = useMemo(() => graphNodes.length > 0, [graphNodes.length]);

    const infoPanelStyle: React.CSSProperties = useMemo(() => {
        const style = {
            width: `${infoPanelWidth}px`,
        } as React.CSSProperties;

        if (isInfoPanelFloating) {
            style.transform = `translate(${infoPanelPosition.x}px, ${infoPanelPosition.y}px)`;
            if (isInfoPanelDragging) {
                style.cursor = 'grabbing';
            }
        }

        return style;
    }, [infoPanelWidth, isInfoPanelFloating, infoPanelPosition.x, infoPanelPosition.y, isInfoPanelDragging]);

    const renderGraphContent = useCallback(() => {
        if (isLoading) {
            return <Spinner size={'large'} className={spinner} />;
        }

        if (error) {
            return (
                <div style={{ padding: '20px', textAlign: 'center' }}>
                    <p>{intl.formatMessage(ExtendedAgentsGraphResources.errorLoadingGraph, { error })}</p>
                </div>
            );
        }

        // Show prompt to select an agent only if there are agents to select from
        // If only skills exist (no agents), skip this check and show the graph
        if (!anchorEntity?.entityName && hasAgents) {
            return (
                <div style={{ padding: '20px' }}>
                    <MessageBar intent="info">
                        <MessageBarBody>{intl.formatMessage(ExtendedAgentsGraphResources.selectAgentPrompt)}</MessageBarBody>
                    </MessageBar>
                </div>
            );
        }

        if (!hasData) {
            return (
                <div style={{ padding: '20px' }}>
                    <MessageBar intent="warning">
                        <MessageBarBody>{intl.formatMessage(ExtendedAgentsGraphResources.noResultsForFilters)}</MessageBarBody>
                    </MessageBar>
                </div>
            );
        }

        if (currentView === ExtendedAgentGraphView.Canvas) {
            return (
                <ReactFlow
                    style={{ width: '100%', height: '100%' }}
                    fitView
                    fitViewOptions={{
                        padding: 0.3,
                        maxZoom: 1.2,
                    }}
                    nodeTypes={{
                        [EXTENDED_AGENT_CARD_TYPE]: ExtendedAgentCard,
                        [TOOL_CARD_TYPE]: ToolCard,
                        [CONNECTOR_CARD_TYPE]: ConnectorCard,
                        [TRIGGER_CARD_TYPE]: TriggerCard,
                        [SKILL_CARD_TYPE]: SkillCard,
                        [SKILL_GROUP_CARD_TYPE]: SkillGroupCard,
                        [EXPANDED_SKILL_GROUP_CARD_TYPE]: ExpandedSkillGroup,
                        [TOOLBOX_CARD_TYPE]: CollapsedToolboxCard,
                        [EXPANDED_TOOLBOX_CARD_TYPE]: ExpandedToolboxCard,
                    }}
                    edgeTypes={{ [EXTENDED_AGENT_EDGE_TYPE]: ExtendedAgentEdge }}
                    nodes={nodes}
                    edges={edges}
                    onNodesChange={onNodesChange}
                    onEdgesChange={onEdgesChange}
                    proOptions={{ hideAttribution: true }}
                    colorMode={theme.isInverted ? 'dark' : 'light'}
                >
                    <MiniMap />
                    <Controls />
                </ReactFlow>
            );
        }

        if (currentView === ExtendedAgentGraphView.Playground) {
            return (
                <div style={{ display: 'flex', flexDirection: 'column', flex: '1 1 auto', height: '100%' }}>
                    <PlaygroundEntitySelector
                        agents={agents}
                        systemTools={systemTools}
                        extendedTools={tools}
                        mcpConnections={mcpConnections}
                        selectedEntity={playgroundEntity}
                        onEntitySelect={entity => setPlaygroundEntity(entity)}
                        isLoading={loading}
                    />
                    {playgroundEntity && playgroundEntity.entityType === 'Agent' && (
                        <AgentPlayground
                            key={`Agent_${playgroundEntity.entity.name}`}
                            refresh={handleRefresh}
                            agents={agents}
                            existingTools={tools}
                            systemTools={systemTools}
                            mcpConnections={mcpConnections}
                            agent={playgroundEntity.entity}
                        />
                    )}
                    {playgroundEntity && playgroundEntity.entityType === 'ExtendedTool' && (
                        <KustoToolPlayground
                            key={`ExtendedTool_${playgroundEntity.entity.name}`}
                            connectors={connectors}
                            agentName={undefined}
                            addToolsToAgent={() => {}}
                            refresh={handleRefresh}
                            kustoTool={playgroundEntity.entity}
                            mode={KustoToolDialogMode.Edit}
                        />
                    )}
                    {playgroundEntity && playgroundEntity.entityType === 'SystemTool' && (
                        <SystemToolPlayground key={`SystemTool_${playgroundEntity.entity.name}`} tool={playgroundEntity.entity} />
                    )}
                    {!playgroundEntity && (
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                alignItems: 'center',
                                justifyContent: 'center',
                                height: '100%',
                                gap: '16px',
                                overflow: 'auto',
                            }}
                        >
                            <BeakerRegular style={{ width: 128, height: 128 }} />
                            <Text>{intl.formatMessage(PlaygroundResources.noSelectionMessage)}</Text>
                        </div>
                    )}
                </div>
            );
        }

        return (
            <ExtendedAgentTableView
                activeTab={currentTableViewTab}
                setActiveTab={setCurrentTableViewTab}
                agents={agents}
                tools={tools}
                systemTools={systemTools}
                connectors={connectors}
                triggers={triggers}
                skills={skills}
                isLoading={loading}
                onRefresh={handleRefresh}
                onEditKustoTool={handleEditKustoTool}
                onEditPythonTool={handleEditPythonTool}
                onEditSkill={skill => {
                    setEditingSkill(skill);
                    setIsSkillDialogOpen(true);
                }}
            />
        );
    }, [
        isLoading,
        error,
        anchorEntity?.entityName,
        hasAgents,
        hasData,
        currentView,
        currentTableViewTab,
        agents,
        tools,
        systemTools,
        connectors,
        triggers,
        skills,
        loading,
        handleRefresh,
        handleEditKustoTool,
        handleEditPythonTool,
        spinner,
        intl,
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        theme.isInverted,
        playgroundEntity,
    ]);

    useEffect(() => {
        setPlaygroundEntity(prev => {
            if (!prev || prev.entityType !== 'Agent') {
                return prev;
            }
            const updatedAgent = agents.find(a => a.name === prev.entity.name);
            return updatedAgent ? { entityType: 'Agent', entity: updatedAgent } : undefined;
        });
    }, [agents]);

    useEffect(() => {
        setPlaygroundEntity(prev => {
            if (!prev || prev.entityType !== 'ExtendedTool') {
                return prev;
            }
            const updatedTool = tools.find(t => t.name === prev.entity.name);
            return updatedTool ? { entityType: 'ExtendedTool', entity: updatedTool } : undefined;
        });
    }, [tools]);

    const showEmptyState = useMemo(() => !isLoading && !hasAgents && !hasSkills, [isLoading, hasAgents, hasSkills]);

    const handleCreateItemStandalone = useCallback(
        (itemType: EntityTypeExt) => {
            if (itemType === 'incidentTrigger') {
                setHandlerCreateOrEditInfo({
                    subAgentTriggerInfo: {
                        agents: agents.map(a => a.name),
                        preSelectedAgent: anchorEntity?.entityType === 'Agent' ? anchorEntity?.entityName : undefined,
                    },
                });
                return;
            }

            if (itemType === 'incidentTriggerWithLearnings') {
                setHandlerCreateOrEditInfo({
                    incidentTriggerWithLearningsInfo: {
                        mcpConnections: mcpConnections,
                        extendedAgents: agents,
                        systemTools: systemTools,
                        extendedTools: tools,
                    },
                });
                return;
            }

            if (itemType === 'scheduledTask') {
                setIsScheduledTaskDialogOpen(true);
                setScheduledTaskStartingAgent(anchorEntity?.entityType === 'Agent' ? anchorEntity?.entityName : undefined);
                setScheduledTaskDialogMode(ScheduledTaskDialogMode.Create);
                setScheduledTaskEditingTask(undefined);
                return;
            }

            if (itemType === 'tool') {
                setCreateToolAgent(undefined);
                setToolDialogMode(KustoToolDialogMode.Create);
                setToolToEdit(undefined);
                setIsToolDialogOpen(true);
                return;
            }

            if (itemType === 'pythonTool') {
                handleCreatePythonTool(undefined);
                return;
            }

            if (itemType === 'agent') {
                setAgentCreateOrEditInfo({
                    agent: undefined,
                    mode: 'create',
                });
                return;
            }

            if (itemType === 'metaAgent') {
                setAgentCreateOrEditInfo({
                    agent: undefined,
                    mode: 'createMetaAgent',
                });
                return;
            }

            if (itemType === 'skill') {
                setEditingSkill(undefined);
                setIsSkillDialogOpen(true);
                return;
            }

            setCreationDialogContext(undefined);
            setCreationDialogInitialTypeOverride(itemType);
            setCreationDialogTriggerAgentName(undefined);
            setCreationSuccess(undefined);
            setIsCreationDialogOpen(true);
        },
        [agents, anchorEntity?.entityType, anchorEntity?.entityName, mcpConnections, systemTools, tools, handleCreatePythonTool]
    );

    const hasMetaAgentOverride = useMemo(() => {
        return agents.some(agent => agent.name === 'meta_agent');
    }, [agents]);

    return (
        <ExtendedAgentGraphContext.Provider
            value={{
                selectedNodeId,
                setSelectedNodeId,
                expandInfoPanel: () => setIsInfoPanelCollapsed(false),
                hoveredNodeId,
                hoverNode,
                unHoverNode,
                nodesToHighlight,
                edgesToHighlight,
                openRelationshipDialog,
                triggerAgentQuickAction: handleAgentQuickAction,
                triggerTriggerQuickAction: handleTriggerQuickAction,
                onViewChange: onChangeViewType,
                onEntitySelect: handleEntitySelect,
                hasSkills,
                isSkillGroupExpanded,
                toggleSkillGroupExpanded,
                expandedToolboxes,
                toggleToolboxExpanded,
                setPlaygroundEntity,
            }}
        >
            <ScheduledTasksContext.Provider
                value={{
                    createTask: scheduledTasksHook.createTask,
                    updateTask: scheduledTasksHook.updateTask,
                    refreshTasks: (anchorEntity?: ExtendedAgentAnchorEntity) =>
                        handleRefresh().then(() => {
                            if (anchorEntity) {
                                setPendingEntitySelection(anchorEntity);
                            }
                        }),
                    pauseTask: scheduledTasksHook.pauseTask,
                    resumeTask: scheduledTasksHook.resumeTask,
                    deleteTask: scheduledTasksHook.deleteTask,
                    runTask: scheduledTasksHook.runTask,
                    getTaskExecutions: scheduledTasksHook.getTaskExecutions,
                    isOperationInProgress,
                    setIsOperationInProgress,
                }}
            >
                <div className={rootContainer}>
                    <ExtendedAgentToolbar
                        currentView={currentView}
                        onViewChange={onChangeViewType}
                        onRefresh={handleRefresh}
                        onCreateItem={handleCreateItemStandalone}
                        isLoading={isLoading}
                        hasData={hasData}
                        showEmptyState={showEmptyState}
                        disableCreateMetaAgent={hasMetaAgentOverride}
                        disableCreateSubagent={hasSkills}
                        disableCreateSkill={hasSubagents}
                    />
                    {creationSuccessMessage && (
                        <div className={statusMessageContainer}>
                            <MessageBar intent="success" layout="multiline">
                                <MessageBarBody>{creationSuccessMessage}</MessageBarBody>
                                <MessageBarActions>
                                    {creationSuccess?.entityType === 'agent' && (
                                        <>
                                            <Button appearance="primary" onClick={handleTestAgentClick}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.testAgentButton)}
                                            </Button>
                                            <Button appearance="secondary" onClick={handleAddTriggerForAgent}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.creationSuccessAddTrigger)}
                                            </Button>
                                            <Button appearance="secondary" onClick={handleAddToolForAgent}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.creationSuccessAddTool)}
                                            </Button>
                                        </>
                                    )}
                                    <Button appearance="transparent" onClick={handleDismissCreationSuccess}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipDismiss)}
                                    </Button>
                                </MessageBarActions>
                            </MessageBar>
                        </div>
                    )}

                    {linkRetryContext && (
                        <div className={statusMessageContainer}>
                            <MessageBar intent="warning" layout="multiline">
                                <MessageBarBody>
                                    {intl.formatMessage(
                                        linkRetryContext.entityType === 'agent'
                                            ? ExtendedAgentsGraphResources.relationshipLinkFailedAgent
                                            : ExtendedAgentsGraphResources.relationshipLinkFailedTool,
                                        {
                                            agentName: linkRetryContext.sourceAgentName,
                                        }
                                    )}
                                    {linkRetryContext.lastError && <div style={{ marginTop: 4 }}>{linkRetryContext.lastError}</div>}
                                </MessageBarBody>
                                <MessageBarActions>
                                    <Button appearance="primary" onClick={handleRetryLink} disabled={isRetryingLink}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.retryLink)}
                                    </Button>
                                    <Button appearance="transparent" onClick={handleDismissLinkError} disabled={isRetryingLink}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.relationshipDismiss)}
                                    </Button>
                                </MessageBarActions>
                            </MessageBar>
                        </div>
                    )}

                    <div className={mergeClasses(container, commonStyles.contentRootBorderAndBackground)}>
                        <div className={visualRoot} ref={visualRootRef}>
                            <div className={reactFlow}>
                                {currentView === ExtendedAgentGraphView.Canvas && hasAnyResources && !showEmptyState && (
                                    <div className={selectorOverlay}>
                                        <ExtendedAgentSelector
                                            agents={agents}
                                            triggers={triggers}
                                            selectedEntity={anchorEntity}
                                            onEntitySelect={handleEntitySelect}
                                            expandInfoPanel={() => setIsInfoPanelCollapsed(false)}
                                            setSelectedNodeId={setSelectedNodeId}
                                            isLoading={loading}
                                            nodes={nodes}
                                            nodeCount={nodes.length}
                                            edgeCount={edges.length}
                                            showAgentPicker={hasAgents}
                                            noAgentsMessage={
                                                hasAgents ? undefined : intl.formatMessage(ExtendedAgentsGraphResources.noAgentsFound)
                                            }
                                        />
                                    </div>
                                )}

                                {showEmptyState ? (
                                    <ExtendedAgentEmptyState
                                        onCreateClick={() => setAgentCreateOrEditInfo({ agent: undefined, mode: 'create' })}
                                        onCreateSkillClick={() => {
                                            setEditingSkill(undefined);
                                            setIsSkillDialogOpen(true);
                                        }}
                                        agents={agents}
                                        skills={skills}
                                    />
                                ) : (
                                    <>{renderGraphContent()}</>
                                )}
                            </div>

                            {currentView === ExtendedAgentGraphView.Canvas && !showEmptyState && !!selectedNode?.data?.data && (
                                <div
                                    ref={infoPanelRef}
                                    className={mergeClasses(infoPanelContainer, isInfoPanelFloating && infoPanelFloating)}
                                    style={isInfoPanelCollapsed ? undefined : infoPanelStyle}
                                >
                                    <ExtendedAgentInfoPanel
                                        selectedNode={selectedNode.data}
                                        tools={tools}
                                        connectors={connectors}
                                        triggers={triggers}
                                        systemTools={systemTools}
                                        sreAgentEndpoint={sreAgentEndpoint}
                                        onRefresh={refetch}
                                        onDragHandlePointerDown={handleInfoPanelPointerDown}
                                        onResizeHandlePointerDown={handleInfoPanelResizePointerDown}
                                        width={infoPanelWidth}
                                        minWidth={INFO_PANEL_MIN_WIDTH}
                                        maxWidth={INFO_PANEL_MAX_WIDTH}
                                        onEditKustoTool={handleEditKustoTool}
                                        onEditPythonTool={handleEditPythonTool}
                                        onEditSkill={skill => {
                                            setEditingSkill(skill);
                                            setIsSkillDialogOpen(true);
                                        }}
                                        collapsibleProps={{ isCollapsed: isInfoPanelCollapsed, setCollapsed: setIsInfoPanelCollapsed }}
                                    />
                                </div>
                            )}
                        </div>
                    </div>

                    <ExtendedAgentRelationshipDialog
                        open={isRelationshipDialogOpen}
                        onOpenChange={handleRelationshipDialogOpenChange}
                        agent={relationshipAgent}
                        existingAgents={agents}
                        existingTools={tools}
                        systemTools={systemTools}
                        onAddHandoff={handleAddExistingHandoff}
                        onAddTool={handleAddExistingTool}
                        onLaunchCreateEntity={handleLaunchLinkedCreation}
                        initialAction={relationshipInitialAction}
                    />

                    <ExtendedAgentCreationDialog
                        open={isCreationDialogOpen}
                        onOpenChange={handleCreationDialogOpenChange}
                        onSubmit={creationDialogContext?.kind === 'linkFromAgent' ? handleCreateAndLinkEntity : handleCreateEntity}
                        initialEntityType={creationDialogInitialType}
                        initialToolType={creationDialogInitialToolType}
                        initialTriggerAgentName={creationDialogTriggerAgentName}
                        contextNotice={creationDialogNotice ? { intent: 'info', message: creationDialogNotice } : undefined}
                        linkContext={creationDialogLinkContext}
                        existingAgents={agents}
                        existingTools={tools}
                        existingConnectors={connectors}
                        systemTools={systemTools}
                        triggerConfig={triggerCardConfig}
                        onTriggerNavigate={handleTriggerNavigate}
                        onConnectorNavigate={handleConnectorNavigate}
                    />

                    <IncidentTriggerCreateDialog
                        onDismiss={(filterName?: string, handlerId?: string, isNew?: boolean) => {
                            setHandlerCreateOrEditInfo(undefined);
                            if (!!filterName || !!handlerId) {
                                handleRefresh().then(() => {
                                    if (isNew) {
                                        if (filterName) {
                                            setPendingEntitySelection({ entityType: 'Trigger', entityName: filterName });
                                        }
                                        return;
                                    }

                                    const trigger = triggers.find(
                                        t =>
                                            t.name?.toLowerCase() === filterName?.toLowerCase() ||
                                            t.name?.toLowerCase() === handlerId?.toLowerCase()
                                    );
                                    if (trigger) {
                                        setPendingEntitySelection({ entityType: 'Trigger', entityName: trigger.name });
                                    }
                                });
                            }
                        }}
                        setHandlerOperationStatus={() => {}}
                        handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                    />
                    <ScheduledTaskCreateOrEditDialog
                        isDialogOpen={isScheduledTaskDialogOpen}
                        setIsDialogOpen={setIsScheduledTaskDialogOpen}
                        mode={scheduledTaskDialogMode}
                        agents={agents}
                        startingAgent={scheduledTaskStartingAgent}
                        scheduledTask={scheduledTaskEditingTask}
                    />

                    <KustoToolDialog
                        isDialogOpen={isToolDialogOpen}
                        setIsDialogOpen={setIsToolDialogOpen}
                        connectors={connectors}
                        agentName={createToolAgent}
                        addToolsToAgent={addToolsToAgent}
                        refresh={() => {
                            setIsInfoPanelCollapsed(true);
                            handleRefresh();
                        }}
                        kustoTool={toolToEdit}
                        mode={toolDialogMode}
                    />

                    <PythonToolDialog
                        isDialogOpen={isPythonToolDialogOpen}
                        setIsDialogOpen={setIsPythonToolDialogOpen}
                        agentName={pythonToolAgentName}
                        addToolsToAgent={addToolsToAgent}
                        refresh={() => {
                            setIsInfoPanelCollapsed(true);
                            handleRefresh();
                        }}
                        pythonTool={pythonToolToEdit}
                        mode={pythonToolDialogMode}
                    />

                    <AddExistingAgentHandoffDialog
                        onDismiss={() => setAgentHandoffPickerInfo(undefined)}
                        agents={agents}
                        addHandoffToAgent={addHandoffToAgent}
                        handoffInfo={agentHandoffPickerInfo}
                    />

                    <AddExistingToolDialog
                        onDismiss={() => setToolPickerInfo(undefined)}
                        addToolsToAgent={addToolsToAgent}
                        existingTools={tools}
                        systemTools={systemTools}
                        mcpConnections={mcpConnections}
                        toolPickerInfo={toolPickerInfo}
                    />

                    <AgentCreateDialog
                        onDismiss={() => setAgentCreateOrEditInfo(undefined)}
                        refresh={(selectedAgent?: string) => {
                            handleRefresh().then(() => {
                                setPendingEntitySelection(selectedAgent ? { entityType: 'Agent', entityName: selectedAgent } : undefined);
                            });
                        }}
                        agents={agents}
                        existingTools={tools}
                        systemTools={systemTools}
                        mcpConnections={mcpConnections}
                        skills={skills}
                        agentCreateOrEditInfo={agentCreateOrEditInfo}
                    />

                    <CreateSkillDialog
                        isOpen={isSkillDialogOpen}
                        onDismiss={() => {
                            setIsSkillDialogOpen(false);
                            setEditingSkill(undefined);
                        }}
                        onSave={handleSaveSkill}
                        existingSkill={editingSkill}
                        existingTools={tools}
                        systemTools={systemTools}
                        mcpConnections={mcpConnections}
                    />
                </div>
            </ScheduledTasksContext.Provider>
        </ExtendedAgentGraphContext.Provider>
    );
});

ExtendedAgentGraphContent.displayName = 'ExtendedAgentGraphContent';

export default ExtendedAgentGraph;

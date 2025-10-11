import { useTheme } from '@fluentui/react';
import {
    Button,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    Radio,
    RadioGroup,
    Spinner,
    mergeClasses,
} from '@fluentui/react-components';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider, useEdgesState, useNodesState, useReactFlow } from '@xyflow/react';
import React, { memo, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { useFeatureFlags } from '../../Common/Hooks/useFeatureFlags';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
} from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentGraph } from '../Hooks/useExtendedAgentGraph';
import { useExtendedAgentGraphLayout } from '../Hooks/useExtendedAgentGraphLayout';
import { useIncidentHandlers } from '../Hooks/useIncidentHandlers';
import { useScheduledTasks } from '../Hooks/useScheduledTasks';
import { useExtendedAgentGraphStyles } from '../Styles/ExtendedAgentGraph.styles';
import { ConnectorCard } from './ConnectorCard';
import { ExtendedAgentCard } from './ExtendedAgentCard';
import { ExtendedAgentCreationDialog } from './ExtendedAgentCreationDialogNew';
import { ExtendedAgentEdge } from './ExtendedAgentEdge';
import { ExtendedAgentEmptyState } from './ExtendedAgentEmptyState';
import {
    CONNECTOR_CARD_TYPE,
    EXTENDED_AGENT_CARD_TYPE,
    EXTENDED_AGENT_EDGE_TYPE,
    TOOL_CARD_TYPE,
    TRIGGER_CARD_TYPE,
} from './ExtendedAgentGraphUtility';
import { ExtendedAgentInfoPanel } from './ExtendedAgentInfoPanel';
import { ExtendedAgentRelationshipDialog } from './ExtendedAgentRelationshipDialog';
import { ExtendedAgentSelector } from './ExtendedAgentSelector';
import { buildMetaAgentYaml, convertExtendedEntityToYaml } from './ExtendedAgentYamlUtils';
import { FloatingActionButton } from './FloatingActionButton';
import { ToolCard } from './ToolCard';
import { TriggerCard } from './TriggerCard';

const clamp = (value: number, min: number, max: number) => Math.min(Math.max(value, min), max);

const INFO_PANEL_MIN_WIDTH = 280;
const INFO_PANEL_MAX_WIDTH = 720;
const INFO_PANEL_DEFAULT_WIDTH = 360;

const ExtendedAgentGraph = () => {
    return (
        <ReactFlowProvider>
            <ExtendedAgentGraphContent />
        </ReactFlowProvider>
    );
};

export enum ExtendedAgentGraphView {
    Grid = 'grid',
    Visual = 'visual',
}

type OperationResult = {
    success: boolean;
    message: string;
};

type AgentQuickAction = 'addHandoff' | 'addTool' | 'createAgent' | 'createTool';

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
        selectedNode,
        setSelectedNode,
        hoveredNodeId,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        loading,
        error,
        agents,
        tools,
        connectors,
        triggers,
        systemTools,
        filters,
        setFilters,
        refetch,
    } = useExtendedAgentGraph();

    const { features } = useFeatureFlags();
    const { incidentHandlers, incidentHandlersLoading } = useIncidentHandlers();
    const { scheduledTasks, loading: scheduledTasksLoading } = useScheduledTasks({ enabled: features.scheduledTasks });

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const {
        visualRoot,
        reactFlow,
        spinner,
        rootContainer,
        container,
        radioGroupContainer,
        selectorOverlay,
        infoPanelContainer,
        infoPanelFloating,
        statusMessageContainer,
    } = useExtendedAgentGraphStyles();

    const theme = useTheme();
    const intl = useIntl();
    const navigate = useNavigate();

    const [currentView, setCurrentView] = useState<ExtendedAgentGraphView>(ExtendedAgentGraphView.Visual);
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
    const [linkRetryContext, setLinkRetryContext] = useState<LinkRetryContext | undefined>(undefined);
    const [isRetryingLink, setIsRetryingLink] = useState(false);
    const [isInfoPanelFloating, setIsInfoPanelFloating] = useState(false);
    const [infoPanelPosition, setInfoPanelPosition] = useState({ x: 0, y: 0 });
    const [isInfoPanelDragging, setIsInfoPanelDragging] = useState(false);
    const [infoPanelWidth, setInfoPanelWidth] = useState(INFO_PANEL_DEFAULT_WIDTH);

    const layoutGraph = useExtendedAgentGraphLayout();

    const previousAgentNameRef = useRef<string | undefined>(undefined);
    const visualRootRef = useRef<HTMLDivElement>(null);
    const infoPanelRef = useRef<HTMLDivElement>(null);
    const infoPanelDragStateRef = useRef<{ pointerId: number; offsetX: number; offsetY: number } | null>(null);
    const infoPanelResizeStateRef = useRef<{ pointerId: number; startX: number; startWidth: number } | null>(null);
    const infoPanelResizeHandleRef = useRef<HTMLDivElement | null>(null);
    const lastFitSignatureRef = useRef<string>('');
    const reactFlowInstance = useReactFlow();

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
        if (currentView !== ExtendedAgentGraphView.Visual) {
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
        if (currentView !== ExtendedAgentGraphView.Visual) {
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
    }, [currentView, isLayouting, loading, nodes, reactFlowInstance]);

    useEffect(() => {
        if (!loading && agents.length > 0 && !filters.agentName) {
            setFilters(prev => ({ ...prev, agentName: agents[0].name }));
        }
    }, [loading, agents, filters.agentName, setFilters]);

    useEffect(() => {
        if (loading) {
            return;
        }

        if (filters.agentName && !agents.some(agent => agent.name === filters.agentName)) {
            setFilters(prev => ({ ...prev, agentName: agents[0]?.name }));
        }
    }, [agents, filters.agentName, loading, setFilters]);

    useEffect(() => {
        const activeAgentName = filters.agentName;

        if (!activeAgentName) {
            previousAgentNameRef.current = undefined;
            if (selectedNode) {
                setSelectedNode(undefined);
            }
            return;
        }

        const primaryNode = graphNodes.find(node => node.id === `agent_${activeAgentName}`);

        if (!primaryNode) {
            if (graphNodes.length === 0) {
                // Wait for layout to supply nodes before updating selection
                return;
            }

            previousAgentNameRef.current = activeAgentName;

            if (selectedNode) {
                setSelectedNode(undefined);
            }

            return;
        }

        const alreadySelectedSameNode = selectedNode?.id === primaryNode.data.id;
        const agentChanged = previousAgentNameRef.current !== activeAgentName;

        if (selectedNode && selectedNode.type !== ExtendedAgentNodeType.Agent) {
            previousAgentNameRef.current = activeAgentName;

            if (agentChanged) {
                setSelectedNode(primaryNode.data);
            }

            return;
        }

        if (agentChanged || !alreadySelectedSameNode) {
            previousAgentNameRef.current = activeAgentName;
            setSelectedNode(primaryNode.data);
        }
    }, [filters.agentName, graphNodes, selectedNode, setSelectedNode]);

    const selectedAgent = useMemo(
        () => (filters.agentName ? agents.find(agent => agent.name === filters.agentName) : undefined),
        [agents, filters.agentName]
    );

    const infoPanelAgent = useMemo(() => {
        if (selectedNode?.type === ExtendedAgentNodeType.Agent) {
            return selectedNode.data as ExtendedAgent;
        }

        return selectedAgent;
    }, [selectedNode, selectedAgent]);

    const relationshipAgent = useMemo(
        () => (relationshipAgentName ? agents.find(agent => agent.name === relationshipAgentName) : undefined),
        [agents, relationshipAgentName]
    );

    const creationDialogInitialType = creationDialogContext?.kind === 'linkFromAgent' ? creationDialogContext.targetType : undefined;

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

    const incidentHandlersCount = incidentHandlersLoading ? null : (incidentHandlers?.length ?? 0);
    const scheduledTasksCount = features.scheduledTasks ? (scheduledTasksLoading ? null : scheduledTasks.length) : null;

    const triggerCardConfig = useMemo(
        () => ({
            isLoading: incidentHandlersLoading || (features.scheduledTasks && scheduledTasksLoading),
            incidentHandlersCount,
            scheduledTasksCount,
            hasScheduledTasksFeature: features.scheduledTasks,
        }),
        [features.scheduledTasks, incidentHandlersCount, incidentHandlersLoading, scheduledTasksCount, scheduledTasksLoading]
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

    const handleAgentSelect = useCallback(
        (agentName?: string) => {
            setFilters(prev => ({ ...prev, agentName }));
        },
        [setFilters]
    );

    const handleSearchQueryChange = useCallback(
        (query: string) => {
            setFilters(prev => ({ ...prev, searchQuery: query }));
        },
        [setFilters]
    );

    const handleRefresh = useCallback(() => {
        refetch();
    }, [refetch]);

    const onChangeViewType = useCallback((view: ExtendedAgentGraphView) => {
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
            type: 'agent' | 'tool' | 'connector' | 'trigger'
        ) => {
            // Trigger type doesn't create entities in the graph
            if (type === 'trigger') {
                return;
            }
            await applyEntity(data, type, { refreshMode: 'reload' });
        },
        [applyEntity]
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
            type: 'agent' | 'tool' | 'connector' | 'trigger'
        ) => {
            // Trigger type is handled separately, just create without linking
            if (type === 'trigger') {
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
        [addHandoffToAgent, addToolToAgent, applyEntity, creationDialogContext, handleCreateEntity, intl]
    );

    const handleAgentQuickAction = useCallback((agentName: string, action: AgentQuickAction) => {
        if (action === 'createAgent' || action === 'createTool') {
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
    }, []);

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

    const handleCreationDialogOpenChange = useCallback((open: boolean) => {
        setIsCreationDialogOpen(open);

        if (!open) {
            setCreationDialogContext(undefined);
        }
    }, []);

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
        navigate('/views/incidentmanagement');
        setCreationSuccess(undefined);
    }, [navigate]);

    const handleScheduledTasksClick = useCallback(() => {
        navigate('/views/scheduledtasks');
        setCreationSuccess(undefined);
    }, [navigate]);

    const handleConnectorNavigate = useCallback(() => {
        navigate('/views/settings/data-connectors');
        setIsCreationDialogOpen(false);
    }, [navigate]);

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
            // Navigate to activities with extended agent parameter for testing
            navigate(`/views/activities?testAgent=${encodeURIComponent(creationSuccess.entityName)}`);
        }
    }, [navigate, creationSuccess]);

    const isLoading = loading || isLayouting;
    const hasAgents = agents.length > 0;
    const hasTools = tools.length > 0;
    const hasConnectors = connectors.length > 0;
    const hasSystemTools = systemTools.length > 0;
    const hasAnyResources = hasAgents || hasTools || hasConnectors || hasSystemTools;
    const hasData = graphNodes.length > 0;

    const renderGraphContent = () => {
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

        if (!filters.agentName) {
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

        if (currentView === ExtendedAgentGraphView.Visual) {
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

        return (
            <div style={{ padding: '20px' }}>
                <MessageBar intent="info">
                    <MessageBarBody>{intl.formatMessage(ExtendedAgentsGraphResources.gridViewPlaceholder)}</MessageBarBody>
                </MessageBar>
            </div>
        );
    };

    const showEmptyState = !isLoading && !hasAgents;

    const infoPanelStyle: React.CSSProperties = {
        width: `${infoPanelWidth}px`,
    };

    if (isInfoPanelFloating) {
        infoPanelStyle.transform = `translate(${infoPanelPosition.x}px, ${infoPanelPosition.y}px)`;
        if (isInfoPanelDragging) {
            infoPanelStyle.cursor = 'grabbing';
        }
    }

    return (
        <ExtendedAgentGraphContext.Provider
            value={{
                selectedNode,
                setSelectedNode,
                hoveredNodeId,
                hoverNode,
                unHoverNode,
                nodesToHighlight,
                edgesToHighlight,
                openRelationshipDialog,
                triggerAgentQuickAction: handleAgentQuickAction,
            }}
        >
            <div className={rootContainer}>
                <div className={radioGroupContainer}>
                    <RadioGroup
                        value={currentView}
                        layout="horizontal"
                        onChange={(_, data) => onChangeViewType(data.value as ExtendedAgentGraphView)}
                    >
                        <Radio value={ExtendedAgentGraphView.Visual} label={intl.formatMessage(ExtendedAgentsGraphResources.visualView)} />
                        <Radio value={ExtendedAgentGraphView.Grid} label={intl.formatMessage(ExtendedAgentsGraphResources.gridView)} />
                    </RadioGroup>
                </div>

                {creationSuccessMessage && (
                    <div className={statusMessageContainer}>
                        <MessageBar intent="success" layout="multiline">
                            <MessageBarBody>{creationSuccessMessage}</MessageBarBody>
                            <MessageBarActions>
                                {creationSuccess?.entityType === 'agent' && (
                                    <Button appearance="primary" onClick={handleTestAgentClick}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.testAgentButton)}
                                    </Button>
                                )}
                                <Button appearance="secondary" onClick={handleIncidentManagementClick}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.quickCreateAgentSuccessLink)}
                                </Button>
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

                <div className={container}>
                    <div className={visualRoot} ref={visualRootRef}>
                        <div className={reactFlow}>
                            {currentView === ExtendedAgentGraphView.Visual && hasAnyResources && (
                                <div className={selectorOverlay}>
                                    <ExtendedAgentSelector
                                        agents={agents}
                                        selectedAgentName={filters.agentName}
                                        searchQuery={filters.searchQuery ?? ''}
                                        onAgentSelect={handleAgentSelect}
                                        onSearchQueryChange={handleSearchQueryChange}
                                        isLoading={loading}
                                        onRefresh={handleRefresh}
                                        selectedAgent={selectedAgent}
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
                                    onCreateClick={() => {
                                        setCreationDialogContext(undefined);
                                        setIsCreationDialogOpen(true);
                                    }}
                                />
                            ) : (
                                <>
                                    {renderGraphContent()}
                                    {currentView === ExtendedAgentGraphView.Visual && !isLoading && hasData && (
                                        <FloatingActionButton
                                            onClick={() => {
                                                setCreationDialogContext(undefined);
                                                setIsCreationDialogOpen(true);
                                            }}
                                            tooltip={intl.formatMessage(ExtendedAgentsGraphResources.createNewEntityTooltip)}
                                        />
                                    )}
                                </>
                            )}
                        </div>

                        {currentView === ExtendedAgentGraphView.Visual && !showEmptyState && (
                            <div
                                ref={infoPanelRef}
                                className={mergeClasses(infoPanelContainer, isInfoPanelFloating && infoPanelFloating)}
                                style={infoPanelStyle}
                            >
                                <ExtendedAgentInfoPanel
                                    selectedAgent={infoPanelAgent}
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
            </div>
        </ExtendedAgentGraphContext.Provider>
    );
});

ExtendedAgentGraphContent.displayName = 'ExtendedAgentGraphContent';

export default ExtendedAgentGraph;

import { Edge, Node } from '@xyflow/react';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import {
    ExtendedAgent,
    ExtendedAgentAnchorEntity,
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    PaginatedResponse,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import { McpConnection } from '../Graph/ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { buildExtendedAgentGraph } from '../Graph/ExtendedAgentGraphUtility';
import { useScheduledTasksV2 } from '../ScheduledTasks/V2/Hooks/useScheduledTasksV2';
import { useIncidentFilters } from './useIncidentFilters';
import { useIncidentHandlers } from './useIncidentHandlers';

export const useExtendedAgentGraph = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [agents, setAgents] = useState<ExtendedAgent[]>([]);
    const [tools, setTools] = useState<ExtendedTool[]>([]);
    const [connectors, setConnectors] = useState<ExtendedConnector[]>([]);
    const [triggers, setTriggers] = useState<ExtendedTrigger[]>([]);
    const [triggersLoading, setTriggersLoading] = useState<boolean>(false);
    const [systemTools, setSystemTools] = useState<SystemTool[]>([]);
    const [mcpConnections, setMcpConnections] = useState<McpConnection[]>([]);

    const [nodes, setNodes] = useState<Node<ExtendedAgentGraphNode>[]>([]);
    const [edges, setEdges] = useState<Edge<ExtendedAgentGraphEdge>[]>([]);

    const [selectedNode, setSelectedNode] = useState<ExtendedAgentGraphNode | undefined>();
    const [hoveredNodeId, setHoveredNodeId] = useState<string | undefined>();

    const [anchorEntity, setAnchorEntity] = useState<ExtendedAgentAnchorEntity>();

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    const scheduledTasksHook = useScheduledTasksV2();
    const incidentFiltersHook = useIncidentFilters();
    const incidentHandlersHook = useIncidentHandlers();

    // Fetch agents
    const fetchAgents = useCallback(async () => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/agents?page=1&limit=200`, {
                headers: getAgentHeaders(),
            });

            if (!response.ok) {
                throw new Error(`Failed to fetch agents: ${response.status}`);
            }

            const data: PaginatedResponse<ExtendedAgent> = await response.json();

            // Fetch document counts for agents with memory enabled
            const agentsWithDocumentCounts = await Promise.all(
                data.data.map(async agent => {
                    // Memory is enabled if the SearchMemory tool is available in the agent's tools
                    const memoryEnabled =
                        agent.tools?.some(t => t.toLowerCase() === 'searchmemory') ||
                        agent.systemTools?.some(t => t.toLowerCase() === 'searchmemory') ||
                        false;

                    if (memoryEnabled) {
                        try {
                            const filesResponse = await fetch(`${sreAgentEndpoint}/api/v1/agentmemory/files`, {
                                headers: getAgentHeaders(),
                            });

                            if (filesResponse.ok) {
                                const filesData = await filesResponse.json();
                                const documentCount = Array.isArray(filesData.files) ? filesData.files.length : 0;

                                return {
                                    ...agent,
                                    metadata: {
                                        ...(agent.metadata || {}),
                                        documentCount,
                                    },
                                };
                            } else {
                                console.warn(`[fetchAgents] Failed to fetch files for ${agent.name}: ${filesResponse.status}`);
                            }
                        } catch (err) {
                            console.warn(`Failed to fetch document count for agent ${agent.name}:`, err);
                        }
                    }
                    return agent;
                })
            );

            setAgents(agentsWithDocumentCounts);
        } catch (err) {
            console.error('Error fetching agents:', err);
            setError('Failed to load agents');
        }
    }, [sreAgentEndpoint]);

    // Fetch tools
    const fetchTools = useCallback(async () => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/tools?page=1&limit=200`, {
                headers: getAgentHeaders(),
            });

            if (!response.ok) {
                throw new Error(`Failed to fetch tools: ${response.status}`);
            }

            const result = await response.json();
            // Handle the nested response structure: { data: { tools: { data: [...] } } }
            const tools: ExtendedTool[] = result.data?.tools?.data || [];
            setTools(tools);
        } catch (err) {
            console.error('Error fetching tools:', err);
            setError('Failed to load tools');
        }
    }, [sreAgentEndpoint]);

    // Fetch connectors
    const fetchConnectors = useCallback(async () => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/dataconnectors`, {
                headers: getAgentHeaders(),
            });

            if (!response.ok) {
                throw new Error(`Failed to fetch connectors: ${response.status}`);
            }

            const data: ExtendedConnector[] = await response.json();
            setConnectors(data);
        } catch (err) {
            console.error('Error fetching connectors:', err);
            setError('Failed to load connectors');
        }
    }, [sreAgentEndpoint]);

    // Fetch triggers (incident handlers and scheduled tasks)
    const fetchTriggers = useCallback(async () => {
        setTriggersLoading(true);
        await Promise.allSettled([scheduledTasksHook.refreshTasks(), incidentHandlersHook.refresh(), incidentFiltersHook.refresh()]);
        setTriggersLoading(false);
    }, [scheduledTasksHook.refreshTasks, incidentFiltersHook.refresh, incidentHandlersHook.refresh]);

    useEffect(() => {
        if (!triggersLoading) {
            if (
                scheduledTasksHook.loadingError !== null ||
                incidentHandlersHook.incidentHandlersLoadingError !== null ||
                incidentFiltersHook.incidentFiltersLoadingError !== null
            ) {
                setError('Failed to load triggers');
                setTriggers([]);
                return;
            }

            const allTriggers: ExtendedTrigger[] = [];
            const incidentFilters = incidentFiltersHook.incidentFilters || [];
            const incidentHandlers = incidentHandlersHook.incidentHandlers || [];
            const scheduledTasks = scheduledTasksHook.scheduledTasks || [];

            // Process incident handlers
            // Create a map of filter ID to handling agent
            const filterAgentMap = new Map<string, string>();
            incidentFilters.forEach((filter: any) => {
                if (filter.id && filter.handlingAgent) {
                    filterAgentMap.set(filter.id, filter.handlingAgent);
                }
            });

            const incidentTriggers: ExtendedTrigger[] = [];
            const handlerFilterIds = new Set<string>();
            incidentHandlers.forEach((handler: any) => {
                const filter = incidentFilters.find(f => f.id === handler.incidentFilterId);
                const agentName = filterAgentMap.get(handler.incidentFilterId) || handler.agentName;
                if (agentName) {
                    handlerFilterIds.add(handler.incidentFilterId);
                    incidentTriggers.push({
                        name: handler.name || handler.id,
                        description: handler.description || 'Incident response handler',
                        type: 'incident' as const,
                        agentName: agentName,
                        status: filter?.isEnabled ? 'Active' : 'Disabled',
                        priority: filter?.priority || '-',
                        incidentType: filter?.incidentType || 'ServiceIssue',
                        service: filter?.impactedService || '-',
                        severity: filter?.priority || '-',
                        enabled: !!filter?.isEnabled,
                        createdAt: handler.createdAt || new Date().toISOString(),
                    });
                }
            });

            // Add incident triggers for filters with handlingAgent but no handler
            // These are subagent triggers where no handler document is created
            incidentFilters.forEach((filter: any) => {
                if (filter.handlingAgent && !handlerFilterIds.has(filter.id)) {
                    incidentTriggers.push({
                        name: filter.id,
                        description: `Incident trigger for ${filter.handlingAgent}`,
                        type: 'incident' as const,
                        agentName: filter.handlingAgent,
                        status: filter.isEnabled ? 'Active' : 'Disabled',
                        priority: filter.priority || '-',
                        incidentType: filter.incidentType || 'ServiceIssue',
                        service: filter.impactedService || '-',
                        severity: filter.priority || '-',
                        titleContains: filter.titleContains || '-',
                        enabled: !!filter.isEnabled,
                        createdAt: filter.createdAt || new Date().toISOString(),
                    });
                }
            });

            allTriggers.push(...incidentTriggers);

            // Process scheduled tasks
            const scheduledTriggers: ExtendedTrigger[] = scheduledTasks.map((task: any) => ({
                name: task.name || task.id,
                description: task.description || 'Scheduled task',
                type: 'scheduled' as const,
                agentName: task.agent, // This field should now be populated correctly
                status: task.status === 'Active' ? 'Active' : 'Disabled',
                schedule: task.cronExpression,
                cronExpression: task.cronExpression,
                timezone: 'UTC',
                enabled: task.status === 'Active',
                createdAt: task.createdAt || new Date().toISOString(),
            }));
            allTriggers.push(...scheduledTriggers);

            setTriggers(allTriggers);
        }
    }, [
        triggersLoading,
        scheduledTasksHook.loadingError,
        incidentHandlersHook.incidentHandlersLoadingError,
        incidentFiltersHook.incidentFiltersLoadingError,
        scheduledTasksHook.scheduledTasks,
        incidentHandlersHook.incidentHandlers,
        incidentFiltersHook.incidentFilters,
    ]);

    // Fetch system tools
    const fetchSystemTools = useCallback(async () => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/systemtools?stableOnly=true`, {
                headers: getAgentHeaders(),
            });

            if (!response.ok) {
                throw new Error(`Failed to fetch system tools: ${response.status}`);
            }

            const data: SystemTool[] = await response.json();
            setSystemTools(data);
        } catch (err) {
            console.error('Error fetching system tools:', err);
            setError('Failed to load system tools');
        }
    }, [sreAgentEndpoint]);

    // Fetch system tools
    const fetchMcpConnections = useCallback(async () => {
        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/mcp/connections/list`, {
                headers: getAgentHeaders(),
            });

            if (!response.ok) {
                throw new Error(`Failed to fetch MCP connections: ${response.status}`);
            }

            const data: McpConnection[] = await response.json();
            setMcpConnections(data);
        } catch (err) {
            console.error('Error fetching MCP connections:', err);
            setError('Failed to load MCP connections');
        }
    }, [sreAgentEndpoint]);

    // Load all data
    useEffect(() => {
        const loadData = async () => {
            setLoading(true);
            setError(null);

            await Promise.all([fetchAgents(), fetchTools(), fetchConnectors(), fetchTriggers(), fetchSystemTools(), fetchMcpConnections()]);

            setLoading(false);
        };

        loadData();
    }, [fetchAgents, fetchTools, fetchConnectors, fetchTriggers, fetchSystemTools, fetchMcpConnections]);

    // Build graph when data changes
    useEffect(() => {
        if (agents.length === 0 && tools.length === 0 && connectors.length === 0 && triggers.length === 0 && systemTools.length === 0) {
            setNodes([]);
            setEdges([]);
            return;
        }

        const { nodes: graphNodes, edges: graphEdges } = buildExtendedAgentGraph(
            agents,
            tools,
            connectors,
            triggers,
            systemTools,
            mcpConnections
        );

        setNodes(graphNodes);
        setEdges(graphEdges);
    }, [agents, tools, connectors, triggers, systemTools, mcpConnections]);

    // Apply filters
    const filteredGraph = useMemo(() => {
        let filteredNodes = [...nodes];
        let filteredEdges = [...edges];

        // Filter by agent selection (and include connected components)
        if (anchorEntity) {
            const rootId = anchorEntity.entityType === 'Agent' ? `agent_${anchorEntity.entityName}` : `trigger_${anchorEntity.entityName}`;

            if (nodes.some(node => node.id === rootId)) {
                const reachableNodeIds = new Set<string>();
                const adjacency = new Map<string, Set<string>>();

                edges.forEach(edge => {
                    if (!adjacency.has(edge.source)) {
                        adjacency.set(edge.source, new Set<string>());
                    }
                    if (!adjacency.has(edge.target)) {
                        adjacency.set(edge.target, new Set<string>());
                    }
                    adjacency.get(edge.source)!.add(edge.target);
                    adjacency.get(edge.target)!.add(edge.source);
                });

                const queue: string[] = [rootId];
                reachableNodeIds.add(rootId);

                while (queue.length > 0) {
                    const current = queue.shift()!;
                    const neighbours = adjacency.get(current);
                    if (!neighbours) {
                        continue;
                    }

                    neighbours.forEach(neighbourId => {
                        if (!reachableNodeIds.has(neighbourId)) {
                            reachableNodeIds.add(neighbourId);
                            queue.push(neighbourId);
                        }
                    });
                }

                filteredNodes = filteredNodes.filter(node => reachableNodeIds.has(node.id));
                filteredEdges = filteredEdges.filter(edge => reachableNodeIds.has(edge.source) && reachableNodeIds.has(edge.target));
            } else {
                filteredNodes = [];
                filteredEdges = [];
            }
        }

        return { nodes: filteredNodes, edges: filteredEdges };
    }, [nodes, edges, anchorEntity, triggers]);

    // Nodes and edges to highlight
    const { nodesToHighlight, edgesToHighlight } = useMemo(() => {
        if (!hoveredNodeId) {
            return { nodesToHighlight: [], edgesToHighlight: [] };
        }

        const connectedNodeIds = new Set<string>();
        const connectedEdgeIds: string[] = [];

        edges.forEach(edge => {
            if (edge.source === hoveredNodeId) {
                connectedNodeIds.add(edge.target);
                connectedEdgeIds.push(edge.id);
            }
            if (edge.target === hoveredNodeId) {
                connectedNodeIds.add(edge.source);
                connectedEdgeIds.push(edge.id);
            }
        });

        return {
            nodesToHighlight: Array.from(connectedNodeIds),
            edgesToHighlight: connectedEdgeIds,
        };
    }, [hoveredNodeId, edges]);

    const hoverNode = useCallback((nodeId: string) => {
        setHoveredNodeId(nodeId);
    }, []);

    const unHoverNode = useCallback(() => {
        setHoveredNodeId(undefined);
    }, []);

    return {
        agents,
        tools,
        connectors,
        scheduledTasksHook,
        incidentHandlersHook,
        incidentFiltersHook,
        triggers,
        systemTools,
        mcpConnections,
        nodes: filteredGraph.nodes,
        edges: filteredGraph.edges,
        selectedNode,
        setSelectedNode,
        hoveredNodeId,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        anchorEntity,
        setAnchorEntity,
        loading,
        error,
        refetch: async () => {
            setLoading(true);
            setError(null);
            await Promise.all([fetchAgents(), fetchTools(), fetchConnectors(), fetchTriggers(), fetchSystemTools()]);
            setLoading(false);
        },
    };
};

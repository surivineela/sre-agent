import { Edge, Node } from '@xyflow/react';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentFilter, IncidentHandler } from '../../Common/Contracts/Azure/IncidentHandler';
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
import { buildExtendedAgentGraph } from '../Graph/ExtendedAgentGraphUtility';

export const useExtendedAgentGraph = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [agents, setAgents] = useState<ExtendedAgent[]>([]);
    const [tools, setTools] = useState<ExtendedTool[]>([]);
    const [connectors, setConnectors] = useState<ExtendedConnector[]>([]);
    const [triggers, setTriggers] = useState<ExtendedTrigger[]>([]);
    const [systemTools, setSystemTools] = useState<SystemTool[]>([]);

    const [nodes, setNodes] = useState<Node<ExtendedAgentGraphNode>[]>([]);
    const [edges, setEdges] = useState<Edge<ExtendedAgentGraphEdge>[]>([]);

    const [selectedNode, setSelectedNode] = useState<ExtendedAgentGraphNode | undefined>();
    const [hoveredNodeId, setHoveredNodeId] = useState<string | undefined>();

    const [anchorEntity, setAnchorEntity] = useState<ExtendedAgentAnchorEntity>();

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

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
        try {
            const [incidentResponse, scheduledResponse, filtersResponse] = await Promise.allSettled([
                fetch(`${sreAgentEndpoint}/api/v1/incidentPlayground/handlers`, {
                    headers: getAgentHeaders(),
                }),
                fetch(`${sreAgentEndpoint}/api/v1/scheduledTasks`, {
                    headers: getAgentHeaders(),
                }),
                fetch(`${sreAgentEndpoint}/api/v1/incidentPlayground/filters`, {
                    headers: getAgentHeaders(),
                }),
            ]);

            const allTriggers: ExtendedTrigger[] = [];

            // Process incident handlers
            if (incidentResponse.status === 'fulfilled' && incidentResponse.value.ok) {
                const incidentHandlers: IncidentHandler[] = await incidentResponse.value.json();

                // Also fetch filters to get the agent mapping
                let filters: IncidentFilter[] = [];
                if (filtersResponse.status === 'fulfilled' && filtersResponse.value.ok) {
                    filters = await filtersResponse.value.json();
                }

                // Create a map of filter ID to handling agent
                const filterAgentMap = new Map<string, string>();
                if (Array.isArray(filters)) {
                    filters.forEach((filter: any) => {
                        if (filter.id && filter.handlingAgent) {
                            filterAgentMap.set(filter.id, filter.handlingAgent);
                        }
                    });
                }

                if (Array.isArray(incidentHandlers)) {
                    const incidentTriggers: ExtendedTrigger[] = [];
                    incidentHandlers.forEach((handler: any) => {
                        const filter = filters.find(f => f.id === handler.incidentFilterId);
                        const agentName = filterAgentMap.get(handler.incidentFilterId) || handler.agentName;
                        if (agentName) {
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
                                titleContains: filter?.titleContains || '-',
                                enabled: !!filter?.isEnabled,
                                createdAt: handler.createdAt || new Date().toISOString(),
                            });
                        }
                    });
                    allTriggers.push(...incidentTriggers);
                }
            }

            // Process scheduled tasks
            if (scheduledResponse.status === 'fulfilled' && scheduledResponse.value.ok) {
                const scheduledTasks = await scheduledResponse.value.json();
                if (Array.isArray(scheduledTasks)) {
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
                }
            }

            setTriggers(allTriggers);
        } catch (err) {
            console.error('Error fetching triggers:', err);
            setError('Failed to load triggers');
        }
    }, [sreAgentEndpoint]);

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

    // Load all data
    useEffect(() => {
        const loadData = async () => {
            setLoading(true);
            setError(null);

            await Promise.all([fetchAgents(), fetchTools(), fetchConnectors(), fetchTriggers(), fetchSystemTools()]);

            setLoading(false);
        };

        loadData();
    }, [fetchAgents, fetchTools, fetchConnectors, fetchTriggers, fetchSystemTools]);

    // Build graph when data changes
    useEffect(() => {
        if (agents.length === 0 && tools.length === 0 && connectors.length === 0 && triggers.length === 0 && systemTools.length === 0) {
            setNodes([]);
            setEdges([]);
            return;
        }

        const { nodes: graphNodes, edges: graphEdges } = buildExtendedAgentGraph(agents, tools, connectors, triggers, systemTools);

        setNodes(graphNodes);
        setEdges(graphEdges);
    }, [agents, tools, connectors, triggers, systemTools]);

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
        triggers,
        systemTools,
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

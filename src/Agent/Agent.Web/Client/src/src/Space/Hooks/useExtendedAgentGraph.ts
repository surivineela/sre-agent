import { Edge, Node } from '@xyflow/react';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import {
    ExtendedAgent,
    ExtendedAgentFilters,
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    PaginatedResponse,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import { buildExtendedAgentGraph, filterGraphBySearch } from '../Graph/ExtendedAgentGraphUtility';

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

    const [filters, setFilters] = useState<ExtendedAgentFilters>({
        agentName: undefined,
        agentType: 'All',
        toolType: 'All',
        triggerType: 'All',
        searchQuery: '',
    });

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
            setAgents(data.data);
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
                const incidentHandlers = await incidentResponse.value.json();

                // Also fetch filters to get the agent mapping
                let filters: any[] = [];
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
                    const incidentTriggers: ExtendedTrigger[] = incidentHandlers.map((handler: any) => ({
                        name: handler.name || handler.id,
                        description: handler.description || 'Incident response handler',
                        type: 'incident' as const,
                        agentName: filterAgentMap.get(handler.incidentFilterId) || handler.agentName, // Get agent from filter
                        status: handler.enabled ? 'Active' : 'Disabled',
                        priority: handler.priority || 'Sev2',
                        incidentType: handler.incidentType || 'ServiceIssue',
                        service: handler.service,
                        severity: handler.severity,
                        enabled: handler.enabled !== false,
                        createdAt: handler.createdAt || new Date().toISOString(),
                    }));
                    allTriggers.push(...incidentTriggers);
                }
            } // Process scheduled tasks
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
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/systemtools`, {
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
        if (filters.agentName) {
            const rootId = `agent_${filters.agentName}`;

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

        // Filter by agent type
        if (filters.agentType && filters.agentType !== 'All') {
            const agentNodeIds = filteredNodes.filter(node => node.data?.agentType === filters.agentType).map(node => node.id);

            const connectedNodeIds = new Set(agentNodeIds);
            filteredEdges.forEach(edge => {
                if (agentNodeIds.includes(edge.source)) {
                    connectedNodeIds.add(edge.target);
                }
            });

            filteredNodes = filteredNodes.filter(node => connectedNodeIds.has(node.id));
            filteredEdges = filteredEdges.filter(edge => connectedNodeIds.has(edge.source) && connectedNodeIds.has(edge.target));
        }

        // Filter by tool type
        if (filters.toolType && filters.toolType !== 'All') {
            const toolNodeIds = filteredNodes.filter(node => node.data?.toolType === filters.toolType).map(node => node.id);

            const connectedNodeIds = new Set(toolNodeIds);
            filteredEdges.forEach(edge => {
                if (toolNodeIds.includes(edge.target)) {
                    connectedNodeIds.add(edge.source);
                }
                if (toolNodeIds.includes(edge.source)) {
                    connectedNodeIds.add(edge.target);
                }
            });

            filteredNodes = filteredNodes.filter(node => connectedNodeIds.has(node.id));
            filteredEdges = filteredEdges.filter(edge => connectedNodeIds.has(edge.source) && connectedNodeIds.has(edge.target));
        }

        // Filter by trigger type
        if (filters.triggerType && filters.triggerType !== 'All') {
            const triggerNodeIds = filteredNodes
                .filter(node => node.data?.triggerType === filters.triggerType || (node.data as any)?.type === filters.triggerType)
                .map(node => node.id);

            const connectedNodeIds = new Set(triggerNodeIds);
            filteredEdges.forEach(edge => {
                if (triggerNodeIds.includes(edge.source)) {
                    connectedNodeIds.add(edge.target);
                }
                if (triggerNodeIds.includes(edge.target)) {
                    connectedNodeIds.add(edge.source);
                }
            });

            filteredNodes = filteredNodes.filter(node => connectedNodeIds.has(node.id));
            filteredEdges = filteredEdges.filter(edge => connectedNodeIds.has(edge.source) && connectedNodeIds.has(edge.target));
        }

        // Filter by search query
        if (filters.searchQuery && filters.searchQuery.trim()) {
            const result = filterGraphBySearch(filteredNodes, filteredEdges, filters.searchQuery);
            filteredNodes = result.nodes;
            filteredEdges = result.edges;
        }

        return { nodes: filteredNodes, edges: filteredEdges };
    }, [nodes, edges, filters, triggers]);

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
        filters,
        setFilters,
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

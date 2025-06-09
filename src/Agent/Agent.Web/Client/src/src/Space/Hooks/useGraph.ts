import { Edge, Node, useEdgesState, useNodesState, useReactFlow } from '@xyflow/react';
import axios from 'axios';
import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { GraphEdge, GraphNode, Resource, ResourceExtended } from '../Contracts/Graph';
import { getNewNodesAndEdges, getSubscriptionIdFromNodeId } from '../Graph/Utility';
import { useGraphLayout } from './useGraphLayout';

export const useGraph = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [graph, setGraph] = useState<Map<string, { appGroupNode?: Node<GraphNode>; nodes: Node<GraphNode>[]; edges: Edge<GraphEdge>[] }>>(
        new Map<string, { nodes: Node<GraphNode>[]; edges: Edge<GraphEdge>[] }>()
    );
    const [isLoading, setIsLoading] = useState(false);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();
    const [hoveredNodeId, setHoveredNodeId] = useState<string>();
    const [nodes, setNodes, onNodesChange] = useNodesState<Node<GraphNode>>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<Edge<GraphEdge>>([]);
    const [nodesToHighlight, setNodesToHighlight] = useState<string[]>([]);
    const [edgesToHighlight, setEdgesToHighlight] = useState<string[]>([]);
    const [selectedAppGroupId, setSelectedAppGroupId] = useState<string | undefined>(undefined);

    const layoutGraph = useGraphLayout();

    const { fitView } = useReactFlow();

    const getResources = async (subscriptionId: string, resourceId: string): Promise<Resource[]> => {
        try {
            const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/${subscriptionId}/appGroups/${resourceId}`, {
                headers: getAgentHeaders(),
            });
            return data ?? [];
        } catch {
            return [];
        }
    };

    const hoverNode = useCallback(
        (nodeId: string) => {
            const nodeIds = [nodeId];
            const edgeIds = [];
            for (const edge of edges) {
                if (edge.source === nodeId) {
                    nodeIds.push(edge.target);
                    edgeIds.push(edge.id);
                }
            }

            setHoveredNodeId(nodeId);
            setNodesToHighlight(nodeIds);
            setEdgesToHighlight(edgeIds);
        },
        [edges]
    );

    const unHoverNode = useCallback(() => {
        setHoveredNodeId(undefined);
        setNodesToHighlight([]);
        setEdgesToHighlight([]);
    }, []);

    const onAppGroupUpdate = useCallback(
        async (appGroup?: ResourceExtended) => {
            setIsLoading(true);

            setSelectedAppGroupId(undefined);

            if (appGroup) {
                if (!graph.has(appGroup.id)) {
                    const resources = await getResources(getSubscriptionIdFromNodeId(appGroup.id), appGroup.id);
                    const { appGroupNode, nodes, edges } = getNewNodesAndEdges(appGroup, resources);
                    layoutGraph(nodes, edges).then(result => {
                        setGraph(prev => {
                            const newGraph = new Map(prev);
                            newGraph.set(appGroup.id, { appGroupNode, ...result });
                            return newGraph;
                        });
                        setNodes(result.nodes);
                        setEdges(result.edges);
                        setIsLoading(prev => {
                            if (prev) {
                                return false;
                            }
                            return prev;
                        });
                    });
                    setSelectedNode(appGroupNode.data);
                } else {
                    const { appGroupNode, nodes, edges } = graph.get(appGroup.id) ?? { nodes: [], edges: [] };
                    setNodes(nodes);
                    setEdges(edges);
                    setIsLoading(false);
                    setSelectedNode(appGroupNode?.data);
                }
            } else {
                setNodes([]);
                setEdges([]);
                setIsLoading(false);
                setSelectedNode(undefined);
            }

            setSelectedAppGroupId(appGroup?.id);
        },
        [graph, setNodes, setEdges]
    );

    useEffect(() => {
        if (nodes.length > 0 && !isLoading) {
            fitView();
        }
    }, [nodes.length, isLoading]);

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        isLoading,
        selectedNode,
        setSelectedNode,
        hoveredNodeId,
        onAppGroupUpdate,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        selectedAppGroupId,
    };
};

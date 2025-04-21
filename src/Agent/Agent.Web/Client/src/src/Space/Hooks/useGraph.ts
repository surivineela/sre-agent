import axios from 'axios';
import { useCallback, useEffect, useState } from 'react';
import { Node, Edge, useNodesState, useEdgesState, useReactFlow } from '@xyflow/react';
import { GraphEdge, GraphNode, Resource, ResourceExtended } from '../Contracts/Graph';
import { getNewNodesAndEdges, getSubscriptionIdFromNodeId } from '../Graph/Utility';
import { useGraphLayout } from './useGraphLayout';

export const getResources = async (subscriptionId: string, resourceId: string): Promise<Resource[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups/${resourceId}`);
        return data ?? [];
    } catch {
        return [];
    }
}

export const useGraph = () => {
    const [graph, setGraph] = useState<Map<string, { nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] }>>(
        new Map<string, { nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] }>()
    );
    const [selectedAppGroupId, setSelectedAppGroupId] = useState<string>();
    const [isLoading, setIsLoading] = useState(false);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();
    const [nodes, setNodes, onNodesChange] = useNodesState<Node<GraphNode>>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<Edge<GraphEdge>>([]);
    const [nodesToHightlight, setNodesToHightlight] = useState<string[]>([]);
    const [edgesToHightlight, setEdgesToHightlight] = useState<string[]>([]);

    const layoutGraph = useGraphLayout();

    const { fitView } = useReactFlow();

    const openPanel = useCallback((node: GraphNode) => {
        setSelectedNode(node);
        setIsPanelOpen(true);
    }, []);

    const closePanel = useCallback(() => {
        setIsPanelOpen(false);
        setSelectedNode(undefined);
    }, []);

    const hoverNode = useCallback((nodeId: string) => {
        const nodeIds = [nodeId];
        const edgeIds = [];
        for (const edge of edges) {
            if (edge.source === nodeId) {
                nodeIds.push(edge.target);
                edgeIds.push(edge.id);
            }
        }

        setNodesToHightlight(nodeIds);
        setEdgesToHightlight(edgeIds);

    }, [edges]);

    const unHoverNode = useCallback(() => {
        setNodesToHightlight([]);
        setEdgesToHightlight([]);
    }, []);

    const onAppGroupUpdate = useCallback(async (appGroup?: ResourceExtended) => {
        closePanel();

        setIsLoading(true);

        if (appGroup) {
            if (!graph.has(appGroup.id)) {
                const resources = await getResources(getSubscriptionIdFromNodeId(appGroup.id), appGroup.id);
                const { nodes, edges } = getNewNodesAndEdges(appGroup, resources);
                layoutGraph(nodes, edges, appGroup.id).then(result => {
                    setGraph(prev => {
                        const newGraph = new Map(prev);
                        newGraph.set(appGroup.id, { ...result });
                        return newGraph;
                    });
                    setNodes(result.nodes);
                    setEdges(result.edges);
                    setSelectedAppGroupId(appGroup?.id);
                    setIsLoading(prev => {
                        if (prev) {
                            return false;
                        }
                        return prev;
                    });
                });
            } else {
                const { nodes, edges } = graph.get(appGroup.id) ?? { nodes: [], edges: [] };
                setNodes(nodes);
                setEdges(edges);
                setSelectedAppGroupId(appGroup?.id);
                setIsLoading(false);
            }
        } else {
            setNodes([]);
            setEdges([]);
            setSelectedAppGroupId(undefined);
            setIsLoading(false);
        }
    }, [graph, closePanel, setNodes, setEdges]);

    useEffect(() => {
        if (nodes.length > 0 && !isLoading) {
            fitView();
        }

    }, [nodes.length, isLoading])

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        isLoading,
        openPanel,
        closePanel,
        isPanelOpen,
        selectedNode,
        onAppGroupUpdate,
        selectedAppGroupId,
        hoverNode,
        unHoverNode,
        nodesToHightlight,
        edgesToHightlight
    };
};

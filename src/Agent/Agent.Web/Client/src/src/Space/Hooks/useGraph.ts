import axios from 'axios';
import { useCallback, useEffect, useReducer, useState } from 'react';
import { Node, Edge, useNodesState, useEdgesState, useReactFlow } from '@xyflow/react';
import { GraphEdge, GraphNode, Resource, ResourceExtended } from '../Contracts/Graph';
import { getNewNodesAndEdges, getSourceAndTargetHandleId, getSubscriptionIdFromNodeId, traverseGraph } from '../Graph/Utility';
import { useElkLayout } from './useElkLayout';

export const getResources = async (subscriptionId: string, resourceId: string): Promise<Resource[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups/${resourceId}`);
        return data ?? [];
    } catch {
        return [];
    }
}

const GraphReducer = (
    state: { nodeMap: Map<string, Node<GraphNode>>, edgeMap: Map<string, Edge<GraphEdge>> },
    action: {
        type: 'ADD_APP_GROUP',
        payload: {
            newNodeMap: Map<string, Node<GraphNode>>;
            newEdgeMap: Map<string, Edge<GraphEdge>>;
        }
    }
) => {
    const { type, payload: { newNodeMap, newEdgeMap } } = action;

    const { nodeMap, edgeMap } = state;

    if (type === 'ADD_APP_GROUP') {
        for (const [key, value] of newNodeMap.entries()) {
            const currentChildrenId = nodeMap.get(key)?.data.childrenIds ?? [];
            const newChildrenId = value.data.childrenIds ?? [];
            const mergedChildrenId = Array.from(new Set([...currentChildrenId, ...newChildrenId]));
            nodeMap.set(key, { ...value, data: { ...value.data, childrenIds: mergedChildrenId } });
        }
        for (const [key, value] of newEdgeMap.entries()) {
            edgeMap.set(key, value);
        }

        return { nodeMap, edgeMap };
    }

    return state;
}

export const useGraph = () => {
    const [graph, dispatch] = useReducer(GraphReducer, { nodeMap: new Map<string, Node<GraphNode>>(), edgeMap: new Map<string, Edge<GraphEdge>>() });
    const [computedGraph, setComputedGraph] = useState<Map<string, { nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] }>>(new Map<string, { nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] }>());
    const [selectedAppGroupId, setSelectedAppGroupId] = useState<string>();
    const [isLoading, setIsLoading] = useState(false);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();
    const [nodes, setNodes, onNodesChange] = useNodesState<Node<GraphNode>>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<Edge<GraphEdge>>([]);
    const [nodesToHightlight, setNodesToHightlight] = useState<string[]>([]);
    const [edgesToHightlight, setEdgesToHightlight] = useState<string[]>([]);

    const layoutGraph = useElkLayout();

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
        const { edgeMap } = graph;
        const nodeIds = [nodeId];
        const edgeIds = [];
        for (const [edgeId, edge] of edgeMap.entries()) {
            if (edge.source === nodeId) {
                nodeIds.push(edge.target);
                edgeIds.push(edgeId);
            }
        }

        setNodesToHightlight(nodeIds);
        setEdgesToHightlight(edgeIds);

    }, [graph]);

    const unHoverNode = useCallback(() => {
        setNodesToHightlight([]);
        setEdgesToHightlight([]);
    }, []);

    const onAppGroupUpdate = useCallback(async (appGroup?: ResourceExtended) => {
        closePanel();

        setIsLoading(true);

        if (appGroup) {
            if (!graph.nodeMap.has(appGroup.id)) {
                const resources = await getResources(getSubscriptionIdFromNodeId(appGroup.id), appGroup.id);
                const { nodeMap, edgeMap } = getNewNodesAndEdges(appGroup, resources);
                dispatch({ type: 'ADD_APP_GROUP', payload: { newNodeMap: nodeMap, newEdgeMap: edgeMap } });
            }
            setSelectedAppGroupId(appGroup.id);
        } else {
            setSelectedAppGroupId(undefined);
        }
    }, [graph, closePanel]);

    useEffect(() => {
        let isSubscribed = true;

        const computeNodesAndEdges = () => {
            if (selectedAppGroupId) {
                if (computedGraph.has(selectedAppGroupId)) {
                    const { nodes, edges } = computedGraph.get(selectedAppGroupId) ?? { nodes: [], edges: [] };
                    setNodes(nodes);
                    setEdges(edges);
                } else {
                    const { nodeMap, edgeMap } = graph;

                    const { nodes: nodesArray, edges: edgesArray } = traverseGraph(nodeMap, edgeMap, selectedAppGroupId);

                    layoutGraph(nodesArray, edgesArray, selectedAppGroupId).then((layout: any) => {
                        const nodes: Node<GraphNode>[] = (layout.children ?? []).map((node: any) => ({ ...node, position: { x: node.x ?? 0, y: node.y ?? 0 } }));
                        const edges: Edge<GraphEdge>[] = (layout.edges ?? []).map((edge: any) => {
                            const source = nodes.find(node => node.id === edge.sources[0]);
                            const target = nodes.find(node => node.id === edge.targets[0]);
                            const edgeResult: Edge<GraphEdge> = { ...edge, id: edge.id, source: edge.sources[0], target: edge.targets[0] };

                            if (source && target) {
                                const sourcePos = source.position;
                                const targetPos = target.position;

                                const { sourceHandle, targetHandle } = getSourceAndTargetHandleId(sourcePos, targetPos, source.id === selectedAppGroupId, target.id === selectedAppGroupId);

                                edgeResult.sourceHandle = sourceHandle;
                                edgeResult.targetHandle = targetHandle;
                            }
                            return edgeResult;
                        });

                        if (isSubscribed) {
                            setComputedGraph(prev => {
                                const newComputedGraph = new Map(prev);
                                newComputedGraph.set(selectedAppGroupId, { nodes, edges });
                                return newComputedGraph;
                            });
                            setIsLoading(false);
                        }
                    }).catch(() => {

                        if (isSubscribed) {
                            setNodes(nodesArray);
                            setEdges(edgesArray);
                            setIsLoading(false);
                        }
                    })
                }
            } else {
                setNodes([]);
                setEdges([]);
                setIsLoading(false);
            }

        };

        computeNodesAndEdges();

        return () => {
            isSubscribed = false;
        }

    }, [graph, selectedAppGroupId, setNodes, setEdges, computedGraph]);

    useEffect(() => {
        fitView();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [nodes.length])

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

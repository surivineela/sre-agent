import axios from 'axios';
import { useCallback, useEffect, useReducer, useState } from 'react';
import { Node, Edge, useNodesState, useEdgesState, useReactFlow } from '@xyflow/react';
import { GraphEdge, GraphNode, Resource, ResourceExtended, Subscription } from '../Contracts/Graph';
import ELK, { LayoutOptions } from 'elkjs';
import { getLinkId, getSourceAndTargetHandleId } from '../Graph/Utility';
import { CUSTOM_EDGE_TYPE, GRAF_CARD_TYPE } from '../Graph/Constants';

const elk = new ELK();

export const getResources = async (subscriptionId: string, resourceId: string): Promise<Resource[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups/${resourceId}`);
        return data ?? [];
    } catch {
        return [];
    }
}

export const createSubscriptionNode = (subscription: Subscription): GraphNode => {
    const { id, name } = subscription;
    const node: GraphNode = {
        id,
        name,
        subscriptionId: id,
        type: 'subscription',
        isVisible: true,
    };
    return node;
}

export const createAppGroupNode = (appGroup: ResourceExtended): GraphNode => {
    const { id, name } = appGroup;

    const subscriptionId = id.split('_')[2];

    const node: GraphNode = {
        id,
        name,
        type: 'appGroup',
        subscriptionId: subscriptionId,
        properties: {
            ...appGroup
        },
        isVisible: true,
    }

    return node;
}

export const createResourceNode = (resource: Resource): GraphNode => {
    const { name, resourceId } = resource;

    const subscriptionId = resourceId.split('_')[2];

    const node: GraphNode = {
        id: resourceId,
        name,
        type: 'resource',
        subscriptionId,
        properties: {
            ...resource
        },
        isVisible: true,
    }

    return node;
}

export const getNodeSize = (type: 'subscription' | 'appGroup' | 'resource'): number => {

    switch (type) {
        case 'subscription':
            return 300;
        case 'appGroup':
            return 250;
        default:
            return 200;
    }
}

const GraphReducer = (
    state: Map<string, { nodeMap: Map<string, Node<GraphNode>>, linkMap: Map<string, Edge<GraphEdge>> }>,
    action: {
        type: 'HIDE_NODES' | 'SHOW_NODES' | 'ADD_NODES',
        payload: {
            appGroupId: string;
            appGroupNode?: GraphNode,
            parentGraphNode?: GraphNode,
            nodeIdsToHide?: string[],
            nodeIdsToShow?: string[],
            nodesToAdd?: GraphNode[]
        }
    }
) => {
    const { type, payload: { appGroupId, appGroupNode, parentGraphNode, nodeIdsToHide, nodeIdsToShow, nodesToAdd } } = action;

    const appGroupMap = state.get(appGroupId);
    const originalNodeMap = appGroupMap?.nodeMap ?? new Map<string, Node<GraphNode>>();
    const originalLinkMap = appGroupMap?.linkMap ?? new Map<string, Edge<GraphEdge>>();
    const shouldAddAppGroupNode = !appGroupMap;
    const nodeMap = new Map<string, Node<GraphNode>>(originalNodeMap);
    const linkMap = new Map<string, Edge<GraphEdge>>(originalLinkMap);

    // if appGroup does not exist in the map, add it to the map. Set the app group node to visible.
    if (shouldAddAppGroupNode) {
        if (appGroupNode) {
            nodeMap.set(appGroupId, { id: appGroupNode.id, type: GRAF_CARD_TYPE, position: { x: 0, y: 0 }, data: { ...appGroupNode } });
        } else {
            return state;
        }
    } else {
        nodeMap.set(appGroupId, { ...nodeMap.get(appGroupId)!, data: { ...nodeMap.get(appGroupId)!.data, isVisible: true } });
    }

    if (type === 'HIDE_NODES' && nodeIdsToHide && parentGraphNode) {
        const nodes = [...nodeIdsToHide];
        const nodeIdSet = new Set<string>();

        while (nodes.length > 0) {
            const nodeId = nodes.shift();
            if (nodeId && !nodeIdSet.has(nodeId)) {
                nodeIdSet.add(nodeId);

                nodes.push(...(nodeMap.get(nodeId)?.data.childrenIds ?? []));
            }
        }
        const nodesToHide = Array.from(nodeIdSet);

        for (const nodeId of nodesToHide) {
            const node = nodeMap.get(nodeId);
            if (node && node.id !== parentGraphNode.id) {
                nodeMap.set(nodeId, { ...node, data: { ...node.data, isVisible: false } });
            }
        }
    } else if (type === 'SHOW_NODES' && nodeIdsToShow) {

        for (const nodeId of nodeIdsToShow) {
            const node = nodeMap.get(nodeId);
            if (node) {
                nodeMap.set(nodeId, { ...node, data: { ...node.data, isVisible: true } });
            }
        }
    } else if (type === 'ADD_NODES' && nodesToAdd) {

        for (const node of nodesToAdd) {
            if (!nodeMap.get(node.id)) {
                nodeMap.set(node.id, { id: node.id, data: node, type: GRAF_CARD_TYPE, position: { x: 0, y: 0 } });
            }

            if (parentGraphNode) {
                const linkId = getLinkId(parentGraphNode.id, node.id);
                const link: Edge<GraphEdge> = {
                    id: linkId,
                    type: CUSTOM_EDGE_TYPE,
                    source: parentGraphNode.id,
                    target: node.id,
                    data: { label: 'link', isVisible: true },
                };
                linkMap.set(linkId, link);
            }
        };

        if (parentGraphNode) {
            const parentNode = nodeMap.get(parentGraphNode.id);
            if (parentNode) {
                nodeMap.set(parentNode.id, { ...parentNode, data: { ...parentNode.data, childrenIds: nodesToAdd.map(node => node.id) } });
            }
        }
    } else {
        return state;
    }

    state.set(appGroupId, { nodeMap, linkMap });

    return new Map<string, { nodeMap: Map<string, Node<GraphNode>>, linkMap: Map<string, Edge<GraphEdge>> }>(state)
}

export const useGraph = () => {
    const [graphs, dispatch] = useReducer(GraphReducer, new Map<string, { nodeMap: Map<string, Node<GraphNode>>, linkMap: Map<string, Edge<GraphEdge>> }>());
    const [selectedAppGroupId, setSelectedAppGroupId] = useState<string>();
    const [isLoading, setIsLoading] = useState(false);
    const [isLoadingSubresources, setIsLoadingSubresources] = useState(false);
    const [isComputingPosition, setIsComputingPosition] = useState(false);
    const [isPanelOpen, setIsPanelOpen] = useState(false);
    const [selectedNode, setSelectedNode] = useState<GraphNode>();
    const [nodes, setNodes, onNodesChange] = useNodesState<Node<GraphNode>>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<Edge<GraphEdge>>([]);
    const [nodesToHightlight, setNodesToHightlight] = useState<string[]>([]);
    const [edgesToHightlight, setEdgesToHightlight] = useState<string[]>([]);

    const { fitView } = useReactFlow();

    const _queryNodes = useCallback(async (parentNode: GraphNode) => {
        const { id, subscriptionId } = parentNode;

        const resources = await getResources(subscriptionId, id);
        return resources.map(resource => createResourceNode(resource));

    }, []);

    const showSubresources = useCallback(async (parentGraphNode: GraphNode) => {
        let childrenIds = parentGraphNode.childrenIds;

        if (!childrenIds) {
            setIsLoadingSubresources(true);
            const newNodes = await _queryNodes(parentGraphNode);
            dispatch({
                type: 'ADD_NODES',
                payload: {
                    appGroupId: selectedAppGroupId ?? '',
                    parentGraphNode,
                    nodesToAdd: newNodes
                }
            });
            childrenIds = newNodes.map(node => node.id);
            setIsLoadingSubresources(false);
        }

        dispatch({
            type: 'SHOW_NODES',
            payload: {
                appGroupId: selectedAppGroupId ?? '',
                parentGraphNode,
                nodeIdsToShow: childrenIds
            }
        });
    }, [_queryNodes, selectedAppGroupId]);

    const hideSubresources = useCallback((parentGraphNode: GraphNode) => {
        dispatch({
            type: 'HIDE_NODES',
            payload: {
                appGroupId: selectedAppGroupId ?? '',
                parentGraphNode,
                nodeIdsToHide: parentGraphNode.childrenIds
            }
        })
    }, [selectedAppGroupId]);

    const areSubresourcesVisible = useCallback((node: GraphNode) => {
        const graph = selectedAppGroupId && graphs.get(selectedAppGroupId);

        if (graph) {
            const { nodeMap } = graph;
            const parentNode = nodeMap.get(node.id);
            const childrenIds = parentNode?.data.childrenIds;
            return !!childrenIds && childrenIds.length > 0 && childrenIds.some(childId => nodeMap.get(childId)?.data.isVisible)
        }
        return false;
    }, [selectedAppGroupId, graphs]);

    const openPanel = useCallback((node: GraphNode) => {
        setSelectedNode(node);
        setIsPanelOpen(true);
    }, []);

    const closePanel = useCallback(() => {
        setIsPanelOpen(false);
        setSelectedNode(undefined);
    }, []);

    const hoverNode = useCallback((nodeId: string) => {
        const graph = selectedAppGroupId && graphs.get(selectedAppGroupId);
        if (graph) {
            const { nodeMap } = graph;
            const node = nodeMap.get(nodeId);
            const childrenNodeIds = node?.data.childrenIds ?? [];
            const linkIds = childrenNodeIds.map(childId => getLinkId(nodeId, childId));
            if (node) {
                setNodesToHightlight([node.id, ...childrenNodeIds]);
                setEdgesToHightlight(linkIds);
            }
        }
    }, [graphs, selectedAppGroupId]);

    const unHoverNode = useCallback(() => {
        setNodesToHightlight([]);
        setEdgesToHightlight([]);
    }, []);

    const onAppGroupUpdate = useCallback(async (appGroup?: ResourceExtended) => {
        closePanel();

        if (appGroup) {
            if (!graphs.has(appGroup.id)) {
                setIsLoading(true);
                const appGroupNode = createAppGroupNode(appGroup);
                const newNodes = await _queryNodes(appGroupNode)
                dispatch({
                    type: 'ADD_NODES',
                    payload: {
                        appGroupId: appGroup.id,
                        appGroupNode,
                        parentGraphNode: appGroupNode,
                        nodesToAdd: newNodes
                    }
                });
                setIsLoading(false);
            }
            setSelectedAppGroupId(appGroup.id);
        } else {
            setSelectedAppGroupId(undefined);
        }
    }, [graphs, _queryNodes, closePanel]);

    useEffect(() => {
        let isSubscribed = true;

        const computeNodesAndEdges = async () => {
            const graph = selectedAppGroupId && graphs.get(selectedAppGroupId);

            if (graph) {
                const { nodeMap, linkMap } = graph;
                setIsComputingPosition(true);

                const nodesArray = Array.from(nodeMap.values()).filter(node => node.data.isVisible);
                const linksArray = Array.from(linkMap.values()).filter(link => {
                    const sourceNode = nodeMap.get(link.source);
                    const targetNode = nodeMap.get(link.target);
                    return sourceNode?.data.isVisible && targetNode?.data.isVisible;
                });

                let layoutOptions: LayoutOptions = nodesArray.length <= 10 ? {
                    'elk.algorithm': 'org.eclipse.elk.layered',                         // the actual algorithm
                    'elk.direction': 'RIGHT',                            // TOP, RIGHT, LEFT, DOWN
                    'elk.layered.spacing.nodeNodeBetweenLayers': '100',
                } : {
                    'elk.algorithm': 'org.eclipse.elk.force',
                    'elk.force.repulsivePower': '0',
                }

                layoutOptions = {
                    ...layoutOptions,
                    'elk.spacing.nodeNode': nodesArray.length < 10 ? '25' : '5', // Reduce space between nodes
                    'elk.spacing.edgeNode': '10',
                    'elk.spacing.edgeEdge': '10',
                }
                const layout = await elk.layout({
                    id: 'root',
                    children: nodesArray.map(node => ({
                        ...node,
                        x: node.position.x,
                        y: node.position.y,
                        width: 200,
                        height: 170,
                    })),
                    edges: linksArray.map(link => ({
                        ...link,
                        id: link.id,
                        sources: [link.source],
                        targets: [link.target],
                        labels: [{ text: link.data?.label ?? "" }]
                    })),
                },
                    {
                        layoutOptions
                    }
                );

                const nodes: Node<GraphNode>[] = (layout.children ?? []).map(node => ({ ...node, position: { x: node.x ?? 0, y: node.y ?? 0 } }));
                const links: Edge<GraphEdge>[] = (layout.edges ?? []).map(link => {
                    const sourcePos = nodes.find(node => node.id === link.sources[0])?.position;
                    const targetPos = nodes.find(node => node.id === link.targets[0])?.position;
                    const linkResult: Edge<GraphEdge> = { ...link, id: link.id, source: link.sources[0], target: link.targets[0] };

                    if (sourcePos && targetPos) {
                        const { sourceHandle, targetHandle } = getSourceAndTargetHandleId(sourcePos, targetPos);
                        linkResult.sourceHandle = sourceHandle;
                        linkResult.targetHandle = targetHandle;
                    }
                    return linkResult;
                });

                if (isSubscribed) {
                    setNodes(nodes);
                    setEdges(links);
                    setIsComputingPosition(false);
                }
            } else {
                setNodes([]);
                setEdges([]);
                setIsComputingPosition(false);
            }

        };

        computeNodesAndEdges();

        return () => {
            isSubscribed = false;
        }

    }, [graphs, selectedAppGroupId, setNodes, setEdges]);

    useEffect(() => {
        fitView();
    }, [nodes.length])

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        isLoading,
        isLoadingSubresources,
        showSubresources,
        hideSubresources,
        areSubresourcesVisible,
        openPanel,
        closePanel,
        isPanelOpen,
        selectedNode,
        onAppGroupUpdate,
        selectedAppGroupId,
        hoverNode,
        unHoverNode,
        nodesToHightlight,
        edgesToHightlight,
        isComputingPosition,
    };
};

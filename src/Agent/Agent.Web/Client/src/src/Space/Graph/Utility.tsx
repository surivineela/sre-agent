import { Edge, Node } from "@xyflow/react";
import { CUSTOM_EDGE_TYPE, GRAPH_CARD_TYPE, GraphEdge, GraphNode, HandlePosition, NodeSize, Resource, ResourceExtended, ScoreCardObject } from "../Contracts/Graph";

export const getAppHealthStatus = (nodeProperties?: Resource) => {
    const appHealthInfo = nodeProperties?.appHealthInfo?.[0]
    if (appHealthInfo) {
        try {
            const healthInfo: ScoreCardObject = JSON.parse(appHealthInfo);
            return healthInfo.Health;
        } catch {
            return undefined;
        }
    }

    return undefined;
}

export const getSubscriptionIdFromNodeId = (nodeId: string): string => {
    return nodeId.split('_')[2] ?? '';
}

export const createAppGroupNode = (appGroup: ResourceExtended): Node<GraphNode> => {
    const { id, name, type, dashboardUrl, appHealthInfo, properties } = appGroup;

    const resource: Resource = {
        name,
        type,
        dashboardUrl: dashboardUrl || properties?.dashboardUrl?.[0] || '',
        resourceId: id,
        appHealthInfo: appHealthInfo || properties?.appHealthInfo || [],
    }

    return createNode(resource);
}

export const createNode = (resource: Resource): Node<GraphNode> => {
    const { name, resourceId } = resource;

    const subscriptionId = getSubscriptionIdFromNodeId(resourceId);

    const node: Node<GraphNode> = {
        id: resourceId,
        type: GRAPH_CARD_TYPE,
        position: { x: 0, y: 0 },
        data: {
            id: resourceId,
            name,
            subscriptionId,
            childrenIds: [],
            properties: {
                ...resource
            },
        }
    }

    return node;
}

export const createGraphEdge = (sourceId: string, targetId: string): Edge<GraphEdge> => {
    const edgeId = getEdgeId(sourceId, targetId);

    const edge: Edge<GraphEdge> = {
        id: edgeId,
        type: CUSTOM_EDGE_TYPE,
        source: sourceId,
        target: targetId,
        data: { label: 'edge' },
    };

    return edge;
}

export const getNewNodesAndEdges = (appGroup: ResourceExtended, resources: Resource[]): { nodeMap: Map<string, Node<GraphNode>>, edgeMap: Map<string, Edge<GraphEdge>> } => {
    const nodeMap = new Map<string, Node<GraphNode>>();
    const edgeMap = new Map<string, Edge<GraphEdge>>();

    const appGroupNode = createAppGroupNode(appGroup);
    nodeMap.set(appGroupNode.id, appGroupNode);

    const populateResourceNodesAndEdges = (parentNode: Node<GraphNode>, resources: Resource[]) => {
        const childrenIds: string[] = [];

        for (const resource of resources) {
            const node = createNode(resource);
            const edge = createGraphEdge(parentNode.id, node.id);

            nodeMap.set(node.id, node);
            edgeMap.set(edge.id, edge);

            if (resource.subItems && resource.subItems.length > 0) {
                populateResourceNodesAndEdges(node, resource.subItems);
            }

            childrenIds.push(node.id);
        }

        nodeMap.set(parentNode.id, { ...parentNode, data: { ...parentNode.data, childrenIds } });
    }

    populateResourceNodesAndEdges(appGroupNode, resources);

    return {
        nodeMap,
        edgeMap
    }
}

export const traverseGraph = (nodeMap: Map<string, Node<GraphNode>>, edgeMap: Map<string, Edge<GraphEdge>>, rootNodeId: string): { nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] } => {
    const nodes: Node<GraphNode>[] = [];
    const edges: Edge<GraphEdge>[] = [];

    const nodeIdSet = new Set<string>();
    const queue: Node<GraphNode>[] = nodeMap.get(rootNodeId) ? [nodeMap.get(rootNodeId)!] : [];

    while (queue.length > 0) {
        const node = queue.shift();
        if (node && !nodeIdSet.has(node.id)) {
            nodeIdSet.add(node.id);
            nodes.push(node);
            const childrenIds = node.data.childrenIds;
            if (childrenIds && childrenIds.length > 0) {
                for (const childId of childrenIds) {
                    const childNode = nodeMap.get(childId);
                    if (childNode) {
                        queue.push(childNode);
                    }
                }
            }

        }
    }

    for (const edge of edgeMap.values()) {
        if (nodeIdSet.has(edge.source) && nodeIdSet.has(edge.target)) {
            edges.push(edge);
        }
    }

    return {
        nodes,
        edges
    }
}

export const getEdgeId = (parentNodeId: string, childNodeId: string) => {
    return `${parentNodeId}-${childNodeId}`;
}

export const getHandleId = (position: HandlePosition, isTarget: boolean) => {
    return `${position}-${isTarget ? 'in' : 'out'}`;
}

export const getSourceAndTargetHandleId = (leftCenterPos: { x: number, y: number }, rightcenterPos: { x: number, y: number }): { sourceHandle: string; targetHandle: string } => {

    const getHandlePosition = (x: number, y: number, position: HandlePosition) => {
        switch (position) {
            case 'T':
                return { x: x, y: y - NodeSize.height / 2 };
            case 'B':
                return { x: x, y: y + NodeSize.height / 2 };
            case 'L':
                return { x: x - NodeSize.width / 2, y: y };
            case 'R':
                return { x: x + NodeSize.width / 2, y: y };
            default:
                return { x, y };
        }
    }

    const getDistance = (x1: number, y1: number, x2: number, y2: number) => {
        return (x2 - x1) ** 2 + (y2 - y1) ** 2;
    }

    let minDistance = Number.MAX_VALUE;
    let sourceHandle = getHandleId('T', false);
    let targetHandle = getHandleId('B', true);
    const positions: HandlePosition[] = ['T', 'B', 'L', 'R']

    const sourceNodeHandlePos = [
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[0]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[1]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[2]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[3])
    ];
    const targetNodeHandlePos = [
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[0]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[1]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[2]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[3])
    ];


    for (let i = 0; i < 4; i++) {
        for (let j = 0; j < 4; j++) {
            const sourcePos = sourceNodeHandlePos[i];
            const targetPos = targetNodeHandlePos[j];

            const distance = getDistance(sourcePos.x, sourcePos.y, targetPos.x, targetPos.y);

            if (distance < minDistance) {
                minDistance = distance;
                sourceHandle = getHandleId(positions[i], false);
                targetHandle = getHandleId(positions[j], true);
            }
        }
    }

    return {
        sourceHandle,
        targetHandle
    }
}

import { Edge, Node } from '@xyflow/react';
import { GraphEdgeLabel } from '../../Strings/SREAgentResources';
import {
    CUSTOM_EDGE_TYPE,
    GRAPH_CARD_TYPE,
    GraphEdge,
    GraphNode,
    HandlePosition,
    NodeRelations,
    NodeSize,
    Resource,
    ResourceExtended,
    ScoreCardObject,
} from '../Contracts/Graph';

export const getAppHealthInfo = (nodeProperties?: ResourceExtended | Resource): ScoreCardObject | undefined => {
    let appHealthInfoString: string | undefined = undefined;
    if (nodeProperties) {
        if ('properties' in nodeProperties) {
            const resourceExtended: ResourceExtended = nodeProperties as ResourceExtended;
            appHealthInfoString = resourceExtended.properties?.appHealthInfo?.[0];
        } else {
            appHealthInfoString = nodeProperties.appHealthInfo?.[0];
        }
    }

    if (appHealthInfoString) {
        try {
            const healthInfo: ScoreCardObject = JSON.parse(appHealthInfoString);
            return healthInfo;
        } catch {
            return undefined;
        }
    }

    return undefined;
};

export const getSubscriptionIdFromNodeId = (nodeId: string): string => {
    return nodeId.split('_')[2] ?? '';
};

export const createAppGroupNode = (appGroup: ResourceExtended): Node<GraphNode> => {
    const { id, name, type, dashboardUrl, appHealthInfo, properties } = appGroup;

    const resource: Resource = {
        name,
        type,
        kind: properties.resourceKind?.[0] || '',
        dashboardUrl: dashboardUrl || properties?.dashboardUrl?.[0] || '',
        resourceId: id,
        appHealthInfo: appHealthInfo || properties?.appHealthInfo || [],
    };

    return createNode(resource);
};

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
            properties: {
                ...resource,
            },
        },
    };

    return node;
};

export const createGraphEdge = (
    sourceId: string,
    targetId: string,
    relation?: string,
    isDirectionReversed: boolean = false
): Edge<GraphEdge> => {
    if (isDirectionReversed) {
        [sourceId, targetId] = [targetId, sourceId];
    }

    const edgeId = getEdgeId(sourceId, targetId);

    const edge: Edge<GraphEdge> = {
        id: edgeId,
        type: CUSTOM_EDGE_TYPE,
        source: sourceId,
        target: targetId,
        label: relation,
        data: { label: 'edge' },
    };

    return edge;
};

export const getNewNodesAndEdges = (
    appGroup: ResourceExtended,
    resources: Resource[]
): { appGroupNode: Node<GraphNode>; nodes: Node<GraphNode>[]; edges: Edge<GraphEdge>[] } => {
    const nodes: Node<GraphNode>[] = [];
    const edges: Edge<GraphEdge>[] = [];

    const appGroupNode = createAppGroupNode(appGroup);
    nodes.push(appGroupNode);

    const populateResourceNodesAndEdges = (parentNode: Node<GraphNode>, resources: Resource[]) => {
        for (const resource of resources) {
            const node = createNode(resource);
            const edge = createGraphEdge(parentNode.id, node.id, resource.relationToParent, resource.isRelationReversed);

            nodes.push(node);
            edges.push(edge);

            if (resource.subItems && resource.subItems.length > 0) {
                populateResourceNodesAndEdges(node, resource.subItems);
            }
        }
    };

    populateResourceNodesAndEdges(appGroupNode, resources);

    return {
        appGroupNode,
        nodes,
        edges,
    };
};

export const getEdgeId = (parentNodeId: string, childNodeId: string) => {
    return `${parentNodeId}-${childNodeId}`;
};

export const getHandleId = (position: HandlePosition, isTarget: boolean) => {
    return `${position}-${isTarget ? 'in' : 'out'}`;
};

export const getSourceAndTargetHandleId = (
    leftCenterPos: { x: number; y: number },
    rightcenterPos: { x: number; y: number }
): { sourceHandle: string; targetHandle: string } => {
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
    };

    const getDistance = (x1: number, y1: number, x2: number, y2: number) => {
        return (x2 - x1) ** 2 + (y2 - y1) ** 2;
    };

    let minDistance = Number.MAX_VALUE;
    let sourceHandle = getHandleId('T', false);
    let targetHandle = getHandleId('B', true);
    const positions: HandlePosition[] = ['T', 'B', 'L', 'R'];

    const sourceNodeHandlePos = Array.from({ length: 4 }, (_, index) =>
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[index])
    );

    const targetNodeHandlePos = Array.from({ length: 4 }, (_, index) =>
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[index])
    );

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
        targetHandle,
    };
};

export const getFriendlyEdgeLabel = (nodeRelations: string) => {
    switch (nodeRelations.toLowerCase()) {
        case NodeRelations.Contains.toLowerCase():
            return GraphEdgeLabel.contains;
        case NodeRelations.Linked.toLowerCase():
        case NodeRelations.ServesCode.toLowerCase():
            return GraphEdgeLabel.linkedTo;
        case NodeRelations.SqlConnected.toLowerCase():
        case NodeRelations.PostgreSqlConnected.toLowerCase():
        case NodeRelations.RedisConnected.toLowerCase():
        case NodeRelations.Connected.toLowerCase():
        case NodeRelations.UsesRedis.toLowerCase():
        case NodeRelations.References.toLowerCase():
            return GraphEdgeLabel.connectsTo;
        case NodeRelations.HasRole.toLowerCase():
            return GraphEdgeLabel.localAuth;
        case NodeRelations.HasIdentity.toLowerCase():
            return GraphEdgeLabel.managedIdentity;
        case NodeRelations.Hosts.toLowerCase():
            return GraphEdgeLabel.hosts;
        case NodeRelations.HostedOn.toLowerCase():
            return GraphEdgeLabel.hostedOn;
        case NodeRelations.RevisionOf.toLowerCase():
            return GraphEdgeLabel.revisionOf;
        case NodeRelations.OwnedBy.toLowerCase():
            return GraphEdgeLabel.ownedBy;
        case NodeRelations.MonitoredBy.toLowerCase():
            return GraphEdgeLabel.monitoredBy;
        case NodeRelations.IsPartOF.toLowerCase():
            return GraphEdgeLabel.isPartOf;
        case NodeRelations.BackedBy.toLowerCase():
            return GraphEdgeLabel.backedBy;
        default:
            return undefined;
    }
};

import axios from 'axios';
import { useCallback, useEffect, useReducer, useState } from 'react';

export type Subscription = {
    id: string;
    name: string;
}

export type ResourceExtended = {
    id: string; // the string has underscore
    name: string;
    type: string;
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    properties: {
        resourceType: string[];
        resourceName: string[];
        resourceId: string[];
        subscriptionId: string[];
        resourceGroupName: string[];
        location: string[];
        runningStatus: string;
        appHealthInfo?: string[] // Convert the json string to ScoreCardObject
    }
}

export type Resource = {
    name: string;
    type: string;
    resourceId: string; // the string has underscore
    appHealthInfo?: string[]; // Convert the json string to ScoreCardObject
    subItems?: Resource[];
}

export type ScoreCardObject = {
    Costs: number; // 7 day window
    Availability?: number | null; // percentage
    Health: 'healthy' | 'unhealthy' | 'unknown';
    Transactions: number;
    AvgLatencyInMs?: number | null;
    AvgMemoryUsage?: number | null; // bytes
    AvgCpuUsage?: number | null; // percentage
    LastDataCaptureTimeStampInUTC?: string | Date;
    IsActive: boolean;
}

export type GraphNodeProperties = ResourceExtended | Resource;

export type GraphNodeObject = {
    id: string;
    name: string;
    type: 'subscription' | 'appGroup' | 'resource';
    subscriptionId: string;
    properties?: GraphNodeProperties;
}

export type GraphNode = GraphNodeObject & {
    isVisible: boolean;
    links?: string[];
}

export type GraphLink = {
    id: string;
    source: string;
    target: string;
    isVisible: boolean;
    // use different types to track the source and target to
    // prevent react graph convert the source and target to objects
    from: string;
    to: string;
}

export const getSubscriptions = async (): Promise<Subscription[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/subscriptions`);
        return data ?? [];
    } catch {
        return [];
    }
}

export const getAppGroups = async (subscriptionId: string): Promise<ResourceExtended[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups`);
        return data ?? [];
    } catch {
        return [];
    }
}

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

const GraphReducer = (
    state: { nodeMap: Map<string, GraphNode>, linkMap: Map<string, GraphLink> },
    action: { type: 'HIDE_NODE' | 'SHOW_NODE' | 'ADD_NODES', payload: { nodeToClick?: GraphNode, parentNode?: GraphNode, nodes?: GraphNode[] } }
) => {
    const { type, payload: { nodeToClick, parentNode, nodes } } = action;

    const { nodeMap, linkMap } = state;

    if (type === 'HIDE_NODE' && nodeToClick) {
        const links = nodeToClick.links ?? [];
        const nodeIdSet = new Set<string>();
        const nodeIdArray = links.map(linkId => linkMap.get(linkId)).filter(link => link !== undefined).map(link => link.to);

        while (nodeIdArray.length > 0) {
            const nodeId = nodeIdArray.shift();
            if (nodeId && !nodeIdSet.has(nodeId)) {
                nodeIdSet.add(nodeId);

                const newNodeIds = (nodeMap.get(nodeId)?.links ?? []).map(linkId => linkMap.get(linkId)).filter(link => link !== undefined).map(link => link.to);
                if (newNodeIds) {
                    nodeIdArray.push(...newNodeIds);
                }
            }
        }

        const nodesToHide = Array.from(nodeIdSet);

        for (const nodeId of nodesToHide) {
            const node = nodeMap.get(nodeId);
            if (node) {
                nodeMap.set(nodeId, { ...node, isVisible: false });
            }
        }

        for (const linkId of linkMap.keys()) {
            const link = linkMap.get(linkId);
            if (link && nodesToHide.includes(link.to)) {
                linkMap.set(linkId, { ...link, isVisible: false });
            }
        }

        return {
            nodeMap,
            linkMap,
        }
    } else if (type === 'SHOW_NODE' && nodeToClick) {
        const links = nodeToClick.links;
        const nodesToShow = (links ?? []).map(linkId => linkMap.get(linkId)).filter(link => link !== undefined).map(link => link.to);

        for (const nodeId of nodesToShow) {
            const node = nodeMap.get(nodeId);
            if (node) {
                nodeMap.set(nodeId, { ...node, isVisible: true });
            }
        }

        for (const linkId of linkMap.keys()) {
            const link = linkMap.get(linkId);
            if (link && nodesToShow.includes(link.to) && nodeMap.get(link.from)?.isVisible) {
                linkMap.set(linkId, { ...link, isVisible: true });
            }
        }

        return {
            nodeMap,
            linkMap,
        }
    } else if (type === 'ADD_NODES' && nodes) {
        const links: string[] = [];

        nodes.forEach((node) => {
            if (!nodeMap.get(node.id)) {
                nodeMap.set(node.id, { ...node });
            }

            if (parentNode) {
                const linkId = `${parentNode.id} ${node.id}`;

                const link: GraphLink = {
                    id: linkId,
                    source: parentNode.id,
                    target: node.id,
                    from: parentNode.id,
                    to: node.id,
                    isVisible: true,
                };
                linkMap.set(linkId, link);

                links.push(linkId);
            }
        });

        if (parentNode) {
            const newParentNode = { ...parentNode, areChildrenVisible: true, isVisible: true, links: [...links] };

            nodeMap.set(parentNode.id, newParentNode);
        }

        return {
            nodeMap,
            linkMap,
        }
    }

    return state;
}

export const useGraph = () => {
    const [graph, dispatch] = useReducer(GraphReducer, { nodeMap: new Map<string, GraphNode>(), linkMap: new Map<string, GraphLink>() });
    const [isLoading, setIsLoading] = useState(false);

    const [nodes, setNodes] = useState<GraphNode[]>([]);
    const [links, setLinks] = useState<GraphLink[]>([]);

    const addNodes = useCallback((parentNode: GraphNode, nodes: GraphNode[]) => {
        dispatch({
            type: 'ADD_NODES',
            payload: {
                parentNode: parentNode,
                nodes: nodes
            }
        });
    }, []);

    const hideNode = useCallback((node: GraphNode) => {
        dispatch({ type: 'HIDE_NODE', payload: { nodeToClick: node } });
    }, []);

    const showNode = useCallback((node: GraphNode) => {
        dispatch({ type: 'SHOW_NODE', payload: { nodeToClick: node } });
    }, []);

    const queryNodes = useCallback(async (parentNode: GraphNode) => {
        const { id, subscriptionId, type } = parentNode;

        if (type === 'subscription') {
            const appGroups = await getAppGroups(id);
            return appGroups.map(appGroup => createAppGroupNode(appGroup));
        } else {
            const resources = await getResources(subscriptionId, id);
            return resources.map(resource => createResourceNode(resource));
        }
    }, []);

    const shouldShowNode = useCallback((node: GraphNode) => {
        const subLinks = node.links;
        if (!subLinks) {
            return true;
        } else if (subLinks.some(link => graph.linkMap.get(link)?.isVisible)) {
            return false;
        }
        return true;
    }, [graph.linkMap]);

    useEffect(() => {
        setNodes(Array.from(graph.nodeMap.values()));
        setLinks(Array.from(graph.linkMap.values()).map(link => {
            if (typeof link.source === 'object') {
                link.source = link.from;
            }
            if (typeof link.target === 'object') {
                link.target = link.to;
            }
            return link;
        }));
    }, [graph])

    useEffect(() => {
        let isSubscribed = true;

        const init = async () => {
            setIsLoading(true);
            const subscriptions = await getSubscriptions();

            const nodes = subscriptions.map((subscription) => {
                return createSubscriptionNode(subscription)
            });

            if (isSubscribed) {
                dispatch({ type: 'ADD_NODES', payload: { nodes } });
                setIsLoading(false);
            }
        }

        init();

        return () => {
            isSubscribed = false;
        }
    }, []);

    return {
        nodes,
        links,
        isLoading: isLoading,
        addNodes,
        hideNode,
        showNode,
        queryNodes,
        shouldShowNode
    };
};

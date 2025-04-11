import axios from 'axios';
import { useCallback, useEffect, useReducer, useState } from 'react';

export type Subscription = {
    subscriptionId: string;
    subscriptionName: string;
}

export type ScoreCard = {
    cost: string;
    availability: string;
    health: 'healthy' | 'unhealthy' | 'unknown';
    requests: unknown[];
    timestamp: string | Date;
}

export type GraphNodeProperties = {
    resourceId: string;
    resourceName: string;
    type: string;
    scorecard?: ScoreCard;
}

export type GraphNodeObject = {
    id: string;
    name: string;
    type: 'subscription' | 'appGroup' | 'resource';
    properties: GraphNodeProperties;
}

export type GraphNode = GraphNodeObject & {
    areChildrenVisible: boolean;
    isVisible: boolean;
    links: string[];
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
        return data.value ?? [];
    } catch {
        return [];
    }
}

export const getAppGroups = async (subscriptionId: string): Promise<GraphNodeProperties[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups`);
        return data.value ?? [];
    } catch {
        return [];
    }
}

export const getResources = async (subscriptionId: string, appGroupId: string): Promise<GraphNodeProperties[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups/${appGroupId}`);
        return data.value ?? [];
    } catch {
        return [];
    }
}

const GraphReducer = (
    state: { nodeMap: Map<string, GraphNode>, linkMap: Map<string, GraphLink> },
    action: { type: 'HIDE_NODE' | 'SHOW_NODE' | 'ADD_NODES', payload: { nodeToClick?: GraphNode, parentNode?: GraphNode, nodes?: GraphNodeObject[] } }
) => {
    const { type, payload: { nodeToClick, parentNode, nodes } } = action;

    const { nodeMap, linkMap } = state;

    if (type === 'HIDE_NODE' && nodeToClick) {
        const markAllNodeInvisibleUnderParent = (node: GraphNode, setCurrentNodeInvisible: boolean) => {
            const newNode: GraphNode = { ...node, areChildrenVisible: false, isVisible: !setCurrentNodeInvisible };
            nodeMap.set(node.id, newNode);
            if (node.links.length > 0) {
                node.links.forEach((childLinkId) => {
                    const link: GraphLink | undefined = linkMap.get(childLinkId);
                    if (link) {
                        const newLink: GraphLink = { ...link, isVisible: false }
                        linkMap.set(childLinkId, newLink);

                        const childNode: GraphNode | undefined = nodeMap.get(link.to);

                        if (childNode) {
                            markAllNodeInvisibleUnderParent(childNode, true);
                        }
                    }

                });
            }
        }

        markAllNodeInvisibleUnderParent(nodeToClick, false);

        return {
            nodeMap,
            linkMap,
        }
    } else if (type === 'SHOW_NODE' && nodeToClick) {
        const newNode: GraphNode = { ...nodeToClick, areChildrenVisible: true, isVisible: true };

        nodeMap.set(nodeToClick.id, newNode);
        newNode.links.forEach((childLinkId) => {
            const childLink: GraphLink | undefined = linkMap.get(childLinkId);
            if (childLink) {
                linkMap.set(childLinkId, { ...childLink, isVisible: true });
                const childNode = nodeMap.get(childLink.to);
                if (childNode) {
                    nodeMap.set(childNode.id, { ...childNode, isVisible: true });
                }
            }
        });

        return {
            nodeMap,
            linkMap,
        }
    } else if (type === 'ADD_NODES' && nodes) {
        const links: string[] = [];

        nodes.forEach((node) => {
            nodeMap.set(node.id, { ...node, isVisible: true, areChildrenVisible: false, links: [] });

            if (parentNode) {
                const linkId = `${parentNode.id}-${node.id}`;

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

    const addNodes = useCallback((parentNode: GraphNode, nodes: GraphNodeObject[]) => {
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
            if (isSubscribed) {
                const nodeObjects = subscriptions.map((subscription) => {
                    const node: GraphNodeObject = {
                        id: subscription.subscriptionId,
                        name: subscription.subscriptionName,
                        type: 'subscription',
                        properties: {
                            resourceId: subscription.subscriptionId,
                            resourceName: subscription.subscriptionName,
                            type: 'subscription',
                        },
                    }
                    return node;
                });

                if (isSubscribed) {
                    dispatch({ type: 'ADD_NODES', payload: { nodes: nodeObjects } });
                    setIsLoading(false);
                }
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
    };
};

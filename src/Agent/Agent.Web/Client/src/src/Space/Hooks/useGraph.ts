import { OptionOnSelectData, SelectionEvents } from '@fluentui/react-components';
import { Edge, Node, useEdgesState, useNodesState, useReactFlow } from '@xyflow/react';
import axios from 'axios';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useParams } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { GraphEdge, GraphNode, Resource, ResourceExtended, Subscription } from '../Contracts/Graph';
import { getNewNodesAndEdges, getSubscriptionIdFromNodeId } from '../Graph/Utility';
import { useGraphLayout } from './useGraphLayout';

export const useGraph = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const intl = useIntl();
    const allKey = 'all';
    const { groupId: initialGroupId } = useParams();
    const initialGroupIdRef = useRef(initialGroupId);

    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [appGroups, setAppGroups] = useState<ResourceExtended[]>([]);
    const [filteredAppGroups, setFilteredAppGroups] = useState<ResourceExtended[]>([]);
    const [selectedSubscription, setSelectedSubscription] = useState<Subscription>();
    const [selectedRscType, setSelectedRscType] = useState<string>(allKey);
    const [selectedAppGroup, setSelectedAppGroup] = useState<ResourceExtended>();
    const [isSubscriptionLoading, setIsSubscriptionLoading] = useState<boolean>(false);
    const [isAppGroupLoading, setIsAppGroupLoading] = useState<boolean>(false);

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

    const resourceTypeFilterOptions = useMemo(() => {
        const options = [{ key: allKey, text: intl.formatMessage(SreAgentResources.all) }];

        if (!isAppGroupLoading && appGroups.length > 0) {
            const uniqueTypes = new Set(appGroups.map(appGroup => appGroup.type));
            uniqueTypes.forEach(type => {
                options.push({ key: type, text: type });
            });
        }

        return options;
    }, [intl, appGroups, isAppGroupLoading, allKey]);

    const getSubscriptions = useCallback(async (): Promise<Subscription[]> => {
        try {
            const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/subscriptions`, {
                headers: getAgentHeaders(),
            });
            return data ?? [];
        } catch {
            return [];
        }
    }, [sreAgentEndpoint]);

    const getAppGroups = useCallback(
        async (subscriptionId: string): Promise<ResourceExtended[]> => {
            try {
                const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/${subscriptionId}/appGroups`, {
                    headers: getAgentHeaders(),
                });
                return data ?? [];
            } catch {
                return [];
            }
        },
        [sreAgentEndpoint]
    );

    const getResources = useCallback(
        async (subscriptionId: string, resourceId: string): Promise<Resource[]> => {
            try {
                const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/${subscriptionId}/appGroups/${resourceId}`, {
                    headers: getAgentHeaders(),
                });
                return data ?? [];
            } catch {
                return [];
            }
        },
        [sreAgentEndpoint]
    );

    const onSelectSubscription = async (_: SelectionEvents, data: OptionOnSelectData) => {
        const id = data.optionValue;
        const selectedSubscription = subscriptions.find(subscription => subscription.id === id);
        if (selectedSubscription) {
            setSelectedSubscription(selectedSubscription);
            setIsAppGroupLoading(true);

            const appGroups = await getAppGroups(selectedSubscription.id);
            setAppGroups(appGroups);
            setSelectedAppGroup(appGroups[0]);
            setIsAppGroupLoading(false);
        }
    };

    const onSelectRscType = (_: SelectionEvents, data: OptionOnSelectData) => {
        const rscType = data.optionValue;
        setSelectedRscType(rscType ?? allKey);

        if (rscType === allKey) {
            setFilteredAppGroups(appGroups);
        } else {
            const filteredAppGroups = appGroups.filter(appGroup => appGroup.type === rscType);
            setFilteredAppGroups(filteredAppGroups);

            // Only set selection if current one is not in filtered results
            if (!selectedAppGroup || !filteredAppGroups.some(ag => ag.id === selectedAppGroup.id)) {
                setSelectedAppGroup(filteredAppGroups[0]);
            }
        }
    };

    const onSelectAppGroupDropdown = (_: SelectionEvents, data: OptionOnSelectData) => {
        const appGroupId = data.optionValue;
        const selectedAppGroup = appGroups.find(appGroup => appGroup.id === appGroupId);
        setSelectedAppGroup(selectedAppGroup);
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

    useEffect(() => {
        if (!hasChatPermissions) {
            setSubscriptions([]);
            setAppGroups([]);
            setFilteredAppGroups([]);
            setIsSubscriptionLoading(false);
            setIsAppGroupLoading(false);
            setIsLoading(false);
            return;
        }

        if (!sreAgentEndpoint) return;

        let isSubscribed = true;

        const init = async () => {
            setIsLoading(true);
            setIsSubscriptionLoading(true);
            setIsAppGroupLoading(true);

            const subscriptions = await getSubscriptions();
            if (isSubscribed) {
                setSubscriptions(subscriptions);
                setIsSubscriptionLoading(false);
            }

            const initialGroupSubscriptionId = initialGroupIdRef.current?.split('/')[1];
            const initialGroupSubscription = initialGroupSubscriptionId
                ? subscriptions.find(sub => sub.id === initialGroupSubscriptionId)
                : undefined;
            const selectedSubscription =
                initialGroupSubscriptionId && initialGroupSubscription ? initialGroupSubscription : subscriptions[0];

            if (isSubscribed) {
                setSelectedSubscription(selectedSubscription);
            }

            const appGroups = await getAppGroups(selectedSubscription.id);

            if (isSubscribed) {
                setAppGroups(appGroups);

                const initialSelectedAppGroup = initialGroupIdRef.current
                    ? appGroups.find(appGroup => appGroup.properties.resourceId?.[0] === initialGroupIdRef.current)
                    : undefined;
                setSelectedAppGroup(initialSelectedAppGroup ?? appGroups[0]);
            }

            if (isSubscribed) {
                setIsAppGroupLoading(false);
                setIsLoading(false);
            }
        };

        init();

        return () => {
            isSubscribed = false;
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [hasChatPermissions, sreAgentEndpoint]);

    useEffect(() => {
        // When appGroups change, apply the current resource type filter
        if (selectedRscType === allKey) {
            setFilteredAppGroups([...appGroups]);
        } else {
            const filteredAppGroups = appGroups.filter(appGroup => appGroup.type === selectedRscType);
            setFilteredAppGroups(filteredAppGroups);
        }
    }, [appGroups, selectedRscType, allKey]);

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
        [graph, setNodes, setEdges, getResources, layoutGraph]
    );

    useEffect(() => {
        onAppGroupUpdate(selectedAppGroup);
        // Intentionally excluding onAppGroupUpdate to prevent infinite rerenders
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [selectedAppGroup]);

    useEffect(() => {
        if (nodes.length > 0 && !isLoading) {
            fitView();
        }
    }, [nodes.length, isLoading, fitView]);

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        isLoading,
        setIsLoading,
        selectedNode,
        setSelectedNode,
        hoveredNodeId,
        onAppGroupUpdate,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        selectedAppGroupId,
        subscriptions,
        appGroups,
        filteredAppGroups,
        selectedSubscription,
        selectedRscType,
        selectedAppGroup,
        isSubscriptionLoading,
        isAppGroupLoading,
        resourceTypeFilterOptions,
        onSelectSubscription,
        onSelectRscType,
        onSelectAppGroupDropdown,
        allKey,
    };
};

import { graphlib, layout } from '@dagrejs/dagre';
import { useEdgesState, useNodesState, useReactFlow, XYPosition } from '@xyflow/react';
import debounce from 'lodash/debounce';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { InvestigationTreeNode, InvestigationTreeState, TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
import { Guid } from '../../Common/Helpers/Guid';
import {
    AgentTaskNodeSize,
    AgentTaskPhaseNodeIdSuffix,
    GraphFlowEdge,
    GraphFlowNode,
    IAgentTaskGraphProps,
    InvestigationGraphFlowEdgeType,
} from '../Contracts/Activities';

interface HypothesisGroup {
    groupNode: GraphFlowNode;
    nodes: GraphFlowNode[];
    edges: GraphFlowEdge[];
}

class DagreSep {
    public static readonly parentNode = {
        ranksep: 150,
        nodesep: 100,
    };

    public static readonly childNode = {
        ranksep: 100,
        nodesep: 50,
    };
}

const GroupNodePadding = 50;

export const useAgentTaskGraphFlow = (props: IAgentTaskGraphProps) => {
    const { treeStateValue, shouldFitView } = props;

    const { fitView } = useReactFlow();

    const [nodes, setNodes, onNodesChange] = useNodesState<GraphFlowNode>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<GraphFlowEdge>([]);
    const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
    const [isDetailsPanelOpen, setIsDetailsPanelOpen] = useState(false);
    const [renderKey, setRenderKey] = useState(Guid.newGuid());

    const selectNode = useCallback((nodeId: string | null) => {
        setSelectedNodeId(nodeId);
        setIsDetailsPanelOpen(!!nodeId);
    }, []);

    const closeDetailsPanel = useCallback(() => {
        setIsDetailsPanelOpen(false);
        setSelectedNodeId(null);
    }, []);

    const selectedNode = useMemo(() => {
        if (!selectedNodeId) return null;

        const node = nodes.find(n => n.id === selectedNodeId);
        if (node) {
            // if the selected node is initial investigation summary or intial investigation steps, then set the parent node as the selected node
            if (node.type === TreeNodeType.InitialInvestigation) {
                // summary or step node's data id is the parent node's id
                const parentNode = nodes.find(n => n.id === node.data.id);
                return parentNode || null;
            }
            return node;
        }
        return null;
    }, [nodes, selectedNodeId]);

    const containerRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const element = containerRef.current;
        if (!element) return;

        const centerGraph = debounce(() => {
            fitView({ padding: 50, duration: 50 });
        }, 100);

        const resizeObserver = new ResizeObserver(() => {
            centerGraph();
        });

        resizeObserver.observe(element);
        return () => resizeObserver.disconnect();
    }, []);

    const getDagreLayoutForHypothesisGroupChildNodes = (
        nodes: GraphFlowNode[],
        edges: GraphFlowEdge[]
    ): {
        nodes: GraphFlowNode[];
        edges: GraphFlowEdge[];
    } => {
        const dagreGraph = new graphlib.Graph({}).setDefaultEdgeLabel(() => ({}));
        // Configure for investigation tree layout - top-to-bottom flow
        dagreGraph.setGraph({
            rankdir: 'TB', // Top-to-bottom for investigation flow
            ranker: 'tight-tree',
            ...DagreSep.childNode,
        });

        if (!nodes || nodes.length === 0) {
            return { nodes: [], edges: [] };
        }

        nodes.forEach(node => {
            dagreGraph.setNode(node.id, {
                ...node,
                ...AgentTaskNodeSize.HypothesisNode,
            });
        });
        // Add edges after nodes
        edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));

        // Run dagre layout algorithm
        layout(dagreGraph);

        const computedNodes = nodes.map(node => {
            const position = dagreGraph.node(node.id);

            return {
                ...node,
                position,
                ...AgentTaskNodeSize.HypothesisNode,
            };
        });

        return {
            nodes: computedNodes,
            edges,
        };
    };

    const getInitialInvestigationNode = (phaseNodes: InvestigationTreeNode[]) => {
        const initialInvestigation =
            phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation)) || null;

        if (initialInvestigation) {
            const getNodeObject = (type: 'summary' | 'steps'): GraphFlowNode => {
                return {
                    id: `${initialInvestigation.id}-${type}`,
                    type: TreeNodeType.InitialInvestigation,
                    position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                    width: AgentTaskNodeSize.InitialInvestigationNode.width,
                    height: AgentTaskNodeSize.InitialInvestigationNode.height,
                    parentId: initialInvestigation.id,
                    extent: 'parent',
                    data: {
                        ...initialInvestigation,
                        showInitialInvestigationSummary: type === 'summary',
                        showInitialInvestigationSteps: type === 'steps',
                    },
                };
            };

            const initialInvestigationGroupNode: GraphFlowNode = {
                id: initialInvestigation.id,
                type: TreeNodeType.NodeGroup,
                position: { x: 0, y: 0 },
                data: {
                    ...initialInvestigation,
                },
            };

            const initialInvestigationSummaryNode: GraphFlowNode | null = initialInvestigation.description
                ? getNodeObject('summary')
                : null;

            const initialInvestigationStepsNode: GraphFlowNode | null =
                (initialInvestigation.gatheringContextSteps ?? []).length > 0 ? getNodeObject('steps') : null;

            return {
                initialInvestigationGroupNode,
                initialInvestigationSummaryNode,
                initialInvestigationStepsNode,
            };
        }
        return null;
    };

    const getHypothesisGroups = (phaseNodes: InvestigationTreeNode[], nodes: Map<string, InvestigationTreeNode>) => {
        const result: HypothesisGroup[] = [];

        const formingHypothesis = phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.FormingHypothesis));
        const initialHypotheseIds = formingHypothesis?.childrenIds || [];

        const addChildHypotheses = (
            parentHypothesis: InvestigationTreeNode,
            graphFlowNodes: GraphFlowNode[],
            graphFlowEdges: GraphFlowEdge[],
            depth: number
        ) => {
            if (parentHypothesis.childrenIds.length === 0) {
                return;
            }

            const children = parentHypothesis.childrenIds;

            children.forEach((childId, index) => {
                const child = nodes.get(childId);
                if (child) {
                    graphFlowNodes.push({
                        id: child.id,
                        type: TreeNodeType.Hypothesis,
                        position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                        data: {
                            ...child,
                            isChild: depth > 0,
                            hasChildren: child.childrenIds.length > 0,
                            index: index + 1,
                        },
                    });

                    // Add edge from parent to child
                    graphFlowEdges.push({
                        id: `${parentHypothesis.id}-${child.id}`,
                        source: parentHypothesis.id,
                        target: child.id,
                        zIndex: 2000,
                        data: {
                            targetId: child.id,
                        },
                        type: InvestigationGraphFlowEdgeType.Children,
                    });

                    // Recursively add grandchildren
                    addChildHypotheses(child, graphFlowNodes, graphFlowEdges, depth + 1);
                }
            });
        };

        // Add initial hypothesis nodes
        if (initialHypotheseIds.length > 0) {
            initialHypotheseIds.forEach((hypothesisId, index) => {
                const hypothesis = nodes.get(hypothesisId);
                if (hypothesis) {
                    const groupNodeId = `group-${hypothesis.id}`;
                    const graphFlowNodes: GraphFlowNode[] = [];
                    const graphFlowEdges: GraphFlowEdge[] = [];

                    const groupNode = {
                        id: groupNodeId,
                        type: TreeNodeType.NodeGroup,
                        position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                        data: {
                            ...hypothesis,
                            title: `Hypothesis ${index + 1}`,
                        },
                    };

                    graphFlowNodes.push({
                        id: hypothesis.id,
                        type: TreeNodeType.Hypothesis,
                        position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                        data: {
                            ...hypothesis,
                            index: index + 1,
                            isChild: false,
                            hasChildren: hypothesis.childrenIds.length > 0,
                        },
                    });

                    // Add child hypotheses recursively
                    addChildHypotheses(hypothesis, graphFlowNodes, graphFlowEdges, 1);
                    result.push({ groupNode, nodes: graphFlowNodes, edges: graphFlowEdges });
                }
            });
        }

        const rootGroup: GraphFlowNode = {
            id: 'hypothesisRootGroup',
            type: TreeNodeType.HypothesisRootGroup,
            position: { x: 0, y: 0 },
            data: {
                id: 'hypothesisRootGroup',
                title: '',
                description: '',
                status: '',
                childrenIds: [],
                expanded: false,
                isValidating: false,
                isLoading: false,
                parentId: undefined,
                nodeType: TreeNodeType.HypothesisRootGroup,
            },
        };

        return {
            hypothesisGroups: result,
            rootGroup,
        };
    };

    const getConclusionNodesAndEdges = (phaseNodes: InvestigationTreeNode[]) => {
        const conclusion = phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion));

        let conclusionNode: GraphFlowNode | null = null;

        if (conclusion) {
            conclusionNode = {
                id: conclusion.id,
                type: TreeNodeType.Conclusion,
                position: { x: 0, y: 0 },
                width: AgentTaskNodeSize.ConclusionNode.width,
                height: AgentTaskNodeSize.ConclusionNode.height,
                data: { ...conclusion },
            };
        }

        return conclusionNode;
    };

    const getGroupNodeDimension = (childNodes: GraphFlowNode[]) => {
        const minX = Math.min(...childNodes.map(n => n.position.x - (n.width || 0) / 2)) - GroupNodePadding;
        const minY = Math.min(...childNodes.map(n => n.position.y - (n.height || 0) / 2)) - GroupNodePadding;
        const maxX = Math.max(...childNodes.map(n => n.position.x + (n.width || 0) / 2)) + GroupNodePadding;
        const maxY = Math.max(...childNodes.map(n => n.position.y + (n.height || 0) / 2)) + GroupNodePadding;

        return {
            minX,
            minY,
            maxX,
            maxY,
        };
    };

    // Take the center position of the child node and calculate its relative position to the group node.
    // After that, shift the center position to top left to match React flow position
    const getChildNodeRelativePositionToGroupNode = (childNode: GraphFlowNode, groupNodePostion: XYPosition) => {
        const centerPositionOfChildNode = childNode.position;
        return {
            x: centerPositionOfChildNode.x - groupNodePostion.x - (childNode.width || 0) / 2,
            y: centerPositionOfChildNode.y - groupNodePostion.y - (childNode.height || 0) / 2,
        };
    };

    const getParentNodePositionY = (previousParentNodePositionY: number, previousParentNodeHeight: number) => {
        return previousParentNodePositionY + previousParentNodeHeight + DagreSep.parentNode.ranksep;
    };

    const layoutInitialInvestigationGroupAndChildrenNode = ({
        initialInvestigationGroupNode,
        initialInvestigationSummaryNode,
        initialInvestigationStepsNode,
    }: {
        initialInvestigationGroupNode: GraphFlowNode;
        initialInvestigationSummaryNode: GraphFlowNode | null;
        initialInvestigationStepsNode: GraphFlowNode | null;
    }) => {
        const nodes: GraphFlowNode[] = [];

        const posX = 20;
        const posY = getParentNodePositionY(0, 0);

        let initialInvestigationGroupNodeWidth = AgentTaskNodeSize.InitialInvestigationNode.width,
            initialInvestigationGroupNodeHeight = AgentTaskNodeSize.InitialInvestigationNode.height;

        const firstNodePos = {
            position: {
                x: GroupNodePadding,
                y: GroupNodePadding,
            },
        };

        const secondNodePos = {
            position: {
                x: GroupNodePadding + AgentTaskNodeSize.InitialInvestigationNode.width + DagreSep.childNode.nodesep,
                y: GroupNodePadding,
            },
        };

        if (initialInvestigationSummaryNode && initialInvestigationStepsNode) {
            initialInvestigationGroupNodeWidth =
                AgentTaskNodeSize.InitialInvestigationNode.width * 2 + GroupNodePadding * 2 + DagreSep.childNode.nodesep;
            initialInvestigationGroupNodeHeight = AgentTaskNodeSize.InitialInvestigationNode.height + GroupNodePadding * 2;

            nodes.push(
                {
                    ...initialInvestigationSummaryNode,
                    ...firstNodePos,
                },
                {
                    ...initialInvestigationStepsNode,
                    ...secondNodePos,
                }
            );
        } else if (initialInvestigationSummaryNode || initialInvestigationStepsNode) {
            initialInvestigationGroupNodeWidth = AgentTaskNodeSize.InitialInvestigationNode.width + GroupNodePadding * 2;
            initialInvestigationGroupNodeHeight = AgentTaskNodeSize.InitialInvestigationNode.height + GroupNodePadding * 2;

            if (initialInvestigationSummaryNode) {
                nodes.push({
                    ...initialInvestigationSummaryNode,
                    ...firstNodePos,
                });
            } else if (initialInvestigationStepsNode) {
                nodes.push({
                    ...initialInvestigationStepsNode,
                    ...firstNodePos,
                });
            }
        }

        //!important: Group node must be in front of the children nodes
        nodes.unshift({
            ...initialInvestigationGroupNode,
            width: initialInvestigationGroupNodeWidth,
            height: initialInvestigationGroupNodeHeight,
            position: { x: posX, y: posY },
        });

        return {
            nodes,
            initialInvestigationGroupNodeHeight,
            centerX: posX + initialInvestigationGroupNodeWidth / 2,
            initialInvestigationGroupNodePositionY: posY,
        };
    };

    const layoutHypothesisGroupAndChildrenNode = (
        hypothesisGroupsAndRootGroup: {
            hypothesisGroups: HypothesisGroup[];
            rootGroup: GraphFlowNode;
        },
        initialInvestigationNodeId: string,
        initialInvestigationNodePositionY: number,
        initialInvestigationNodeHeight: number,
        graphCenterX: number
    ) => {
        const nodes: GraphFlowNode[] = [];
        const edges: GraphFlowEdge[] = [];

        let maxHeightOfGroupNodes = 0;
        const groupNodesWithDimension: GraphFlowNode[] = [];

        const { hypothesisGroups, rootGroup } = hypothesisGroupsAndRootGroup;

        for (const group of hypothesisGroups) {
            const groupNode = group.groupNode;

            // Use dagre to get the center positions of each child node of the group
            const layout = getDagreLayoutForHypothesisGroupChildNodes(group.nodes, group.edges);

            // Get the dimension of the group based on the child nodes' positions
            // for the purpose of getting the width and the height of the group node
            const { minX, minY, maxX, maxY } = getGroupNodeDimension(layout.nodes);
            const width = Math.abs(maxX - minX);
            const height = Math.abs(maxY - minY);

            // Set each child node's parentId to the group node's id,
            // and reset their position to make them being placed within the group node but maintain the relative positions among each other
            const nodesWithNewPosition: GraphFlowNode[] = layout.nodes.map(node => ({
                ...node,
                parentId: groupNode.id,
                extent: 'parent',
                position: getChildNodeRelativePositionToGroupNode(node, { x: minX, y: minY }),
            }));

            // Set the width and height for the group node
            const groupNodeWithDimension: GraphFlowNode = {
                ...groupNode,
                parentId: rootGroup.id,
                extent: 'parent',
                width,
                height,
            };
            groupNodesWithDimension.push(groupNodeWithDimension);

            nodes.push(...nodesWithNewPosition);
            edges.push(...layout.edges);

            maxHeightOfGroupNodes = Math.max(maxHeightOfGroupNodes, height);
        }

        // Get the width of all the group nodes
        const groupNodesWidth =
            groupNodesWithDimension.reduce((acc, node) => acc + (node.width || 0), 0) +
            DagreSep.parentNode.nodesep * (groupNodesWithDimension.length - 1);
        const rootGroupNodeWidth = groupNodesWidth + GroupNodePadding * 2;
        const rootGroupNodeHeight = maxHeightOfGroupNodes + GroupNodePadding * 2;
        // Compute the start position of the group nodes based on the center position of the graph
        const rootGroupNodePosX = graphCenterX - rootGroupNodeWidth / 2;
        const rootGroupNodePosY = getParentNodePositionY(initialInvestigationNodePositionY, initialInvestigationNodeHeight);

        const rootGroupNodeWithPosAndDimension: GraphFlowNode = {
            ...rootGroup,
            position: { x: rootGroupNodePosX, y: rootGroupNodePosY },
            width: rootGroupNodeWidth,
            height: rootGroupNodeHeight,
        };

        // Add edges from the initial investigation node to the hypothesis root group node
        const edgeFromInitialInvestigationNodeToRootGroup: GraphFlowEdge = {
            id: `${initialInvestigationNodeId}-${rootGroupNodeWithPosAndDimension.id}`,
            source: initialInvestigationNodeId,
            target: rootGroupNodeWithPosAndDimension.id,
            zIndex: -99,
            data: {
                fromInitialInvestigation: true,
                targetId: rootGroupNodeWithPosAndDimension.id,
            },
            type: InvestigationGraphFlowEdgeType.Parents,
        };
        edges.push(edgeFromInitialInvestigationNodeToRootGroup);

        // Layout the group nodes horizontally relatively to the group node
        let posX = GroupNodePadding;
        const posY = GroupNodePadding;
        for (const groupNode of groupNodesWithDimension) {
            groupNode.position = { x: posX, y: posY };
            // !important: Group node must be in front of the children nodes
            nodes.unshift(groupNode);
            posX += (groupNode.width || 0) + DagreSep.parentNode.nodesep;
        }

        nodes.unshift(rootGroupNodeWithPosAndDimension);

        return {
            nodes,
            edges,
            hypothesisRootGroupNodeHeight: rootGroupNodeHeight,
            hypothesisRootGroupNodePositionY: rootGroupNodePosY,
        };
    };

    const layoutConclusionNode = (
        conclusionNode: GraphFlowNode,
        hypothesisGroupRootNodeId: string,
        hypothesisRootGroupNodePositionY: number,
        hypothesisRootGroupNodeHeight: number,
        graphCenterX: number
    ) => {
        const posX = graphCenterX - (AgentTaskNodeSize.ConclusionNode.width || 0) / 2;
        const posY = getParentNodePositionY(hypothesisRootGroupNodePositionY, hypothesisRootGroupNodeHeight);

        conclusionNode = { ...conclusionNode, position: { x: posX, y: posY } };

        const edge: GraphFlowEdge = {
            id: `${hypothesisGroupRootNodeId}-${conclusionNode.id}`,
            source: hypothesisGroupRootNodeId,
            target: conclusionNode.id,
            zIndex: -99,
            data: {
                targetId: conclusionNode.id,
            },
            type: InvestigationGraphFlowEdgeType.Parents,
        };

        return {
            conclusionNode,
            edge,
        };
    };

    const constructGraph = (treeState: InvestigationTreeState | null) => {
        const graphFlowNodes: GraphFlowNode[] = [];
        const graphFlowEdges: GraphFlowEdge[] = [];

        if (!treeState || treeState.rootNodeIds.length === 0) {
            return {
                graphFlowNodes: [],
                graphFlowEdges: [],
            };
        }

        const { rootNodeIds, nodes } = treeState;

        // Get phase nodes
        const phaseNodes: InvestigationTreeNode[] = [];
        rootNodeIds.forEach(id => {
            const node = nodes.get(id);
            if (
                node &&
                (node?.nodeType === TreeNodeType.InitialInvestigation || node?.nodeType === TreeNodeType.Conclusion) &&
                !node.title.toLowerCase().includes('validating hypothesis')
            ) {
                phaseNodes.push(node);
            }
        });

        // Add initial investigation nodes
        const initialInvestigationNodes = getInitialInvestigationNode(phaseNodes);
        if (!initialInvestigationNodes?.initialInvestigationGroupNode) {
            return {
                graphFlowNodes: [],
                graphFlowEdges: [],
            };
        }
        const {
            nodes: initialInvestigationNodesWithLayout,
            initialInvestigationGroupNodeHeight,
            centerX,
            initialInvestigationGroupNodePositionY,
        } = layoutInitialInvestigationGroupAndChildrenNode(initialInvestigationNodes);
        graphFlowNodes.push(...initialInvestigationNodesWithLayout);

        // Add hypothesis nodes
        const hypothesisGroupsAndRootGroup = getHypothesisGroups(phaseNodes, nodes);
        const {
            nodes: hypothesisNodes,
            edges: hypothesisEdges,
            hypothesisRootGroupNodeHeight,
            hypothesisRootGroupNodePositionY,
        } = layoutHypothesisGroupAndChildrenNode(
            hypothesisGroupsAndRootGroup,
            initialInvestigationNodes.initialInvestigationGroupNode.id,
            initialInvestigationGroupNodePositionY,
            initialInvestigationGroupNodeHeight,
            centerX
        );
        graphFlowNodes.push(...hypothesisNodes);
        graphFlowEdges.push(...hypothesisEdges);

        // Add conclusion nodes
        const conclusionNode = getConclusionNodesAndEdges(phaseNodes);

        if (conclusionNode) {
            const { conclusionNode: conclusionNodeWithPosition, edge: conclusionEdge } = layoutConclusionNode(
                conclusionNode,
                hypothesisGroupsAndRootGroup.rootGroup.id,
                hypothesisRootGroupNodePositionY,
                hypothesisRootGroupNodeHeight,
                centerX
            );
            graphFlowNodes.push(conclusionNodeWithPosition);
            graphFlowEdges.push(conclusionEdge);
        }

        return {
            graphFlowNodes,
            graphFlowEdges,
        };
    };

    useEffect(() => {
        const treeState = treeStateValue?.treeState || null;
        const { graphFlowNodes, graphFlowEdges } = constructGraph(treeState);

        setNodes(graphFlowNodes);
        setEdges(graphFlowEdges);

        if (shouldFitView) {
            setRenderKey(Guid.newGuid());
        }
    }, [treeStateValue, shouldFitView]);

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        renderKey,
        containerRef,

        selectNode,
        selectedNodeId,
        selectedNode,
        isDetailsPanelOpen,
        closeDetailsPanel,
    };
};

import { graphlib, layout } from '@dagrejs/dagre';
import { MarkerType, useEdgesState, useNodesState, useReactFlow, XYPosition } from '@xyflow/react';
import { useEffect } from 'react';
import { InvestigationTreeNode, InvestigationTreeState, TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
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
    const { treeStateValue } = props;

    const { fitView } = useReactFlow();

    const [nodes, setNodes, onNodesChange] = useNodesState<GraphFlowNode>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<GraphFlowEdge>([]);

    const getDagreLayout = (
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

        const getNodeWidthAndHeight = (node: GraphFlowNode) => {
            switch (node.type) {
                case TreeNodeType.Group:
                    return AgentTaskNodeSize.GroupNode;
                case TreeNodeType.Phase:
                    return AgentTaskNodeSize.PhaseNode;
                default:
                    return AgentTaskNodeSize.HypothesisNode;
            }
        };

        if (!nodes || nodes.length === 0) {
            return { nodes: [], edges: [] };
        }

        nodes.forEach(node => {
            dagreGraph.setNode(node.id, {
                ...node,
                ...getNodeWidthAndHeight(node),
            });
        });
        // Add edges after nodes
        edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));

        // Run dagre layout algorithm
        layout(dagreGraph);

        const computedNodes = nodes.map(node => {
            const position = dagreGraph.node(node.id);
            const { width, height } = getNodeWidthAndHeight(node);

            return {
                ...node,
                width,
                height,
                position,
            };
        });

        return {
            nodes: computedNodes,
            edges,
        };
    };

    const getInitialInvestigationNode = (phaseNodes: InvestigationTreeNode[]): GraphFlowNode | null => {
        const initialInvestigation =
            phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation)) || null;

        return initialInvestigation
            ? {
                  id: initialInvestigation.id,
                  type: TreeNodeType.Phase,
                  position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                  width: AgentTaskNodeSize.PhaseNode.width,
                  height: AgentTaskNodeSize.PhaseNode.height,
                  data: { ...initialInvestigation },
              }
            : null;
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
                        markerEnd: {
                            type: MarkerType.ArrowClosed,
                            width: 20,
                            height: 20,
                        },

                        data: {
                            edgeType: InvestigationGraphFlowEdgeType.HypothesisToHypothesis,
                            sourceId: parentHypothesis.id,
                            targetId: child.id,
                        },
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
                        type: TreeNodeType.Group,
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

        return result;
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

    // Find the horizontal position of the top left corner of a parent node ( a phase node ) based on the group nodes layout
    const getNonGroupParentNodePositionX = (groupsNodesCenterX: number) => {
        return groupsNodesCenterX - AgentTaskNodeSize.PhaseNode.width / 2;
    };

    const getParentNodePositionY = (previousParentNodePositionY: number, previousParentNodeHeight: number) => {
        return previousParentNodePositionY + previousParentNodeHeight + DagreSep.parentNode.ranksep;
    };

    const getInitialInvestigationNodePosition = (groupsNodeCenterX: number, initialInvestigationNode: GraphFlowNode) => {
        if (groupsNodeCenterX === 0) {
            const layout = getDagreLayout([initialInvestigationNode], []);
            return layout.nodes[0].position;
        }

        return {
            x: getNonGroupParentNodePositionX(groupsNodeCenterX),
            y: getParentNodePositionY(0, 0), // Initial position Y is 0, height is 0
        };
    };

    const layoutHypothesisGroupAndChildrenNode = (
        hypothesisGroups: HypothesisGroup[],
        initialInvestigationNodeId: string,
        initialInvestigationNodePositionY: number,
        initialInvestigationNodeHeight: number
    ) => {
        const nodes: GraphFlowNode[] = [];
        const edges: GraphFlowEdge[] = [];

        let startX = 10;
        const initialStartX = 10;
        const startY = getParentNodePositionY(initialInvestigationNodePositionY, initialInvestigationNodeHeight);
        let maxHeightOfGroupNodes = 0;

        for (const group of hypothesisGroups) {
            const groupNode = group.groupNode;

            // Use dagre to get the center positions of each child node of the group
            const layout = getDagreLayout(group.nodes, group.edges);

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

            // Set the width, height and position of the group node
            const groupNodeWithComputedPositionAndDimension: GraphFlowNode = {
                ...groupNode,
                width,
                height,
                position: { x: startX, y: startY },
            };

            // Add edges from the initial investigation node to the group node
            const edgeFromInitialInvestigationNodeToGroupNode: GraphFlowEdge = {
                id: `${initialInvestigationNodeId}-${groupNode.id}`,
                source: initialInvestigationNodeId,
                target: groupNode.id,
                zIndex: -99,
                markerEnd: {
                    type: MarkerType.ArrowClosed,
                    width: 20,
                    height: 20,
                },
                data: {
                    edgeType: InvestigationGraphFlowEdgeType.HypothesisToHypothesis,
                    sourceId: initialInvestigationNodeId,
                    targetId: groupNode.id,
                },
            };

            nodes.push(...[groupNodeWithComputedPositionAndDimension, ...nodesWithNewPosition]);
            edges.push(...[edgeFromInitialInvestigationNodeToGroupNode, ...layout.edges]);

            // Repeat the same procedure for next group by moving the start position horizontally to the right
            startX += width + DagreSep.parentNode.nodesep;
            maxHeightOfGroupNodes = Math.max(maxHeightOfGroupNodes, height);
        }

        const groupNodesWidth = startX - DagreSep.parentNode.nodesep - initialStartX;
        const groupNodesCenterX = initialStartX + groupNodesWidth / 2;

        return {
            nodes,
            edges,
            groupNodesCenterX,
            maxHeightOfGroupNodes,
            groupNodesPositionY: startY,
        };
    };

    const getConclusionNodesAndEdges = (
        phaseNodes: InvestigationTreeNode[],
        position: XYPosition,
        parentIds: string[]
    ): {
        conclusionNode: GraphFlowNode | null;
        conclusionEdges: GraphFlowEdge[];
    } => {
        const conclusion = phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion));

        let conclusionNode: GraphFlowNode | null = null;
        const conclusionEdges: GraphFlowEdge[] = [];

        if (conclusion) {
            conclusionNode = {
                id: conclusion.id,
                type: TreeNodeType.Phase,
                position,
                data: { ...conclusion },
            };

            if (parentIds.length > 0) {
                parentIds.forEach(parentId => {
                    conclusionEdges.push({
                        id: `${parentId}-${conclusion.id}`,
                        source: parentId,
                        target: conclusion.id,
                        markerEnd: {
                            type: MarkerType.ArrowClosed,
                            width: 20,
                            height: 20,
                        },
                        zIndex: -99,
                        data: {
                            edgeType: InvestigationGraphFlowEdgeType.HypothesisToConclusion,
                            sourceId: parentId,
                            targetId: conclusion.id,
                        },
                    });
                });
            }
        }

        return {
            conclusionNode,
            conclusionEdges,
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
            if (node && node?.nodeType === TreeNodeType.Phase && !node.title.toLowerCase().includes('validating hypothesis')) {
                phaseNodes.push(node);
            }
        });

        // Add initial investigation node
        const initialInvestigationNode = getInitialInvestigationNode(phaseNodes);
        if (!initialInvestigationNode) {
            return {
                graphFlowNodes: [],
                graphFlowEdges: [],
            };
        }
        const initialInvestigationNodePositionY = getParentNodePositionY(0, 0);

        // Add hypothesis nodes
        const hypothesisGroups = getHypothesisGroups(phaseNodes, nodes);
        const {
            nodes: hypothesisNodes,
            edges: hypothesisEdges,
            groupNodesCenterX,
            maxHeightOfGroupNodes,
            groupNodesPositionY,
        } = layoutHypothesisGroupAndChildrenNode(
            hypothesisGroups,
            initialInvestigationNode.id,
            initialInvestigationNodePositionY,
            initialInvestigationNode.height || 0
        );
        graphFlowNodes.push(...hypothesisNodes);
        graphFlowEdges.push(...hypothesisEdges);

        initialInvestigationNode.position = getInitialInvestigationNodePosition(groupNodesCenterX, initialInvestigationNode);
        graphFlowNodes.push(initialInvestigationNode);

        const { conclusionNode, conclusionEdges } = getConclusionNodesAndEdges(
            phaseNodes,
            { x: getNonGroupParentNodePositionX(groupNodesCenterX), y: getParentNodePositionY(groupNodesPositionY, maxHeightOfGroupNodes) },
            hypothesisGroups.map(group => group.groupNode.id)
        );

        if (conclusionNode) {
            graphFlowNodes.push(conclusionNode);
            graphFlowEdges.push(...conclusionEdges);
        }
        return {
            graphFlowNodes,
            graphFlowEdges,
        };
    };

    const centerGraph = (onInit: boolean) => {
        setTimeout(
            () => {
                fitView({ minZoom: 0.5, maxZoom: 1.5, padding: 50, duration: 50, interpolate: 'smooth' });
            },
            onInit ? 300 : 100
        );
    };

    useEffect(() => {
        const treeState = treeStateValue.treeState;
        const { graphFlowNodes, graphFlowEdges } = constructGraph(treeState);

        setNodes(graphFlowNodes);
        setEdges(graphFlowEdges);
        centerGraph(false);
    }, [treeStateValue]);

    useEffect(() => {});

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        centerGraph,
    };
};

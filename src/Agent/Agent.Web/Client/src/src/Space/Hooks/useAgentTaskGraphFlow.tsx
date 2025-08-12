import { graphlib, layout } from '@dagrejs/dagre';
import { MarkerType, useEdgesState, useNodesState, useReactFlow } from '@xyflow/react';
import { useEffect } from 'react';
import { InvestigationTreeNode, TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
import {
    AgentTaskNodeSize,
    AgentTaskPhaseNodeIdSuffix,
    GraphFlowEdge,
    GraphFlowNode,
    IAgentTaskGraphProps,
    InvestigationGraphFlowEdgeType,
} from '../Contracts/Activities';

export const useAgentTaskGraphFlow = (props: IAgentTaskGraphProps) => {
    const { treeStateValue } = props;

    const { fitView } = useReactFlow();

    const [nodes, setNodes, onNodesChange] = useNodesState<GraphFlowNode>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<GraphFlowEdge>([]);

    const isConclusionNode = (nodeId: string) => nodeId.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion);

    const getNodeWidth = (node: GraphFlowNode) =>
        node.type === TreeNodeType.Hypothesis ? AgentTaskNodeSize.HypothesisNode.width : AgentTaskNodeSize.PhaseNode.width;

    const getDagreLayout = (nodes: GraphFlowNode[], edges: GraphFlowEdge[]) => {
        // Add error handling for empty inputs
        if (!nodes || nodes.length === 0) {
            return { nodes: [], edges: [] };
        }

        const dagreGraph = new graphlib.Graph({}).setDefaultEdgeLabel(() => ({}));

        // Configure for investigation tree layout - top-to-bottom flow
        dagreGraph.setGraph({
            rankdir: 'TB', // Top-to-bottom for investigation flow
            ranksep: 200, // Vertical spacing between levels
            nodesep: 150, // Horizontal spacing between nodes at same level
            ranker: 'tight-tree',
        });

        // Add nodes first, then edges (following working pattern)
        nodes.forEach(node => {
            if (!isConclusionNode(node.id)) {
                dagreGraph.setNode(node.id, {
                    ...node, // Spread the node object like in working implementation
                    width: getNodeWidth(node), // Use the getNodeWidth function
                    height: 160, // Standard hypothesis node height
                });
            }
        });

        // Add edges after nodes
        edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));

        // Run dagre layout algorithm
        layout(dagreGraph);

        // Convert dagre positions back to React Flow format
        const computedNodes: GraphFlowNode[] = [];
        nodes.forEach(node => {
            if (!isConclusionNode(node.id)) {
                const position = dagreGraph.node(node.id);
                // We are shifting the dagre node position (anchor=center center) to the top left
                // so it matches the React Flow node anchor point (top left).
                const x = position.x - getNodeWidth(node) / 2; // Half of node width to center
                const y = position.y - 80; // Half of node height to center

                computedNodes.push({
                    ...node,
                    position: { x, y },
                });
            }
        });

        const conclusionNodeX =
            computedNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation))?.position.x ?? 160;
        const conclusionNodeY = Math.max(...computedNodes.map(node => node.position.y)) + 1.5 * AgentTaskNodeSize.HypothesisNode.height; // Place conclusion node below all others
        const conclusionNode = nodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion));

        if (conclusionNode) {
            computedNodes.push({
                ...conclusionNode,
                width: 320, // Standard conclusion node width
                height: 160, // Standard conclusion node height
                position: { x: conclusionNodeX, y: conclusionNodeY },
            });
        }

        return {
            nodes: computedNodes,
            edges,
        };
    };

    useEffect(() => {
        const treeState = treeStateValue.treeState;

        if (!treeState || treeState.rootNodeIds.length === 0) {
            setNodes([]);
            setEdges([]);
            return;
        }

        const graphFlowNodes: GraphFlowNode[] = [];
        const graphFlowEdges: GraphFlowEdge[] = [];

        const { rootNodeIds, nodes } = treeState;

        // Get phase nodes
        const phaseNodes: InvestigationTreeNode[] = [];
        rootNodeIds.forEach(id => {
            const node = nodes.get(id);
            if (node && node?.nodeType === TreeNodeType.Phase && !node.title.toLowerCase().includes('validating hypothesis')) {
                phaseNodes.push(node);
            }
        });

        const initialInvestigation = phaseNodes.find(node =>
            node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.InitialInvestigation)
        );
        const formingHypothesis = phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.FormingHypothesis));
        const conclusion = phaseNodes.find(node => node.id.toLowerCase().includes(AgentTaskPhaseNodeIdSuffix.Conclusion));

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

        // Add initial investigation node
        if (initialInvestigation) {
            graphFlowNodes.push({
                id: initialInvestigation.id,
                type: TreeNodeType.Phase,
                position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                data: { ...initialInvestigation },
            });
        }

        // Add initial hypothesis nodes
        const initialHypotheseIds = formingHypothesis?.childrenIds || [];
        if (initialHypotheseIds.length > 0) {
            initialHypotheseIds.forEach((hypothesisId, index) => {
                const hypothesis = nodes.get(hypothesisId);
                if (hypothesis) {
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

                    // Add edge from initial investigation to hypothesis
                    if (initialInvestigation) {
                        graphFlowEdges.push({
                            id: `${initialInvestigation.id}-${hypothesis.id}`,
                            source: initialInvestigation.id,
                            target: hypothesis.id,
                            markerEnd: {
                                type: MarkerType.ArrowClosed,
                                width: 20,
                                height: 20,
                            },
                            data: {
                                edgeType: InvestigationGraphFlowEdgeType.PhaseToHypothesis,
                                sourceId: initialInvestigation.id,
                                targetId: hypothesis.id,
                            },
                        });
                    }

                    // Add child hypotheses recursively
                    addChildHypotheses(hypothesis, graphFlowNodes, graphFlowEdges, 1);
                }
            });
        }

        // Add Conclusion
        if (conclusion) {
            graphFlowNodes.push({
                id: conclusion.id,
                type: TreeNodeType.Phase,
                position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                data: { ...conclusion },
            });
        }

        const { nodes: computedNodes, edges: computedEdges } = getDagreLayout(graphFlowNodes, graphFlowEdges);

        setNodes(computedNodes);
        setEdges(computedEdges);

        setTimeout(() => fitView({ minZoom: 0.5, maxZoom: 1.2, padding: 50, duration: 100, interpolate: 'smooth' }), 100);
    }, [treeStateValue]);

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
    };
};

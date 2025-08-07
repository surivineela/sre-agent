import { Edge, Node, useEdgesState, useNodesState } from '@xyflow/react';
import { useCallback, useEffect, useMemo } from 'react';
import { InvestigationTreeNode } from '../../Contexts/InvestigationTreeContext';
import { useInvestigationLayout } from './useInvestigationLayout';

type FlowNode = Node<any>;
type FlowEdge = Edge<any>;

export const useInvestigationFlow = (
    rootNodes: InvestigationTreeNode[],
    onShowDetails?: (title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => void
) => {
    const [nodes, setNodes, onNodesChange] = useNodesState<FlowNode>([]);
    const [edges, setEdges, onEdgesChange] = useEdgesState<FlowEdge>([]);
    const layoutGraph = useInvestigationLayout();

    // Create a stable memoized version that only changes when content actually changes
    const stableRootNodes = useMemo(() => {
        console.log('📦 STABLE REFERENCE CHANGED - Creating new stable rootNodes reference');
        console.log(
            '📦 New stable nodes:',
            rootNodes.length,
            rootNodes.map(n => `${n.id}-${n.title}`)
        );
        return rootNodes;
    }, [
        rootNodes
            .map(node => {
                // Recursively hash all children and their descendants
                function hashNodeAndDescendants(node: InvestigationTreeNode): string {
                    const nodeHash = `${node.id}-${node.title}-${node.description}-${node.status}-${node.isLoading}-${node.isValidating}-${node.expanded}-${node.children.length}-${node.steps?.length ?? 0}`;
                    if (!node.children || node.children.length === 0) {
                        return nodeHash;
                    }
                    const childrenHashes = node.children.map(child => hashNodeAndDescendants(child)).join('|');
                    return `${nodeHash}|${childrenHashes}`;
                }

                return hashNodeAndDescendants(node);
            })
            .join('||'),
    ]);

    const addChildHypotheses = useCallback(
        (parentHypothesis: InvestigationTreeNode, flowNodes: FlowNode[], flowEdges: FlowEdge[], depth: number) => {
            if (!parentHypothesis.expanded || parentHypothesis.children.length === 0) {
                return;
            }

            const children = parentHypothesis.children;

            children.forEach((child, index) => {
                flowNodes.push({
                    id: child.id,
                    type: 'hypothesis',
                    position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                    data: {
                        id: child.id,
                        title: child.title,
                        description: child.description,
                        status: child.status,
                        isValidating: child.isValidating,
                        isChild: depth > 0,
                        hasChildren: child.children.length > 0,
                        expanded: child.expanded,
                        index: index + 1,
                        nodeType: 'hypothesis',
                        // Include detailed step data for overlay
                        steps: child.steps || [],
                        onShowDetails,
                    },
                });

                // Add edge from parent to child
                flowEdges.push({
                    id: `${parentHypothesis.id}-${child.id}`,
                    source: parentHypothesis.id,
                    target: child.id,
                    type: 'investigation',
                    data: {
                        edgeType: 'hypothesis-to-hypothesis',
                    },
                });

                // Recursively add grandchildren
                addChildHypotheses(child, flowNodes, flowEdges, depth + 1);
            });
        },
        []
    );

    const convertTreeToFlow = useCallback(() => {
        console.log('🔄 convertTreeToFlow RECREATED! This should be rare!');
        console.log('🔍 convertTreeToFlow called with rootNodes:', stableRootNodes.length, stableRootNodes);

        // Skip if no stable root nodes
        if (!stableRootNodes || stableRootNodes.length === 0) {
            console.log('⏭️ Skipping conversion - no stable root nodes');
            setNodes([]);
            setEdges([]);
            return;
        }

        const flowNodes: FlowNode[] = [];
        const flowEdges: FlowEdge[] = [];

        // Filter phase nodes
        const phaseNodes = stableRootNodes.filter(
            node => node.nodeType === 'phase' && !node.title.toLowerCase().includes('validating hypothesis')
        );

        console.log('🔍 Filtered phase nodes:', phaseNodes.length, phaseNodes);

        // Find specific phase nodes
        const initialInvestigation = phaseNodes.find(node => node.id.toLowerCase().includes('initial-investigation'));
        const formingHypothesis = phaseNodes.find(node => node.id.toLowerCase().includes('forming-hypothesis'));
        const conclusion = phaseNodes.find(node => node.id.toLowerCase().includes('conclusion'));

        // 1. Add Initial Investigation
        if (initialInvestigation) {
            console.log('🎯 Creating Initial Investigation node:');
            console.log('   - Title:', initialInvestigation.title);
            console.log('   - Description:', initialInvestigation.description);
            console.log('   - Status:', initialInvestigation.status);

            flowNodes.push({
                id: initialInvestigation.id,
                type: 'phase',
                position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                data: {
                    id: initialInvestigation.id,
                    title: initialInvestigation.title,
                    description: initialInvestigation.description,
                    status: initialInvestigation.status,
                    isLoading: initialInvestigation.isLoading,
                    nodeType: 'phase',
                    // Include detailed step data for overlay
                    steps: initialInvestigation.gatheringContextSteps || [],
                    onShowDetails,
                },
            });
        }

        // 2. Add Initial Hypotheses and their children recursively
        const initialHypotheses = formingHypothesis?.children || [];

        if (initialHypotheses.length > 0) {
            initialHypotheses.forEach((hypothesis, index) => {
                flowNodes.push({
                    id: hypothesis.id,
                    type: 'hypothesis',
                    position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                    data: {
                        id: hypothesis.id,
                        title: hypothesis.title,
                        description: hypothesis.description,
                        status: hypothesis.status,
                        isValidating: hypothesis.isValidating,
                        isChild: false,
                        hasChildren: hypothesis.children.length > 0,
                        expanded: hypothesis.expanded,
                        index: index + 1,
                        nodeType: 'hypothesis',
                        // Include detailed step data for overlay
                        steps: hypothesis.steps || [],
                        onShowDetails,
                    },
                });

                // Add edge from initial investigation to hypothesis
                if (initialInvestigation) {
                    flowEdges.push({
                        id: `${initialInvestigation.id}-${hypothesis.id}`,
                        source: initialInvestigation.id,
                        target: hypothesis.id,
                        type: 'investigation',
                        data: {
                            edgeType: 'phase-to-hypothesis',
                        },
                    });
                }

                // Add child hypotheses recursively
                addChildHypotheses(hypothesis, flowNodes, flowEdges, 1);
            });
        }

        // 3. Add Conclusion
        if (conclusion) {
            flowNodes.push({
                id: conclusion.id,
                type: 'phase',
                position: { x: 0, y: 0 }, // Temporary position - will be set by dagre
                data: {
                    id: conclusion.id,
                    title: conclusion.title,
                    description: conclusion.description,
                    status: conclusion.status,
                    isLoading: conclusion.isLoading,
                    nodeType: 'phase',
                    onShowDetails,
                },
            });
        }

        console.log('🚀 Built flow nodes:', flowNodes.length, flowNodes);
        console.log('🚀 Built flow edges:', flowEdges.length, flowEdges);

        // Apply dagre layout to get proper positioning
        const { nodes: layoutedNodes, edges: layoutedEdges } = layoutGraph(flowNodes, flowEdges);

        console.log('🎨 Layouted nodes:', layoutedNodes.length, layoutedNodes);
        console.log('🎨 Layouted edges:', layoutedEdges.length, layoutedEdges);

        // Manually position conclusion node at the bottom
        const conclusionNodeIndex = layoutedNodes.findIndex(
            node => node.data?.nodeType === 'phase' && node.id.toLowerCase().includes('conclusion')
        );

        const initialInvestigationIndex = layoutedNodes.findIndex(
            node => node.data?.nodeType === 'phase' && node.id.toLowerCase().includes('initial-investigation')
        );

        // Calculate the visual bounds of all hypothesis nodes for centering
        const hypothesisNodes = layoutedNodes.filter(node => node.data?.nodeType === 'hypothesis');

        if (hypothesisNodes.length > 0) {
            const minX = Math.min(...hypothesisNodes.map(node => node.position.x));
            const maxX = Math.max(...hypothesisNodes.map(node => node.position.x));
            const centerX = (minX + maxX) / 2;

            console.log('📏 Calculated flow bounds:', { minX, maxX, centerX });

            // Center the initial investigation over the hypothesis tree
            if (initialInvestigationIndex !== -1) {
                layoutedNodes[initialInvestigationIndex].position.x = centerX;
                console.log('🎯 Centered initial investigation at X:', centerX);
            }

            // Center the conclusion over the hypothesis tree and position at bottom
            if (conclusionNodeIndex !== -1) {
                const otherNodes = layoutedNodes.filter((_, index) => index !== conclusionNodeIndex);
                const maxY = otherNodes.length > 0 ? Math.max(...otherNodes.map(node => node.position.y)) : 0;

                layoutedNodes[conclusionNodeIndex].position.y = maxY + 300;
                layoutedNodes[conclusionNodeIndex].position.x = centerX;

                console.log('🎯 Centered conclusion at:', {
                    x: centerX,
                    y: maxY + 300,
                    title: layoutedNodes[conclusionNodeIndex].data?.title,
                });
            }
        } else {
            // Fallback: if no hypothesis nodes, use default center position
            const defaultCenterX = 400;

            if (initialInvestigationIndex !== -1) {
                layoutedNodes[initialInvestigationIndex].position.x = defaultCenterX;
            }

            if (conclusionNodeIndex !== -1) {
                const otherNodes = layoutedNodes.filter((_, index) => index !== conclusionNodeIndex);
                const maxY = otherNodes.length > 0 ? Math.max(...otherNodes.map(node => node.position.y)) : 0;

                layoutedNodes[conclusionNodeIndex].position.y = maxY + 300;
                layoutedNodes[conclusionNodeIndex].position.x = defaultCenterX;
            }
        }

        // Debug the nodes being created
        layoutedNodes.forEach(node => {
            console.log('🔍 Layouted node:', { id: node.id, type: node.type, title: node.data?.title, position: node.position });
        });

        setNodes(layoutedNodes);
        setEdges(layoutedEdges);
    }, [stableRootNodes, layoutGraph, addChildHypotheses]);

    // Update flow when tree data changes
    useEffect(() => {
        convertTreeToFlow();
    }, [convertTreeToFlow]);

    return {
        nodes,
        edges,
        onNodesChange,
        onEdgesChange,
        refreshLayout: convertTreeToFlow,
    };
};

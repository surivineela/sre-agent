import { graphlib, layout } from '@dagrejs/dagre';
import { Edge, Node } from '@xyflow/react';
import { useCallback } from 'react';

export const useInvestigationLayout = () => {
    const getDagreLayout = useCallback((nodes: Node[], edges: Edge[]) => {
        // Add error handling for empty inputs
        if (!nodes || nodes.length === 0) {
            return { nodes: [], edges: [] };
        }

        const dagreGraph = new graphlib.Graph().setDefaultEdgeLabel(() => ({}));

        // Configure for investigation tree layout - top-to-bottom flow
        dagreGraph.setGraph({
            rankdir: 'TB', // Top-to-bottom for investigation flow
            ranksep: 200, // Vertical spacing between levels
            nodesep: 150, // Horizontal spacing between nodes at same level
        });

        // Add nodes first, then edges (following working pattern)
        nodes.forEach(node => {
            dagreGraph.setNode(node.id, {
                ...node, // Spread the node object like in working implementation
                width: 320, // Standard hypothesis node width
                height: 160, // Standard hypothesis node height
            });
        });

        // Add edges after nodes
        edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));

        // Run dagre layout algorithm
        layout(dagreGraph);

        // Convert dagre positions back to React Flow format
        const computedNodes = nodes.map(node => {
            const position = dagreGraph.node(node.id);
            // We are shifting the dagre node position (anchor=center center) to the top left
            // so it matches the React Flow node anchor point (top left).
            const x = position.x - 160; // Half of node width to center
            const y = position.y - 80; // Half of node height to center

            return {
                ...node,
                position: { x, y },
            };
        });

        return {
            nodes: computedNodes,
            edges,
        };
    }, []); // Empty dependency array to prevent recreation

    return getDagreLayout;
};

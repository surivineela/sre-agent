import ELK from 'elkjs/lib/elk-api';
import { GraphEdge, GraphNode, NodeSize } from './Contracts/Graph';
import { Edge, Node } from '@xyflow/react';

const elk = new ELK({
    workerUrl: '../elk-worker.min.js'
})

self.onmessage = async (event: MessageEvent<{ nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[] }>) => {
    const { nodes, edges } = event.data;

    try {
        const layout = await elk.layout({
            id: 'root',
            children: nodes.map(node => ({
                ...node,
                x: node.position.x,
                y: node.position.y,
                width: NodeSize.width,
                height: NodeSize.height,
            })),
            edges: edges.map(edge => ({
                ...edge,
                id: edge.id,
                sources: [edge.source],
                targets: [edge.target],
                labels: [{ text: edge.data?.label ?? "" }]
            })),
        },
            {
                layoutOptions: {
                    'elk.algorithm': 'org.eclipse.elk.force',
                    'elk.force.repulsivePower': '0',
                    'elk.spacing.nodeNode': '5', // Reduce space between nodes
                    'elk.spacing.edgeNode': '10',
                    'elk.spacing.edgeEdge': '10',
                }
            }
        )

        self.postMessage({ type: 'success', layout })

    } catch (error) {
        self.postMessage({ type: 'error', error: error || 'Layout failed' })
    }
}
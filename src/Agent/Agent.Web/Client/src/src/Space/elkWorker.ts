import ELK from 'elkjs/lib/elk-api';
import { GraphEdge, GraphNode, NodeSize } from './Contracts/Graph';
import { Edge, Node } from '@xyflow/react';
import debounce from 'lodash/debounce';

const elk = new ELK({
    workerUrl: '../elk-worker.min.js'
})

self.onmessage = debounce(async (event: MessageEvent<{ nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[], rootNodeId: string }>) => {
    const { nodes, edges, rootNodeId } = event.data;

    try {
        let layoutOptions: any = nodes.length <= 10 ? {
            'elk.algorithm': 'org.eclipse.elk.layered',                         // the actual algorithm
            'elk.direction': 'RIGHT',                            // TOP, RIGHT, LEFT, DOWN
            'elk.layered.spacing.nodeNodeBetweenLayers': '100',
        } : {
            'elk.algorithm': 'org.eclipse.elk.force',
            'elk.force.repulsivePower': '0',
        }

        layoutOptions = {
            ...layoutOptions,
            'elk.spacing.nodeNode': nodes.length < 10 ? '25' : '5', // Reduce space between nodes
            'elk.spacing.edgeNode': '10',
            'elk.spacing.edgeEdge': '10',
        }
        const layout = await elk.layout({
            id: 'root',
            children: nodes.map(node => ({
                ...node,
                x: node.position.x,
                y: node.position.y,
                width: node.id === rootNodeId ? NodeSize.appGroupWidth : NodeSize.width,
                height: node.id === rootNodeId ? NodeSize.appGroupHeight : NodeSize.height,
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
                layoutOptions
            }
        )

        self.postMessage({ type: 'success', layout })

    } catch (error) {
        self.postMessage({ type: 'error', error: error || 'Layout failed' })
    }
}, 500)
import { Edge, Node } from '@xyflow/react';
import ELK from 'elkjs/lib/elk-api';
import {
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeSize,
    ExtendedAgentNodeType,
} from './Contracts/ExtendedAgentGraph';

const elk = new ELK({
    workerUrl: '../elk-worker.min.js',
});

const getNodeDimensions = (nodeType?: ExtendedAgentNodeType) => {
    switch (nodeType) {
        case ExtendedAgentNodeType.Agent:
            return { width: ExtendedAgentNodeSize.agentWidth, height: ExtendedAgentNodeSize.agentHeight };
        case ExtendedAgentNodeType.Tool:
            return { width: ExtendedAgentNodeSize.toolWidth, height: ExtendedAgentNodeSize.toolHeight };
        case ExtendedAgentNodeType.Connector:
            return { width: ExtendedAgentNodeSize.connectorWidth, height: ExtendedAgentNodeSize.connectorHeight };
        case ExtendedAgentNodeType.Trigger:
            return { width: ExtendedAgentNodeSize.triggerWidth, height: ExtendedAgentNodeSize.triggerHeight };
        default:
            return { width: ExtendedAgentNodeSize.agentWidth, height: ExtendedAgentNodeSize.agentHeight };
    }
};

self.onmessage = async (event: MessageEvent<{ nodes: Node<ExtendedAgentGraphNode>[]; edges: Edge<ExtendedAgentGraphEdge>[] }>) => {
    const { nodes, edges } = event.data;

    try {
        const layout = await elk.layout(
            {
                id: 'root',
                children: nodes.map(node => {
                    const { width, height } = getNodeDimensions(node.data?.type);
                    return {
                        ...node,
                        x: node.position.x,
                        y: node.position.y,
                        width,
                        height,
                    };
                }),
                edges: edges.map(edge => ({
                    ...edge,
                    id: edge.id,
                    sources: [edge.source],
                    targets: [edge.target],
                })),
            },
            {
                layoutOptions: {
                    'elk.algorithm': 'org.eclipse.elk.force',
                    'elk.force.repulsivePower': '0',
                    'elk.spacing.nodeNode': '5', // Reduce space between nodes
                    'elk.spacing.edgeNode': '10',
                    'elk.spacing.edgeEdge': '10',
                },
            }
        );

        self.postMessage({ type: 'success', layout });
    } catch (error) {
        self.postMessage({ type: 'error', error: error || 'Layout failed' });
    }
};

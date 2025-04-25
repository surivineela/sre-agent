import { graphlib, layout } from '@dagrejs/dagre';
import { Edge, Node } from '@xyflow/react';
import { useEffect, useRef } from 'react';
import { GraphEdge, GraphNode, NodeSize } from '../Contracts/Graph';
import { getSourceAndTargetHandleId } from '../Graph/Utility';

export const useGraphLayout = () => {
    const workerRef = useRef<Worker>();

    const getEdges = (nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[]) => {
        return edges.map((edge: any) => {
            const edgeSource = edge.sources?.[0] ?? edge.source;
            const edgeTarget = edge.targets?.[0] ?? edge.target;
            const source = nodes.find(node => node.id === edgeSource);
            const target = nodes.find(node => node.id === edgeTarget);
            const edgeResult: Edge<GraphEdge> = { ...edge, id: edge.id, source: edgeSource, target: edgeTarget };

            if (source && target) {
                const sourcePos = source.position;
                const targetPos = target.position;

                const { sourceHandle, targetHandle } = getSourceAndTargetHandleId(sourcePos, targetPos);

                edgeResult.sourceHandle = sourceHandle;
                edgeResult.targetHandle = targetHandle;
            }
            return edgeResult;
        });
    };

    const getElkLayout = async (
        nodes: Node<GraphNode>[],
        edges: Edge<GraphEdge>[]
    ): Promise<{ nodes: Node<GraphNode>[]; edges: Edge<GraphEdge>[] }> => {
        return new Promise((resolve, reject) => {
            const worker = workerRef.current;
            if (!worker) return reject('Worker not initialized');

            const handleMessage = (event: MessageEvent<{ error: unknown; type: string; layout: any }>) => {
                const { type, layout } = event.data;

                if (type === 'success' && layout) {
                    const computedNodes: Node<GraphNode>[] = (layout.children ?? []).map((node: any) => ({
                        ...node,
                        position: { x: node.x ?? 0, y: node.y ?? 0 },
                    }));
                    const computedEdges: Edge<GraphEdge>[] = getEdges(computedNodes, layout.edges ?? []);

                    resolve({ nodes: computedNodes, edges: computedEdges });
                } else {
                    resolve({ nodes, edges });
                }

                worker.removeEventListener('message', handleMessage);
            };

            worker.onmessage = event => {
                handleMessage(event);
            };
            worker.postMessage({ nodes, edges });
        });
    };

    const getDagreLayout = (nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[]) => {
        const dagreGraph = new graphlib.Graph().setDefaultEdgeLabel(() => ({}));
        dagreGraph.setGraph({ rankdir: 'LR', ranksep: NodeSize.width });

        edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));
        nodes.forEach(node =>
            dagreGraph.setNode(node.id, {
                ...node,
                width: NodeSize.width,
                height: NodeSize.height,
            })
        );

        layout(dagreGraph);

        const computedNodes = nodes.map(node => {
            const position = dagreGraph.node(node.id);
            // We are shifting the dagre node position (anchor=center center) to the top left
            // so it matches the React Flow node anchor point (top left).
            const x = position.x - 100;
            const y = position.y - 100;

            return { ...node, position: { x, y } };
        });

        const computedEdges = getEdges(computedNodes, edges);

        return {
            nodes: computedNodes,
            edges: computedEdges,
        };
    };

    const layoutGraph = async (nodes: Node<GraphNode>[], edges: Edge<GraphEdge>[]) => {
        if (nodes.length < 20) {
            return Promise.resolve(getDagreLayout(nodes, edges));
        } else {
            return getElkLayout(nodes, edges);
        }
    };

    useEffect(() => {
        workerRef.current = new Worker(new URL('../elkWorker.ts', import.meta.url), {
            type: 'module',
        });

        return () => {
            workerRef.current?.terminate();
        };
    }, []);

    return layoutGraph;
};

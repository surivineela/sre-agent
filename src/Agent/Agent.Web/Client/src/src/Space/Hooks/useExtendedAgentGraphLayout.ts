import { graphlib, layout } from '@dagrejs/dagre';
import { Edge, Node } from '@xyflow/react';
import { useCallback, useEffect, useRef } from 'react';
import {
    ExtendedAgentGraphEdge,
    ExtendedAgentGraphNode,
    ExtendedAgentNodeSize,
    ExtendedAgentNodeType,
    SkillGroupData,
    ToolboxData,
} from '../Contracts/ExtendedAgentGraph';
import { EXPANDED_SKILL_GROUP_CARD_TYPE, EXPANDED_TOOLBOX_CARD_TYPE } from '../Graph/ExtendedAgentGraphUtility';
import { getSourceAndTargetHandleId } from '../Graph/Utility';

export const useExtendedAgentGraphLayout = () => {
    const workerRef = useRef<Worker>();

    const getNodeDimensions = useCallback((node: Node<ExtendedAgentGraphNode>) => {
        switch (node.data?.type) {
            case ExtendedAgentNodeType.Agent:
                return { width: ExtendedAgentNodeSize.agentWidth, height: ExtendedAgentNodeSize.agentHeight };
            case ExtendedAgentNodeType.Tool:
            case ExtendedAgentNodeType.SystemTool:
                return { width: ExtendedAgentNodeSize.toolWidth, height: ExtendedAgentNodeSize.toolHeight };
            case ExtendedAgentNodeType.Connector:
                return { width: ExtendedAgentNodeSize.connectorWidth, height: ExtendedAgentNodeSize.connectorHeight };
            case ExtendedAgentNodeType.Trigger:
                return { width: ExtendedAgentNodeSize.triggerWidth, height: ExtendedAgentNodeSize.triggerHeight };
            case ExtendedAgentNodeType.Skill:
                return { width: ExtendedAgentNodeSize.skillWidth, height: ExtendedAgentNodeSize.skillHeight };
            case ExtendedAgentNodeType.SkillGroup:
                // Check if this is an expanded skill group and calculate dynamic height
                if (node.type === EXPANDED_SKILL_GROUP_CARD_TYPE) {
                    const skillGroupData = node.data?.data as SkillGroupData | undefined;
                    const skillCount = skillGroupData?.skillCount ?? 1;
                    return {
                        width: ExtendedAgentNodeSize.skillGroupWidth,
                        height: ExtendedAgentNodeSize.getExpandedSkillGroupHeight(skillCount),
                    };
                }
                return { width: ExtendedAgentNodeSize.skillGroupWidth, height: ExtendedAgentNodeSize.skillGroupHeight };
            case ExtendedAgentNodeType.Toolbox:
                // Check if this is an expanded toolbox and calculate dynamic height
                if (node.type === EXPANDED_TOOLBOX_CARD_TYPE) {
                    const toolboxData = node.data?.data as ToolboxData | undefined;
                    const toolCount = toolboxData?.toolCount ?? 1;
                    return {
                        width: ExtendedAgentNodeSize.toolboxWidth,
                        height: ExtendedAgentNodeSize.getExpandedToolboxHeight(toolCount),
                    };
                }
                // Collapsed toolbox - calculate based on preview rows (up to 3)
                {
                    const toolboxData = node.data?.data as ToolboxData | undefined;
                    const toolCount = toolboxData?.toolCount ?? 1;
                    return {
                        width: ExtendedAgentNodeSize.toolboxWidth,
                        height: ExtendedAgentNodeSize.getCollapsedToolboxHeight(toolCount),
                    };
                }
            default:
                return { width: ExtendedAgentNodeSize.agentWidth, height: ExtendedAgentNodeSize.agentHeight };
        }
    }, []);

    const getEdges = useCallback(
        (nodes: Node<ExtendedAgentGraphNode>[], edges: Edge<ExtendedAgentGraphEdge>[]): Edge<ExtendedAgentGraphEdge>[] => {
            return edges.map((edge: any) => {
                const edgeSource = edge.sources?.[0] ?? edge.source;
                const edgeTarget = edge.targets?.[0] ?? edge.target;
                const source = nodes.find(node => node.id === edgeSource);
                const target = nodes.find(node => node.id === edgeTarget);
                const edgeResult: Edge<ExtendedAgentGraphEdge> = {
                    ...edge,
                    id: edge.id,
                    source: edgeSource,
                    target: edgeTarget,
                };

                if (source && target) {
                    const sourcePos = source.position;
                    const targetPos = target.position;

                    const { sourceHandle, targetHandle } = getSourceAndTargetHandleId(sourcePos, targetPos);

                    edgeResult.sourceHandle = sourceHandle;
                    edgeResult.targetHandle = targetHandle;
                }
                return edgeResult;
            });
        },
        []
    );

    const getDagreLayout = useCallback(
        (nodes: Node<ExtendedAgentGraphNode>[], edges: Edge<ExtendedAgentGraphEdge>[]) => {
            const dagreGraph = new graphlib.Graph().setDefaultEdgeLabel(() => ({}));
            dagreGraph.setGraph({
                rankdir: 'LR',
                ranksep: ExtendedAgentNodeSize.agentWidth,
                nodesep: ExtendedAgentNodeSize.toolHeight + 40,
            });

            edges.forEach(edge => dagreGraph.setEdge(edge.source, edge.target));
            nodes.forEach(node => {
                const { width, height } = getNodeDimensions(node);
                dagreGraph.setNode(node.id, {
                    ...node,
                    width,
                    height,
                });
            });

            layout(dagreGraph);

            const computedNodes = nodes.map(node => {
                const position = dagreGraph.node(node.id);
                const { width, height } = getNodeDimensions(node);
                const x = position.x - width / 2;
                const y = position.y - height / 2;

                return { ...node, position: { x, y } };
            });

            const computedEdges = getEdges(computedNodes, edges);

            return {
                nodes: computedNodes,
                edges: computedEdges,
            };
        },
        [getEdges, getNodeDimensions]
    );

    const getElkLayout = useCallback(
        async (nodes: Node<ExtendedAgentGraphNode>[], edges: Edge<ExtendedAgentGraphEdge>[]) => {
            return new Promise<{ nodes: Node<ExtendedAgentGraphNode>[]; edges: Edge<ExtendedAgentGraphEdge>[] }>(resolve => {
                const worker = workerRef.current;
                if (!worker) {
                    resolve(getDagreLayout(nodes, edges));
                    return;
                }

                const handleMessage = (event: MessageEvent<{ error: unknown; type: string; layout: any }>) => {
                    const { type, layout } = event.data;

                    if (type === 'success' && layout) {
                        const computedNodes: Node<ExtendedAgentGraphNode>[] = (layout.children ?? []).map((node: any) => ({
                            ...node,
                            position: { x: node.x ?? 0, y: node.y ?? 0 },
                        }));
                        const computedEdges: Edge<ExtendedAgentGraphEdge>[] = getEdges(computedNodes, layout.edges ?? []);

                        resolve({ nodes: computedNodes, edges: computedEdges });
                    } else {
                        resolve({ nodes, edges });
                    }

                    worker.removeEventListener('message', handleMessage);
                    worker.removeEventListener('error', handleError);
                };

                const handleError = () => {
                    resolve(getDagreLayout(nodes, edges));
                    worker.removeEventListener('message', handleMessage);
                    worker.removeEventListener('error', handleError);
                };

                worker.addEventListener('message', handleMessage);
                worker.addEventListener('error', handleError);
                worker.postMessage({ nodes, edges });
            });
        },
        [getDagreLayout, getEdges]
    );

    const layoutGraph = useCallback(
        async (nodes: Node<ExtendedAgentGraphNode>[], edges: Edge<ExtendedAgentGraphEdge>[]) => {
            if (nodes.length < 50) {
                return Promise.resolve(getDagreLayout(nodes, edges));
            }

            return getElkLayout(nodes, edges);
        },
        [getDagreLayout, getElkLayout]
    );

    useEffect(() => {
        workerRef.current = new Worker(new URL('../extendedAgentElkWorker.ts', import.meta.url), {
            type: 'module',
        });

        return () => {
            workerRef.current?.terminate();
            workerRef.current = undefined;
        };
    }, []);

    return layoutGraph;
};

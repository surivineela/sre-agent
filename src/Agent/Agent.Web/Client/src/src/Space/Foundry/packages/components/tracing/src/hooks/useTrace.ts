import { useMemo } from 'react';
import type { ISpan, ISpanTreeNode } from '../types/trace';

const sortByStartTime = (a: ISpanTreeNode, b: ISpanTreeNode) => {
    const tA = a.start_time ? new Date(a.start_time).getTime() : 0;
    const tB = b.start_time ? new Date(b.start_time).getTime() : 0;
    return tA - tB;
};

export function useTreeViewNodes(
    selectedSpanId: string | undefined,
    spans: ISpan[]
): {
    rootNodes: ISpanTreeNode[];
    nodes: ISpanTreeNode[];
    defaultOpenItems: string[];
} {
    return useMemo(() => {
        const nodesMap: Record<string, ISpanTreeNode> = {};
        const allNodes: ISpanTreeNode[] = [];
        const defaultOpenItems: string[] = [];

        // Build nodes and map
        for (const span of spans) {
            if (span.context?.span_id === undefined) {
                continue;
            }
            const node: ISpanTreeNode = { ...span, uiChildren: [] };
            nodesMap[span.context.span_id] = node;
            allNodes.push(node);
        }

        // Link children to parents
        for (const node of allNodes) {
            if (node.parent_id) {
                nodesMap[node.parent_id].uiChildren.push(node);
            }
        }

        // Sort children
        for (const node of allNodes) {
            if (node.uiChildren.length > 0) {
                node.uiChildren.sort(sortByStartTime);
            }
        }

        // Find root nodes
        const rootNodes = allNodes.filter(node => !node.parent_id).sort(sortByStartTime);

        // Default open items: parents of selected, or non-genai types
        const selectedNode = allNodes.find(n => n.context?.span_id === selectedSpanId);
        for (const node of allNodes) {
            if (selectedNode?.parent_id && node.context?.span_id === selectedNode.parent_id) {
                defaultOpenItems.push(node.context?.span_id ?? '');
            }
        }

        return {
            rootNodes,
            nodes: allNodes,
            defaultOpenItems,
        };
    }, [spans, selectedSpanId]);
}

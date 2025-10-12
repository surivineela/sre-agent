import { Tree, TreeItem } from '@fluentui/react-components';
import type { JSX } from 'react';
import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { Thread } from '../../../../../../../Common/Contracts/DataPlane/Thread';
import { ThreadTraceResources } from '../../../../../../../Strings/SREAgentResources';
import { useWindowSize } from '../../../../../../Hooks/useWindowSize';
import { useTreeViewNodes } from '../hooks/useTrace';
import type { ISpan, ISpanTreeNode } from '../types/trace';
import { useTreeViewStyles } from './TreeView.Styles';
import { TreeViewItem } from './treeViewItem/TreeViewItem';

interface Connection {
    nodeId: string | undefined;
    parentId: string | null | undefined;
}

const getConnection = (node: ISpanTreeNode): Connection => {
    return {
        nodeId: node.context?.span_id,
        parentId: node.parent_id,
    };
};

interface ITreeViewPros {
    selectedSpanId: string | undefined;
    spans: ISpan[];
    onSpanIdSelect: (spanId: string | undefined) => void;
    initialOpenItems?: string[];
    thread: Thread;
}

export function TreeView({ selectedSpanId, spans, onSpanIdSelect, initialOpenItems, thread }: ITreeViewPros): JSX.Element {
    const intl = useIntl();
    const styles = useTreeViewStyles();
    const treeData = useTreeViewNodes(selectedSpanId, spans);
    const [openIds, setOpenIds] = useState<string[]>(
        initialOpenItems ? [...initialOpenItems, ...treeData.defaultOpenItems] : treeData.defaultOpenItems
    );
    const { width, height } = useWindowSize();
    const timeoutRef = useRef<number | undefined>(undefined);
    const handleToggleOpen = (spanId: string) => {
        setOpenIds(prevOpenIds => (prevOpenIds.includes(spanId) ? prevOpenIds.filter(id => id !== spanId) : [...prevOpenIds, spanId]));
    };

    const [connections, setConnections] = useState<Connection[]>(treeData.nodes.map(getConnection));

    useEffect(() => {
        setConnections(prev => {
            const needsUpdate =
                prev.length !== treeData.nodes.length ||
                prev.some(
                    (info, index) =>
                        info.nodeId !== treeData.nodes[index]?.context?.span_id || info.parentId !== treeData.nodes[index]?.parent_id
                );
            if (needsUpdate) {
                return treeData.nodes.map(getConnection);
            }
            return prev;
        });
    }, [treeData.nodes]);

    useLayoutEffect(() => {
        const renderPaths = () => {
            for (const connection of connections) {
                const { nodeId, parentId } = connection;
                if (!nodeId || !parentId || !openIds.includes(parentId)) {
                    continue;
                }

                const layerElement = document.querySelector('[data-tree-path-layer="path-layer"]');
                const parentElement = document.querySelector(`[data-tree-path-id="${parentId}"]`);
                const currentElement = document.querySelector(`[data-tree-path-id="${nodeId}"]`);
                const targetDiv = document.querySelector('[data-tree-path-layer="path-layer"]');

                if (!parentElement || !currentElement || !targetDiv || !layerElement) {
                    continue;
                }

                const layerRect = layerElement.getBoundingClientRect();
                const parentRect = parentElement.getBoundingClientRect();
                const currentRect = currentElement.getBoundingClientRect();
                const currentRectMiddle = (currentRect.top + currentRect.bottom - 16) / 2;

                const left = (parentElement as HTMLElement).offsetLeft + 18;
                const top = parentRect.top - layerRect.top + parentRect.height / 2;

                const pathElement = document.createElement('div');
                pathElement.className = `${styles.traceTreePath} traceTreePath`;
                pathElement.style.setProperty('left', `${left.toString()}px`);
                pathElement.style.setProperty('top', `${top.toString()}px`);
                pathElement.style.setProperty('width', `${(currentRect.left - parentRect.left).toString()}px`);
                pathElement.style.setProperty('height', `${(currentRectMiddle - parentRect.top).toString()}px`);
                pathElement.setAttribute('aria-hidden', 'true');
                pathElement.dataset.testid = 'trace-tree-path';
                targetDiv.append(pathElement);
            }
        };

        timeoutRef.current = window.setTimeout(renderPaths, 300);

        return () => {
            if (timeoutRef.current !== undefined) {
                window.clearTimeout(timeoutRef.current);
            }
            for (const element of document.querySelectorAll('.traceTreePath')) {
                element.remove();
            }
        };
    }, [connections, openIds, styles.traceTreePath, width, height]);

    const renderTree = (nodes: ISpanTreeNode[]): JSX.Element[] =>
        nodes.map(node => {
            const spanId = node.context?.span_id;
            return (
                <span key={spanId}>
                    <TreeItem
                        key={spanId}
                        itemType={Array.isArray(node.uiChildren) && node.uiChildren.length > 0 ? 'branch' : 'leaf'}
                        onClick={e => {
                            e.preventDefault();
                            onSpanIdSelect(spanId);
                        }}
                        value={spanId}
                    >
                        <TreeViewItem
                            isOpen={Boolean(spanId && openIds.includes(spanId))}
                            isSelected={Boolean(spanId && spanId === selectedSpanId)}
                            onToggleOpen={handleToggleOpen}
                            span={node}
                            thread={thread}
                        />
                        {Array.isArray(node.uiChildren) && node.uiChildren.length > 0 ? <Tree>{renderTree(node.uiChildren)}</Tree> : null}
                    </TreeItem>
                </span>
            );
        });

    return (
        <Tree aria-label={intl.formatMessage(ThreadTraceResources.tree)} className={styles.tree} openItems={openIds}>
            <div className={styles.pathLayerWrapper}>
                <div className={styles.pathLayer} data-tree-path-layer="path-layer" />
            </div>
            {renderTree(treeData.rootNodes)}
        </Tree>
    );
}

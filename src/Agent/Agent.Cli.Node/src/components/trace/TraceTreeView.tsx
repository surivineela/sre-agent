/**
 * Trace Tree View - Hierarchical display of spans
 * Left pane of the trace view showing span tree with connectors
 *
 * Optimized to minimize flickering:
 * - TreeNodeRow only re-renders when its specific selection state changes
 * - Uses stable references and careful memoization
 */
import React, { memo, useMemo, createContext, useContext } from 'react';
import { Box, Text } from 'ink';
import type { ISpanTreeNode } from '../../types/trace';
import {
  getSpanTitle,
  getSpanIcon,
  getSpanColor,
  getSpanDuration,
  formatDuration,
} from '../../types/trace';
import { theme } from '../../theme';

export interface TraceTreeViewProps {
  nodes: ISpanTreeNode[];
  selectedId?: string;
  expandedIds: Set<string>;
  onSelect: (id: string) => void;
  onToggleExpand: (id: string) => void;
  maxHeight?: number;
}

// Context to avoid prop drilling selectedId/expandedIds through every node
interface TreeContextValue {
  selectedId: string | undefined;
  expandedIds: Set<string>;
}
const TreeContext = createContext<TreeContextValue>({ selectedId: undefined, expandedIds: new Set() });

// Individual tree node row - heavily memoized
const TreeNodeRow = memo<{
  spanId: string;
  isSelected: boolean;
  isExpanded: boolean;
  hasChildren: boolean;
  duration: number | undefined;
  icon: string;
  color: string;
  title: string;
  status: string | undefined;
  prefix: string;
  isLast: boolean;
}>(({ spanId, isSelected, isExpanded, hasChildren, duration, icon, color, title, status, prefix, isLast }) => {
  // Tree connector characters
  const connector = isLast ? '└─' : '├─';

  // Status indicator
  const statusIndicator = status === 'running' ? '◐' :
    status === 'failed' ? '✗' :
    status === 'cancelled' ? '○' : '';

  return (
    <Box flexDirection="row">
      {/* Tree prefix */}
      <Text color="gray">{prefix}{connector}</Text>

      {/* Expand/collapse indicator */}
      {hasChildren ? (
        <Text color="gray">{isExpanded ? '▼' : '▶'} </Text>
      ) : (
        <Text>  </Text>
      )}

      {/* Selection indicator */}
      {isSelected && <Text color={theme.ink.brand}>▎</Text>}

      {/* Span icon */}
      <Text color={color}>{icon} </Text>

      {/* Title */}
      <Text
        color={isSelected ? 'white' : undefined}
        bold={isSelected}
        wrap="truncate"
      >
        {title}
      </Text>

      {/* Status */}
      {statusIndicator && (
        <Text color={status === 'running' ? 'yellow' : 'red'}> {statusIndicator}</Text>
      )}

      {/* Duration */}
      {duration !== undefined && (
        <Text color="gray"> ({formatDuration(duration)})</Text>
      )}
    </Box>
  );
});

TreeNodeRow.displayName = 'TreeNodeRow';

// Recursive tree node component - uses context for selection state
const TreeNode = memo<{
  node: ISpanTreeNode;
  isLast: boolean;
  prefix: string;
}>(({ node, isLast, prefix }) => {
  const { selectedId, expandedIds } = useContext(TreeContext);
  const spanId = node.context.span_id;
  const isSelected = spanId === selectedId;
  const isExpanded = expandedIds.has(spanId);
  const hasChildren = node.children.length > 0;
  const duration = getSpanDuration(node);
  const icon = getSpanIcon(node);
  const color = getSpanColor(node);
  const title = getSpanTitle(node);
  const childPrefix = prefix + (isLast ? '   ' : '│  ');

  return (
    <Box flexDirection="column">
      <TreeNodeRow
        spanId={spanId}
        isSelected={isSelected}
        isExpanded={isExpanded}
        hasChildren={hasChildren}
        duration={duration}
        icon={icon}
        color={color}
        title={title}
        status={node.status}
        prefix={prefix}
        isLast={isLast}
      />

      {/* Children */}
      {hasChildren && isExpanded && (
        <Box flexDirection="column">
          {node.children.map((child, index) => (
            <TreeNode
              key={child.context.span_id}
              node={child}
              isLast={index === node.children.length - 1}
              prefix={childPrefix}
            />
          ))}
        </Box>
      )}
    </Box>
  );
});

TreeNode.displayName = 'TreeNode';

// Main tree view component
export const TraceTreeView: React.FC<TraceTreeViewProps> = memo(({
  nodes,
  selectedId,
  expandedIds,
  onSelect,
  onToggleExpand,
  maxHeight,
}) => {
  // Memoize context value to prevent unnecessary re-renders
  const contextValue = useMemo(
    () => ({ selectedId, expandedIds }),
    [selectedId, expandedIds]
  );

  if (nodes.length === 0) {
    return (
      <Box padding={1}>
        <Text color="gray">No trace data available</Text>
      </Box>
    );
  }

  return (
    <TreeContext.Provider value={contextValue}>
      <Box
        flexDirection="column"
        paddingX={1}
        height={maxHeight}
        overflowY="hidden"
      >
        {/* Header */}
        <Box marginBottom={1}>
          <Text bold color={theme.ink.brand}>Trace Timeline</Text>
          <Text color="gray"> ({nodes.length} root spans)</Text>
        </Box>

        {/* Tree */}
        <Box flexDirection="column">
          {nodes.map((node, index) => (
            <TreeNode
              key={node.context.span_id}
              node={node}
              isLast={index === nodes.length - 1}
              prefix=""
            />
          ))}
        </Box>

        {/* Help hint */}
        <Box marginTop={1}>
          <Text color="gray" dimColor>
            ↑/↓ navigate • Enter select • Space expand • q close
          </Text>
        </Box>
      </Box>
    </TreeContext.Provider>
  );
});

TraceTreeView.displayName = 'TraceTreeView';

export default TraceTreeView;

/**
 * Trace View - Full-screen trace viewer
 * Two-pane layout: tree view (left) + detail panel (right)
 *
 * Optimized to minimize flickering by:
 * - Using refs for navigation state to avoid re-renders
 * - Batching updates with debounce (16ms debounce, 50ms max wait)
 * - Using startTransition for non-urgent updates
 */
import React, { useState, useCallback, useMemo, useEffect, useRef, memo, startTransition } from 'react';
import { Box, Text, useInput, useStdout } from 'ink';
import { spawn } from 'child_process';
import { writeFileSync } from 'fs';
import { tmpdir } from 'os';
import { join } from 'path';
import type { ISpan, ISpanTreeNode } from '../../types/trace';
import { buildSpanTree } from '../../types/trace';
import { TraceTreeView } from './TraceTreeView';
import { TraceDetailPanel } from './TraceDetailPanel';
import { theme, BABY_PINK, BABY_BLUE } from '../../theme';
import { getTraceService, initTraceService } from '../../services/traceService';
import { createDebounce } from '../../utils/debounce';

export interface TraceViewProps {
  spans?: ISpan[];
  onClose: () => void;
  threadId?: string;
  agentName?: string;
  serverUrl?: string;
  appInsightsAppId?: string;
}

// Memoized header component
const TraceHeader = memo<{
  agentName?: string;
  spanCount: number;
  threadId?: string;
  warningCount: number;
  errorCount: number;
}>(({ agentName, spanCount, threadId, warningCount, errorCount }) => (
  <Box
    borderStyle="round"
    borderColor={theme.ink.brand}
    paddingX={1}
    justifyContent="space-between"
  >
    <Box>
      <Text color={BABY_PINK} bold>Trace View</Text>
      {agentName && (
        <>
          <Text color="gray"> • </Text>
          <Text color="cyan">{agentName}</Text>
        </>
      )}
    </Box>
    <Box>
      <Text color="gray">{spanCount} spans</Text>
      {threadId && (
        <>
          <Text color="gray"> • Thread: </Text>
          <Text dimColor>{threadId.slice(0, 8)}...</Text>
        </>
      )}
      {warningCount > 0 && (
        <>
          <Text color="gray"> • </Text>
          <Text color="yellow">{warningCount} warnings</Text>
        </>
      )}
      {errorCount > 0 && (
        <>
          <Text color="gray"> • </Text>
          <Text color="red">{errorCount} errors</Text>
        </>
      )}
    </Box>
  </Box>
));

TraceHeader.displayName = 'TraceHeader';

// Memoized footer component
const TraceFooter = memo(() => (
  <Box paddingX={1} justifyContent="space-between">
    <Box>
      <Text color="gray">
        <Text color={BABY_BLUE}>↑↓</Text> navigate
        <Text color="gray"> • </Text>
        <Text color={BABY_BLUE}>←→</Text> expand/pane
        <Text color="gray"> • </Text>
        <Text color={BABY_BLUE}>Space</Text> toggle
        <Text color="gray"> • </Text>
        <Text color={BABY_BLUE}>e</Text>/<Text color={BABY_BLUE}>c</Text> all
        <Text color="gray"> • </Text>
        <Text color={BABY_BLUE}>r</Text> raw/vim
      </Text>
    </Box>
    <Box>
      <Text color="gray">
        <Text color={BABY_BLUE}>q</Text>/<Text color={BABY_BLUE}>Esc</Text> close
      </Text>
    </Box>
  </Box>
));

TraceFooter.displayName = 'TraceFooter';

export const TraceView: React.FC<TraceViewProps> = memo(({
  spans: initialSpans,
  onClose,
  threadId,
  agentName,
  serverUrl,
  appInsightsAppId,
}) => {
  const { stdout } = useStdout();

  // Get terminal dimensions once on mount, use state to prevent flickering
  const [dimensions] = useState(() => ({
    height: stdout?.rows || 24,
    width: stdout?.columns || 80,
  }));

  const terminalHeight = dimensions.height;
  const terminalWidth = dimensions.width;

  // Track if we've entered alternate screen (ref to avoid re-entry)
  const enteredAltScreenRef = useRef(false);

  // Enter alternate screen buffer SYNCHRONOUSLY on first render
  // This must happen before any React rendering to prevent flicker
  if (!enteredAltScreenRef.current) {
    enteredAltScreenRef.current = true;
    // Enter alternate screen buffer + clear + hide cursor
    process.stdout.write('\x1b[?1049h\x1b[2J\x1b[H\x1b[?25l');
  }

  // Cleanup: leave alternate screen buffer on unmount
  useEffect(() => {
    return () => {
      // Show cursor + leave alternate screen buffer + clear main screen to refresh
      process.stdout.write('\x1b[?25h\x1b[?1049l\x1b[2J\x1b[H');
    };
  }, []);

  // Use ref to track if we've fetched data
  const hasFetchedRef = useRef(false);

  // Data state (triggers re-render only when data changes)
  const [spans, setSpans] = useState<ISpan[]>(initialSpans || []);
  const [isLoading, setIsLoading] = useState(!initialSpans?.length && !!threadId);
  const [error, setError] = useState<string | undefined>();
  const [parseWarnings, setParseWarnings] = useState<string[]>([]);
  const [parseErrors, setParseErrors] = useState<string[]>([]);

  // Navigation state - use a single state object to batch updates
  const [navState, setNavState] = useState<{
    selectedId: string | undefined;
    expandedIds: Set<string>;
    focusPane: 'tree' | 'detail';
  }>({
    selectedId: undefined,
    expandedIds: new Set(),
    focusPane: 'tree',
  });

  // Debounced navigation state update to prevent flickering during rapid key presses
  // 50ms debounce to batch rapid keypresses, 100ms max wait for responsiveness
  const [debouncedSetNavState, cleanupDebounce] = useMemo(
    () => createDebounce(
      (updater: (prev: typeof navState) => typeof navState) => {
        startTransition(() => {
          setNavState(updater);
        });
      },
      50,   // debounce: batch updates within 50ms
      100   // max wait: ensure update happens within 100ms
    ),
    []
  );

  // Cleanup debounce on unmount
  useEffect(() => {
    return () => cleanupDebounce();
  }, [cleanupDebounce]);

  // Fetch trace data when component mounts
  useEffect(() => {
    if (hasFetchedRef.current || !threadId || initialSpans?.length) {
      return;
    }
    hasFetchedRef.current = true;

    const fetchData = async () => {
      setIsLoading(true);
      setError(undefined);

      try {
        // Initialize trace service if server URL is provided
        if (serverUrl) {
          initTraceService(serverUrl, appInsightsAppId);
        }

        const traceService = getTraceService();
        const result = await traceService.fetchTraces(threadId);

        if (result.error) {
          setError(result.error);
        } else {
          setSpans(result.spans);
          setParseWarnings(result.parseWarnings);
          setParseErrors(result.parseErrors);

          // Set initial selection and expansion
          if (result.spans.length > 0) {
            const roots = new Set<string>();
            for (const span of result.spans) {
              if (!span.parent_id) {
                roots.add(span.context.span_id);
              }
            }
            setNavState({
              selectedId: result.spans[0].context.span_id,
              expandedIds: roots,
              focusPane: 'tree',
            });
          }
        }
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err));
      } finally {
        setIsLoading(false);
      }
    };

    fetchData();
  }, [threadId, serverUrl, appInsightsAppId, initialSpans]);

  // Initialize selection and expansion when spans change
  useEffect(() => {
    if (spans.length > 0 && !navState.selectedId) {
      const roots = new Set<string>();
      for (const span of spans) {
        if (!span.parent_id) {
          roots.add(span.context.span_id);
        }
      }
      setNavState(prev => ({
        ...prev,
        selectedId: spans[0].context.span_id,
        expandedIds: roots,
      }));
    }
  }, [spans, navState.selectedId]);

  // Build tree structure - memoized
  const treeNodes = useMemo(() => buildSpanTree(spans), [spans]);

  // Flatten tree for navigation - memoized
  const flattenedNodes = useMemo(() => {
    const result: ISpanTreeNode[] = [];
    const flatten = (nodes: ISpanTreeNode[]) => {
      for (const node of nodes) {
        result.push(node);
        if (navState.expandedIds.has(node.context.span_id) && node.children.length > 0) {
          flatten(node.children);
        }
      }
    };
    flatten(treeNodes);
    return result;
  }, [treeNodes, navState.expandedIds]);

  // Get selected span - memoized
  const selectedSpan = useMemo(
    () => spans.find(s => s.context.span_id === navState.selectedId) || null,
    [spans, navState.selectedId]
  );

  // Keyboard handling with debounced state updates to prevent flickering
  useInput(useCallback((input: string, key: { upArrow?: boolean; downArrow?: boolean; leftArrow?: boolean; rightArrow?: boolean; escape?: boolean; return?: boolean; tab?: boolean }) => {
    // Close on q or Escape
    if (input === 'q' || key.escape) {
      onClose();
      return;
    }

    // Navigation - update only selectedId, debounced
    if (key.upArrow) {
      debouncedSetNavState(prev => {
        const currentIndex = flattenedNodes.findIndex(n => n.context.span_id === prev.selectedId);
        if (currentIndex > 0) {
          return { ...prev, selectedId: flattenedNodes[currentIndex - 1].context.span_id };
        }
        return prev;
      });
      return;
    }

    if (key.downArrow) {
      debouncedSetNavState(prev => {
        const currentIndex = flattenedNodes.findIndex(n => n.context.span_id === prev.selectedId);
        if (currentIndex < flattenedNodes.length - 1) {
          return { ...prev, selectedId: flattenedNodes[currentIndex + 1].context.span_id };
        } else if (currentIndex === -1 && flattenedNodes.length > 0) {
          return { ...prev, selectedId: flattenedNodes[0].context.span_id };
        }
        return prev;
      });
      return;
    }

    // Left arrow: collapse node in tree, or switch to tree pane from detail
    if (key.leftArrow) {
      debouncedSetNavState(prev => {
        if (prev.focusPane === 'detail') {
          // Switch to tree pane
          return { ...prev, focusPane: 'tree' };
        }
        // In tree pane: collapse current node if expanded
        if (prev.selectedId && prev.expandedIds.has(prev.selectedId)) {
          const next = new Set(prev.expandedIds);
          next.delete(prev.selectedId);
          return { ...prev, expandedIds: next };
        }
        return prev;
      });
      return;
    }

    // Right arrow: expand node in tree, or switch to detail pane from tree
    if (key.rightArrow) {
      debouncedSetNavState(prev => {
        if (prev.focusPane === 'tree') {
          // Check if current node has children and is not expanded
          const currentNode = flattenedNodes.find(n => n.context.span_id === prev.selectedId);
          if (currentNode && currentNode.children.length > 0 && !prev.expandedIds.has(prev.selectedId!)) {
            // Expand the node
            const next = new Set(prev.expandedIds);
            next.add(prev.selectedId!);
            return { ...prev, expandedIds: next };
          }
          // Switch to detail pane
          return { ...prev, focusPane: 'detail' };
        }
        return prev;
      });
      return;
    }

    // Toggle expand on Space or Enter
    if ((input === ' ' || key.return) && navState.selectedId) {
      debouncedSetNavState(prev => {
        const next = new Set(prev.expandedIds);
        if (next.has(prev.selectedId!)) {
          next.delete(prev.selectedId!);
        } else {
          next.add(prev.selectedId!);
        }
        return { ...prev, expandedIds: next };
      });
      return;
    }

    // Tab to switch panes
    if (key.tab) {
      debouncedSetNavState(prev => ({
        ...prev,
        focusPane: prev.focusPane === 'tree' ? 'detail' : 'tree',
      }));
      return;
    }

    // Expand all
    if (input === 'e') {
      debouncedSetNavState(prev => ({
        ...prev,
        expandedIds: new Set(spans.map(s => s.context.span_id)),
      }));
      return;
    }

    // Collapse all
    if (input === 'c') {
      const roots = new Set<string>();
      for (const span of spans) {
        if (!span.parent_id) {
          roots.add(span.context.span_id);
        }
      }
      debouncedSetNavState(prev => ({ ...prev, expandedIds: roots }));
      return;
    }

    // Jump to first
    if (input === 'g') {
      if (flattenedNodes.length > 0) {
        debouncedSetNavState(prev => ({ ...prev, selectedId: flattenedNodes[0].context.span_id }));
      }
      return;
    }

    // Jump to last
    if (input === 'G') {
      if (flattenedNodes.length > 0) {
        debouncedSetNavState(prev => ({ ...prev, selectedId: flattenedNodes[flattenedNodes.length - 1].context.span_id }));
      }
      return;
    }

    // Raw trace - export to file and open in vim
    if (input === 'r') {
      if (spans.length > 0) {
        try {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const filename = `trace-${threadId?.slice(0, 8) || 'unknown'}-${timestamp}.json`;
          const filepath = join(tmpdir(), filename);
          const traceData = JSON.stringify(spans, null, 2);
          writeFileSync(filepath, traceData, 'utf-8');

          // Open in vim (spawn detached so it doesn't block)
          const vim = spawn('vim', [filepath], {
            stdio: 'inherit',
            detached: false,
          });

          vim.on('close', () => {
            // Restore screen after vim exits
            process.stdout.write('\x1b[2J\x1b[H');
          });
        } catch (err) {
          // Silently fail - vim might not be available
        }
      }
      return;
    }
  }, [flattenedNodes, navState.selectedId, onClose, spans, threadId]));

  // Calculate pane dimensions (memoized)
  const { contentHeight, leftPaneWidth, rightPaneWidth } = useMemo(() => ({
    contentHeight: terminalHeight - 4,
    leftPaneWidth: Math.floor(terminalWidth * 0.4),
    rightPaneWidth: terminalWidth - Math.floor(terminalWidth * 0.4) - 3,
  }), [terminalHeight, terminalWidth]);

  // Stable callbacks for child components - MUST be defined before early returns
  const handleSelect = useCallback((id: string) => {
    setNavState(prev => ({ ...prev, selectedId: id }));
  }, []);

  const handleToggleExpand = useCallback((id: string) => {
    setNavState(prev => {
      const next = new Set(prev.expandedIds);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return { ...prev, expandedIds: next };
    });
  }, []);

  // Show loading state
  if (isLoading) {
    return (
      <Box flexDirection="column" height={terminalHeight} alignItems="center" justifyContent="center">
        <Text color={BABY_PINK}>Loading trace data...</Text>
        {threadId && <Text color="gray">Thread: {threadId}</Text>}
      </Box>
    );
  }

  // Show error state
  if (error) {
    return (
      <Box flexDirection="column" height={terminalHeight} padding={2}>
        <Text color="red" bold>Error loading traces</Text>
        <Text color="red">{error}</Text>
        <Box marginTop={1}>
          <Text color="gray">Press </Text>
          <Text color={BABY_BLUE}>q</Text>
          <Text color="gray"> to close</Text>
        </Box>
      </Box>
    );
  }

  return (
    <Box flexDirection="column" height={terminalHeight}>
      {/* Header */}
      <TraceHeader
        agentName={agentName}
        spanCount={spans.length}
        threadId={threadId}
        warningCount={parseWarnings.length}
        errorCount={parseErrors.length}
      />

      {/* Main content - two panes */}
      <Box flexDirection="row" height={contentHeight}>
        {/* Left pane - Tree */}
        <Box
          flexDirection="column"
          width={leftPaneWidth}
          borderStyle="single"
          borderColor={navState.focusPane === 'tree' ? BABY_BLUE : 'gray'}
        >
          <TraceTreeView
            nodes={treeNodes}
            selectedId={navState.selectedId}
            expandedIds={navState.expandedIds}
            onSelect={handleSelect}
            onToggleExpand={handleToggleExpand}
            maxHeight={contentHeight - 2}
          />
        </Box>

        {/* Separator */}
        <Box flexDirection="column" width={1}>
          <Text color="gray">│</Text>
        </Box>

        {/* Right pane - Details */}
        <Box
          flexDirection="column"
          width={rightPaneWidth}
          borderStyle="single"
          borderColor={navState.focusPane === 'detail' ? BABY_BLUE : 'gray'}
        >
          <TraceDetailPanel
            span={selectedSpan}
            maxHeight={contentHeight - 2}
            isFocused={navState.focusPane === 'detail'}
          />
        </Box>
      </Box>

      {/* Footer */}
      <TraceFooter />
    </Box>
  );
});

TraceView.displayName = 'TraceView';

export default TraceView;

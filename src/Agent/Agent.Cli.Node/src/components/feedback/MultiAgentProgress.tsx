/**
 * MultiAgentProgress - Tree-style collapsible progress display
 *
 * Used for:
 * - Running parallel agents display
 * - Reasoning/thinking traces
 * - Tool execution trees
 *
 * Example output:
 * ● Running 3 Explore agents... (ctrl+o to expand)
 * ├─ Explore Agent.Cli.Node structure · 5 tool uses · 19.9k tokens
 * │  L Initializing...
 * ├─ Explore Agent.Cli C# implementation · 0 tool uses · 19.9k tokens
 * │  L Initializing...
 * └─ Explore Agent.Web chat implementation · 0 tool uses · 19.8k tokens
 *    L Initializing...
 *    ctrl+b to run in background
 */
import React, { useState, useEffect } from 'react';
import { Box, Text, useInput } from 'ink';
import { theme } from '../../theme';

export type TaskStatus = 'pending' | 'running' | 'completed' | 'failed';

export interface AgentTask {
  id: string;
  name: string;
  status: TaskStatus;
  statusText?: string;
  toolUses?: number;
  tokens?: number;
  children?: AgentTask[];
  error?: string;
}

export interface MultiAgentProgressProps {
  /** Title for the progress group */
  title: string;
  /** List of tasks/agents */
  tasks: AgentTask[];
  /** Whether expanded by default */
  defaultExpanded?: boolean;
  /** Callback when expand toggled */
  onToggleExpand?: (expanded: boolean) => void;
  /** Callback for background action */
  onRunInBackground?: () => void;
  /** Show keyboard hints */
  showHints?: boolean;
  /** Custom expand shortcut hint */
  expandHint?: string;
  /** Custom background shortcut hint */
  backgroundHint?: string;
  /** Whether to show spinner animation */
  showSpinner?: boolean;
}

// Braille spinner - smooth 8-frame rotation
const SPINNER_FRAMES = ['⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷'];

// Tree characters
const TREE = {
  vertical: '│',
  branch: '├─',
  lastBranch: '└─',
  indent: '   ',
  subIndent: '│  ',
  lastSubIndent: '   ',
  statusPrefix: 'L',
};

/**
 * Format token count (19900 -> 19.9k)
 */
function formatTokens(tokens: number): string {
  if (tokens >= 1000) {
    return `${(tokens / 1000).toFixed(1)}k`;
  }
  return String(tokens);
}

/**
 * Get status color based on task status
 */
function getStatusColor(status: TaskStatus): string {
  switch (status) {
    case 'running':
      return theme.ink.brand;
    case 'completed':
      return theme.ink.success;
    case 'failed':
      return theme.ink.error;
    default:
      return theme.ink.muted;
  }
}

/**
 * Single task item in the tree
 */
const TaskItem: React.FC<{
  task: AgentTask;
  isLast: boolean;
  depth: number;
  parentIsLast?: boolean[];
  spinnerFrame: number;
}> = ({ task, isLast, depth, parentIsLast = [], spinnerFrame }) => {
  const statusColor = getStatusColor(task.status);

  // Build indent based on depth and parent positions
  let indent = '';
  for (let i = 0; i < depth; i++) {
    indent += parentIsLast[i] ? TREE.lastSubIndent : TREE.subIndent;
  }

  const branch = isLast ? TREE.lastBranch : TREE.branch;

  return (
    <Box flexDirection="column">
      {/* Main task line */}
      <Box>
        <Text color="gray">{indent}</Text>
        <Text color="gray">{branch} </Text>

        {/* Task name */}
        <Text color={statusColor}>{task.name}</Text>

        {/* Stats */}
        {task.toolUses !== undefined && (
          <Text color="gray"> · {task.toolUses} tool uses</Text>
        )}
        {task.tokens !== undefined && (
          <Text color="gray"> · {formatTokens(task.tokens)} tokens</Text>
        )}
      </Box>

      {/* Status line */}
      {task.statusText && (
        <Box>
          <Text color="gray">{indent}</Text>
          <Text color="gray">
            {isLast ? TREE.lastSubIndent : TREE.subIndent}
          </Text>
          <Text color="gray">{TREE.statusPrefix} </Text>
          {task.status === 'running' && (
            <Text color={theme.ink.brand}>{SPINNER_FRAMES[spinnerFrame]} </Text>
          )}
          <Text color={statusColor}>{task.statusText}</Text>
        </Box>
      )}

      {/* Error message */}
      {task.error && (
        <Box>
          <Text color="gray">{indent}</Text>
          <Text color="gray">
            {isLast ? TREE.lastSubIndent : TREE.subIndent}
          </Text>
          <Text color={theme.ink.error}>Error: {task.error}</Text>
        </Box>
      )}

      {/* Children */}
      {task.children?.map((child, index) => (
        <TaskItem
          key={child.id}
          task={child}
          isLast={index === task.children!.length - 1}
          depth={depth + 1}
          parentIsLast={[...parentIsLast, isLast]}
          spinnerFrame={spinnerFrame}
        />
      ))}
    </Box>
  );
};

export const MultiAgentProgress: React.FC<MultiAgentProgressProps> = ({
  title,
  tasks,
  defaultExpanded = true,
  onToggleExpand,
  onRunInBackground,
  showHints = true,
  expandHint = 'ctrl+o to expand',
  backgroundHint = 'ctrl+b to run in background',
  showSpinner = true,
}) => {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [spinnerFrame, setSpinnerFrame] = useState(0);

  // Count running tasks
  const runningCount = tasks.filter((t) => t.status === 'running').length;
  const totalCount = tasks.length;
  const hasRunning = runningCount > 0;

  // Spinner animation
  useEffect(() => {
    if (!showSpinner || !hasRunning) return;

    const timer = setInterval(() => {
      setSpinnerFrame((prev) => (prev + 1) % SPINNER_FRAMES.length);
    }, 80);

    return () => clearInterval(timer);
  }, [showSpinner, hasRunning]);

  // Keyboard shortcuts
  useInput((input, key) => {
    // Toggle expand with Ctrl+O
    if (key.ctrl && input === 'o') {
      const newExpanded = !expanded;
      setExpanded(newExpanded);
      onToggleExpand?.(newExpanded);
      return;
    }

    // Run in background with Ctrl+B
    if (key.ctrl && input === 'b' && onRunInBackground) {
      onRunInBackground();
      return;
    }
  });

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Header line */}
      <Box>
        {/* Status bullet */}
        <Text color={hasRunning ? theme.ink.brand : theme.ink.success}>
          {hasRunning ? SPINNER_FRAMES[spinnerFrame] : '●'}
        </Text>
        <Text> </Text>

        {/* Title with count */}
        <Text bold>{title}</Text>
        {!expanded && (
          <Text color="gray">
            {' '}
            ({expandHint})
          </Text>
        )}
      </Box>

      {/* Expanded task list */}
      {expanded && (
        <Box flexDirection="column">
          {tasks.map((task, index) => (
            <TaskItem
              key={task.id}
              task={task}
              isLast={index === tasks.length - 1}
              depth={0}
              spinnerFrame={spinnerFrame}
            />
          ))}
        </Box>
      )}

      {/* Hints */}
      {showHints && expanded && onRunInBackground && (
        <Box marginTop={0}>
          <Text color="gray" dimColor>
            {TREE.lastSubIndent}
            {backgroundHint}
          </Text>
        </Box>
      )}
    </Box>
  );
};

/**
 * Reasoning trace display - uses same tree structure
 */
export interface ReasoningStep {
  id: string;
  title: string;
  content?: string;
  status: TaskStatus;
  duration?: number;
  children?: ReasoningStep[];
}

export interface ReasoningTraceProps {
  title?: string;
  steps: ReasoningStep[];
  defaultExpanded?: boolean;
  showDuration?: boolean;
}

export const ReasoningTrace: React.FC<ReasoningTraceProps> = ({
  title = 'Reasoning',
  steps,
  defaultExpanded = false,
  showDuration = true,
}) => {
  const [expanded, setExpanded] = useState(defaultExpanded);
  const [spinnerFrame, setSpinnerFrame] = useState(0);

  const hasRunning = steps.some((s) => s.status === 'running');

  useEffect(() => {
    if (!hasRunning) return;
    const timer = setInterval(() => {
      setSpinnerFrame((prev) => (prev + 1) % SPINNER_FRAMES.length);
    }, 80);
    return () => clearInterval(timer);
  }, [hasRunning]);

  useInput((input, key) => {
    if (key.ctrl && input === 'r') {
      setExpanded(!expanded);
    }
  });

  const renderStep = (
    step: ReasoningStep,
    isLast: boolean,
    depth: number,
    parentIsLast: boolean[] = []
  ): React.ReactNode => {
    const statusColor = getStatusColor(step.status);

    let indent = '';
    for (let i = 0; i < depth; i++) {
      indent += parentIsLast[i] ? TREE.lastSubIndent : TREE.subIndent;
    }

    const branch = isLast ? TREE.lastBranch : TREE.branch;

    return (
      <Box key={step.id} flexDirection="column">
        <Box>
          <Text color="gray">{indent}</Text>
          <Text color="gray">{branch} </Text>
          {step.status === 'running' && (
            <Text color={theme.ink.brand}>{SPINNER_FRAMES[spinnerFrame]} </Text>
          )}
          {step.status === 'completed' && (
            <Text color={theme.ink.success}>✓ </Text>
          )}
          {step.status === 'failed' && (
            <Text color={theme.ink.error}>✗ </Text>
          )}
          <Text color={statusColor}>{step.title}</Text>
          {showDuration && step.duration && (
            <Text color="gray"> ({step.duration}ms)</Text>
          )}
        </Box>

        {step.content && expanded && (
          <Box>
            <Text color="gray">{indent}</Text>
            <Text color="gray">
              {isLast ? TREE.lastSubIndent : TREE.subIndent}
            </Text>
            <Text color="gray" dimColor>
              {step.content}
            </Text>
          </Box>
        )}

        {step.children?.map((child, index) =>
          renderStep(
            child,
            index === step.children!.length - 1,
            depth + 1,
            [...parentIsLast, isLast]
          )
        )}
      </Box>
    );
  };

  return (
    <Box flexDirection="column" marginY={1}>
      <Box>
        <Text color={theme.ink.muted}>💭</Text>
        <Text> </Text>
        <Text color={theme.ink.muted} bold>
          {title}
        </Text>
        <Text color="gray" dimColor>
          {' '}
          (ctrl+r to {expanded ? 'collapse' : 'expand'})
        </Text>
      </Box>

      {expanded && (
        <Box flexDirection="column">
          {steps.map((step, index) =>
            renderStep(step, index === steps.length - 1, 0)
          )}
        </Box>
      )}
    </Box>
  );
};

export default MultiAgentProgress;

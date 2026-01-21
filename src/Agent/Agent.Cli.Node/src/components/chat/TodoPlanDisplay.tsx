/**
 * Todo/Plan display - CLI version
 * Shows task list with status indicators
 * Ported from Agent.Web\Client TodoPlanChatMessage.tsx
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { theme } from '../../theme';

export type TodoStatus = 'pending' | 'in_progress' | 'completed' | 'failed' | 'skipped';

export interface TodoItem {
  id: string;
  content: string;
  status: TodoStatus;
  activeForm?: string;
}

export interface TodoPlan {
  id: string;
  title: string;
  items: TodoItem[];
}

export interface TodoPlanDisplayProps {
  plan: TodoPlan;
  showDetails?: boolean;
  compact?: boolean;
}

// Spinner frames for in_progress
const SPINNER_FRAMES = ['◐', '◓', '◑', '◒'];

// Get status config
const getStatusConfig = (status: TodoStatus) => {
  switch (status) {
    case 'pending':
      return { bullet: '○', color: theme.ink.muted };
    case 'in_progress':
      return { bullet: '●', color: theme.ink.warning };
    case 'completed':
      return { bullet: '●', color: theme.ink.success };
    case 'failed':
      return { bullet: '●', color: theme.ink.error };
    case 'skipped':
      return { bullet: '○', color: theme.ink.muted };
    default:
      return { bullet: '○', color: theme.ink.muted };
  }
};

/**
 * Single todo item display
 */
const TodoItemRow: React.FC<{
  item: TodoItem;
  index: number;
  spinnerFrame: number;
}> = ({ item, index, spinnerFrame }) => {
  const config = getStatusConfig(item.status);
  const isActive = item.status === 'in_progress';
  const bullet = isActive ? SPINNER_FRAMES[spinnerFrame] : config.bullet;

  // Display text based on status
  const displayText = isActive && item.activeForm
    ? item.activeForm
    : item.content;

  return (
    <Box>
      <Text color={theme.ink.muted}>{String(index + 1).padStart(2, ' ')}. </Text>
      <Text color={config.color}>{bullet}</Text>
      <Text> </Text>
      <Text
        color={item.status === 'completed' ? theme.ink.muted : theme.ink.text}
        strikethrough={item.status === 'skipped'}
        bold={isActive}
      >
        {displayText}
      </Text>
    </Box>
  );
};

export const TodoPlanDisplay: React.FC<TodoPlanDisplayProps> = ({
  plan,
  showDetails = true,
  compact = false,
}) => {
  const [spinnerFrame, setSpinnerFrame] = useState(0);

  // Check if any item is in progress
  const hasActiveItem = plan.items.some(item => item.status === 'in_progress');

  // Spinner animation
  useEffect(() => {
    if (!hasActiveItem) return;
    const timer = setInterval(() => {
      setSpinnerFrame((f) => (f + 1) % SPINNER_FRAMES.length);
    }, 100);
    return () => clearInterval(timer);
  }, [hasActiveItem]);

  // Count stats
  const completed = plan.items.filter(i => i.status === 'completed').length;
  const total = plan.items.length;
  const progress = total > 0 ? Math.round((completed / total) * 100) : 0;

  if (compact) {
    return (
      <Box>
        <Text color={theme.ink.info}>📋</Text>
        <Text> </Text>
        <Text bold>{plan.title}</Text>
        <Text color={theme.ink.muted}> ({completed}/{total})</Text>
        {hasActiveItem && (
          <>
            <Text> </Text>
            <Text color={theme.ink.warning}>{SPINNER_FRAMES[spinnerFrame]}</Text>
          </>
        )}
      </Box>
    );
  }

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Header */}
      <Box>
        <Text color={theme.ink.muted}>┌─ </Text>
        <Text color={theme.ink.info}>📋</Text>
        <Text> </Text>
        <Text bold>{plan.title}</Text>
      </Box>

      {/* Progress bar */}
      <Box marginLeft={3}>
        <Text color={theme.ink.muted}>│ </Text>
        <Text color={theme.ink.muted}>[</Text>
        <Text color={theme.ink.success}>{'█'.repeat(Math.floor(progress / 10))}</Text>
        <Text color={theme.ink.muted}>{'░'.repeat(10 - Math.floor(progress / 10))}</Text>
        <Text color={theme.ink.muted}>]</Text>
        <Text color={theme.ink.muted}> {progress}%</Text>
        <Text color={theme.ink.muted}> ({completed}/{total})</Text>
      </Box>

      {/* Items */}
      {showDetails && (
        <Box flexDirection="column" marginLeft={3}>
          {plan.items.map((item, index) => (
            <Box key={item.id}>
              <Text color={theme.ink.muted}>│ </Text>
              <TodoItemRow item={item} index={index} spinnerFrame={spinnerFrame} />
            </Box>
          ))}
        </Box>
      )}

      {/* Footer */}
      <Box marginLeft={3}>
        <Text color={theme.ink.muted}>└─</Text>
      </Box>
    </Box>
  );
};

/**
 * Compact todo summary for status bar
 */
export const TodoProgressBadge: React.FC<{
  completed: number;
  total: number;
  hasActive?: boolean;
}> = ({ completed, total, hasActive = false }) => {
  const [frame, setFrame] = useState(0);

  useEffect(() => {
    if (!hasActive) return;
    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % SPINNER_FRAMES.length);
    }, 100);
    return () => clearInterval(timer);
  }, [hasActive]);

  const progress = total > 0 ? Math.round((completed / total) * 100) : 0;
  const color = progress === 100 ? theme.ink.success : theme.ink.info;

  return (
    <Box>
      {hasActive ? (
        <Text color={theme.ink.warning}>{SPINNER_FRAMES[frame]}</Text>
      ) : (
        <Text color={color}>●</Text>
      )}
      <Text> </Text>
      <Text color={theme.ink.muted}>Tasks: </Text>
      <Text color={color}>{completed}/{total}</Text>
    </Box>
  );
};

export default TodoPlanDisplay;

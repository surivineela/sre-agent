/**
 * Tool invocation display component - refined visual feedback
 * Clear status indicators with smooth animations
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import type { ToolCall, ToolResult } from '../../types';
import { theme, BABY_PINK, BABY_BLUE } from '../../theme';

export type ToolStatus = 'pending' | 'running' | 'complete' | 'error' | 'denied';

export interface ToolInvocationProps {
  tool: ToolCall;
  status: ToolStatus;
  result?: ToolResult;
  showInput?: boolean;
  expandedByDefault?: boolean;
  compact?: boolean;
}

// Smoother spinner animation
const spinnerFrames = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];

const getStatusConfig = (status: ToolStatus) => {
  switch (status) {
    case 'pending':
      return { bullet: '○', color: 'gray', label: 'Pending' };
    case 'running':
      return { bullet: '●', color: 'yellow', label: 'Running' };
    case 'complete':
      return { bullet: '●', color: 'green', label: 'Done' };
    case 'error':
      return { bullet: '●', color: 'red', label: 'Error' };
    case 'denied':
      return { bullet: '●', color: 'yellow', label: 'Denied' };
  }
};

// Format tool input for display
const formatInput = (input: Record<string, unknown>): string => {
  const entries = Object.entries(input);
  if (entries.length === 0) return '';

  // For single string args, just show the value
  if (entries.length === 1) {
    const [key, value] = entries[0];
    if (typeof value === 'string') {
      const display = value.length > 60 ? value.slice(0, 60) + '...' : value;
      return key === 'command' || key === 'path' || key === 'query'
        ? display
        : `${key}: ${display}`;
    }
  }

  // For multiple args, show key=value pairs
  return entries
    .map(([k, v]) => {
      const val = typeof v === 'string'
        ? (v.length > 30 ? v.slice(0, 30) + '...' : v)
        : JSON.stringify(v);
      return `${k}=${val}`;
    })
    .join(', ');
};

// Format duration
const formatDuration = (ms: number): string => {
  if (ms < 1000) return `${ms}ms`;
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}m ${secs}s`;
};

// Format output for display
const formatOutput = (output: unknown, maxLength = 200): string => {
  if (output === null || output === undefined) return '';

  let str: string;
  if (typeof output === 'string') {
    str = output;
  } else {
    str = JSON.stringify(output, null, 2);
  }

  return str.length > maxLength ? str.slice(0, maxLength) + '...' : str;
};

export const ToolInvocation: React.FC<ToolInvocationProps> = ({
  tool,
  status,
  result,
  showInput = true,
  expandedByDefault = false,
  compact = true,
}) => {
  const [frame, setFrame] = useState(0);
  const [expanded] = useState(expandedByDefault);
  const config = getStatusConfig(status);

  // Spinner animation (faster for smoother feel)
  useEffect(() => {
    if (status !== 'running') return;
    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % spinnerFrames.length);
    }, 80);
    return () => clearInterval(timer);
  }, [status]);

  const bullet = status === 'running' ? spinnerFrames[frame] : config.bullet;
  const inputStr = showInput ? formatInput(tool.input) : '';

  return (
    <Box flexDirection="column" marginTop={compact ? 0 : 1}>
      {/* Main tool line */}
      <Box>
        <Text color={config.color}>{bullet}</Text>
        <Text> </Text>
        <Text bold color={BABY_BLUE}>{tool.name}</Text>
        {inputStr && (
          <>
            <Text color="gray"> </Text>
            <Text color="gray" dimColor>{inputStr}</Text>
          </>
        )}
        {result?.duration && (
          <Text color="gray" dimColor> · {formatDuration(result.duration)}</Text>
        )}
      </Box>

      {/* Sub-detail line - only show for errors or when expanded */}
      {status === 'error' && result?.error && (
        <Box marginLeft={2}>
          <Text color="gray">⎿ </Text>
          <Text color="red">{result.error}</Text>
        </Box>
      )}
      {status === 'denied' && (
        <Box marginLeft={2}>
          <Text color="gray">⎿ </Text>
          <Text color="yellow">Permission denied</Text>
        </Box>
      )}
    </Box>
  );
};

/**
 * Compact tool invocation for inline display
 */
export const ToolInvocationCompact: React.FC<{
  toolName: string;
  status: ToolStatus;
  duration?: number;
}> = ({ toolName, status, duration }) => {
  const config = getStatusConfig(status);

  return (
    <Box>
      <Text color={config.color}>{config.bullet}</Text>
      <Text> </Text>
      <Text bold color={BABY_BLUE}>{toolName}</Text>
      {duration && (
        <Text color="gray" dimColor> · {formatDuration(duration)}</Text>
      )}
    </Box>
  );
};

export default ToolInvocation;

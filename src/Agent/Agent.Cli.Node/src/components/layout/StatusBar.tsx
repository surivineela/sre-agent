/**
 * Status bar component - Claude Code inspired
 * Shows permissions, current activity, keyboard shortcuts
 */
import React from 'react';
import { Box, Text } from 'ink';
import type { ConnectionStatus, LoopStatus } from '../../types';
import { theme } from '../../theme';

export interface StatusBarProps {
  isProcessing?: boolean;
  loopStatus?: LoopStatus;
  connectionStatus?: ConnectionStatus;
  permissionMode?: 'normal' | 'bypass';
  currentFile?: string;
  currentTask?: string;
  elapsedTime?: number;
  tokenCount?: number;
}

// Format elapsed time
const formatElapsed = (ms: number): string => {
  const seconds = Math.floor(ms / 1000);
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  const secs = seconds % 60;
  return `${minutes}m ${secs}s`;
};

// Format tokens
const formatTokens = (tokens: number): string => {
  if (tokens < 1000) return `${tokens}`;
  return `${(tokens / 1000).toFixed(1)}k`;
};

export const StatusBar: React.FC<StatusBarProps> = ({
  isProcessing = false,
  permissionMode = 'normal',
  currentFile,
  currentTask,
  elapsedTime,
  tokenCount,
}) => {
  const showTaskInfo = isProcessing && currentTask;

  return (
    <Box flexDirection="column">
      {/* Divider line */}
      <Box>
        <Text color={theme.ink.muted}>{'─'.repeat(80)}</Text>
      </Box>

      {/* Main status bar */}
      <Box justifyContent="space-between">
        {/* Left side - input area is above this */}
        <Box />

        {/* Right side - status info */}
        <Box>
          {showTaskInfo && (
            <>
              <Text color={theme.ink.warning}>⏳</Text>
              <Text color={theme.ink.muted}> {currentTask}</Text>
              {elapsedTime !== undefined && (
                <Text color={theme.ink.muted}> · {formatElapsed(elapsedTime)}</Text>
              )}
              {tokenCount !== undefined && (
                <Text color={theme.ink.muted}> · ↓ {formatTokens(tokenCount)} tokens</Text>
              )}
            </>
          )}
        </Box>
      </Box>

      {/* Bottom bar with permissions and current file */}
      <Box marginTop={1}>
        {/* Permission indicator */}
        <Text color={theme.ink.brand}>▸▸</Text>
        <Text> </Text>
        {permissionMode === 'bypass' ? (
          <Text color={theme.ink.warning}>bypass permissions on</Text>
        ) : (
          <Text color={theme.ink.muted}>permissions normal</Text>
        )}

        {/* Current file being edited */}
        {currentFile && (
          <>
            <Text color={theme.ink.muted}> · </Text>
            <Text color={theme.ink.info}>{currentFile}</Text>
          </>
        )}

        {/* Status indicator */}
        {isProcessing && (
          <>
            <Text color={theme.ink.muted}> </Text>
            <Text color={theme.ink.muted}>(</Text>
            <Text color={theme.ink.warning}>running</Text>
            <Text color={theme.ink.muted}>)</Text>
          </>
        )}
      </Box>
    </Box>
  );
};

/**
 * Compact status indicator for inline use
 */
export const StatusIndicator: React.FC<{
  status: LoopStatus;
  showLabel?: boolean;
}> = ({ status, showLabel = true }) => {
  const configs: Record<LoopStatus, { color: string; label: string }> = {
    idle: { color: theme.ink.success, label: 'Ready' },
    thinking: { color: theme.ink.warning, label: 'Thinking' },
    streaming: { color: theme.ink.info, label: 'Streaming' },
    tool_execution: { color: theme.ink.brand, label: 'Executing' },
    awaiting_permission: { color: theme.ink.warning, label: 'Awaiting permission' },
  };

  const config = configs[status];

  return (
    <Box>
      <Text color={config.color}>●</Text>
      {showLabel && (
        <Text color={theme.ink.muted}> {config.label}</Text>
      )}
    </Box>
  );
};

export default StatusBar;

/**
 * Execution message component - Claude Code style
 * Tree-like structure with animated status dots
 */
import React, { useState, useEffect } from 'react';
import { Box, Text, useStdout } from 'ink';
import { theme, BABY_BLUE, BABY_PINK } from '../../theme';

// Shimmer colors: dark pink -> baby pink -> white
const SHIMMER_COLORS = [
  '#C48B9F',  // Dark pink
  '#D4A5B5',  // Medium pink
  '#E8B8C8',  // Light pink
  BABY_PINK,  // Baby pink (#F8BBD9)
  '#FCD5E8',  // Very light pink
  '#FFFFFF',  // White
  '#FCD5E8',  // Very light pink
  BABY_PINK,  // Baby pink
  '#E8B8C8',  // Light pink
  '#D4A5B5',  // Medium pink
];

/**
 * Shimmering text component - cycles through pink to white colors
 */
export const ShimmeringText: React.FC<{ children: string }> = ({ children }) => {
  const [colorIndex, setColorIndex] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setColorIndex(i => (i + 1) % SHIMMER_COLORS.length);
    }, 150);
    return () => clearInterval(interval);
  }, []);

  return <Text color={SHIMMER_COLORS[colorIndex]}>{children}</Text>;
};

export type ExecutionStatus =
  | 'Pending'
  | 'PendingAuthorization'
  | 'Running'
  | 'Completed'
  | 'Failed'
  | 'Cancelled';

export type ExecutionType = 'azCli' | 'kubectl' | 'psql' | 'bash';

export interface ExecutionMessageProps {
  id: string;
  description: string;
  command: string;
  type: ExecutionType;
  status: ExecutionStatus;
  output?: string;
  error?: string;
  riskLevel?: 'low' | 'medium' | 'high';
  executedBy?: string;
  showOutput?: boolean;
  timeout?: number;
}

// Animated dot for running state
const AnimatedDot: React.FC<{ color: string }> = ({ color }) => {
  const [visible, setVisible] = useState(true);

  useEffect(() => {
    const interval = setInterval(() => {
      setVisible(v => !v);
    }, 400);
    return () => clearInterval(interval);
  }, []);

  return <Text color={color}>{visible ? '●' : '○'}</Text>;
};

// Get type name for display
const getTypeName = (type: ExecutionType): string => {
  switch (type) {
    case 'azCli': return 'AzCli';
    case 'kubectl': return 'Kubectl';
    case 'psql': return 'Psql';
    case 'bash': return 'Bash';
    default: return 'Bash';
  }
};

// Truncate command for display
const truncateCommand = (cmd: string, maxLen: number): string => {
  if (cmd.length <= maxLen) return cmd;
  return cmd.slice(0, maxLen - 3) + '...';
};

// Count lines and optionally truncate
const processOutput = (text: string, maxLines: number = 5): { lines: string[]; truncated: number } => {
  const allLines = text.split('\n');
  if (allLines.length <= maxLines) {
    return { lines: allLines, truncated: 0 };
  }
  return {
    lines: allLines.slice(0, maxLines),
    truncated: allLines.length - maxLines,
  };
};

export const ExecutionMessage: React.FC<ExecutionMessageProps> = ({
  command,
  type,
  status,
  output,
  error,
  showOutput = false,
  timeout,
  executedBy,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 80;
  const maxCommandLen = Math.max(40, terminalWidth - 30);

  const isPending = status === 'Pending' || status === 'PendingAuthorization';
  const isRunning = status === 'Running';
  const isCompleted = status === 'Completed';
  const isFailed = status === 'Failed' || status === 'Cancelled';
  const isDone = isCompleted || isFailed;

  // Should show output when expanded or completed
  const shouldShowOutput = (showOutput || isDone) && (output || error);

  // Process output
  const outputData = React.useMemo(() => {
    const content = error || output;
    if (!content) return null;
    return processOutput(content.trim());
  }, [output, error]);

  // Command display with type name
  const typeName = getTypeName(type);
  const commandDisplay = truncateCommand(command, maxCommandLen);
  const timeoutDisplay = timeout ? ` timeout: ${Math.round(timeout / 1000)}s` : '';

  // Determine dot color based on status
  const getDotColor = () => {
    if (isFailed) return 'red';
    if (isCompleted) return 'green';
    if (isRunning) return BABY_BLUE;
    return 'gray';
  };

  const dotColor = getDotColor();

  return (
    <Box flexDirection="column">
      {/* Main command line: ● Type(command) timeout: Xs */}
      <Box flexDirection="row">
        {/* Status dot - gray (pending) -> blue animated (running) -> green/red (done) */}
        {isPending && <Text color="gray">○</Text>}
        {isRunning && <AnimatedDot color={BABY_BLUE} />}
        {isCompleted && <Text color="green">●</Text>}
        {isFailed && <Text color="red">●</Text>}

        <Text> </Text>
        <Text bold color="white">
          {typeName}
        </Text>
        <Text color="gray">(</Text>
        <Text>{commandDisplay}</Text>
        <Text color="gray">)</Text>
        {timeoutDisplay && <Text color="gray">{timeoutDisplay}</Text>}
        {executedBy && <Text color="gray"> by {executedBy}</Text>}
      </Box>

      {/* Authorization hint */}
      {status === 'PendingAuthorization' && (
        <Box paddingLeft={3}>
          <Text color={theme.ink.warning}>↳ Requires authorization</Text>
        </Box>
      )}

      {/* Output tree branch */}
      {shouldShowOutput && outputData && (
        <Box flexDirection="column" paddingLeft={2}>
          {/* First line with branch symbol */}
          <Box>
            <Text color="gray">⎿  </Text>
            <Text color={error ? 'red' : undefined}>{outputData.lines[0]}</Text>
          </Box>

          {/* Remaining lines */}
          {outputData.lines.slice(1).map((line, i) => (
            <Box key={i}>
              <Text color="gray">   </Text>
              <Text color={error ? 'red' : undefined}>{line}</Text>
            </Box>
          ))}

          {/* Truncation indicator */}
          {outputData.truncated > 0 && (
            <Box>
              <Text color="gray">   … +{outputData.truncated} lines (ctrl+o to expand)</Text>
            </Box>
          )}
        </Box>
      )}

      {/* Collapsed output hint when not showing output */}
      {!shouldShowOutput && (output || error) && (
        <Box paddingLeft={2}>
          <Text color="gray">
            ⎿  {(output || error || '').split('\n').length} lines (ctrl+o to expand)
          </Text>
        </Box>
      )}
    </Box>
  );
};

/**
 * Compact execution status badge for inline display
 */
export const ExecutionStatusBadge: React.FC<{
  status: ExecutionStatus;
  type: ExecutionType;
}> = ({ status, type }) => {
  const isCompleted = status === 'Completed';
  const isFailed = status === 'Failed' || status === 'Cancelled';
  const isRunning = status === 'Running';

  return (
    <Box gap={1}>
      {isRunning && <AnimatedDot color={BABY_BLUE} />}
      {isCompleted && <Text color="green">●</Text>}
      {isFailed && <Text color="red">●</Text>}
      {!isRunning && !isCompleted && !isFailed && <Text color="gray">○</Text>}
      <Text color={theme.ink.muted}>{getTypeName(type)}</Text>
    </Box>
  );
};

export default ExecutionMessage;

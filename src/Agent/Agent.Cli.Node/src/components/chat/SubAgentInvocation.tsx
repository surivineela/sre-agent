/**
 * SubAgent invocation display component for Task tool
 * Shows parallel subagent executions with real-time progress
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { BABY_PINK, BABY_BLUE } from '../../theme';
import type {
  SubAgentType,
  SubAgentStatus,
  SubAgentToolInvocation,
  SubAgentExecution,
} from '../../types';

// Re-export types for convenience
export type { SubAgentType, SubAgentStatus, SubAgentToolInvocation, SubAgentExecution };

export interface SubAgentInvocationProps {
  execution: SubAgentExecution;
  compact?: boolean;
  showToolProgress?: boolean;
}

export interface SubAgentGroupProps {
  executions: SubAgentExecution[];
  compact?: boolean;
  showToolProgress?: boolean;
}

// Smooth spinner animation
const spinnerFrames = ['⠋', '⠙', '⠹', '⠸', '⠼', '⠴', '⠦', '⠧', '⠇', '⠏'];

// Get symbol for subagent type (Unicode symbols, no emoji)
const getSubAgentSymbol = (type: SubAgentType): string => {
  switch (type) {
    case 'Explore':
      return '◈';
    case 'Plan':
      return '◆';
    case 'CodeReview':
      return '◇';
    case 'KustoQuery':
      return '▣';
  }
};

// Get color for subagent type
const getSubAgentColor = (type: SubAgentType): string => {
  switch (type) {
    case 'Explore':
      return '#64b5f6'; // Light blue
    case 'Plan':
      return '#ba68c8'; // Purple
    case 'CodeReview':
      return '#81c784'; // Green
    case 'KustoQuery':
      return '#ffb74d'; // Orange
  }
};

// Get status config
const getStatusConfig = (status: SubAgentStatus) => {
  switch (status) {
    case 'pending':
      return { bullet: '○', color: 'gray', label: 'Pending' };
    case 'running':
      return { bullet: '●', color: 'yellow', label: 'Running' };
    case 'complete':
      return { bullet: '●', color: 'green', label: 'Done' };
    case 'error':
      return { bullet: '●', color: 'red', label: 'Error' };
  }
};

// Truncate text
const truncate = (str: string, maxLen: number): string => {
  if (str.length <= maxLen) return str;
  return str.slice(0, maxLen - 1) + '…';
};

// Extract summary from result (first meaningful line)
const getSummary = (result?: string): string | null => {
  if (!result) return null;
  const lines = result.split('\n').filter(l => l.trim() && !l.startsWith('#') && !l.startsWith('**'));
  if (lines.length === 0) return null;
  const firstLine = lines[0].trim();
  return firstLine.length > 60 ? firstLine.slice(0, 60) + '...' : firstLine;
};

/**
 * Single subagent execution display
 */
export const SubAgentInvocation: React.FC<SubAgentInvocationProps> = ({
  execution,
  compact = true,
  showToolProgress = true,
}) => {
  const [frame, setFrame] = useState(0);
  const config = getStatusConfig(execution.status);
  const typeColor = getSubAgentColor(execution.subagentType);

  // Spinner animation
  useEffect(() => {
    if (execution.status !== 'running') return;
    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % spinnerFrames.length);
    }, 80);
    return () => clearInterval(timer);
  }, [execution.status]);

  const bullet = execution.status === 'running' ? spinnerFrames[frame] : config.bullet;
  const symbol = getSubAgentSymbol(execution.subagentType);
  const summary = getSummary(execution.result);

  return (
    <Box flexDirection="column" marginTop={compact ? 0 : 1}>
      {/* Main subagent line */}
      <Box>
        <Text color={config.color}>{bullet}</Text>
        <Text> </Text>
        <Text color={typeColor}>{symbol}</Text>
        <Text> </Text>
        <Text bold color={typeColor}>{execution.subagentType}</Text>
        <Text color="gray"> </Text>
        <Text color="white">{truncate(execution.description, 50)}</Text>
      </Box>

      {/* Show tool progress if running and enabled */}
      {showToolProgress && execution.status === 'running' && execution.toolInvocations && (
        <Box flexDirection="column" marginLeft={2}>
          {execution.toolInvocations.slice(-3).map((tool, idx) => (
            <Box key={idx}>
              <Text color="gray">⎿ </Text>
              <Text color={tool.status === 'running' ? 'yellow' : tool.status === 'complete' ? 'green' : 'red'}>
                {tool.status === 'running' ? spinnerFrames[frame] : tool.status === 'complete' ? '✓' : '✗'}
              </Text>
              <Text color="gray"> {tool.toolName}</Text>
              {tool.description && (
                <Text color="gray" dimColor> {truncate(tool.description, 30)}</Text>
              )}
            </Box>
          ))}
        </Box>
      )}

      {/* Show summary when completed */}
      {execution.status === 'complete' && summary && (
        <Box marginLeft={2}>
          <Text color="gray">⎿ </Text>
          <Text color="green">✓ </Text>
          <Text color="gray" dimColor>{summary}</Text>
        </Box>
      )}

      {/* Show error if failed */}
      {execution.status === 'error' && execution.error && (
        <Box marginLeft={2}>
          <Text color="gray">⎿ </Text>
          <Text color="red">✗ {truncate(execution.error, 60)}</Text>
        </Box>
      )}
    </Box>
  );
};

/**
 * Group of parallel subagent executions
 * Shows multiple subagents running concurrently with visual tree structure
 */
export const SubAgentGroup: React.FC<SubAgentGroupProps> = ({
  executions,
  compact = true,
  showToolProgress = true,
}) => {
  const [frame, setFrame] = useState(0);
  const runningCount = executions.filter(e => e.status === 'running').length;
  const completedCount = executions.filter(e => e.status === 'complete').length;
  const totalCount = executions.length;

  // Spinner animation for group header
  useEffect(() => {
    if (runningCount === 0) return;
    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % spinnerFrames.length);
    }, 80);
    return () => clearInterval(timer);
  }, [runningCount]);

  if (executions.length === 0) return null;

  // Single execution - render directly
  if (executions.length === 1) {
    return <SubAgentInvocation execution={executions[0]} compact={compact} showToolProgress={showToolProgress} />;
  }

  // Multiple parallel executions with visual tree
  return (
    <Box flexDirection="column" marginTop={compact ? 0 : 1}>
      {/* Group header showing parallel count */}
      <Box>
        <Text color="cyan" bold>┬</Text>
        <Text color="gray"> Running </Text>
        <Text color="white" bold>{totalCount}</Text>
        <Text color="gray"> tasks in parallel </Text>
        {runningCount > 0 ? (
          <>
            <Text color="yellow">{spinnerFrames[frame]}</Text>
            <Text color="gray" dimColor> {runningCount} active</Text>
          </>
        ) : (
          <Text color="green">✓ done</Text>
        )}
      </Box>

      {/* Parallel executions with tree structure */}
      {executions.map((exec, index) => {
        const isLast = index === executions.length - 1;
        const prefix = isLast ? '└' : '├';
        const lineChar = isLast ? ' ' : '│';
        const summary = getSummary(exec.result);

        return (
          <Box key={exec.id} flexDirection="column">
            <Box>
              <Text color="cyan">{prefix}─ </Text>
              <SubAgentStatusLine
                type={exec.subagentType}
                description={exec.description}
                status={exec.status}
              />
            </Box>
            {/* Show tool progress indented under the tree */}
            {showToolProgress && exec.status === 'running' && exec.toolInvocations && (
              <Box flexDirection="column">
                {exec.toolInvocations.slice(-2).map((tool, idx) => (
                  <Box key={idx}>
                    <Text color="cyan">{lineChar}  </Text>
                    <Text color="gray">⎿ </Text>
                    <Text color={tool.status === 'running' ? 'yellow' : tool.status === 'complete' ? 'green' : 'red'}>
                      {tool.status === 'running' ? spinnerFrames[frame] : tool.status === 'complete' ? '✓' : '✗'}
                    </Text>
                    <Text color="gray"> {tool.toolName}</Text>
                  </Box>
                ))}
              </Box>
            )}
            {/* Show summary when completed */}
            {exec.status === 'complete' && summary && (
              <Box>
                <Text color="cyan">{lineChar}  </Text>
                <Text color="green">✓ </Text>
                <Text color="gray" dimColor>{summary}</Text>
              </Box>
            )}
            {/* Show error if failed */}
            {exec.status === 'error' && exec.error && (
              <Box>
                <Text color="cyan">{lineChar}  </Text>
                <Text color="red">✗ {truncate(exec.error, 50)}</Text>
              </Box>
            )}
          </Box>
        );
      })}
    </Box>
  );
};

/**
 * Compact inline display for subagent status
 */
export const SubAgentStatusLine: React.FC<{
  type: SubAgentType;
  description: string;
  status: SubAgentStatus;
}> = ({ type, description, status }) => {
  const config = getStatusConfig(status);
  const typeColor = getSubAgentColor(type);
  const symbol = getSubAgentSymbol(type);

  return (
    <Box>
      <Text color={config.color}>{config.bullet}</Text>
      <Text> </Text>
      <Text color={typeColor}>{symbol}</Text>
      <Text> </Text>
      <Text color={typeColor}>{type}</Text>
      <Text color="gray">: </Text>
      <Text color="white">{truncate(description, 40)}</Text>
    </Box>
  );
};

export default SubAgentInvocation;

/**
 * Activity Indicator - Claude Code style bullet point status
 * Shows operation status with colored bullet and details
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { theme } from '../../theme';

export type ActivityStatus = 'pending' | 'running' | 'success' | 'error';

export interface ActivityIndicatorProps {
  status: ActivityStatus;
  label: string;
  detail?: string;
  subDetail?: string;
  duration?: number;
  showSpinner?: boolean;
}

// Braille spinner - smooth 8-frame rotation
const spinnerFrames = ['⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷'];

const getStatusConfig = (status: ActivityStatus) => {
  switch (status) {
    case 'pending':
      return { bullet: '○', color: theme.ink.muted };
    case 'running':
      return { bullet: '●', color: theme.ink.brand };
    case 'success':
      return { bullet: '●', color: theme.ink.success };
    case 'error':
      return { bullet: '●', color: theme.ink.error };
  }
};

export const ActivityIndicator: React.FC<ActivityIndicatorProps> = ({
  status,
  label,
  detail,
  subDetail,
  duration,
  showSpinner = true,
}) => {
  const [frame, setFrame] = useState(0);
  const config = getStatusConfig(status);

  // Spinner animation for running status
  useEffect(() => {
    if (status !== 'running' || !showSpinner) return;

    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % spinnerFrames.length);
    }, 100);

    return () => clearInterval(timer);
  }, [status, showSpinner]);

  const bullet = status === 'running' && showSpinner
    ? spinnerFrames[frame]
    : config.bullet;

  // Format duration
  const formatDuration = (ms: number) => {
    if (ms < 1000) return `${ms}ms`;
    const seconds = Math.floor(ms / 1000);
    if (seconds < 60) return `${seconds}s`;
    const minutes = Math.floor(seconds / 60);
    const secs = seconds % 60;
    return `${minutes}m ${secs}s`;
  };

  return (
    <Box flexDirection="column">
      {/* Main line */}
      <Box>
        <Text color={config.color}>{bullet}</Text>
        <Text> </Text>
        <Text bold>{label}</Text>
        {detail && (
          <>
            <Text color={theme.ink.muted}>(</Text>
            <Text color={theme.ink.info}>{detail}</Text>
            <Text color={theme.ink.muted}>)</Text>
          </>
        )}
      </Box>

      {/* Sub-detail line (indented) */}
      {subDetail && (
        <Box marginLeft={2}>
          <Text color={theme.ink.muted}>└ </Text>
          <Text color={theme.ink.muted}>{subDetail}</Text>
          {duration !== undefined && (
            <Text color={theme.ink.muted}> · {formatDuration(duration)}</Text>
          )}
        </Box>
      )}
    </Box>
  );
};

/**
 * Fetch activity - like "Fetch(https://example.com)"
 */
export interface FetchActivityProps {
  url: string;
  status: ActivityStatus;
  bytesReceived?: number;
  statusCode?: number;
  error?: string;
}

export const FetchActivity: React.FC<FetchActivityProps> = ({
  url,
  status,
  bytesReceived,
  statusCode,
  error,
}) => {
  const formatBytes = (bytes: number) => {
    if (bytes < 1024) return `${bytes}B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)}KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)}MB`;
  };

  let subDetail = '';
  if (status === 'success' && bytesReceived !== undefined) {
    subDetail = `Received ${formatBytes(bytesReceived)}`;
    if (statusCode) subDetail += ` (${statusCode} OK)`;
  } else if (status === 'error' && error) {
    subDetail = `Error: ${error}`;
  } else if (status === 'running') {
    subDetail = 'Fetching...';
  }

  return (
    <ActivityIndicator
      status={status}
      label="Fetch"
      detail={url}
      subDetail={subDetail}
    />
  );
};

/**
 * Web Search activity
 */
export interface WebSearchActivityProps {
  query: string;
  status: ActivityStatus;
  resultCount?: number;
  duration?: number;
}

export const WebSearchActivity: React.FC<WebSearchActivityProps> = ({
  query,
  status,
  resultCount,
  duration,
}) => {
  let subDetail = '';
  if (status === 'success') {
    subDetail = `Did ${resultCount ?? 1} search`;
    if (duration) subDetail += ` in ${Math.floor(duration / 1000)}s`;
  } else if (status === 'running') {
    subDetail = 'Searching...';
  }

  return (
    <ActivityIndicator
      status={status}
      label="Web Search"
      detail={`"${query}"`}
      subDetail={subDetail}
    />
  );
};

/**
 * Explore activity - for codebase exploration
 */
export interface ExploreActivityProps {
  description: string;
  status: ActivityStatus;
  toolUses?: number;
  tokens?: number;
  duration?: number;
}

export const ExploreActivity: React.FC<ExploreActivityProps> = ({
  description,
  status,
  toolUses,
  tokens,
  duration,
}) => {
  let subDetail = '';
  if (status === 'success') {
    const parts = [];
    if (toolUses) parts.push(`${toolUses} tool uses`);
    if (tokens) parts.push(`${(tokens / 1000).toFixed(1)}k tokens`);
    if (duration) {
      const mins = Math.floor(duration / 60000);
      const secs = Math.floor((duration % 60000) / 1000);
      parts.push(`${mins}m ${secs}s`);
    }
    subDetail = `Done (${parts.join(' · ')})`;
  } else if (status === 'running') {
    subDetail = 'Exploring...';
  }

  return (
    <ActivityIndicator
      status={status}
      label="Explore"
      detail={description}
      subDetail={subDetail}
    />
  );
};

/**
 * Generic tool activity
 */
export interface ToolActivityProps {
  toolName: string;
  status: ActivityStatus;
  input?: string;
  output?: string;
  duration?: number;
  error?: string;
}

export const ToolActivity: React.FC<ToolActivityProps> = ({
  toolName,
  status,
  input,
  output,
  duration,
  error,
}) => {
  let subDetail = '';
  if (status === 'success') {
    if (output) {
      subDetail = output.length > 50 ? output.slice(0, 50) + '...' : output;
    }
  } else if (status === 'error' && error) {
    subDetail = `Error: ${error}`;
  } else if (status === 'running') {
    subDetail = 'Executing...';
  }

  return (
    <ActivityIndicator
      status={status}
      label={toolName}
      detail={input}
      subDetail={subDetail}
      duration={duration}
    />
  );
};

export default ActivityIndicator;

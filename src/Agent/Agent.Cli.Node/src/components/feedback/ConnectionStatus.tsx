/**
 * ConnectionStatus component - Claude Code style connection status display (SPEC-011)
 * Shows detailed connection state, reconnection progress, and recovery options
 */
import React from 'react';
import { Box, Text, useInput } from 'ink';
import { theme } from '../../theme';
import type { EnhancedConnectionState } from '../../services/streaming';

// Re-export for backwards compatibility
export type ConnectionStateDetails = EnhancedConnectionState;

export interface ConnectionStatusProps {
  state: EnhancedConnectionState;
  onRetry: () => void;
  onCancel: () => void;
  onAuth?: () => void;
}

export const ConnectionStatus: React.FC<ConnectionStatusProps> = ({
  state,
  onRetry,
  onCancel,
  onAuth,
}) => {
  // Don't show anything when connected
  if (state.status === 'connected') return null;

  // Handle keyboard shortcuts
  useInput((input, key) => {
    if (input.toLowerCase() === 'r') {
      onRetry();
    } else if (input.toLowerCase() === 'c' || key.escape) {
      onCancel();
    } else if (input.toLowerCase() === 'a' && state.status === 'auth-required' && onAuth) {
      onAuth();
    }
  });

  const getStatusIcon = (): string => {
    switch (state.status) {
      case 'connecting':
        return '◐';
      case 'reconnecting':
        return '⚠';
      case 'auth-required':
        return '🔑';
      default:
        return '○';
    }
  };

  const getStatusColor = (): string => {
    switch (state.status) {
      case 'connecting':
      case 'reconnecting':
        return 'yellow';
      case 'auth-required':
        return 'cyan';
      default:
        return 'red';
    }
  };

  const getTitle = (): string => {
    switch (state.status) {
      case 'connecting':
        return 'Connecting...';
      case 'reconnecting':
        return 'Connection Lost';
      case 'auth-required':
        return 'Authentication Required';
      default:
        return 'Disconnected';
    }
  };

  const statusColor = getStatusColor();

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor={statusColor}
      paddingX={1}
      marginY={1}
    >
      {/* Header */}
      <Box>
        <Text color={statusColor}>{getStatusIcon()} </Text>
        <Text color={statusColor} bold>{getTitle()}</Text>
      </Box>

      {/* Server URL */}
      {state.serverUrl && (
        <Box>
          <Text color="gray">{state.serverUrl}</Text>
        </Box>
      )}

      {/* Status message - Reconnecting with countdown */}
      {state.status === 'reconnecting' && (
        <Box marginTop={1}>
          <Text color="gray">
            Reconnecting in {state.nextRetryIn}s... (attempt {state.attempt}/{state.maxAttempts})
          </Text>
        </Box>
      )}

      {/* Status message - Connecting */}
      {state.status === 'connecting' && state.attempt > 0 && (
        <Box marginTop={1}>
          <Text color="gray">Attempt {state.attempt}/{state.maxAttempts}...</Text>
        </Box>
      )}

      {/* Status message - Auth required */}
      {state.status === 'auth-required' && (
        <Box marginTop={1}>
          <Text color="gray">Run /auth or press [A] to re-authenticate</Text>
        </Box>
      )}

      {/* Error message */}
      {state.lastError && (
        <Box marginTop={1}>
          <Text color="red">{state.lastError}</Text>
        </Box>
      )}

      {/* Actions for reconnecting/disconnected */}
      {(state.status === 'reconnecting' || state.status === 'disconnected') && (
        <Box marginTop={1} gap={2}>
          <Text color={theme.ink.brand}>[R]etry Now</Text>
          <Text color="gray">[C]ancel</Text>
        </Box>
      )}

      {/* Actions for auth-required */}
      {state.status === 'auth-required' && onAuth && (
        <Box marginTop={1} gap={2}>
          <Text color={theme.ink.brand}>[A]uthenticate</Text>
          <Text color="gray">[C]ancel</Text>
        </Box>
      )}
    </Box>
  );
};

export default ConnectionStatus;

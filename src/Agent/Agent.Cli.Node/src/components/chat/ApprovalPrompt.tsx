/**
 * Approval prompt - CLI version
 * Shows approval requests with keyboard shortcuts
 * Ported from Agent.Web\Client ApprovalMessage.tsx
 */
import React, { useState, useEffect } from 'react';
import { Box, Text, useInput } from 'ink';
import { theme } from '../../theme';

export type ApprovalStatus =
  | 'Pending'
  | 'PendingAuthorization'
  | 'Approved'
  | 'Authorized'
  | 'Cancelled';

export interface ApprovalPromptProps {
  id: string;
  description: string;
  status: ApprovalStatus;
  oboTokenScope?: string;
  onApprove?: () => void;
  onCancel?: () => void;
  decisionUser?: string;
  isLoading?: boolean;
  showHints?: boolean;
}

// Braille spinner - smooth 8-frame rotation
const SPINNER_FRAMES = ['⣾', '⣽', '⣻', '⢿', '⡿', '⣟', '⣯', '⣷'];

export const ApprovalPrompt: React.FC<ApprovalPromptProps> = ({
  description,
  status,
  oboTokenScope,
  onApprove,
  onCancel,
  decisionUser,
  isLoading = false,
  showHints = true,
}) => {
  const [frame, setFrame] = useState(0);
  const isPending = status === 'Pending' || status === 'PendingAuthorization';

  // Spinner animation when loading
  useEffect(() => {
    if (!isLoading) return;
    const timer = setInterval(() => {
      setFrame((f) => (f + 1) % SPINNER_FRAMES.length);
    }, 100);
    return () => clearInterval(timer);
  }, [isLoading]);

  // Keyboard input
  useInput((input, key) => {
    if (!isPending || isLoading) return;

    if (input === 'y' || input === 'Y' || key.return) {
      onApprove?.();
    } else if (input === 'n' || input === 'N' || key.escape) {
      onCancel?.();
    }
  });

  // Get status display config
  const getStatusConfig = () => {
    switch (status) {
      case 'Approved':
      case 'Authorized':
        return { color: theme.ink.success, label: status === 'Authorized' ? 'Authorized' : 'Approved', bullet: '✓' };
      case 'Cancelled':
        return { color: theme.ink.muted, label: 'Cancelled', bullet: '✗' };
      default:
        return { color: theme.ink.warning, label: 'Pending', bullet: '?' };
    }
  };

  const statusConfig = getStatusConfig();

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor={isPending ? theme.ink.warning : theme.ink.muted}
      paddingX={1}
      marginY={1}
    >
      {/* Header */}
      <Box>
        <Text color={isPending ? theme.ink.warning : statusConfig.color} bold>
          {isPending ? '⚠' : statusConfig.bullet}
        </Text>
        <Text> </Text>
        <Text bold>{description}</Text>
      </Box>

      {/* Token scope info */}
      {oboTokenScope && (
        <Box marginTop={0}>
          <Text color={theme.ink.muted}>Scope: </Text>
          <Text color={theme.ink.info}>{oboTokenScope}</Text>
        </Box>
      )}

      {/* Pending state */}
      {isPending && (
        <Box marginTop={1} flexDirection="column">
          <Box>
            <Text color={theme.ink.muted}>
              {status === 'PendingAuthorization'
                ? 'User authorization required'
                : 'Agent permissions required'}
            </Text>
          </Box>

          {isLoading ? (
            <Box marginTop={1}>
              <Text color={theme.ink.info}>{SPINNER_FRAMES[frame]}</Text>
              <Text color={theme.ink.muted}> Processing...</Text>
            </Box>
          ) : showHints ? (
            <Box marginTop={1}>
              <Text color={theme.ink.success} bold>[Y]</Text>
              <Text color={theme.ink.muted}> {status === 'PendingAuthorization' ? 'Authorize' : 'Approve'}</Text>
              <Text>  </Text>
              <Text color={theme.ink.error} bold>[N]</Text>
              <Text color={theme.ink.muted}> Cancel</Text>
            </Box>
          ) : null}
        </Box>
      )}

      {/* Completed state */}
      {!isPending && (
        <Box marginTop={1}>
          <Text color={statusConfig.color}>{statusConfig.label}</Text>
          {decisionUser && (
            <Text color={theme.ink.muted}> by {decisionUser}</Text>
          )}
        </Box>
      )}
    </Box>
  );
};

/**
 * Compact approval status for inline display
 */
export const ApprovalStatusBadge: React.FC<{
  status: ApprovalStatus;
}> = ({ status }) => {
  const isPending = status === 'Pending' || status === 'PendingAuthorization';
  const isApproved = status === 'Approved' || status === 'Authorized';

  const color = isPending
    ? theme.ink.warning
    : isApproved
      ? theme.ink.success
      : theme.ink.muted;

  const label = isPending
    ? 'Pending'
    : isApproved
      ? 'Approved'
      : 'Cancelled';

  return (
    <Box>
      <Text color={color}>●</Text>
      <Text> </Text>
      <Text color={color}>{label}</Text>
    </Box>
  );
};

export default ApprovalPrompt;

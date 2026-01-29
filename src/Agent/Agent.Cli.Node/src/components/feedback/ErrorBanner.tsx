/**
 * ErrorBanner component - formatted error display with actions (SPEC-012)
 *
 * Displays errors in a visually distinct, user-friendly format with:
 * - Color-coded categories
 * - Actionable suggestions
 * - Keyboard shortcuts for quick actions
 * - Dismiss option
 */
import React from 'react';
import { Box, Text, useInput, useStdout } from 'ink';
import { theme } from '../../theme';
import type { FormattedError } from '../../utils/errors';
import { getErrorCategoryColor } from '../../utils/errors';

export interface ErrorBannerProps {
  error: FormattedError;
  onAction?: (key: string) => void;
  onDismiss?: () => void;
}

export const ErrorBanner: React.FC<ErrorBannerProps> = ({
  error,
  onAction,
  onDismiss,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 80;
  const boxWidth = Math.min(70, terminalWidth - 4);

  const color = getErrorCategoryColor(error.category);

  // Handle keyboard shortcuts
  useInput((input, key) => {
    // Check for dismiss
    if (input.toLowerCase() === 'd' && onDismiss) {
      onDismiss();
      return;
    }

    // Check for action shortcuts
    if (error.actions && onAction) {
      const action = error.actions.find(
        (a) => a.key.toLowerCase() === input.toLowerCase()
      );
      if (action) {
        onAction(action.key);
      }
    }

    // Escape also dismisses
    if (key.escape && onDismiss) {
      onDismiss();
    }
  });

  return (
    <Box
      flexDirection="column"
      borderStyle="round"
      borderColor={color}
      paddingX={1}
      marginY={1}
      width={boxWidth}
    >
      {/* Header with icon and title */}
      <Box>
        <Text color={color} bold>
          {'\u2717'} {error.title}
        </Text>
      </Box>

      {/* Main error message */}
      <Box marginTop={1}>
        <Text color="white">{error.message}</Text>
      </Box>

      {/* Details list */}
      {error.details && error.details.length > 0 && (
        <Box flexDirection="column" marginTop={1}>
          <Text color={color}>Possible causes:</Text>
          {error.details.map((detail, i) => (
            <Box key={i} marginLeft={1}>
              <Text color="gray">{'\u2022'} {detail}</Text>
            </Box>
          ))}
        </Box>
      )}

      {/* Suggestions list */}
      {error.suggestions && error.suggestions.length > 0 && (
        <Box flexDirection="column" marginTop={1}>
          <Text color={color}>Try:</Text>
          {error.suggestions.map((suggestion, i) => (
            <Box key={i} marginLeft={1}>
              <Text color="gray">{'\u2022'} {suggestion}</Text>
            </Box>
          ))}
        </Box>
      )}

      {/* Actions */}
      {(error.actions || onDismiss) && (
        <Box marginTop={1} gap={2}>
          {error.actions?.map((action) => (
            <Text key={action.key} color={theme.ink.brand}>
              [{action.key}]{action.label.slice(1)}
            </Text>
          ))}
          {onDismiss && <Text color="gray">[D]ismiss</Text>}
        </Box>
      )}
    </Box>
  );
};

export default ErrorBanner;

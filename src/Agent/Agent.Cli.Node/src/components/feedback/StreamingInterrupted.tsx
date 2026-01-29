/**
 * StreamingInterrupted component - Shows when streaming is interrupted
 * Provides options to continue, resend, or cancel
 */
import React from 'react';
import { Box, Text, useInput } from 'ink';
import { theme } from '../../theme';

interface StreamingInterruptedProps {
  partialResponse?: string;
  onContinue: () => void;
  onResend: () => void;
  onCancel: () => void;
}

export const StreamingInterrupted: React.FC<StreamingInterruptedProps> = ({
  partialResponse,
  onContinue,
  onResend,
  onCancel,
}) => {
  // Handle keyboard shortcuts
  useInput((input, key) => {
    if (input.toLowerCase() === 'c') {
      onContinue();
    } else if (input.toLowerCase() === 'r') {
      onResend();
    } else if (input.toLowerCase() === 'x' || key.escape) {
      onCancel();
    }
  });

  // Truncate partial response for preview
  const getPreview = (): string => {
    if (!partialResponse) return '';
    const lastChars = partialResponse.slice(-100);
    // Find last complete word/sentence boundary
    const lines = lastChars.split('\n');
    return lines.slice(-3).join('\n');
  };

  const preview = getPreview();

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Warning header */}
      <Box>
        <Text color="yellow">⚠ </Text>
        <Text color="yellow" bold>Connection lost - response interrupted</Text>
      </Box>

      {/* Reconnecting indicator */}
      <Box marginTop={1}>
        <Text color="gray">Reconnecting... </Text>
        <Text color="yellow">●</Text>
        <Text color="gray">○○</Text>
      </Box>

      {/* Partial content preview */}
      {preview && (
        <Box
          marginTop={1}
          borderStyle="single"
          borderColor="gray"
          paddingX={1}
          flexDirection="column"
        >
          <Text color="gray" dimColor>Last received:</Text>
          <Box marginTop={1}>
            <Text color="gray">
              {preview}
              <Text color="yellow">▋</Text>
            </Text>
          </Box>
        </Box>
      )}

      {/* Actions */}
      <Box marginTop={1} gap={2}>
        <Text color={theme.ink.brand}>[C]ontinue</Text>
        <Text color="yellow">[R]esend</Text>
        <Text color="gray">[X] Cancel</Text>
      </Box>
    </Box>
  );
};

export default StreamingInterrupted;

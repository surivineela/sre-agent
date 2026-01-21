/**
 * InlineError component - simple inline error display (SPEC-012)
 *
 * For less critical errors that don't need a full banner.
 * Shows a simple error message with optional suggestion.
 */
import React from 'react';
import { Box, Text } from 'ink';

export interface InlineErrorProps {
  message: string;
  suggestion?: string;
}

export const InlineError: React.FC<InlineErrorProps> = ({
  message,
  suggestion,
}) => {
  return (
    <Box flexDirection="column">
      <Box>
        <Text color="red">{'\u2717'} </Text>
        <Text color="red">{message}</Text>
      </Box>
      {suggestion && (
        <Box marginLeft={2}>
          <Text color="gray">{'\u2192'} {suggestion}</Text>
        </Box>
      )}
    </Box>
  );
};

export default InlineError;

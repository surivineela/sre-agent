/**
 * User message display
 */
import React from 'react';
import { Box, Text } from 'ink';
import type { Message } from '../../types';
import { theme } from '../../theme';

export interface UserMessageProps {
  message: Message;
  showTimestamp?: boolean;
  compact?: boolean;
}

export const UserMessage: React.FC<UserMessageProps> = ({
  message,
  compact = false,
}) => {
  const lines = message.content.split('\n');

  return (
    <Box flexDirection="row" gap={1} marginTop={1}>
      <Box width={1}>
        <Text color={theme.ink.muted}>❯</Text>
      </Box>
      <Box flexDirection="column" flexGrow={1}>
        {lines.map((line, i) => (
          <Text key={i} color={theme.ink.muted} wrap="wrap">{line}</Text>
        ))}
      </Box>
    </Box>
  );
};

export default UserMessage;

/**
 * Divider component - Horizontal line separator
 */
import React from 'react';
import { Box, Text } from 'ink';

export interface DividerProps {
  title?: string;
  color?: string;
  width?: number;
  character?: string;
}

export const Divider: React.FC<DividerProps> = ({
  title,
  color = 'gray',
  width,
  character = '─',
}) => {
  if (title) {
    return (
      <Box>
        <Text color={color}>
          {character.repeat(3)} {title} {character.repeat(width ? Math.max(0, width - title.length - 6) : 20)}
        </Text>
      </Box>
    );
  }

  return (
    <Box>
      <Text color={color}>{character.repeat(width || 40)}</Text>
    </Box>
  );
};

export default Divider;

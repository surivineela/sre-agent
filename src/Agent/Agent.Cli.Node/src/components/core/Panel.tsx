/**
 * Panel component - A bordered container with optional title
 */
import React from 'react';
import { Box, Text } from 'ink';

export interface PanelProps {
  title?: string;
  color?: string;
  borderStyle?: 'single' | 'double' | 'round' | 'bold' | 'singleDouble' | 'doubleSingle' | 'classic';
  padding?: number;
  children?: React.ReactNode;
}

export const Panel: React.FC<PanelProps> = ({
  title,
  color = 'cyan',
  borderStyle = 'round',
  padding = 1,
  children,
}) => {
  return (
    <Box
      flexDirection="column"
      borderStyle={borderStyle}
      borderColor={color}
      paddingX={padding}
      paddingY={padding > 0 ? Math.max(0, padding - 1) : 0}
    >
      {title && (
        <Box marginBottom={children ? 1 : 0}>
          <Text bold color={color}>
            {title}
          </Text>
        </Box>
      )}
      {children}
    </Box>
  );
};

export default Panel;

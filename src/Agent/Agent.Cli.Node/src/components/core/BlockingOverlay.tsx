/**
 * BlockingOverlay - Full-screen blocking container for modals
 * Centers child content and fills the entire terminal
 */
import React from 'react';
import { Box, useStdout } from 'ink';

export interface BlockingOverlayProps {
  children: React.ReactNode;
  /** Whether to center content vertically (default: true) */
  centerVertical?: boolean;
  /** Whether to center content horizontally (default: true) */
  centerHorizontal?: boolean;
}

export const BlockingOverlay: React.FC<BlockingOverlayProps> = ({
  children,
  centerVertical = true,
  centerHorizontal = true,
}) => {
  const { stdout } = useStdout();
  const height = stdout?.rows || 24;
  const width = stdout?.columns || 80;

  return (
    <Box
      flexDirection="column"
      height={height}
      width={width}
      alignItems={centerHorizontal ? 'center' : 'flex-start'}
      justifyContent={centerVertical ? 'center' : 'flex-start'}
    >
      {children}
    </Box>
  );
};

export default BlockingOverlay;

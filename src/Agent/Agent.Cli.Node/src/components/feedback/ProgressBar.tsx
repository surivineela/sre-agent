/**
 * Progress bar component
 *
 * Supports two modes:
 * 1. Simple: value (0-1) for percentage-based progress
 * 2. Count: current/total for item-based progress
 */
import React from 'react';
import { Box, Text, useStdout } from 'ink';

export interface ProgressBarProps {
  /** Progress value from 0 to 1 (use this OR current/total) */
  value?: number;
  /** Current item count (use with total for count mode) */
  current?: number;
  /** Total item count (use with current for count mode) */
  total?: number;
  /** Width of the progress bar in characters */
  width?: number;
  /** Label to show before the bar */
  label?: string;
  /** Whether to show percentage */
  showPercentage?: boolean;
  /** Whether to show count (e.g., "3/7 files") */
  showCount?: boolean;
  /** Unit label for count (e.g., "files", "items") */
  countLabel?: string;
  /** Color for the filled portion */
  color?: string;
  /** Color for the empty portion */
  backgroundColor?: string;
  /** Character for filled portion */
  filledChar?: string;
  /** Character for empty portion */
  emptyChar?: string;
  /** Use adaptive width based on terminal */
  adaptiveWidth?: boolean;
}

export const ProgressBar: React.FC<ProgressBarProps> = ({
  value,
  current,
  total,
  width: customWidth,
  label,
  showPercentage = true,
  showCount = false,
  countLabel,
  color = 'cyan',
  backgroundColor = 'gray',
  filledChar = '█',
  emptyChar = '░',
  adaptiveWidth = false,
}) => {
  const { stdout } = useStdout();

  // Calculate width
  let width = customWidth || 30;
  if (adaptiveWidth && stdout?.columns) {
    // Leave room for percentage and count display
    const reservedSpace = 25;
    width = Math.min(40, Math.max(15, stdout.columns - reservedSpace));
  }

  // Calculate progress value
  let progressValue: number;
  if (current !== undefined && total !== undefined && total > 0) {
    progressValue = current / total;
  } else if (value !== undefined) {
    progressValue = value;
  } else {
    progressValue = 0;
  }

  const clampedValue = Math.max(0, Math.min(1, progressValue));
  const filledWidth = Math.round(clampedValue * width);
  const emptyWidth = width - filledWidth;
  const percentage = Math.round(clampedValue * 100);

  return (
    <Box>
      {/* Percentage at start */}
      {showPercentage && (
        <Text color="gray">{percentage.toString().padStart(3)}% </Text>
      )}

      {/* Progress bar with brackets */}
      <Text color={color}>[{filledChar.repeat(filledWidth)}</Text>
      <Text color={backgroundColor}>{emptyChar.repeat(emptyWidth)}]</Text>

      {/* Count display */}
      {showCount && current !== undefined && total !== undefined && (
        <Text color="gray">
          {' '}
          {current}/{total}
          {countLabel && ` ${countLabel}`}
        </Text>
      )}

      {/* Label at end */}
      {label && <Text color="gray"> {label}</Text>}
    </Box>
  );
};

export default ProgressBar;

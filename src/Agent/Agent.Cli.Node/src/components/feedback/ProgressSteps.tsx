/**
 * ProgressSteps Component - Step-by-step progress indicator
 *
 * Shows multi-step progress with visual indicators:
 * - ✓ Completed steps (green)
 * - ● Running step (cyan)
 * - ✗ Failed step (red)
 * - ○ Pending steps (gray)
 */
import React from 'react';
import { Box, Text } from 'ink';
import type { ProgressState, ProgressStepStatus } from '../../services/progress';

export interface ProgressStepsProps {
  state: ProgressState;
  title?: string;
  showStepNumber?: boolean;
}

/**
 * Get step indicator character and color based on status
 */
const getStepIndicator = (
  status: ProgressStepStatus
): { char: string; color: string } => {
  switch (status) {
    case 'completed':
      return { char: '●', color: 'green' };
    case 'running':
      return { char: '●', color: 'cyan' };
    case 'failed':
      return { char: '●', color: 'red' };
    default:
      return { char: '○', color: 'gray' };
  }
};

/**
 * Get text color based on status
 */
const getTextColor = (status: ProgressStepStatus): string => {
  switch (status) {
    case 'completed':
      return 'white';
    case 'running':
      return 'white';
    case 'failed':
      return 'red';
    default:
      return 'gray';
  }
};

export const ProgressSteps: React.FC<ProgressStepsProps> = ({
  state,
  title,
  showStepNumber = true,
}) => {
  const { steps, currentStepIndex, isComplete, isFailed, error } = state;

  // Don't render if no steps
  if (steps.length === 0) {
    return null;
  }

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Title */}
      {title && (
        <Box marginBottom={1}>
          <Text bold>{title}</Text>
        </Box>
      )}

      {/* Steps list */}
      {steps.map((step, index) => {
        const { char, color } = getStepIndicator(step.status);
        const textColor = getTextColor(step.status);

        return (
          <Box key={index} marginLeft={2}>
            <Text color={color}>{char}</Text>
            <Text> </Text>
            <Text color={textColor}>{step.name}</Text>
            {step.message && step.status === 'running' && (
              <Text color="gray"> - {step.message}</Text>
            )}
            {step.duration !== undefined && step.status === 'completed' && (
              <Text color="gray" dimColor>
                {' '}
                ({Math.round(step.duration)}ms)
              </Text>
            )}
          </Box>
        );
      })}

      {/* Current step summary */}
      {showStepNumber && !isComplete && !isFailed && currentStepIndex >= 0 && (
        <Box marginTop={1}>
          <Text color="gray">
            Step {currentStepIndex + 1}/{steps.length}
            {steps[currentStepIndex]?.message &&
              ` - ${steps[currentStepIndex].message}`}
          </Text>
        </Box>
      )}

      {/* Completion message */}
      {isComplete && (
        <Box marginTop={1}>
          <Text color="green">✅ All steps completed!</Text>
        </Box>
      )}

      {/* Error message */}
      {isFailed && error && (
        <Box marginTop={1}>
          <Text color="red">Error: {error}</Text>
        </Box>
      )}
    </Box>
  );
};

export default ProgressSteps;

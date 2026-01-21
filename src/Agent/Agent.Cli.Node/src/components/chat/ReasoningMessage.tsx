/**
 * Reasoning/Thinking message - CLI version
 * Shows thinking steps with timeline bullets like the web version
 * Ported from Agent.Web\Client ReasoningChatMessage.tsx
 */
import React, { useState, useEffect } from 'react';
import { Box, Text } from 'ink';
import { theme } from '../../theme';

export interface ReasoningStep {
  id: string;
  title: string;
  details?: string;
}

export interface ReasoningMessageProps {
  steps: ReasoningStep[];
  active?: boolean;
  title?: string;
  elapsedTime?: string;
}

// Pulsing bullet animation frames
const PULSE_FRAMES = ['●', '◉', '○', '◉'];

export const ReasoningMessage: React.FC<ReasoningMessageProps> = ({
  steps,
  active = false,
  title = 'Thought process',
  elapsedTime,
}) => {
  const [pulseFrame, setPulseFrame] = useState(0);
  const [expanded, setExpanded] = useState(active);

  // Pulsing animation for active step
  useEffect(() => {
    if (!active) return;
    const timer = setInterval(() => {
      setPulseFrame((f) => (f + 1) % PULSE_FRAMES.length);
    }, 300);
    return () => clearInterval(timer);
  }, [active]);

  // Auto-expand when active
  useEffect(() => {
    if (active) setExpanded(true);
  }, [active]);

  if (steps.length === 0) return null;

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Header */}
      <Box>
        <Text color={theme.ink.muted}>┌─ </Text>
        <Text bold color={active ? theme.ink.warning : theme.ink.muted}>
          {active ? steps[steps.length - 1]?.title || 'Thinking...' : title}
        </Text>
        {elapsedTime && !active && (
          <Text color={theme.ink.muted}> ({elapsedTime})</Text>
        )}
        {!active && (
          <Text color={theme.ink.muted}>
            {' '}[{expanded ? '−' : '+'}]
          </Text>
        )}
      </Box>

      {/* Steps timeline */}
      {expanded && (
        <Box flexDirection="column" paddingLeft={1}>
          {steps.map((step, index) => {
            const isLast = index === steps.length - 1;
            const isActive = active && isLast;
            const bullet = isActive ? PULSE_FRAMES[pulseFrame] : '●';
            const bulletColor = isActive ? theme.ink.warning : theme.ink.muted;

            return (
              <Box key={step.id} flexDirection="column">
                {/* Step line */}
                <Box>
                  <Text color={theme.ink.muted}>│ </Text>
                  <Text color={bulletColor}>{bullet}</Text>
                  <Text> </Text>
                  <Text bold={isActive} color={isActive ? theme.ink.text : theme.ink.muted}>
                    {step.title}
                  </Text>
                </Box>

                {/* Step details */}
                {step.details && (
                  <Box paddingLeft={4}>
                    <Text color={theme.ink.muted}>│   </Text>
                    <Text color={theme.ink.muted} wrap="wrap">
                      {step.details.length > 100
                        ? step.details.slice(0, 100) + '...'
                        : step.details}
                    </Text>
                  </Box>
                )}

                {/* Vertical connector line */}
                {!isLast && (
                  <Box>
                    <Text color={theme.ink.muted}>│ │</Text>
                  </Box>
                )}
              </Box>
            );
          })}
        </Box>
      )}

      {/* Footer */}
      <Box>
        <Text color={theme.ink.muted}>└─</Text>
        {!expanded && steps.length > 0 && (
          <Text color={theme.ink.muted}> {steps.length} step{steps.length !== 1 ? 's' : ''}</Text>
        )}
      </Box>
    </Box>
  );
};

export default ReasoningMessage;

/**
 * WizardPrompt - Multi-step interactive wizard component
 *
 * Claude Code style numbered selection with keyboard navigation
 */
import React, { useState } from 'react';
import { Box, Text, useInput } from 'ink';
import { theme } from '../../theme';
import type { WizardStep, WizardOption } from '../../commands/types';

export interface WizardPromptProps {
  step: WizardStep;
  stepNumber: number;
  totalSteps: number;
  onSelect: (value: string) => void;
  onCancel: () => void;
  onBack?: () => void;
}

export const WizardPrompt: React.FC<WizardPromptProps> = ({
  step,
  stepNumber,
  totalSteps,
  onSelect,
  onCancel,
  onBack,
}) => {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [inputValue, setInputValue] = useState(step.defaultValue || '');

  const options = step.options || [];

  useInput((input, key) => {
    if (step.type === 'select') {
      // Number key selection (1-9)
      const num = parseInt(input, 10);
      if (num >= 1 && num <= options.length) {
        onSelect(options[num - 1].key);
        return;
      }

      // Arrow navigation
      if (key.upArrow) {
        setSelectedIndex((i) => Math.max(0, i - 1));
        return;
      }
      if (key.downArrow) {
        setSelectedIndex((i) => Math.min(options.length - 1, i + 1));
        return;
      }

      // Enter to select
      if (key.return) {
        onSelect(options[selectedIndex].key);
        return;
      }
    } else if (step.type === 'input') {
      // Text input mode
      if (key.return) {
        onSelect(inputValue);
        return;
      }
      if (key.backspace || key.delete) {
        setInputValue((v) => v.slice(0, -1));
        return;
      }
      if (input && !key.ctrl && !key.meta) {
        setInputValue((v) => v + input);
        return;
      }
    } else if (step.type === 'confirm') {
      // Y/N confirmation
      if (input === 'y' || input === 'Y') {
        onSelect('yes');
        return;
      }
      if (input === 'n' || input === 'N') {
        onSelect('no');
        return;
      }
      if (key.return) {
        onSelect(selectedIndex === 0 ? 'yes' : 'no');
        return;
      }
      if (key.upArrow || key.downArrow) {
        setSelectedIndex((i) => (i === 0 ? 1 : 0));
        return;
      }
    }

    // Escape to cancel
    if (key.escape) {
      onCancel();
      return;
    }

    // Backspace at start to go back
    if ((key.backspace || key.delete) && step.type === 'select' && onBack) {
      onBack();
    }
  });

  return (
    <Box flexDirection="column" marginTop={1}>
      {/* Step progress */}
      <Box marginBottom={1}>
        <Text color={theme.ink.muted}>
          Step {stepNumber} of {totalSteps}
        </Text>
      </Box>

      {/* Step title */}
      <Box marginBottom={1}>
        <Text bold color={theme.ink.brand}>
          {step.title}
        </Text>
      </Box>

      {/* Prompt */}
      <Box marginBottom={1}>
        <Text>{step.prompt}</Text>
      </Box>

      {/* Options for select type */}
      {step.type === 'select' && (
        <Box flexDirection="column">
          {options.map((option, index) => (
            <Box key={option.key}>
              <Text color={index === selectedIndex ? theme.ink.brand : 'white'}>
                {index === selectedIndex ? '› ' : '  '}
              </Text>
              <Text color={theme.ink.muted}>{index + 1}. </Text>
              <Text color={index === selectedIndex ? theme.ink.brand : 'white'} bold={index === selectedIndex}>
                {option.label}
              </Text>
              {option.description && (
                <Text color={theme.ink.muted}> - {option.description}</Text>
              )}
            </Box>
          ))}
        </Box>
      )}

      {/* Input field for input type */}
      {step.type === 'input' && (
        <Box>
          <Text color={theme.ink.brand}>{'> '}</Text>
          <Text>{inputValue}</Text>
          <Text backgroundColor={theme.ink.brand} color="black">{' '}</Text>
        </Box>
      )}

      {/* Confirm options */}
      {step.type === 'confirm' && (
        <Box flexDirection="column">
          <Box>
            <Text color={selectedIndex === 0 ? theme.ink.brand : 'white'}>
              {selectedIndex === 0 ? '› ' : '  '}
            </Text>
            <Text color={theme.ink.muted}>Y. </Text>
            <Text color={selectedIndex === 0 ? theme.ink.brand : 'white'} bold={selectedIndex === 0}>
              Yes
            </Text>
          </Box>
          <Box>
            <Text color={selectedIndex === 1 ? theme.ink.brand : 'white'}>
              {selectedIndex === 1 ? '› ' : '  '}
            </Text>
            <Text color={theme.ink.muted}>N. </Text>
            <Text color={selectedIndex === 1 ? theme.ink.brand : 'white'} bold={selectedIndex === 1}>
              No
            </Text>
          </Box>
        </Box>
      )}

      {/* Hint */}
      <Box marginTop={1}>
        <Text color={theme.ink.muted} dimColor>
          {step.type === 'select'
            ? 'Use ↑↓ or number keys to select · Enter to confirm · Esc to cancel'
            : step.type === 'input'
              ? 'Type your answer · Enter to confirm · Esc to cancel'
              : 'Y/N or ↑↓ to select · Enter to confirm · Esc to cancel'}
        </Text>
      </Box>
    </Box>
  );
};

export default WizardPrompt;

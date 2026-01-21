/**
 * ChoicePrompt - Claude Code style blocking choice prompt
 *
 * Features:
 * - Numbered options (1. Yes, 2. Yes allow..., 3. No)
 * - Selected indicator (›)
 * - Keyboard hints at bottom
 * - Blocking input capture
 * - Support for descriptions per option
 */
import React, { useState, useCallback } from 'react';
import { Box, Text, useInput, useApp } from 'ink';
import { theme } from '../../theme';

export interface ChoiceOption<T = string> {
  label: string;
  value: T;
  description?: string;
}

export interface ChoicePromptProps<T = string> {
  /** The question to display */
  question: string;
  /** Available choices */
  options: ChoiceOption<T>[];
  /** Called when user selects an option */
  onSelect: (option: ChoiceOption<T>) => void;
  /** Called when user cancels (Esc) */
  onCancel?: () => void;
  /** Initial selected index */
  initialIndex?: number;
  /** Show keyboard hints at bottom */
  showHints?: boolean;
  /** Custom hint text */
  hintText?: string;
  /** Whether to show numbers before options */
  showNumbers?: boolean;
}

export function ChoicePrompt<T = string>({
  question,
  options,
  onSelect,
  onCancel,
  initialIndex = 0,
  showHints = true,
  hintText,
  showNumbers = true,
}: ChoicePromptProps<T>): React.ReactElement {
  const [selectedIndex, setSelectedIndex] = useState(initialIndex);
  const { exit } = useApp();

  const handleSelect = useCallback(() => {
    const selectedOption = options[selectedIndex];
    if (selectedOption) {
      onSelect(selectedOption);
    }
  }, [selectedIndex, options, onSelect]);

  useInput((input, key) => {
    // Navigate up
    if (key.upArrow) {
      setSelectedIndex((prev) => (prev > 0 ? prev - 1 : options.length - 1));
      return;
    }

    // Navigate down
    if (key.downArrow) {
      setSelectedIndex((prev) => (prev < options.length - 1 ? prev + 1 : 0));
      return;
    }

    // Select on Enter
    if (key.return) {
      handleSelect();
      return;
    }

    // Cancel on Escape
    if (key.escape) {
      if (onCancel) {
        onCancel();
      } else {
        exit();
      }
      return;
    }

    // Number shortcuts (1-9)
    const num = parseInt(input, 10);
    if (!isNaN(num) && num >= 1 && num <= options.length) {
      setSelectedIndex(num - 1);
      // Auto-select on number press
      const selectedOption = options[num - 1];
      if (selectedOption) {
        onSelect(selectedOption);
      }
      return;
    }
  });

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Question */}
      <Box marginBottom={1}>
        <Text bold>{question}</Text>
      </Box>

      {/* Options */}
      <Box flexDirection="column">
        {options.map((option, index) => {
          const isSelected = index === selectedIndex;
          const number = index + 1;

          return (
            <Box key={String(option.value)} flexDirection="row">
              {/* Selection indicator */}
              <Text color={isSelected ? theme.ink.brand : 'gray'}>
                {isSelected ? '› ' : '  '}
              </Text>

              {/* Number */}
              {showNumbers && (
                <Text color={isSelected ? theme.ink.brand : 'white'}>
                  {number}.{' '}
                </Text>
              )}

              {/* Label */}
              <Text
                color={isSelected ? theme.ink.brand : 'white'}
                bold={isSelected}
              >
                {option.label}
              </Text>

              {/* Description */}
              {option.description && (
                <Text color="gray"> {option.description}</Text>
              )}
            </Box>
          );
        })}
      </Box>

      {/* Keyboard hints */}
      {showHints && (
        <Box marginTop={1}>
          <Text color="gray" dimColor>
            {hintText || 'Esc to cancel · Tab to add additional instructions'}
          </Text>
        </Box>
      )}
    </Box>
  );
}

/**
 * Simple Yes/No/Cancel choice prompt
 */
export interface ConfirmChoiceProps {
  question: string;
  onConfirm: () => void;
  onDeny: () => void;
  onCancel?: () => void;
  confirmLabel?: string;
  denyLabel?: string;
  showHints?: boolean;
}

export const ConfirmChoice: React.FC<ConfirmChoiceProps> = ({
  question,
  onConfirm,
  onDeny,
  onCancel,
  confirmLabel = 'Yes',
  denyLabel = 'No',
  showHints = true,
}) => {
  const options: ChoiceOption<'confirm' | 'deny'>[] = [
    { label: confirmLabel, value: 'confirm' },
    { label: denyLabel, value: 'deny' },
  ];

  const handleSelect = (option: ChoiceOption<'confirm' | 'deny'>) => {
    if (option.value === 'confirm') {
      onConfirm();
    } else {
      onDeny();
    }
  };

  return (
    <ChoicePrompt
      question={question}
      options={options}
      onSelect={handleSelect}
      onCancel={onCancel}
      showHints={showHints}
    />
  );
};

/**
 * Permission choice prompt (like Claude Code's bash permission)
 */
export interface PermissionChoiceProps {
  command: string;
  description: string;
  onAllow: () => void;
  onAllowForProject: () => void;
  onDeny: () => void;
}

export const PermissionChoice: React.FC<PermissionChoiceProps> = ({
  command,
  description,
  onAllow,
  onAllowForProject,
  onDeny,
}) => {
  const options: ChoiceOption<'allow' | 'allow-project' | 'deny'>[] = [
    { label: 'Yes', value: 'allow' },
    {
      label: 'Yes, allow reading from this project',
      value: 'allow-project',
      description: '(remembers for session)',
    },
    { label: 'No', value: 'deny' },
  ];

  const handleSelect = (
    option: ChoiceOption<'allow' | 'allow-project' | 'deny'>
  ) => {
    switch (option.value) {
      case 'allow':
        onAllow();
        break;
      case 'allow-project':
        onAllowForProject();
        break;
      case 'deny':
        onDeny();
        break;
    }
  };

  return (
    <Box flexDirection="column" marginY={1}>
      {/* Command display */}
      <Box
        borderStyle="single"
        borderColor="yellow"
        paddingX={1}
        marginBottom={1}
      >
        <Box flexDirection="column">
          <Text bold color="yellow">
            Bash command
          </Text>
          <Box marginTop={1}>
            <Text color="white">{command}</Text>
          </Box>
          <Text color="gray">{description}</Text>
        </Box>
      </Box>

      {/* Choice */}
      <ChoicePrompt
        question="Do you want to proceed?"
        options={options}
        onSelect={handleSelect}
        onCancel={onDeny}
      />
    </Box>
  );
};

export default ChoicePrompt;

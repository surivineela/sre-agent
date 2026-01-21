/**
 * Confirm input component for yes/no prompts
 */
import React, { useState } from 'react';
import { Box, Text, useInput } from 'ink';

export interface ConfirmInputProps {
  message: string;
  defaultValue?: boolean;
  onConfirm: (confirmed: boolean) => void;
  yesLabel?: string;
  noLabel?: string;
  color?: string;
}

export const ConfirmInput: React.FC<ConfirmInputProps> = ({
  message,
  defaultValue = false,
  onConfirm,
  yesLabel = 'Yes',
  noLabel = 'No',
  color = 'yellow',
}) => {
  const [value, setValue] = useState(defaultValue);

  useInput((input, key) => {
    // Toggle with left/right arrows
    if (key.leftArrow || key.rightArrow) {
      setValue((prev) => !prev);
      return;
    }

    // Confirm on Enter
    if (key.return) {
      onConfirm(value);
      return;
    }

    // Yes shortcuts
    if (input.toLowerCase() === 'y') {
      setValue(true);
      onConfirm(true);
      return;
    }

    // No shortcuts
    if (input.toLowerCase() === 'n') {
      setValue(false);
      onConfirm(false);
      return;
    }

    // Escape = no
    if (key.escape) {
      onConfirm(false);
      return;
    }
  });

  return (
    <Box>
      <Text color={color}>? </Text>
      <Text>{message} </Text>
      <Text color="gray">[</Text>
      <Text color={value ? 'green' : 'gray'} bold={value}>
        {yesLabel}
      </Text>
      <Text color="gray">/</Text>
      <Text color={!value ? 'red' : 'gray'} bold={!value}>
        {noLabel}
      </Text>
      <Text color="gray">]</Text>
    </Box>
  );
};

export default ConfirmInput;

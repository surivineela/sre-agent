/**
 * InlineApproval - Claude Code style approval prompt for CLI commands
 * Shows approve/deny options with text input for feedback
 */
import React, { useState } from 'react';
import { Box, Text, useInput } from 'ink';

export interface InlineApprovalProps {
  command: string;
  onApprove: () => void;
  onDeny: () => void;
  onSendMessage?: (message: string) => void;
}

// Truncate command for display
function truncateCommand(command: string, maxLen: number = 70): string {
  const trimmed = command.trim();
  if (trimmed.length <= maxLen) return trimmed;
  return trimmed.slice(0, maxLen - 3) + '...';
}

type Mode = 'select' | 'input';

export const InlineApproval: React.FC<InlineApprovalProps> = ({
  command,
  onApprove,
  onDeny,
  onSendMessage,
}) => {
  const [mode, setMode] = useState<Mode>('select');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [inputValue, setInputValue] = useState('');

  const options = [
    { label: 'Yes', action: onApprove },
    { label: 'No', action: onDeny },
  ];

  useInput((input, key) => {
    if (mode === 'select') {
      // Arrow key navigation
      if (key.upArrow) {
        setSelectedIndex(i => Math.max(0, i - 1));
        return;
      }
      if (key.downArrow) {
        setSelectedIndex(i => Math.min(options.length - 1, i + 1));
        return;
      }

      // Enter to confirm selection
      if (key.return) {
        options[selectedIndex].action();
        return;
      }

      // Escape to deny
      if (key.escape) {
        onDeny();
        return;
      }

      // Quick keys
      const lower = input.toLowerCase();
      if (lower === 'y') {
        onApprove();
      } else if (lower === 'n') {
        onDeny();
      } else if (input && !key.ctrl && !key.meta) {
        // Start typing - switch to input mode
        setMode('input');
        setInputValue(input);
      }
    } else {
      // Input mode
      if (key.return) {
        if (inputValue.trim() && onSendMessage) {
          onSendMessage(inputValue.trim());
        }
        return;
      }

      if (key.escape) {
        // Cancel input, go back to select mode
        setMode('select');
        setInputValue('');
        return;
      }

      if (key.backspace || key.delete) {
        setInputValue(v => v.slice(0, -1));
        if (inputValue.length <= 1) {
          setMode('select');
          setInputValue('');
        }
        return;
      }

      // Add character
      if (input && !key.ctrl && !key.meta) {
        setInputValue(v => v + input);
      }
    }
  });

  const displayCommand = truncateCommand(command);

  return (
    <Box flexDirection="column" marginTop={1}>
      {/* Question */}
      <Box>
        <Text color="yellow">? </Text>
        <Text>Allow </Text>
        <Text color="cyan">{displayCommand}</Text>
        <Text>?</Text>
      </Box>

      {mode === 'select' ? (
        <>
          {/* Options */}
          <Box flexDirection="column" marginLeft={2}>
            {options.map((opt, i) => (
              <Box key={opt.label}>
                <Text color={selectedIndex === i ? 'cyan' : 'gray'}>
                  {selectedIndex === i ? '❯ ' : '  '}
                </Text>
                <Text color={selectedIndex === i ? 'white' : 'gray'}>
                  {opt.label}
                </Text>
              </Box>
            ))}
          </Box>
          {/* Hint */}
          <Box marginLeft={2} marginTop={1}>
            <Text color="gray" dimColor>↑↓ select · enter confirm · or type a message</Text>
          </Box>
        </>
      ) : (
        /* Text input mode */
        <Box marginLeft={2} marginTop={1}>
          <Text color="cyan">&gt; </Text>
          <Text>{inputValue}</Text>
          <Text color="cyan">▋</Text>
        </Box>
      )}
    </Box>
  );
};

export default InlineApproval;

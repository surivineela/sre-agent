/**
 * System message component - Claude Code style formatted notifications
 * Displays info, success, warning, error, divider, and hint messages
 */
import React from 'react';
import { Box, Text, useStdout } from 'ink';
import type { SystemMessage as SystemMessageType } from '../../types';

export interface SystemMessageProps {
  message: SystemMessageType;
}

const TYPE_CONFIG: Record<string, { icon: string; color: string; bordered: boolean }> = {
  info: { icon: '', color: 'gray', bordered: false },
  success: { icon: '✓', color: 'green', bordered: false },
  warning: { icon: '⚠', color: 'yellow', bordered: true },
  error: { icon: '✗', color: 'red', bordered: true },
  divider: { icon: '', color: 'gray', bordered: false },
  hint: { icon: '💡', color: 'cyan', bordered: false },
};

export const SystemMessage: React.FC<SystemMessageProps> = ({ message }) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 80;
  const config = TYPE_CONFIG[message.type] || TYPE_CONFIG.info;

  // Divider style - centered text with lines on both sides
  if (message.type === 'divider') {
    const text = message.content;
    const lineLength = Math.floor((terminalWidth - text.length - 4) / 2);
    const line = '━'.repeat(Math.max(4, lineLength));
    return (
      <Box justifyContent="center" marginY={1}>
        <Text color="gray">{line} </Text>
        <Text color="gray" bold>{text}</Text>
        <Text color="gray"> {line}</Text>
      </Box>
    );
  }

  // Info style (centered, subtle) - for untitled info messages
  if (message.type === 'info' && !message.title) {
    const text = message.content;
    const lineLength = Math.floor((terminalWidth - text.length - 8) / 2);
    const line = '─'.repeat(Math.max(4, lineLength));
    return (
      <Box justifyContent="center" marginY={1}>
        <Text color="gray" dimColor>{line} {text} {line}</Text>
      </Box>
    );
  }

  // Bordered style (warning, error, or titled info)
  if (config.bordered || message.title) {
    const title = message.title || message.type.charAt(0).toUpperCase() + message.type.slice(1);
    const boxWidth = Math.min(70, terminalWidth - 4);
    const contentWidth = boxWidth - 4; // Account for borders and padding

    // Word wrap content
    const wrapText = (text: string, width: number): string[] => {
      const words = text.split(' ');
      const lines: string[] = [];
      let currentLine = '';

      for (const word of words) {
        if (currentLine.length + word.length + 1 <= width) {
          currentLine += (currentLine ? ' ' : '') + word;
        } else {
          if (currentLine) lines.push(currentLine);
          currentLine = word;
        }
      }
      if (currentLine) lines.push(currentLine);
      return lines;
    };

    const contentLines = wrapText(message.content, contentWidth);

    return (
      <Box
        flexDirection="column"
        borderStyle="round"
        borderColor={config.color}
        paddingX={1}
        marginY={1}
        width={boxWidth}
      >
        {/* Header */}
        <Box>
          {config.icon && <Text color={config.color}>{config.icon} </Text>}
          <Text color={config.color} bold>{title}</Text>
        </Box>

        {/* Content */}
        <Box flexDirection="column" marginTop={1}>
          {contentLines.map((line, i) => (
            <Text key={i}>{line}</Text>
          ))}
        </Box>

        {/* Action hint */}
        {message.action && (
          <Box marginTop={1}>
            <Text color="cyan">[{message.action.label}]</Text>
            <Text color="gray"> → {message.action.command}</Text>
          </Box>
        )}
      </Box>
    );
  }

  // Simple inline style (success, hint, untitled warning)
  return (
    <Box marginY={1}>
      {config.icon && <Text color={config.color}>{config.icon} </Text>}
      <Text color={config.color}>{message.content}</Text>
    </Box>
  );
};

export default SystemMessage;

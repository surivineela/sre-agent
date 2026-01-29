/**
 * Header component - Two-column layout with vertical divider
 */
import React from 'react';
import { Box, Text, useStdout } from 'ink';
import type { ConnectionStatus } from '../../types';
import { LOGO_MINI, BABY_PINK, BABY_BLUE } from '../../theme';
import stringWidth from 'string-width';

export interface HeaderProps {
  connectionStatus?: ConnectionStatus;
  serverUrl?: string;
  remoteAgentName?: string;
  userName?: string;
}

export const Header: React.FC<HeaderProps> = ({
  connectionStatus = 'disconnected',
  serverUrl,
  remoteAgentName,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 120;

  // Layout calculations
  const boxWidth = Math.max(60, terminalWidth - 2);
  const innerWidth = boxWidth - 2; // Account for left and right │
  const leftColWidth = Math.floor(innerWidth * 0.45);
  const rightColWidth = innerWidth - leftColWidth - 1; // -1 for middle │

  // Connection status
  const getStatusInfo = () => {
    switch (connectionStatus) {
      case 'connected':
        return { symbol: '●', color: 'green' as const, text: 'Connected' };
      case 'connecting':
        return { symbol: '◐', color: 'yellow' as const, text: 'Connecting...' };
      default:
        return { symbol: '○', color: 'red' as const, text: 'Offline' };
    }
  };
  const status = getStatusInfo();

  // Server name (truncated)
  const getServerName = () => {
    if (!serverUrl) return 'Not configured';
    try {
      const url = new URL(serverUrl);
      const name = url.hostname;
      const maxLen = rightColWidth - 2;
      return name.length > maxLen ? name.slice(0, maxLen - 3) + '...' : name;
    } catch {
      return serverUrl.slice(0, rightColWidth - 2);
    }
  };

  // Current directory (truncated)
  const cwd = process.cwd();
  const cwdDisplay = cwd.length > leftColWidth - 2
    ? '...' + cwd.slice(-(leftColWidth - 5))
    : cwd;

  // Get username
  const user = process.env.USER || process.env.USERNAME || 'User';

  // Pad text to width (accounting for visual width)
  const padRight = (text: string, width: number): string => {
    const visualWidth = stringWidth(text);
    const pad = Math.max(0, width - visualWidth);
    return text + ' '.repeat(pad);
  };

  // Center text (accounting for visual width)
  const centerText = (text: string, width: number): string => {
    const visualWidth = stringWidth(text);
    const totalPad = Math.max(0, width - visualWidth);
    const leftPad = Math.floor(totalPad / 2);
    const rightPad = totalPad - leftPad;
    return ' '.repeat(leftPad) + text + ' '.repeat(rightPad);
  };

  // Create a row with proper padding
  const createRow = (leftText: string, rightText: string, leftColor?: string, rightColor?: string, leftBold?: boolean, rightBold?: boolean) => {
    const paddedLeft = padRight(leftText, leftColWidth);
    const paddedRight = padRight(rightText, rightColWidth);
    return (
      <Box>
        <Text color="gray">│</Text>
        <Text color={leftColor} bold={leftBold}>{paddedLeft}</Text>
        <Text color="gray">│</Text>
        <Text color={rightColor} bold={rightBold}>{paddedRight}</Text>
        <Text color="gray">│</Text>
      </Box>
    );
  };

  // Top border: ╭─ + title + ─...─ + ╮
  // Total: 2 (╭─) + title + dashes + 1 (╮) = boxWidth
  const title = ' SRE Agent v0.0.1 ';
  const topLineLen = boxWidth - 3 - stringWidth(title); // 3 = ╭─ (2) + ╮ (1)

  // Status display text
  const statusText = `${status.symbol} ${status.text}${remoteAgentName ? ` · ${remoteAgentName}` : ''}`;

  return (
    <Box flexDirection="column" marginBottom={1}>
      {/* Top border */}
      <Box>
        <Text color="gray">╭─</Text>
        <Text color={BABY_PINK} bold>{title}</Text>
        <Text color="gray">{'─'.repeat(Math.max(0, topLineLen))}╮</Text>
      </Box>

      {/* Row 1: Empty */}
      {createRow('', '')}

      {/* Row 2: Welcome | Tips header */}
      {createRow(centerText(`Welcome back, ${user}!`, leftColWidth), ' Tips for getting started', 'white', BABY_BLUE, true, true)}

      {/* Row 3: Empty | /init tip */}
      {createRow('', ' Run /init to configure your workspace', undefined, 'white')}

      {/* Row 4: Logo line 1 | /agent tip */}
      {createRow(centerText(LOGO_MINI[0], leftColWidth), ' Run /agent to create or manage agents', BABY_BLUE, 'white')}

      {/* Row 5: Logo line 2 | /help tip */}
      {createRow(centerText(LOGO_MINI[1], leftColWidth), ' Run /help for all available commands', BABY_BLUE, 'white')}

      {/* Row 6: Logo line 3 | Empty */}
      {createRow(centerText(LOGO_MINI[2], leftColWidth), '', BABY_BLUE)}

      {/* Row 7: Empty | Server header */}
      {createRow('', ' Server', undefined, BABY_BLUE, false, true)}

      {/* Row 8: Status | Server name */}
      {createRow(centerText(statusText, leftColWidth), ` ${getServerName()}`, status.color, 'gray')}

      {/* Row 9: CWD | Empty */}
      {createRow(centerText(cwdDisplay, leftColWidth), '', 'gray')}

      {/* Bottom border */}
      <Box>
        <Text color="gray">╰{'─'.repeat(boxWidth - 2)}╯</Text>
      </Box>
    </Box>
  );
};

export default Header;

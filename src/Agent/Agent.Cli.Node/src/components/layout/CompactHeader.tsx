/**
 * CompactHeader - Minimal status bar header (Claude Code style)
 * Maximum 3 lines including border, shows essential status info only
 */
import React from 'react';
import { Box, Text, useStdout } from 'ink';
import type { ConnectionStatus } from '../../types';
import { theme } from '../../theme';

export interface CompactHeaderProps {
  connectionStatus?: ConnectionStatus;
  serverUrl?: string;
  remoteAgentName?: string;
}

export const CompactHeader: React.FC<CompactHeaderProps> = ({
  connectionStatus = 'disconnected',
  serverUrl,
  remoteAgentName,
}) => {
  const { stdout } = useStdout();
  const terminalWidth = stdout?.columns || 80;

  // Connection status indicator
  const getStatusInfo = () => {
    switch (connectionStatus) {
      case 'connected':
        return { symbol: '●', color: 'green' as const };
      case 'connecting':
        return { symbol: '◐', color: 'yellow' as const };
      default:
        return { symbol: '○', color: 'red' as const };
    }
  };
  const status = getStatusInfo();

  // Server name (hostname only)
  const getServerName = () => {
    if (!serverUrl) return 'Not configured';
    try {
      const url = new URL(serverUrl);
      return url.hostname + (url.port ? ':' + url.port : '');
    } catch {
      return serverUrl.slice(0, 30);
    }
  };

  // Build title
  const title = ' SRE Agent ';

  // Build the status line content
  const statusParts: string[] = [];
  statusParts.push(getServerName());
  if (remoteAgentName) {
    statusParts.push(`Agent: ${remoteAgentName}`);
  }
  statusParts.push('/help for commands');

  const statusLine = statusParts.join(' · ');

  // Calculate border widths
  const titleLineLeft = '╭─';
  const titleLineRight = '─'.repeat(Math.max(0, terminalWidth - titleLineLeft.length - title.length - 2)) + '╮';
  const bottomBorder = '─'.repeat(Math.max(0, terminalWidth - 2));

  return (
    <Box flexDirection="column" marginBottom={1}>
      {/* Top border with title */}
      <Box>
        <Text color="gray">{titleLineLeft}</Text>
        <Text color={theme.ink.brand} bold>{title}</Text>
        <Text color="gray">{titleLineRight}</Text>
      </Box>

      {/* Single status line */}
      <Box>
        <Text color="gray">│ </Text>
        <Text color={status.color}>{status.symbol}</Text>
        <Text color="gray"> {statusLine}</Text>
        <Box flexGrow={1} />
        <Text color="gray"> │</Text>
      </Box>

      {/* Bottom border */}
      <Box>
        <Text color="gray">╰{bottomBorder}╯</Text>
      </Box>
    </Box>
  );
};

export default CompactHeader;

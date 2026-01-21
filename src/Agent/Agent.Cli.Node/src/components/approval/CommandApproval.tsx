/**
 * CommandApproval - Full-screen blocking approval for CLI commands
 * Provides Claude Code-style approval experience for az cli and kubectl
 */
import React, { useState, useMemo } from 'react';
import { Box, Text, useInput } from 'ink';

export type CommandType = 'az' | 'kubectl' | 'shell' | 'unknown';
export type RiskLevel = 'read' | 'write' | 'delete' | 'admin';

export interface CommandApprovalProps {
  command: string;
  description?: string;
  workingDirectory?: string;
  onApprove: () => void;
  onApproveAlways?: () => void;
  onDeny: () => void;
  showAlwaysOption?: boolean;
}

// Detect command type from command string
function detectCommandType(command: string): CommandType {
  const trimmed = command.trim().toLowerCase();
  if (trimmed.startsWith('az ')) return 'az';
  if (trimmed.startsWith('kubectl ') || trimmed.startsWith('k ')) return 'kubectl';
  if (trimmed.includes('bash') || trimmed.includes('sh ') || trimmed.includes('powershell')) return 'shell';
  return 'unknown';
}

// Detect risk level based on command
function detectRiskLevel(command: string, type: CommandType): RiskLevel {
  const lower = command.toLowerCase();

  // Delete operations
  const deleteKeywords = ['delete', 'remove', 'rm ', 'destroy', 'purge', 'drop'];
  if (deleteKeywords.some(k => lower.includes(k))) return 'delete';

  // Admin operations
  const adminKeywords = ['role', 'rbac', 'policy', 'identity', 'credential', 'secret', 'keyvault'];
  if (adminKeywords.some(k => lower.includes(k))) return 'admin';

  // Write operations
  const writeKeywords = ['create', 'update', 'set', 'add', 'apply', 'patch', 'scale', 'restart', 'stop', 'start'];
  if (writeKeywords.some(k => lower.includes(k))) return 'write';

  // Read operations (default)
  return 'read';
}

// Parse and syntax highlight command
interface CommandToken {
  text: string;
  type: 'command' | 'subcommand' | 'flag' | 'value' | 'resource' | 'normal';
}

function tokenizeCommand(command: string, cmdType: CommandType): CommandToken[] {
  const tokens: CommandToken[] = [];
  const parts = command.split(/(\s+)/);

  let isFirstWord = true;
  let expectValue = false;

  for (const part of parts) {
    if (!part || /^\s+$/.test(part)) {
      if (part) tokens.push({ text: part, type: 'normal' });
      continue;
    }

    if (isFirstWord) {
      tokens.push({ text: part, type: 'command' });
      isFirstWord = false;
      continue;
    }

    if (part.startsWith('--') || part.startsWith('-')) {
      tokens.push({ text: part, type: 'flag' });
      expectValue = true;
      continue;
    }

    if (expectValue) {
      tokens.push({ text: part, type: 'value' });
      expectValue = false;
      continue;
    }

    // Check for resource identifiers
    if (part.startsWith('/subscriptions/') || part.includes('resourcegroups')) {
      tokens.push({ text: part, type: 'resource' });
      continue;
    }

    // Subcommands for az/kubectl
    if (cmdType === 'az' || cmdType === 'kubectl') {
      const subcommands = ['get', 'list', 'show', 'describe', 'create', 'delete', 'update', 'apply', 'logs', 'exec', 'group', 'account', 'vm', 'aks', 'storage', 'network', 'pods', 'deployments', 'services', 'nodes', 'namespace', 'configmap', 'secret'];
      if (subcommands.includes(part.toLowerCase())) {
        tokens.push({ text: part, type: 'subcommand' });
        continue;
      }
    }

    tokens.push({ text: part, type: 'normal' });
  }

  return tokens;
}

// Get color for token type
function getTokenColor(type: CommandToken['type']): string {
  switch (type) {
    case 'command': return 'cyan';
    case 'subcommand': return 'green';
    case 'flag': return 'yellow';
    case 'value': return 'magenta';
    case 'resource': return 'blue';
    default: return 'white';
  }
}

// Get risk level display config
function getRiskConfig(level: RiskLevel) {
  switch (level) {
    case 'delete':
      return { color: 'red', label: 'DESTRUCTIVE', icon: '⚠' };
    case 'admin':
      return { color: 'yellow', label: 'ADMIN', icon: '🔐' };
    case 'write':
      return { color: 'yellow', label: 'WRITE', icon: '✏' };
    default:
      return { color: 'green', label: 'READ', icon: '👁' };
  }
}

// Get command type display
function getCommandTypeDisplay(type: CommandType) {
  switch (type) {
    case 'az':
      return { label: 'Azure CLI', color: 'blue', icon: '☁' };
    case 'kubectl':
      return { label: 'Kubernetes', color: 'cyan', icon: '☸' };
    case 'shell':
      return { label: 'Shell', color: 'yellow', icon: '>' };
    default:
      return { label: 'Command', color: 'gray', icon: '$' };
  }
}

export const CommandApproval: React.FC<CommandApprovalProps> = ({
  command,
  description,
  workingDirectory,
  onApprove,
  onApproveAlways,
  onDeny,
  showAlwaysOption = true,
}) => {
  const [selectedIndex, setSelectedIndex] = useState(0);

  const commandType = useMemo(() => detectCommandType(command), [command]);
  const riskLevel = useMemo(() => detectRiskLevel(command, commandType), [command, commandType]);
  const tokens = useMemo(() => tokenizeCommand(command, commandType), [command, commandType]);
  const riskConfig = getRiskConfig(riskLevel);
  const typeConfig = getCommandTypeDisplay(commandType);

  const options = useMemo(() => {
    const opts = [
      { key: '1', label: 'Yes, run this command', action: onApprove },
      { key: '3', label: 'No, deny', action: onDeny },
    ];
    if (showAlwaysOption && onApproveAlways) {
      opts.splice(1, 0, { key: '2', label: 'Yes, allow always for this session', action: onApproveAlways });
    }
    return opts;
  }, [onApprove, onApproveAlways, onDeny, showAlwaysOption]);

  useInput((input, key) => {
    // Number keys for direct selection
    const numKey = parseInt(input);
    if (numKey >= 1 && numKey <= options.length) {
      options[numKey - 1].action();
      return;
    }

    // Arrow key navigation
    if (key.upArrow) {
      setSelectedIndex(i => Math.max(0, i - 1));
    } else if (key.downArrow) {
      setSelectedIndex(i => Math.min(options.length - 1, i + 1));
    }

    // Enter to confirm selection
    if (key.return) {
      options[selectedIndex].action();
    }

    // Y/N shortcuts
    if (input === 'y' || input === 'Y') {
      onApprove();
    } else if (input === 'n' || input === 'N' || key.escape) {
      onDeny();
    }
  });

  return (
    <Box flexDirection="column" borderStyle="round" borderColor={riskConfig.color} paddingX={2} paddingY={1}>
      {/* Header */}
      <Box marginBottom={1}>
        <Text color={riskConfig.color} bold>{riskConfig.icon} </Text>
        <Text bold>Command Approval Required</Text>
      </Box>

      {/* Command type and risk badge */}
      <Box marginBottom={1}>
        <Box marginRight={2}>
          <Text color={typeConfig.color}>{typeConfig.icon} </Text>
          <Text color={typeConfig.color}>{typeConfig.label}</Text>
        </Box>
        <Box>
          <Text color={riskConfig.color} bold>[{riskConfig.label}]</Text>
        </Box>
      </Box>

      {/* Description if provided */}
      {description && (
        <Box marginBottom={1}>
          <Text color="gray">{description}</Text>
        </Box>
      )}

      {/* Command display with syntax highlighting */}
      <Box
        flexDirection="column"
        borderStyle="single"
        borderColor="gray"
        paddingX={1}
        paddingY={0}
        marginBottom={1}
      >
        {workingDirectory && (
          <Box>
            <Text color="gray" dimColor>$ cd {workingDirectory}</Text>
          </Box>
        )}
        <Box flexWrap="wrap">
          <Text color="gray">$ </Text>
          {tokens.map((token, i) => (
            <Text key={i} color={getTokenColor(token.type)}>{token.text}</Text>
          ))}
        </Box>
      </Box>

      {/* Options */}
      <Box flexDirection="column" marginTop={1}>
        {options.map((opt, i) => (
          <Box key={opt.key}>
            <Text color={selectedIndex === i ? 'cyan' : 'gray'}>
              {selectedIndex === i ? '› ' : '  '}
            </Text>
            <Text color={selectedIndex === i ? 'white' : 'gray'} bold={selectedIndex === i}>
              {opt.key}. {opt.label}
            </Text>
          </Box>
        ))}
      </Box>

      {/* Keyboard hints */}
      <Box marginTop={1} borderStyle="single" borderColor="gray" borderTop borderBottom={false} borderLeft={false} borderRight={false} paddingTop={1}>
        <Text color="gray" dimColor>
          [1-{options.length}] Select · [Y] Approve · [N] Deny · [↑↓] Navigate · [Enter] Confirm · [Esc] Cancel
        </Text>
      </Box>
    </Box>
  );
};

export default CommandApproval;

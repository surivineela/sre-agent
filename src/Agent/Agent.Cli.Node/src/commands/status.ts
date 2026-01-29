/**
 * /status command - Show workspace and connection status
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const statusCommand: SlashCommand = {
  name: 'status',
  aliases: ['s'],
  description: 'Show workspace and connection status',
  usage: '/status',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { services, onOutput, onClear } = ctx;
    const config = services.config.get();

    // Clear previous output
    onClear();

    const lines = [
      '┌─ Workspace Status',
      '│',
    ];

    // Configuration
    const serverUrl = config.server?.url;
    if (serverUrl) {
      lines.push(`│  Configuration: ● Configured`);
      lines.push(`│    Server URL: ${serverUrl}`);
      lines.push(`│    Auth Required: ${config.server?.authRequired ?? false}`);
    } else {
      lines.push(`│  Configuration: ○ Not configured`);
      lines.push(`│    Run 'srectl init --resource-url <url>' to configure`);
    }

    lines.push('│');

    // Connection
    // Note: We'd need to expose more connection metadata here
    lines.push(`│  Connection: Checking...`);

    lines.push('│');

    // Current profile
    if (config.currentProfile) {
      lines.push(`│  Profile: ${config.currentProfile}`);
    }

    // MCP Servers
    const mcpCount = Object.keys(config.mcpServers || {}).length;
    if (mcpCount > 0) {
      lines.push(`│  MCP Servers: ${mcpCount} configured`);
    }

    lines.push('│');
    lines.push('└─');

    onOutput(lines.join('\n'));
    return { success: true, silent: true };
  },
};

export const versionCommand: SlashCommand = {
  name: 'version',
  aliases: ['v'],
  description: 'Show version information',
  usage: '/version',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { onOutput } = ctx;
    // Import version dynamically to avoid circular deps
    const { VERSION, NAME, getFullVersionInfo } = await import('../version');

    onOutput(`${NAME} v${VERSION}\n${getFullVersionInfo()}`);
    return { success: true, silent: true };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerStatusCommands(): void {
  commandRegistry.register(statusCommand);
  commandRegistry.register(versionCommand);
}

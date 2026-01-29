/**
 * /config command - Configuration management
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const configCommand: SlashCommand = {
  name: 'config',
  aliases: ['cfg'],
  description: 'Show or edit configuration',
  usage: '/config [key] [value]',
  examples: ['/config', '/config server.url', '/config ui.theme dark'],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { services, args, onOutput } = ctx;
    const config = services.config.get();

    // No args - show all config
    if (args.length === 0) {
      const lines = [
        '┌─ Configuration',
        '│',
        `│  Server: ${config.server?.url || 'Not configured'}`,
        `│  Auth Required: ${config.server?.authRequired ?? false}`,
        `│  Timeout: ${config.server?.timeout || 30000}ms`,
        '│',
        `│  Current Profile: ${config.currentProfile || 'default'}`,
        `│  Profiles: ${Object.keys(config.profiles || {}).length}`,
        '│',
        `│  UI Theme: ${config.ui?.theme || 'auto'}`,
        `│  Compact Mode: ${config.ui?.compactMode ?? false}`,
        `│  Show Timestamps: ${config.ui?.showTimestamps ?? true}`,
        '│',
        `│  MCP Servers: ${Object.keys(config.mcpServers || {}).length}`,
        `│  Debug Mode: ${config.debug?.enabled ?? false}`,
        '│',
        '└─',
      ];
      onOutput(lines.join('\n'));
      return { success: true, silent: true };
    }

    // One arg - show specific key
    if (args.length === 1) {
      const key = args[0];
      const value = getNestedValue(config, key);
      if (value === undefined) {
        return { success: false, message: `Unknown config key: ${key}` };
      }
      onOutput(`${key}: ${JSON.stringify(value, null, 2)}`);
      return { success: true, silent: true };
    }

    // Two+ args - set value (not implemented for safety)
    return {
      success: false,
      message: 'Config editing not yet implemented. Edit ~/.config/sre-cli/config.json directly.',
    };
  },
};

/**
 * Get nested value from object by dot-notation key
 */
function getNestedValue(obj: Record<string, unknown>, key: string): unknown {
  const parts = key.split('.');
  let current: unknown = obj;

  for (const part of parts) {
    if (current === null || current === undefined) return undefined;
    if (typeof current !== 'object') return undefined;
    current = (current as Record<string, unknown>)[part];
  }

  return current;
}

// Export registration function (called lazily to avoid circular deps)
export function registerConfigCommand(): void {
  commandRegistry.register(configCommand);
}

/**
 * Display mode commands - /compact, /verbose, /timestamps
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const compactCommand: SlashCommand = {
  name: 'compact',
  aliases: [],
  description: 'Toggle compact display mode',
  usage: '/compact [on|off]',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onStateChange, onOutput } = ctx;

    let enabled: boolean;
    if (args.length === 0) {
      // Toggle
      enabled = true; // Would need to read current state
      onOutput('Compact mode toggled');
    } else {
      enabled = args[0] === 'on' || args[0] === 'true' || args[0] === '1';
      onOutput(`Compact mode ${enabled ? 'enabled' : 'disabled'}`);
    }

    onStateChange({ compactMode: enabled });
    return { success: true, silent: true };
  },
};

export const verboseCommand: SlashCommand = {
  name: 'verbose',
  aliases: ['debug'],
  description: 'Toggle verbose/debug output',
  usage: '/verbose [on|off]',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onStateChange, onOutput } = ctx;

    let enabled: boolean;
    if (args.length === 0) {
      enabled = true;
      onOutput('Verbose mode toggled');
    } else {
      enabled = args[0] === 'on' || args[0] === 'true' || args[0] === '1';
      onOutput(`Verbose mode ${enabled ? 'enabled' : 'disabled'}`);
    }

    onStateChange({ verboseMode: enabled });
    return { success: true, silent: true };
  },
};

export const timestampsCommand: SlashCommand = {
  name: 'timestamps',
  aliases: ['ts'],
  description: 'Toggle message timestamps',
  usage: '/timestamps [on|off]',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onStateChange, onOutput } = ctx;

    let enabled: boolean;
    if (args.length === 0) {
      enabled = true;
      onOutput('Timestamps toggled');
    } else {
      enabled = args[0] === 'on' || args[0] === 'true' || args[0] === '1';
      onOutput(`Timestamps ${enabled ? 'shown' : 'hidden'}`);
    }

    onStateChange({ showTimestamps: enabled });
    return { success: true, silent: true };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerDisplayCommands(): void {
  commandRegistry.register(compactCommand);
  commandRegistry.register(verboseCommand);
  commandRegistry.register(timestampsCommand);
}

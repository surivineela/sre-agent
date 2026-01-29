/**
 * History commands - /history, /undo
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const historyCommand: SlashCommand = {
  name: 'history',
  aliases: ['hist'],
  description: 'Show conversation history',
  usage: '/history [count]',
  examples: ['/history', '/history 5'],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput } = ctx;
    const count = parseInt(args[0] || '10', 10);

    // Would get history from store
    const lines = [
      '┌─ Recent Commands',
      '│',
      '│  (History will be shown here)',
      '│',
      `│  Showing last ${count} entries`,
      '│',
      '└─',
    ];

    onOutput(lines.join('\n'));
    return { success: true, silent: true };
  },
};

export const undoCommand: SlashCommand = {
  name: 'undo',
  aliases: [],
  description: 'Undo the last action (if possible)',
  usage: '/undo',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { onOutput } = ctx;

    onOutput('Undo not available for this action.');

    // Would integrate with undo system
    return { success: true, silent: true };
  },
};

// Note: /agents and /tools commands removed - use /agent list and /tool list instead

// Export registration function (called lazily to avoid circular deps)
export function registerHistoryCommands(): void {
  commandRegistry.register(historyCommand);
  commandRegistry.register(undoCommand);
}

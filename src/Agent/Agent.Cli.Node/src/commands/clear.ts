/**
 * /clear and /exit commands
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const clearCommand: SlashCommand = {
  name: 'clear',
  aliases: ['cls'],
  description: 'Clear the chat history and screen',
  usage: '/clear',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    ctx.onClear();
    return { success: true, silent: true };
  },
};

export const exitCommand: SlashCommand = {
  name: 'exit',
  aliases: ['quit', 'q'],
  description: 'Exit the CLI',
  usage: '/exit',
  execute: async (_ctx: CommandContext): Promise<CommandResult> => {
    return { success: true, shouldExit: true, message: 'Goodbye!' };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerClearCommands(): void {
  commandRegistry.register(clearCommand);
  commandRegistry.register(exitCommand);
}

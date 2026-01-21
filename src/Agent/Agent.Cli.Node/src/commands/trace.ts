/**
 * Trace command - /trace
 * Opens full-screen trace view for the current conversation
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const traceCommand: SlashCommand = {
  name: 'trace',
  aliases: ['traces', 'spans'],
  description: 'Open full-screen trace view for the current conversation',
  usage: '/trace',
  examples: [
    '/trace - Open trace view',
  ],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    // Open trace view - App.tsx will handle the actual view
    return {
      success: true,
      traceView: {},
      silent: true,
    };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerTraceCommand(): void {
  commandRegistry.register(traceCommand);
}

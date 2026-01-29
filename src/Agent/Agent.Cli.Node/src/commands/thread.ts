/**
 * /thread Command - Thread/conversation management
 *
 * Manages chat threads:
 * - /thread new - Start a new conversation thread
 * - /thread info - Show current thread information
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult } from './types';

/**
 * /thread command handler
 */
async function handleThreadCommand(ctx: CommandContext): Promise<CommandResult> {
  const subCommand = ctx.args[0]?.toLowerCase();

  // No subcommand - show help
  if (!subCommand) {
    ctx.onOutput(`
╭──────────────────────────────────────────────────────────╮
│  Thread Management                                       │
╰──────────────────────────────────────────────────────────╯

Commands:
  /thread new       Start a new conversation thread
  /thread info      Show current thread information

The CLI automatically creates threads when you send messages.
Use /thread new to start fresh.
`);
    return { success: true };
  }

  // New thread subcommand
  if (subCommand === 'new') {
    // Signal to clear the current thread
    ctx.onStateChange({ currentPlan: null });
    ctx.onClear();
    ctx.onOutput('Started new conversation thread. Send a message to begin.');
    return { success: true };
  }

  // Info subcommand
  if (subCommand === 'info') {
    ctx.onOutput(`
Thread Information:
  Status: Active
  Messages: (check /history for details)

Use /thread new to start a fresh conversation.
`);
    return { success: true };
  }

  return {
    success: false,
    message: `Unknown subcommand: ${subCommand}\n\nUse /thread for help.`,
  };
}

/**
 * /thread command definition
 */
const threadCommand: SlashCommand = {
  name: 'thread',
  aliases: ['conversation', 'chat'],
  description: 'Manage conversation threads',
  usage: '/thread [new|info]',
  examples: [
    '/thread new',
    '/thread info',
  ],
  execute: handleThreadCommand,
};

/**
 * Register /thread command
 */
export function registerThreadCommand(): void {
  commandRegistry.register(threadCommand);
}

export default threadCommand;

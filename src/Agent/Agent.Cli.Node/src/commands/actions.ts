/**
 * Action commands - /approve, /cancel, /retry
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const approveCommand: SlashCommand = {
  name: 'approve',
  aliases: ['yes', 'y'],
  description: 'Approve pending action',
  usage: '/approve [action_id]',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput } = ctx;
    const actionId = args[0];

    if (actionId) {
      onOutput(`Approving action: ${actionId}`);
    } else {
      onOutput('Approving pending action...');
    }

    // Would integrate with approval system
    return { success: true, message: 'Action approved' };
  },
};

export const cancelCommand: SlashCommand = {
  name: 'cancel',
  aliases: ['no', 'n'],
  description: 'Cancel pending action or current operation',
  usage: '/cancel [action_id]',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput } = ctx;
    const actionId = args[0];

    if (actionId) {
      onOutput(`Cancelling action: ${actionId}`);
    } else {
      onOutput('Cancelling pending action...');
    }

    // Would integrate with cancellation system
    return { success: true, message: 'Action cancelled' };
  },
};

export const retryCommand: SlashCommand = {
  name: 'retry',
  aliases: ['r'],
  description: 'Retry the last failed action',
  usage: '/retry',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { onOutput } = ctx;

    onOutput('Retrying last action...');

    // Would integrate with retry system
    return { success: true, message: 'Retrying...' };
  },
};

export const stopCommand: SlashCommand = {
  name: 'stop',
  aliases: ['abort'],
  description: 'Stop the current agent operation',
  usage: '/stop',
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { onOutput } = ctx;

    onOutput('Stopping current operation...');

    // Would integrate with abort controller
    return { success: true, message: 'Operation stopped' };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerActionCommands(): void {
  commandRegistry.register(approveCommand);
  commandRegistry.register(cancelCommand);
  commandRegistry.register(retryCommand);
  commandRegistry.register(stopCommand);
}

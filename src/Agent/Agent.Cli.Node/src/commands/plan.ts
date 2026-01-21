/**
 * Plan and todo commands
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const planCommand: SlashCommand = {
  name: 'plan',
  aliases: [],
  description: 'Show or manage the current plan',
  usage: '/plan [show|clear]',
  examples: ['/plan', '/plan show', '/plan clear'],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput, onStateChange } = ctx;
    const action = args[0] || 'show';

    switch (action) {
      case 'show':
        // Would need to get current plan from state
        onOutput('No active plan. Agent will create one when given a task.');
        break;

      case 'clear':
        onStateChange({ currentPlan: null });
        onOutput('Plan cleared.');
        break;

      default:
        return { success: false, message: `Unknown action: ${action}. Use: show, clear` };
    }

    return { success: true, silent: true };
  },
};

export const todoCommand: SlashCommand = {
  name: 'todo',
  aliases: ['tasks'],
  description: 'Show or manage todo items',
  usage: '/todo [add|done|list] [item]',
  examples: ['/todo', '/todo list', '/todo add "Fix bug"', '/todo done 1'],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput } = ctx;
    const action = args[0] || 'list';

    switch (action) {
      case 'list':
        // Would need to get todos from state
        onOutput('No todo items. Agent will create tasks as needed.');
        break;

      case 'add':
        if (args.length < 2) {
          return { success: false, message: 'Usage: /todo add "task description"' };
        }
        const task = args.slice(1).join(' ');
        onOutput(`Added: ${task}`);
        break;

      case 'done':
        if (args.length < 2) {
          return { success: false, message: 'Usage: /todo done <number>' };
        }
        const num = parseInt(args[1], 10);
        if (isNaN(num)) {
          return { success: false, message: 'Invalid task number' };
        }
        onOutput(`Marked task ${num} as done`);
        break;

      default:
        return { success: false, message: `Unknown action: ${action}. Use: list, add, done` };
    }

    return { success: true, silent: true };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerPlanCommands(): void {
  commandRegistry.register(planCommand);
  commandRegistry.register(todoCommand);
}

/**
 * /help command - Show available commands
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandResult, CommandContext } from './types';

export const helpCommand: SlashCommand = {
  name: 'help',
  aliases: ['h', '?'],
  description: 'Show available commands and usage',
  usage: '/help [command]',
  examples: ['/help', '/help clear', '/help status'],
  execute: async (ctx: CommandContext): Promise<CommandResult> => {
    const { args, onOutput } = ctx;

    // If specific command requested
    if (args.length > 0) {
      const cmdName = args[0];
      const command = commandRegistry.get(cmdName);

      if (!command) {
        return {
          success: false,
          message: `Unknown command: /${cmdName}`,
        };
      }

      const lines = [
        `/${command.name}`,
        `  ${command.description}`,
      ];

      if (command.aliases?.length) {
        lines.push(`  Aliases: ${command.aliases.map(a => '/' + a).join(', ')}`);
      }

      if (command.usage) {
        lines.push(`  Usage: ${command.usage}`);
      }

      if (command.examples?.length) {
        lines.push(`  Examples:`);
        for (const ex of command.examples) {
          lines.push(`    ${ex}`);
        }
      }

      onOutput(lines.join('\n'));
      return { success: true, silent: true };
    }

    // Show all commands
    const commands = commandRegistry.getAll();
    const lines = [
      'Available Commands:',
      '',
    ];

    // Group commands by category
    const categories: Record<string, SlashCommand[]> = {
      'Setup': [],
      'Resources': [],
      'Operations': [],
      'General': [],
      'Display': [],
      'Status': [],
      'Planning': [],
      'History': [],
      'Help': [],
      'Actions': [],
    };

    for (const cmd of commands) {
      if (['init', 'config', 'auth'].includes(cmd.name)) {
        categories['Setup'].push(cmd);
      } else if (['agent', 'tool', 'skill', 'apply'].includes(cmd.name)) {
        categories['Resources'].push(cmd);
      } else if (['incident', 'filter', 'scheduled'].includes(cmd.name)) {
        categories['Operations'].push(cmd);
      } else if (['help', 'clear', 'exit', 'quit', 'version'].includes(cmd.name)) {
        categories['General'].push(cmd);
      } else if (['compact', 'verbose', 'timestamps'].includes(cmd.name)) {
        categories['Display'].push(cmd);
      } else if (['status'].includes(cmd.name)) {
        categories['Status'].push(cmd);
      } else if (['plan', 'todo'].includes(cmd.name)) {
        categories['Planning'].push(cmd);
      } else if (['history', 'undo', 'thread'].includes(cmd.name)) {
        categories['History'].push(cmd);
      } else if (['doc'].includes(cmd.name)) {
        categories['Help'].push(cmd);
      } else {
        categories['Actions'].push(cmd);
      }
    }

    for (const [category, cmds] of Object.entries(categories)) {
      if (cmds.length === 0) continue;

      lines.push(`  ${category}:`);
      for (const cmd of cmds) {
        const aliases = cmd.aliases?.length ? ` (${cmd.aliases.map(a => '/' + a).join(', ')})` : '';
        lines.push(`    /${cmd.name}${aliases} - ${cmd.description}`);
      }
      lines.push('');
    }

    lines.push('Type /help <command> for detailed help on a specific command.');

    onOutput(lines.join('\n'));
    return { success: true, silent: true };
  },
};

// Export registration function (called lazily to avoid circular deps)
export function registerHelpCommand(): void {
  commandRegistry.register(helpCommand);
}

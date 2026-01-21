/**
 * Command Registry - Separate file to avoid circular dependencies
 */

import type { SlashCommand } from './types';

/**
 * Command registry - stores all available slash commands
 */
class CommandRegistry {
  private commands: Map<string, SlashCommand> = new Map();
  private aliases: Map<string, string> = new Map();

  /**
   * Register a command
   */
  register(command: SlashCommand): void {
    this.commands.set(command.name.toLowerCase(), command);

    // Register aliases
    if (command.aliases) {
      for (const alias of command.aliases) {
        this.aliases.set(alias.toLowerCase(), command.name.toLowerCase());
      }
    }
  }

  /**
   * Get a command by name or alias
   */
  get(name: string): SlashCommand | undefined {
    const lowerName = name.toLowerCase();
    const actualName = this.aliases.get(lowerName) || lowerName;
    return this.commands.get(actualName);
  }

  /**
   * Check if a command exists
   */
  has(name: string): boolean {
    const lowerName = name.toLowerCase();
    return this.commands.has(lowerName) || this.aliases.has(lowerName);
  }

  /**
   * Get all commands (for help)
   */
  getAll(): SlashCommand[] {
    return Array.from(this.commands.values());
  }

  /**
   * Get commands grouped by category
   */
  getGrouped(): Record<string, SlashCommand[]> {
    const all = this.getAll();
    return {
      'General': all.filter(c => ['help', 'clear', 'exit', 'quit', 'version'].includes(c.name)),
      'Setup': all.filter(c => ['init', 'config', 'auth'].includes(c.name)),
      'Resources': all.filter(c => ['agent', 'tool', 'skill', 'apply'].includes(c.name)),
      'Operations': all.filter(c => ['incident', 'filter', 'scheduled'].includes(c.name)),
      'Display': all.filter(c => ['compact', 'verbose', 'timestamps', 'trace'].includes(c.name)),
      'Status': all.filter(c => ['status'].includes(c.name)),
      'Planning': all.filter(c => ['plan', 'todo'].includes(c.name)),
      'Actions': all.filter(c => ['approve', 'cancel', 'retry'].includes(c.name)),
      'History': all.filter(c => ['history', 'undo', 'thread'].includes(c.name)),
      'Help': all.filter(c => ['doc'].includes(c.name)),
    };
  }
}

// Global registry instance - exported early to break circular deps
export const commandRegistry = new CommandRegistry();

/**
 * Slash Command System
 *
 * Provides Claude Code-style slash commands for the interactive CLI.
 * Commands are invoked by typing /<command> in the chat input.
 */

// Import registry for local use AND re-export
import { commandRegistry } from './registry';
export { commandRegistry };

// Re-export types from separate file to avoid circular deps
export type {
  CommandContext,
  CommandState,
  CommandResult,
  SlashCommand,
  EditorConfig,
  WizardConfig,
  WizardStep,
  WizardOption,
  TraceViewConfig,
} from './types';

// Import types for local use
import type { CommandContext, CommandResult } from './types';

/**
 * Command suggestion with name and description
 */
export interface CommandSuggestion {
  name: string;
  description: string;
  matchType: 'name' | 'alias' | 'description';
}

/**
 * Get all command names for autocomplete
 */
export function getCommandNames(): string[] {
  initCommands();
  const commands = commandRegistry.getAll();
  const names: string[] = [];

  for (const cmd of commands) {
    names.push(cmd.name);
    if (cmd.aliases) {
      names.push(...cmd.aliases);
    }
  }

  return names.sort();
}

/**
 * Get command suggestions based on partial input (searches name, aliases, AND description)
 */
export function getCommandSuggestions(partial: string): string[] {
  const suggestions = getCommandSuggestionsWithDescriptions(partial);
  return suggestions.map(s => '/' + s.name);
}

/**
 * Get command suggestions with descriptions (for rich autocomplete display)
 */
export function getCommandSuggestionsWithDescriptions(partial: string): CommandSuggestion[] {
  if (!partial.startsWith('/')) return [];

  initCommands();
  const search = partial.slice(1).toLowerCase();
  const commands = commandRegistry.getAll();
  const suggestions: CommandSuggestion[] = [];
  const seen = new Set<string>();

  for (const cmd of commands) {
    // Skip if already added
    if (seen.has(cmd.name)) continue;

    // Check if name matches
    if (cmd.name.toLowerCase().startsWith(search)) {
      suggestions.push({
        name: cmd.name,
        description: cmd.description,
        matchType: 'name',
      });
      seen.add(cmd.name);
      continue;
    }

    // Check if any alias matches
    const aliasMatch = cmd.aliases?.find(a => a.toLowerCase().startsWith(search));
    if (aliasMatch) {
      suggestions.push({
        name: cmd.name,
        description: cmd.description,
        matchType: 'alias',
      });
      seen.add(cmd.name);
      continue;
    }

    // Check if description contains the search term (only if search is 2+ chars)
    if (search.length >= 2 && cmd.description.toLowerCase().includes(search)) {
      suggestions.push({
        name: cmd.name,
        description: cmd.description,
        matchType: 'description',
      });
      seen.add(cmd.name);
    }
  }

  // Sort: name matches first, then alias matches, then description matches
  return suggestions.sort((a, b) => {
    const order = { name: 0, alias: 1, description: 2 };
    if (order[a.matchType] !== order[b.matchType]) {
      return order[a.matchType] - order[b.matchType];
    }
    return a.name.localeCompare(b.name);
  });
}

/**
 * Parse slash command from input
 * Returns null if input is not a slash command
 */
export function parseSlashCommand(input: string): { name: string; args: string[] } | null {
  const trimmed = input.trim();

  if (!trimmed.startsWith('/')) {
    return null;
  }

  const parts = trimmed.slice(1).split(/\s+/);
  const name = parts[0] || '';
  const args = parts.slice(1);

  if (!name) {
    return null;
  }

  return { name, args };
}

/**
 * Execute a slash command
 */
export async function executeSlashCommand(
  input: string,
  ctx: Omit<CommandContext, 'args' | 'rawInput'>
): Promise<CommandResult> {
  // Ensure commands are registered
  initCommands();

  const parsed = parseSlashCommand(input);

  if (!parsed) {
    return {
      success: false,
      message: 'Invalid command format',
    };
  }

  const command = commandRegistry.get(parsed.name);

  if (!command) {
    return {
      success: false,
      message: `Unknown command: /${parsed.name}. Type /help for available commands.`,
    };
  }

  const fullCtx: CommandContext = {
    ...ctx,
    args: parsed.args,
    rawInput: input,
  };

  try {
    return await command.execute(fullCtx);
  } catch (error) {
    return {
      success: false,
      message: `Command failed: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}

/**
 * Check if input is a slash command
 */
export function isSlashCommand(input: string): boolean {
  return input.trim().startsWith('/');
}

// Track if commands have been initialized
let commandsInitialized = false;

/**
 * Initialize all commands - called lazily to avoid circular deps
 */
export function initCommands(): void {
  if (commandsInitialized) return;
  commandsInitialized = true;

  // Import and register all commands
  registerHelpCommand();
  registerClearCommands();
  registerStatusCommands();
  registerConfigCommand();
  registerDisplayCommands();
  registerPlanCommands();
  registerActionCommands();
  registerHistoryCommands();
  registerInitCommand();
  registerToolCommand();
  registerAgentCommand();
  registerThreadCommand();
  registerAuthCommand();
  registerUseCommand();

  // New commands for feature parity (SPEC-007)
  registerVersionCommand();
  registerDocCommand();
  registerApplyCommand();
  registerSkillCommand();
  registerScheduledCommand();
  registerIncidentCommand();
  registerFilterCommand();

  // Trace view command
  registerTraceCommand();
}

// Import command registration functions
import { registerHelpCommand } from './help';
import { registerClearCommands } from './clear';
import { registerStatusCommands } from './status';
import { registerConfigCommand } from './config';
import { registerDisplayCommands } from './display';
import { registerPlanCommands } from './plan';
import { registerActionCommands } from './actions';
import { registerHistoryCommands } from './history';
import { registerInitCommand } from './init';
import { registerToolCommand } from './tool';
import { registerAgentCommand } from './agent';
import { registerThreadCommand } from './thread';
import { registerAuthCommand } from './auth';
import { registerUseCommand } from './use';

// New commands for feature parity (SPEC-007)
import { registerVersionCommand } from './version';
import { registerDocCommand } from './doc';
import { registerApplyCommand } from './apply';
import { registerSkillCommand } from './skill';
import { registerScheduledCommand } from './scheduled';
import { registerIncidentCommand } from './incident';
import { registerFilterCommand } from './filter';

// Trace view command
import { registerTraceCommand } from './trace';

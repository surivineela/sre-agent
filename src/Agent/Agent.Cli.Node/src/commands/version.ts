/**
 * /version Command - Display version information
 *
 * Shows CLI version, Node.js version, and platform info
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult } from './types';

// Package version (imported at build time)
const CLI_VERSION = '1.0.0';

/**
 * Get version information
 */
function getVersionInfo(): string {
  const lines = [
    '',
    '┌─ SRE Agent CLI',
    '│',
    `│  Version:   ${CLI_VERSION}`,
    `│  Node.js:   ${process.version}`,
    `│  Platform:  ${process.platform} (${process.arch})`,
    '│',
    '└─',
    '',
  ];
  return lines.join('\n');
}

/**
 * Version command handler
 */
async function handleVersionCommand(ctx: CommandContext): Promise<CommandResult> {
  const { onOutput } = ctx;

  onOutput(getVersionInfo());

  return { success: true, silent: true };
}

/**
 * Version command definition
 */
const versionCommand: SlashCommand = {
  name: 'version',
  aliases: ['v', 'ver'],
  description: 'Show CLI version information',
  usage: '/version',
  examples: ['/version', '/ver'],
  execute: handleVersionCommand,
};

/**
 * Register the version command
 */
export function registerVersionCommand(): void {
  commandRegistry.register(versionCommand);
}

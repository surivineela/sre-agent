/**
 * /auth Command - Authentication management
 *
 * Commands:
 * - /auth status - Check authentication status
 * - /auth login  - Authenticate (Azure CLI or API key)
 * - /auth logout - Clear stored credentials
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult } from './types';

/**
 * /auth command handler
 */
async function handleAuthCommand(ctx: CommandContext): Promise<CommandResult> {
  const subCommand = ctx.args[0]?.toLowerCase();

  // No subcommand - show help
  if (!subCommand) {
    ctx.onOutput(`
Authentication Management

Commands:
  /auth status   Check authentication status
  /auth login    Authenticate with Azure or API key
  /auth logout   Clear stored credentials

Authentication methods (tried in order):
  1. Azure CLI (run "az login" in terminal)
  2. API key (set SRE_API_KEY environment variable)
  3. Stored API key (via /auth login)
`);
    return { success: true };
  }

  // Status subcommand
  if (subCommand === 'status') {
    try {
      const isAuthenticated = await ctx.services.auth.isAuthenticated();

      if (isAuthenticated) {
        ctx.onOutput('✅ Authenticated');
        ctx.onOutput('');
        ctx.onOutput('You can access the SRE Agent server.');
      } else {
        ctx.onOutput('❌ Not authenticated');
        ctx.onOutput('');
        ctx.onOutput('To authenticate:');
        ctx.onOutput('  • Run "az login" in your terminal (Azure CLI)');
        ctx.onOutput('  • Or set SRE_API_KEY environment variable');
      }
    } catch (error) {
      ctx.onOutput('❌ Not authenticated');
      ctx.onOutput('');
      ctx.onOutput(`Error: ${error instanceof Error ? error.message : String(error)}`);
    }
    return { success: true };
  }

  // Login subcommand
  if (subCommand === 'login') {
    const apiKey = ctx.args[1];

    if (apiKey) {
      // Store the provided API key
      try {
        await ctx.services.auth.storeApiKey(apiKey);
        ctx.onOutput('✅ API key stored securely');
      } catch (error) {
        ctx.onOutput(`❌ Failed to store API key: ${error instanceof Error ? error.message : String(error)}`);
        ctx.onOutput('');
        ctx.onOutput('Alternative: Set SRE_API_KEY environment variable');
      }
    } else {
      // Try Azure login
      ctx.onOutput('Checking Azure CLI authentication...');
      ctx.onOutput('');

      try {
        const isAuthenticated = await ctx.services.auth.isAuthenticated();

        if (isAuthenticated) {
          ctx.onOutput('✅ Already authenticated via Azure CLI');
        } else {
          ctx.onOutput('❌ Not authenticated');
          ctx.onOutput('');
          ctx.onOutput('To authenticate:');
          ctx.onOutput('  1. Run "az login" in your terminal');
          ctx.onOutput('  2. Or run: /auth login <api-key>');
          ctx.onOutput('  3. Or set: SRE_API_KEY=<your-key>');
        }
      } catch (error) {
        ctx.onOutput(`❌ Authentication check failed: ${error instanceof Error ? error.message : String(error)}`);
      }
    }
    return { success: true };
  }

  // Logout subcommand
  if (subCommand === 'logout') {
    try {
      await ctx.services.auth.clearCredentials();
      ctx.onOutput('✅ Credentials cleared');
      ctx.onOutput('');
      ctx.onOutput('Note: Azure CLI credentials are managed separately.');
      ctx.onOutput('Run "az logout" to sign out of Azure.');
    } catch (error) {
      ctx.onOutput(`⚠ ${error instanceof Error ? error.message : String(error)}`);
    }
    return { success: true };
  }

  return {
    success: false,
    message: `Unknown subcommand: ${subCommand}\n\nUse /auth for help.`,
  };
}

/**
 * /auth command definition
 */
const authCommand: SlashCommand = {
  name: 'auth',
  aliases: ['login'],
  description: 'Manage authentication',
  usage: '/auth [status|login|logout]',
  examples: [
    '/auth status',
    '/auth login',
    '/auth login <api-key>',
    '/auth logout',
  ],
  execute: handleAuthCommand,
};

/**
 * Register /auth command
 */
export function registerAuthCommand(): void {
  commandRegistry.register(authCommand);
}

export default authCommand;

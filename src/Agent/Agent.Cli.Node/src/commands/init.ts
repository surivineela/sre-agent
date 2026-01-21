/**
 * /init Command - Initialize SRE Agent workspace
 *
 * Matches Agent.Cli behavior:
 * 1. Prompt for SRE Agent server URL
 * 2. Validate URL format
 * 3. Create workspace directories (agents/, tools/, skills/, scheduledtasks/)
 * 4. Save config to ~/.sreagent/config.json
 * 5. Test server connection
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult } from './types';
import { ConfigService } from '../services/config';
import { createWorkspaceDirectories } from '../utils/examples';
import { progressService } from '../services/progress';

/**
 * Validate URL format
 */
function isValidUrl(url: string): boolean {
  try {
    const parsed = new URL(url);
    return parsed.protocol === 'http:' || parsed.protocol === 'https:';
  } catch {
    return false;
  }
}

/**
 * Test connection to the server
 */
async function testServerConnection(
  serverUrl: string
): Promise<{ success: boolean; message: string; agentCount?: number }> {
  try {
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/agents`,
      {
        method: 'GET',
        headers: {
          Accept: 'application/json',
        },
      }
    );

    if (!response.ok) {
      if (response.status === 401 || response.status === 403) {
        return {
          success: false,
          message: `Authentication required (${response.status}). You may need to log in.`,
        };
      }
      return {
        success: false,
        message: `Server returned ${response.status} ${response.statusText}`,
      };
    }

    const data = await response.json();
    let agentCount = 0;

    if (Array.isArray(data)) {
      agentCount = data.length;
    } else if (data.value && Array.isArray(data.value)) {
      agentCount = data.value.length;
    }

    return {
      success: true,
      message: `Connected! Found ${agentCount} agent(s).`,
      agentCount,
    };
  } catch (error) {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      return {
        success: false,
        message: `Could not reach server. Check if URL is correct and server is running.`,
      };
    }
    return {
      success: false,
      message: `Connection failed: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}

/**
 * /init command handler
 */
async function handleInitCommand(ctx: CommandContext): Promise<CommandResult> {
  const serverUrl = ctx.args[0];

  // If no URL provided, show usage
  if (!serverUrl) {
    ctx.onOutput(`
╭──────────────────────────────────────────────────────────╮
│  Initialize SRE Agent Workspace                          │
╰──────────────────────────────────────────────────────────╯

Usage: /init <server-url>

Example:
  /init https://localhost:7023
  /init https://sreagent.azurewebsites.net

This will:
  • Validate the server URL
  • Create workspace directories (agents/, tools/, skills/, scheduledtasks/)
  • Save configuration to ~/.sreagent/config.json
  • Test connection to the server
`);
    return { success: true };
  }

  // Validate URL format
  if (!isValidUrl(serverUrl)) {
    return {
      success: false,
      message: `Invalid URL format: ${serverUrl}\n\nPlease provide a valid HTTP(S) URL, e.g., https://localhost:7023`,
    };
  }

  // Initialize progress tracking
  progressService.initialize([
    'Validating server URL',
    'Creating workspace directories',
    'Saving configuration',
    'Initializing CLI config',
    'Testing server connection',
  ]);

  const configService = new ConfigService();

  try {
    // Step 1: Validate URL (already done above)
    progressService.nextStep(`Validating ${serverUrl}`);
    // URL already validated above
    await new Promise((resolve) => setTimeout(resolve, 100)); // Brief pause for visual feedback

    // Step 2: Create workspace directories
    progressService.nextStep('agents/, tools/, skills/, scheduledtasks/');
    const workspacePath = process.cwd();
    await createWorkspaceDirectories(workspacePath);

    // Step 3: Save configuration
    progressService.nextStep('~/.sreagent/config.json');
    const authRequired = !ConfigService.isLocalhost(serverUrl);
    const now = new Date();
    await configService.saveSreAgentConfig({
      resourceUrl: serverUrl,
      authRequired,
      lastUpdated: now,
      createdAt: now,
    });

    // Step 4: Initialize CLI config
    progressService.nextStep('Writing config...');
    await configService.initialize(serverUrl);

    // Step 5: Test connection
    progressService.nextStep(`Connecting to ${serverUrl}...`);
    const connectionResult = await testServerConnection(serverUrl);

    // Complete progress
    progressService.complete();

    // Clear progress after a brief moment
    setTimeout(() => progressService.reset(), 500);

    // Show summary
    ctx.onOutput('');
    if (connectionResult.success) {
      ctx.onOutput(`✅ SRE Agent CLI initialized successfully!`);
      ctx.onOutput('');
      ctx.onOutput(`Server:  ${serverUrl}`);
      ctx.onOutput(`Auth:    ${authRequired ? 'Required (run "az login" or set SRE_API_KEY)' : 'Not required (localhost)'}`);
      ctx.onOutput(`Status:  ${connectionResult.message}`);
    } else {
      ctx.onOutput(`⚠️ Initialization completed with warnings`);
      ctx.onOutput('');
      ctx.onOutput(`Server:  ${serverUrl}`);
      ctx.onOutput(`Auth:    ${authRequired ? 'Required' : 'Not required'}`);
      ctx.onOutput(`Status:  ${connectionResult.message}`);
      ctx.onOutput('');
      ctx.onOutput('Note: You can still use local commands. Try /status later.');
    }
    ctx.onOutput('');
    ctx.onOutput('Next steps:');
    ctx.onOutput('  /auth status  - Check authentication');
    ctx.onOutput('  /agent        - Create or manage agents');
    ctx.onOutput('  /tool         - Create or manage tools');
    ctx.onOutput('  /status       - Check connection status');
    ctx.onOutput('');

    return { success: true };
  } catch (error) {
    progressService.fail(error instanceof Error ? error.message : String(error));

    // Clear progress after showing error
    setTimeout(() => progressService.reset(), 2000);

    return {
      success: false,
      message: `Initialization failed: ${error instanceof Error ? error.message : String(error)}`,
    };
  }
}

/**
 * /init command definition
 */
const initCommand: SlashCommand = {
  name: 'init',
  description: 'Initialize SRE Agent workspace with server URL',
  usage: '/init <server-url>',
  examples: [
    '/init https://localhost:7023',
    '/init https://sreagent.azurewebsites.net',
  ],
  execute: handleInitCommand,
};

/**
 * Register /init command
 */
export function registerInitCommand(): void {
  commandRegistry.register(initCommand);
}

export default initCommand;

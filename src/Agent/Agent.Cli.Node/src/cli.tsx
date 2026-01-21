#!/usr/bin/env node
/**
 * SRE CLI - Interactive Agent CLI
 *
 * A Claude Code-inspired terminal experience for SRE Agent management.
 */
import { render } from 'ink';
import { program } from 'commander';
import { App } from './components/App';
import { initializeServices, connectServices } from './services';
import { NAME, getFullVersionInfo } from './version';
import { logger } from './utils/logger';
import type { CLIOptions, Services } from './types';

/**
 * Run the CLI in interactive mode
 */
async function runInteractiveMode(
  prompt: string[] | undefined,
  _options: CLIOptions,
  services: Services
): Promise<void> {
  const initialPrompt = prompt?.join(' ');

  const { waitUntilExit } = render(
    <App
      initialPrompt={initialPrompt}
      services={services}
    />,
    {
      exitOnCtrlC: false, // We handle this ourselves
      incrementalRendering: true, // Enable incremental rendering to reduce flickering
      patchConsole: true, // Ensure console output doesn't interfere
    }
  );

  await waitUntilExit();
}

/**
 * Run the CLI in batch mode (non-interactive)
 */
async function runBatchMode(
  prompt: string[] | undefined,
  options: CLIOptions,
  _services: Services
): Promise<void> {
  const input = prompt?.join(' ');

  if (!input) {
    console.error('Error: No input provided for batch mode');
    process.exit(1);
  }

  // TODO: Implement batch mode with agentic loop
  console.log('Batch mode is not yet fully implemented.');
  console.log(`Input: ${input}`);

  if (options.output === 'json') {
    console.log(JSON.stringify({
      success: false,
      error: 'Batch mode not yet implemented',
    }, null, 2));
  }

  process.exit(1);
}

/**
 * Main entry point
 */
async function main(): Promise<void> {
  // Initialize services
  let services: Services;
  try {
    services = await initializeServices();
  } catch (err) {
    console.error('Failed to initialize services:', err);
    process.exit(1);
  }

  // Configure CLI
  program
    .name(NAME)
    .description('Interactive SRE Agent CLI')
    .version(getFullVersionInfo(), '-v, --version', 'Show version information')
    .option('-d, --debug', 'Enable debug mode')
    .option('-q, --quiet', 'Suppress non-essential output')
    .option('--no-color', 'Disable colored output')
    .option('--batch', 'Run in batch mode (non-interactive)')
    .option('-o, --output <format>', 'Output format (json|text)', 'text')
    .option('-c, --config <path>', 'Config file path')
    .option('-p, --profile <name>', 'Use specific profile')
    .argument('[prompt...]', 'Initial prompt or command');

  // Main action
  program.action(async (prompt: string[] | undefined, options: CLIOptions) => {
    // Configure debug mode
    if (options.debug) {
      logger.configure({ enabled: true, level: 'debug' });
    }

    // Try to connect to backend
    try {
      await connectServices(services);
    } catch (err) {
      logger.warn('Could not connect to backend', err);
      // Continue anyway - we can work offline
    }

    if (options.batch) {
      await runBatchMode(prompt, options, services);
    } else {
      await runInteractiveMode(prompt, options, services);
    }
  });

  // Agent subcommand with nested commands
  const agentCmd = program
    .command('agent')
    .description('Agent management commands');

  agentCmd
    .command('list')
    .description('List all agents')
    .option('--json', 'Output as JSON')
    .action(async (options: { json?: boolean }) => {
      try {
        const agents = await services.api.listAgents();
        if (options.json) {
          console.log(JSON.stringify(agents, null, 2));
        } else {
          if (agents.length === 0) {
            console.log('No agents found.');
          } else {
            console.log('Agents:');
            for (const agent of agents) {
              console.log(`  • ${agent.name}`);
            }
          }
        }
      } catch (err) {
        console.error('Failed to list agents:', err);
        process.exit(1);
      }
    });

  // Tool subcommand with nested commands
  const toolCmd = program
    .command('tool')
    .description('Tool management commands');

  toolCmd
    .command('list')
    .description('List all tools')
    .option('--json', 'Output as JSON')
    .action(async (options: { json?: boolean }) => {
      try {
        const tools = await services.api.listTools();
        if (options.json) {
          console.log(JSON.stringify(tools, null, 2));
        } else {
          if (tools.length === 0) {
            console.log('No tools found.');
          } else {
            console.log('Tools:');
            for (const tool of tools) {
              console.log(`  • ${tool.name} - ${tool.description || 'No description'}`);
            }
          }
        }
      } catch (err) {
        console.error('Failed to list tools:', err);
        process.exit(1);
      }
    });

  // Config subcommand with nested commands
  const configCmd = program
    .command('config')
    .description('Configuration management');

  configCmd
    .command('show')
    .description('Show current configuration')
    .action(() => {
      const config = services.config.get();
      console.log(JSON.stringify(config, null, 2));
    });

  configCmd
    .command('init')
    .description('Initialize configuration')
    .argument('<serverUrl>', 'Server URL')
    .action(async (serverUrl: string) => {
      try {
        await services.config.initialize(serverUrl);
        console.log('Configuration initialized successfully.');
      } catch (err) {
        console.error('Failed to initialize configuration:', err);
        process.exit(1);
      }
    });

  // Version command (alternative)
  program
    .command('version')
    .description('Show version information')
    .action(() => {
      console.log(getFullVersionInfo());
    });

  // Parse arguments
  try {
    await program.parseAsync(process.argv);
  } catch (err) {
    console.error('Error:', err);
    process.exit(1);
  }
}

// Run main
main().catch((err) => {
  console.error('Fatal error:', err);
  process.exit(1);
});

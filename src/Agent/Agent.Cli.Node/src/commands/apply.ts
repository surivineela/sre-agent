/**
 * /apply Command - Apply YAML Configuration Files
 *
 * Deploy agents, tools, skills, and other resources to the server
 */
import * as fs from 'fs/promises';
import * as path from 'path';
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult, WizardConfig } from './types';
import { getAuthService } from '../services/auth';

// ============================================================================
// AUTH HELPERS
// ============================================================================

async function getAuthHeaders(): Promise<Record<string, string>> {
  try {
    const authService = getAuthService();
    const token = await authService.getToken();
    return { Authorization: `Bearer ${token}` };
  } catch {
    return {};
  }
}

// ============================================================================
// YAML TYPE DETECTION
// ============================================================================

type ResourceType = 'agent' | 'tool' | 'skill' | 'filter' | 'scheduled' | 'unknown';

function detectYamlType(content: string): ResourceType {
  const kindMatch = content.match(/kind:\s*(\w+)/i);
  const kind = kindMatch?.[1]?.toLowerCase() || '';

  if (kind.includes('agent') || kind === 'extendedagent') return 'agent';
  if (kind.includes('tool')) return 'tool';
  if (kind.includes('skill')) return 'skill';
  if (kind.includes('filter')) return 'filter';
  if (kind.includes('scheduled') || kind.includes('task')) return 'scheduled';

  // Fallback: check apiVersion
  const apiMatch = content.match(/apiVersion:\s*srectl\.(\w+)/i);
  const apiType = apiMatch?.[1]?.toLowerCase() || '';

  if (apiType === 'agent') return 'agent';
  if (apiType === 'tool') return 'tool';
  if (apiType === 'skill') return 'skill';
  if (apiType === 'filter') return 'filter';
  if (apiType === 'scheduled') return 'scheduled';

  return 'unknown';
}

function extractResourceName(content: string): string | undefined {
  const nameMatch = content.match(/name:\s*([^\n\r]+)/);
  return nameMatch?.[1]?.trim();
}

// ============================================================================
// API FUNCTIONS
// ============================================================================

interface ApplyResult {
  success: boolean;
  message?: string;
  error?: string;
}

async function applyResource(
  serverUrl: string,
  type: ResourceType,
  name: string,
  content: string,
  dryRun: boolean
): Promise<ApplyResult> {
  const endpoints: Record<ResourceType, string> = {
    agent: 'api/v2/extendedAgent/agents',
    tool: 'api/v2/extendedAgent/tools',
    skill: 'api/v2/extendedAgent/skills',
    filter: 'api/v2/extendedAgent/filters',
    scheduled: 'api/v2/extendedAgent/scheduledTasks',
    unknown: '',
  };

  const endpoint = endpoints[type];
  if (!endpoint) {
    return { success: false, error: 'Unknown resource type' };
  }

  try {
    const authHeaders = await getAuthHeaders();
    const url = `${serverUrl.replace(/\/$/, '')}/${endpoint}/${name}${dryRun ? '?dryRun=true' : ''}`;

    const response = await fetch(url, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/x-yaml',
        Accept: 'application/json',
        ...authHeaders,
      },
      body: content,
    });

    if (!response.ok) {
      const errorText = await response.text();
      return { success: false, error: errorText || `HTTP ${response.status}` };
    }

    return {
      success: true,
      message: dryRun
        ? `Validation successful for ${type} "${name}"`
        : `Successfully applied ${type} "${name}" to server`,
    };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

// ============================================================================
// WIZARD
// ============================================================================

async function findYamlFiles(dir: string): Promise<string[]> {
  const files: string[] = [];
  const subdirs = ['agents', 'tools', 'skills', 'filters', 'scheduled'];

  // Check root directory
  try {
    const rootFiles = await fs.readdir(dir);
    for (const file of rootFiles) {
      if (file.endsWith('.yaml') || file.endsWith('.yml')) {
        files.push(file);
      }
    }
  } catch {
    // Ignore
  }

  // Check subdirectories
  for (const subdir of subdirs) {
    try {
      const subdirPath = path.join(dir, subdir);
      const subdirFiles = await fs.readdir(subdirPath);
      for (const file of subdirFiles) {
        if (file.endsWith('.yaml') || file.endsWith('.yml')) {
          files.push(`${subdir}/${file}`);
        }
      }
    } catch {
      // Ignore
    }
  }

  return files;
}

function createApplyWizard(ctx: CommandContext, yamlFiles: string[]): WizardConfig {
  const fileOptions = yamlFiles.slice(0, 10).map(file => ({
    key: file,
    label: file,
    description: `Apply ${file}`,
  }));

  if (fileOptions.length === 0) {
    fileOptions.push({
      key: 'none',
      label: 'No YAML files found',
      description: 'Create resources first with /agent, /tool, or /skill',
    });
  }

  return {
    id: 'apply-wizard',
    title: 'Apply Configuration',
    steps: [
      {
        id: 'file',
        title: 'Select File',
        prompt: 'Which configuration file would you like to apply?',
        type: 'select',
        options: fileOptions,
      },
      {
        id: 'mode',
        title: 'Apply Mode',
        prompt: 'How would you like to apply this configuration?',
        type: 'select',
        options: [
          { key: 'validate', label: 'Validate Only (Dry Run)', description: 'Check for errors without deploying' },
          { key: 'apply', label: 'Apply to Server', description: 'Deploy the configuration' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.file === 'none') {
        return { success: false, message: 'No file selected. Create resources first.' };
      }

      const filePath = path.join(process.cwd(), data.file);
      const dryRun = data.mode === 'validate';

      return await applyYamlFile(ctx, filePath, dryRun, false);
    },
  };
}

// ============================================================================
// CORE APPLY LOGIC
// ============================================================================

async function applyYamlFile(
  ctx: CommandContext,
  filePath: string,
  dryRun: boolean,
  _force: boolean
): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  if (!serverUrl) {
    return {
      success: false,
      message: 'Server not configured. Run /init first.',
    };
  }

  // Read file
  let content: string;
  try {
    content = await fs.readFile(filePath, 'utf-8');
  } catch (error) {
    return {
      success: false,
      message: `Cannot read file: ${filePath}\n${error}`,
    };
  }

  // Detect type and name
  const type = detectYamlType(content);
  const name = extractResourceName(content);

  if (type === 'unknown') {
    return {
      success: false,
      message: 'Cannot determine resource type from YAML. Check apiVersion and kind fields.',
    };
  }

  if (!name) {
    return {
      success: false,
      message: 'Cannot determine resource name from YAML. Check metadata.name field.',
    };
  }

  onOutput(`\n┌─ Applying ${type}: ${name}`);
  onOutput(`│  File: ${path.basename(filePath)}`);
  onOutput(`│  Mode: ${dryRun ? 'Dry Run (Validation)' : 'Apply'}`);
  onOutput('│');

  // Apply
  const result = await applyResource(serverUrl, type, name, content, dryRun);

  if (result.success) {
    onOutput(`│  ✓ ${result.message}`);
    onOutput('└─\n');
    return { success: true, silent: true };
  } else {
    onOutput(`│  ✗ Error: ${result.error}`);
    onOutput('└─\n');
    return { success: false, silent: true };
  }
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleApplyCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args } = ctx;

  // Parse arguments
  const pathArg = args.find(a => !a.startsWith('--'));
  const dryRun = args.includes('--dry-run') || args.includes('-n');
  const force = args.includes('--force') || args.includes('-f');

  // No path - show interactive wizard
  if (!pathArg) {
    const yamlFiles = await findYamlFiles(process.cwd());
    return {
      success: true,
      silent: true,
      wizard: createApplyWizard(ctx, yamlFiles),
    };
  }

  // Resolve path
  const filePath = path.isAbsolute(pathArg)
    ? pathArg
    : path.join(process.cwd(), pathArg);

  return await applyYamlFile(ctx, filePath, dryRun, force);
}

/**
 * Apply command definition
 */
const applyCommand: SlashCommand = {
  name: 'apply',
  aliases: ['deploy', 'push'],
  description: 'Apply YAML configuration files to the server',
  usage: '/apply [path] [--dry-run] [--force]',
  examples: [
    '/apply',
    '/apply agents/my_agent.yaml',
    '/apply agents/my_agent.yaml --dry-run',
    '/apply tools/my_tool.yaml --force',
  ],
  execute: handleApplyCommand,
};

/**
 * Register the apply command
 */
export function registerApplyCommand(): void {
  commandRegistry.register(applyCommand);
}

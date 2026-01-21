/**
 * /tool Command - Interactive tool creation and management
 *
 * Matches C# Agent.Cli tool commands:
 * - create: Create new tool (KustoTool, LinkTool, PythonTool)
 * - list: List tools (local or server)
 * - edit: Edit tool with VimEditor
 * - apply: Deploy tool to server
 * - delete: Delete tool from server
 * - validate: Validate tool YAML
 * - diff: Compare local vs remote
 * - migrate: Convert V1 to V2 format
 * - test: Test tool execution
 * - show-types: Display available tool types
 * - show-connectors: Display configured connectors
 */
import * as fs from 'fs/promises';
import * as path from 'path';
import * as yaml from 'yaml';
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult, WizardConfig, WizardStep } from './types';

export type ToolType = 'KustoTool' | 'LinkTool' | 'PythonTool';

interface ToolV2 {
  name: string;
  type: ToolType;
  description?: string;
  connector?: string;
  database?: string;
  query?: string;
  template?: string;
  functionCode?: string;
  timeout?: number;
  dependencies?: string[];
  parameters?: ToolParameter[];
}

interface ToolParameter {
  name: string;
  type: string;
  description?: string;
  required?: boolean;
  default?: string;
}

interface ServerTool {
  name: string;
  type: string;
  description?: string;
  connector?: string;
  database?: string;
  query?: string;
  template?: string;
  functionCode?: string;
  timeout?: number;
  dependencies?: string[];
  parameters?: ToolParameter[];
}

/**
 * Get auth headers for API calls
 */
async function getAuthHeaders(ctx: CommandContext): Promise<Record<string, string>> {
  try {
    const token = await ctx.services.auth?.getToken?.();
    if (token) {
      return {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'Authorization': `Bearer ${token}`,
      };
    }
  } catch {
    // Fall through to no auth
  }
  return {
    'Content-Type': 'application/json',
    'Accept': 'application/json',
  };
}

/**
 * Fetch tools from server
 */
async function fetchToolsFromServer(
  ctx: CommandContext,
  search?: string
): Promise<{ success: boolean; tools?: ServerTool[]; error?: string }> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, error: 'No server configured. Run /init first.' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const url = new URL('/api/v2/extendedAgent/tools', serverUrl);
    if (search) {
      url.searchParams.set('search', search);
    }

    const response = await fetch(url.toString(), { headers });

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}: ${response.statusText}` };
    }

    const tools = await response.json();
    return { success: true, tools: Array.isArray(tools) ? tools : [] };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Fetch a single tool from server
 */
async function fetchToolFromServer(
  ctx: CommandContext,
  toolName: string
): Promise<{ success: boolean; tool?: ServerTool; error?: string }> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, error: 'No server configured. Run /init first.' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/tools/${encodeURIComponent(toolName)}`,
      { headers }
    );

    if (response.status === 404) {
      return { success: false, error: `Tool '${toolName}' not found on server` };
    }

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}: ${response.statusText}` };
    }

    const tool = await response.json();
    return { success: true, tool };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Apply tool to server
 */
async function applyToolToServer(
  ctx: CommandContext,
  tool: ToolV2,
  dryRun: boolean = false
): Promise<{ success: boolean; message?: string; error?: string }> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, error: 'No server configured. Run /init first.' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const url = new URL(`/api/v2/extendedAgent/tools/${encodeURIComponent(tool.name)}`, serverUrl);
    if (dryRun) {
      url.searchParams.set('dryRun', 'true');
    }

    const response = await fetch(url.toString(), {
      method: 'PUT',
      headers,
      body: JSON.stringify(tool),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => null);
      return {
        success: false,
        error: errorData?.message || `HTTP ${response.status}: ${response.statusText}`,
      };
    }

    const result = await response.json().catch(() => ({}));
    return {
      success: true,
      message: dryRun ? 'Dry run completed successfully' : (result.message || 'Tool applied successfully'),
    };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Delete tool from server
 */
async function deleteToolFromServer(
  ctx: CommandContext,
  toolName: string,
  dryRun: boolean = false
): Promise<{ success: boolean; message?: string; error?: string; dependentAgents?: string[] }> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, error: 'No server configured. Run /init first.' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const url = new URL(`/api/v2/extendedAgent/tools/${encodeURIComponent(toolName)}`, serverUrl);
    if (dryRun) {
      url.searchParams.set('dryRun', 'true');
    }

    const response = await fetch(url.toString(), {
      method: 'DELETE',
      headers,
    });

    if (response.status === 409) {
      // Conflict - tool is in use
      const data = await response.json().catch(() => ({}));
      return {
        success: false,
        error: 'Tool is in use by agents',
        dependentAgents: data.dependentAgents || [],
      };
    }

    if (response.status === 204 || response.status === 404) {
      return { success: true, message: 'Tool deleted successfully' };
    }

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}: ${response.statusText}` };
    }

    return { success: true, message: dryRun ? 'Dry run: tool would be deleted' : 'Tool deleted successfully' };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Fetch data connectors from server
 */
async function fetchConnectors(
  ctx: CommandContext
): Promise<{ success: boolean; connectors?: string[]; error?: string }> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, error: 'No server configured. Run /init first.' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v1/extendedAgent/dataconnectors`,
      { headers }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}: ${response.statusText}` };
    }

    const connectors = await response.json();
    return { success: true, connectors: Array.isArray(connectors) ? connectors : [] };
  } catch (error) {
    return { success: false, error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Test a Python tool
 */
async function testPythonTool(
  ctx: CommandContext,
  functionCode: string,
  parameters: Record<string, string> = {},
  dependencies: string[] = [],
  timeoutSeconds = 120
): Promise<{
  success: boolean;
  result?: unknown;
  stdout?: string;
  stderr?: string;
  executionTimeMs?: number;
  errorMessage?: string;
}> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, errorMessage: 'No server configured' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v1/extendedAgent/tools/python/test`,
      {
        method: 'POST',
        headers,
        body: JSON.stringify({
          FunctionCode: functionCode,
          TimeoutSeconds: timeoutSeconds,
          Parameters: parameters,
          Dependencies: dependencies,
          ParameterDefinitions: [],
          AuthEnabled: false,
        }),
      }
    );

    if (!response.ok) {
      return { success: false, errorMessage: `HTTP ${response.status}: ${response.statusText}` };
    }

    const data = await response.json();
    return {
      success: data.Success,
      result: data.Result,
      stdout: data.Stdout,
      stderr: data.Stderr,
      executionTimeMs: data.ExecutionTimeMs,
      errorMessage: data.ErrorMessage,
    };
  } catch (error) {
    return { success: false, errorMessage: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Test a Kusto query
 */
async function testKustoTool(
  ctx: CommandContext,
  toolName: string,
  query: string,
  connector: string,
  database: string,
  parameters: Record<string, string> = {}
): Promise<{
  success: boolean;
  rowCount?: number;
  columns?: string[];
  rows?: Record<string, unknown>[];
  executionTimeMs?: number;
  errorMessage?: string;
}> {
  const serverUrl = ctx.services.config.get().server?.url;
  if (!serverUrl) {
    return { success: false, errorMessage: 'No server configured' };
  }

  try {
    const headers = await getAuthHeaders(ctx);
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v1/extendedAgent/tools/${encodeURIComponent(toolName)}/test`,
      {
        method: 'POST',
        headers,
        body: JSON.stringify({
          Query: query,
          Connector: connector,
          Database: database,
          Mode: 'query',
          Parameters: parameters,
        }),
      }
    );

    if (!response.ok) {
      return { success: false, errorMessage: `HTTP ${response.status}: ${response.statusText}` };
    }

    const data = await response.json();
    return {
      success: data.Success,
      rowCount: data.RowCount,
      columns: data.Columns,
      rows: data.Rows,
      executionTimeMs: data.ExecutionTimeMs,
      errorMessage: data.ErrorMessage,
    };
  } catch (error) {
    return { success: false, errorMessage: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Find tool file by name
 */
async function findToolFile(toolName: string): Promise<string | null> {
  const toolsDir = path.join(process.cwd(), 'tools');

  // Try different file patterns
  const patterns = [
    `${toolName}.yaml`,
    `${toolName}.yml`,
    `${toolName}/${toolName}.yaml`,
    `${toolName}/${toolName}.yml`,
  ];

  for (const pattern of patterns) {
    const filePath = path.join(toolsDir, pattern);
    try {
      await fs.access(filePath);
      return filePath;
    } catch {
      // Continue to next pattern
    }
  }

  // Try recursive search
  try {
    const searchDir = async (dir: string): Promise<string | null> => {
      const entries = await fs.readdir(dir, { withFileTypes: true });
      for (const entry of entries) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          const found = await searchDir(fullPath);
          if (found) return found;
        } else if (
          entry.name === `${toolName}.yaml` ||
          entry.name === `${toolName}.yml`
        ) {
          return fullPath;
        }
      }
      return null;
    };
    return await searchDir(toolsDir);
  } catch {
    return null;
  }
}

/**
 * Parse tool from YAML file
 */
async function parseToolFile(filePath: string): Promise<{ tool?: ToolV2; error?: string }> {
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    const parsed = yaml.parse(content);

    if (!parsed || typeof parsed !== 'object') {
      return { error: 'Invalid YAML format' };
    }

    // Handle ToolList (multi-document)
    if (parsed.tools && Array.isArray(parsed.tools)) {
      return { tool: parsed.tools[0] as ToolV2 };
    }

    return { tool: parsed as ToolV2 };
  } catch (error) {
    return { error: error instanceof Error ? error.message : String(error) };
  }
}

/**
 * Get local tools
 */
async function getLocalTools(): Promise<{ name: string; type: string; filePath: string }[]> {
  const toolsDir = path.join(process.cwd(), 'tools');
  const tools: { name: string; type: string; filePath: string }[] = [];

  const scanDir = async (dir: string) => {
    try {
      const entries = await fs.readdir(dir, { withFileTypes: true });
      for (const entry of entries) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
          await scanDir(fullPath);
        } else if (entry.name.endsWith('.yaml') || entry.name.endsWith('.yml')) {
          try {
            const content = await fs.readFile(fullPath, 'utf-8');
            const parsed = yaml.parse(content);
            const name = parsed?.name || entry.name.replace(/\.ya?ml$/, '');
            const type = parsed?.type || 'Unknown';
            tools.push({ name, type, filePath: fullPath });
          } catch {
            // Skip invalid files
          }
        }
      }
    } catch {
      // Directory doesn't exist or not readable
    }
  };

  await scanDir(toolsDir);
  return tools;
}

/**
 * Generate tool YAML template
 */
function generateToolTemplate(name: string, type: ToolType, options: Partial<ToolV2> = {}): string {
  const tool: ToolV2 = {
    name,
    type,
    description: options.description || `${name} tool`,
    ...options,
  };

  if (type === 'KustoTool') {
    tool.connector = options.connector || 'your-kusto-cluster';
    tool.database = options.database || 'your-database';
    tool.query = options.query || `// Your Kusto query here
// Use {paramName} for parameters
YourTable
| where TimeGenerated > ago(1h)
| take 100`;
    tool.parameters = options.parameters || [
      { name: 'limit', type: 'int', description: 'Number of rows to return', default: '100' },
    ];
  } else if (type === 'LinkTool') {
    tool.template = options.template || 'https://example.com/dashboard?id={resourceId}';
    tool.parameters = options.parameters || [
      { name: 'resourceId', type: 'string', description: 'Resource identifier', required: true },
    ];
  } else if (type === 'PythonTool') {
    tool.functionCode = options.functionCode || `def main(params):
    """
    Main function for the tool.

    Args:
        params: Dictionary of input parameters

    Returns:
        Result object or string
    """
    # Your code here
    return {"status": "success", "message": "Hello from Python!"}`;
    tool.timeout = options.timeout || 30;
    tool.dependencies = options.dependencies || [];
    tool.parameters = options.parameters || [
      { name: 'input', type: 'string', description: 'Input parameter' },
    ];
  }

  return yaml.stringify(tool);
}

/**
 * Create wizard for tool creation
 */
function createToolWizard(ctx: CommandContext): WizardConfig {
  const steps: WizardStep[] = [
    {
      id: 'type',
      title: 'Select Tool Type',
      prompt: 'What type of tool do you want to create?',
      type: 'select',
      options: [
        { key: 'KustoTool', label: 'KustoTool', description: 'Query Azure Data Explorer (Kusto)' },
        { key: 'LinkTool', label: 'LinkTool', description: 'Generate URL links with parameters' },
        { key: 'PythonTool', label: 'PythonTool', description: 'Execute custom Python code' },
      ],
    },
    {
      id: 'name',
      title: 'Tool Name',
      prompt: 'Enter a name for the tool (lowercase, underscores allowed):',
      type: 'input',
      placeholder: 'my_tool',
    },
    {
      id: 'description',
      title: 'Description',
      prompt: 'Enter a brief description of what this tool does:',
      type: 'input',
      placeholder: 'Describe the tool purpose',
    },
  ];

  return {
    id: 'tool-create',
    title: 'Create New Tool',
    steps,
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      const toolName = data.name || 'new_tool';
      const toolType = data.type as ToolType;
      const description = data.description || '';

      // Generate template
      const content = generateToolTemplate(toolName, toolType, { description });

      // Ensure tools directory exists
      const toolsDir = path.join(process.cwd(), 'tools');
      await fs.mkdir(toolsDir, { recursive: true });

      const filePath = path.join(toolsDir, `${toolName}.yaml`);

      // Check if exists
      try {
        await fs.access(filePath);
        return {
          success: false,
          message: `Tool file already exists: ${filePath}\nUse /tool edit ${toolName} to modify it.`,
        };
      } catch {
        // Good - doesn't exist
      }

      // Write and open in editor
      await fs.writeFile(filePath, content, 'utf-8');

      return {
        success: true,
        message: `Created ${toolType}: ${toolName}`,
        editor: {
          content,
          filename: `${toolName}.yaml`,
          filePath,
          fileType: 'yaml',
        },
      };
    },
  };
}

/**
 * Create wizard for tool apply
 */
function createApplyWizard(ctx: CommandContext, tools: { name: string; filePath: string }[]): WizardConfig {
  const steps: WizardStep[] = [
    {
      id: 'tool',
      title: 'Select Tool to Apply',
      prompt: 'Which tool do you want to deploy to the server?',
      type: 'select',
      options: tools.map((t) => ({
        key: t.name,
        label: t.name,
        description: path.relative(process.cwd(), t.filePath),
      })),
    },
    {
      id: 'dryRun',
      title: 'Dry Run?',
      prompt: 'Do you want to perform a dry run first (preview without applying)?',
      type: 'confirm',
    },
  ];

  return {
    id: 'tool-apply',
    title: 'Apply Tool to Server',
    steps,
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      const toolName = data.tool;
      const dryRun = data.dryRun === 'yes';

      const filePath = await findToolFile(toolName);
      if (!filePath) {
        return { success: false, message: `Tool file not found: ${toolName}` };
      }

      const { tool, error } = await parseToolFile(filePath);
      if (error || !tool) {
        return { success: false, message: `Failed to parse tool: ${error}` };
      }

      const result = await applyToolToServer(ctx, tool, dryRun);

      if (result.success) {
        return {
          success: true,
          message: dryRun
            ? `✓ Dry run successful for '${toolName}'\n\nRun without dry-run to apply changes.`
            : `✓ Successfully applied tool '${toolName}' to server`,
        };
      } else {
        return { success: false, message: `Failed to apply tool: ${result.error}` };
      }
    },
  };
}

/**
 * Create wizard for tool deletion
 */
function createDeleteWizard(ctx: CommandContext, toolName: string): WizardConfig {
  const steps: WizardStep[] = [
    {
      id: 'target',
      title: 'Delete From',
      prompt: `Where do you want to delete '${toolName}' from?`,
      type: 'select',
      options: [
        { key: 'server', label: 'Server only', description: 'Delete from remote server' },
        { key: 'local', label: 'Local only', description: 'Delete local file' },
        { key: 'both', label: 'Both', description: 'Delete from server and local' },
      ],
    },
    {
      id: 'confirm',
      title: 'Confirm Deletion',
      prompt: `Are you sure you want to delete '${toolName}'? This cannot be undone.`,
      type: 'confirm',
    },
  ];

  return {
    id: 'tool-delete',
    title: `Delete Tool: ${toolName}`,
    steps,
    currentStep: 0,
    data: { toolName },
    onComplete: async (data) => {
      if (data.confirm !== 'yes') {
        return { success: true, message: 'Deletion cancelled.' };
      }

      const name = data.toolName;
      const target = data.target;
      const results: string[] = [];

      // Delete from server
      if (target === 'server' || target === 'both') {
        const result = await deleteToolFromServer(ctx, name);
        if (result.success) {
          results.push(`✓ Deleted from server`);
        } else if (result.dependentAgents && result.dependentAgents.length > 0) {
          results.push(`✗ Cannot delete from server - used by agents: ${result.dependentAgents.join(', ')}`);
        } else {
          results.push(`✗ Server deletion failed: ${result.error}`);
        }
      }

      // Delete local file
      if (target === 'local' || target === 'both') {
        const filePath = await findToolFile(name);
        if (filePath) {
          try {
            await fs.unlink(filePath);
            results.push(`✓ Deleted local file: ${path.relative(process.cwd(), filePath)}`);
          } catch (error) {
            results.push(`✗ Failed to delete local file: ${error}`);
          }
        } else {
          results.push(`✗ Local file not found`);
        }
      }

      return { success: true, message: results.join('\n') };
    },
  };
}

/**
 * Create wizard for syncing tools from server
 */
function createSyncWizard(ctx: CommandContext): WizardConfig {
  const steps: WizardStep[] = [
    {
      id: 'confirm',
      title: 'Sync Tools from Server',
      prompt: 'This will download all tools from the server to your local tools/ directory. Existing files with the same name will be overwritten. Continue?',
      type: 'confirm',
    },
  ];

  return {
    id: 'tool-sync',
    title: 'Sync Tools from Server',
    steps,
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.confirm !== 'yes') {
        return { success: true, message: 'Sync cancelled.' };
      }

      const result = await fetchToolsFromServer(ctx);
      if (!result.success || !result.tools) {
        return { success: false, message: `Failed to fetch tools: ${result.error}` };
      }

      if (result.tools.length === 0) {
        return { success: true, message: 'No tools found on server.' };
      }

      // Ensure tools directory
      const toolsDir = path.join(process.cwd(), 'tools');
      await fs.mkdir(toolsDir, { recursive: true });

      const results: string[] = [];
      let successCount = 0;

      for (const tool of result.tools) {
        try {
          const content = yaml.stringify(tool);
          const filePath = path.join(toolsDir, `${tool.name}.yaml`);
          await fs.writeFile(filePath, content, 'utf-8');
          results.push(`✓ ${tool.name}`);
          successCount++;
        } catch (error) {
          results.push(`✗ ${tool.name}: ${error}`);
        }
      }

      return {
        success: true,
        message: `Synced ${successCount}/${result.tools.length} tools:\n\n${results.join('\n')}`,
      };
    },
  };
}

/**
 * Handle /tool list command
 */
async function handleListCommand(ctx: CommandContext): Promise<CommandResult> {
  const showServer = ctx.args.includes('--server') || ctx.args.includes('-s');
  const searchArg = ctx.args.find((a) => a.startsWith('--search='));
  const search = searchArg?.split('=')[1];

  if (showServer) {
    // List from server
    ctx.onOutput('Fetching tools from server...');
    const result = await fetchToolsFromServer(ctx, search);

    if (!result.success) {
      return { success: false, message: `Failed to fetch tools: ${result.error}` };
    }

    if (!result.tools || result.tools.length === 0) {
      ctx.onOutput('No tools found on server.');
      return { success: true };
    }

    ctx.onOutput(`\nServer Tools (${result.tools.length}):\n`);
    for (const tool of result.tools) {
      ctx.onOutput(`  • ${tool.name}`);
      ctx.onOutput(`    Type: ${tool.type}`);
      if (tool.description) {
        ctx.onOutput(`    ${tool.description}`);
      }
    }
  } else {
    // List local tools
    const tools = await getLocalTools();

    if (tools.length === 0) {
      ctx.onOutput('No local tools found in tools/ directory.');
      ctx.onOutput('');
      ctx.onOutput('Use /tool create to create a new tool, or');
      ctx.onOutput('Use /tool list --server to list server tools.');
      return { success: true };
    }

    ctx.onOutput(`\nLocal Tools (${tools.length}):\n`);
    for (const tool of tools) {
      ctx.onOutput(`  • ${tool.name}`);
      ctx.onOutput(`    Type: ${tool.type}`);
      ctx.onOutput(`    File: ${path.relative(process.cwd(), tool.filePath)}`);
    }
  }

  ctx.onOutput('');
  ctx.onOutput('Commands:');
  ctx.onOutput('  /tool edit <name>    Edit a tool');
  ctx.onOutput('  /tool apply <name>   Apply to server');
  ctx.onOutput('  /tool test <name>    Test a tool');

  return { success: true };
}

/**
 * Handle /tool edit command
 */
async function handleEditCommand(ctx: CommandContext, toolName: string): Promise<CommandResult> {
  const filePath = await findToolFile(toolName);

  if (!filePath) {
    return {
      success: false,
      message: `Tool not found: ${toolName}\n\nUse /tool list to see available tools.`,
    };
  }

  const content = await fs.readFile(filePath, 'utf-8');

  return {
    success: true,
    silent: true,
    editor: {
      content,
      filename: path.basename(filePath),
      filePath,
      fileType: 'yaml',
    },
  };
}

/**
 * Validate a single tool
 */
async function validateSingleTool(ctx: CommandContext, toolName: string): Promise<CommandResult> {
  const filePath = await findToolFile(toolName);

  if (!filePath) {
    return { success: false, message: `Tool not found: ${toolName}` };
  }

  const { tool, error } = await parseToolFile(filePath);

  if (error) {
    return { success: false, message: `YAML parse error: ${error}` };
  }

  if (!tool) {
    return { success: false, message: 'Failed to parse tool' };
  }

  // Validate required fields
  const errors: string[] = [];

  if (!tool.name) {
    errors.push('Missing required field: name');
  }

  if (!tool.type) {
    errors.push('Missing required field: type');
  } else if (!['KustoTool', 'LinkTool', 'PythonTool'].includes(tool.type)) {
    errors.push(`Invalid tool type: ${tool.type}`);
  }

  // Type-specific validation
  if (tool.type === 'KustoTool') {
    if (!tool.connector) errors.push('KustoTool requires connector');
    if (!tool.database) errors.push('KustoTool requires database');
    if (!tool.query) errors.push('KustoTool requires query');
  } else if (tool.type === 'LinkTool') {
    if (!tool.template) errors.push('LinkTool requires template');
  } else if (tool.type === 'PythonTool') {
    if (!tool.functionCode) errors.push('PythonTool requires functionCode');
  }

  if (errors.length > 0) {
    ctx.onOutput(`✗ Validation failed for '${toolName}':\n`);
    errors.forEach((e) => ctx.onOutput(`  • ${e}`));
    return { success: false };
  }

  ctx.onOutput(`✓ Tool '${toolName}' is valid`);
  return { success: true };
}

/**
 * Validate all local tools
 */
async function validateAllTools(ctx: CommandContext): Promise<CommandResult> {
  const tools = await getLocalTools();

  if (tools.length === 0) {
    return { success: true, message: 'No tools found to validate.' };
  }

  ctx.onOutput(`Validating ${tools.length} tool(s)...\n`);

  let validCount = 0;
  let invalidCount = 0;

  for (const { name, filePath } of tools) {
    const { tool, error } = await parseToolFile(filePath);

    if (error || !tool) {
      ctx.onOutput(`✗ ${name}: Parse error - ${error}`);
      invalidCount++;
      continue;
    }

    // Quick validation
    const errors: string[] = [];
    if (!tool.name) errors.push('missing name');
    if (!tool.type) errors.push('missing type');

    if (errors.length > 0) {
      ctx.onOutput(`✗ ${name}: ${errors.join(', ')}`);
      invalidCount++;
    } else {
      ctx.onOutput(`✓ ${name}`);
      validCount++;
    }
  }

  ctx.onOutput('');
  ctx.onOutput(`Results: ${validCount} valid, ${invalidCount} invalid`);

  return { success: invalidCount === 0 };
}

/**
 * Test a tool
 */
async function testTool(ctx: CommandContext, toolName: string): Promise<CommandResult> {
  const filePath = await findToolFile(toolName);

  if (!filePath) {
    return { success: false, message: `Tool not found: ${toolName}` };
  }

  const { tool, error } = await parseToolFile(filePath);

  if (error || !tool) {
    return { success: false, message: `Failed to parse tool: ${error}` };
  }

  ctx.onOutput(`Testing ${tool.type}: ${toolName}...\n`);

  if (tool.type === 'PythonTool') {
    if (!tool.functionCode) {
      return { success: false, message: 'PythonTool requires functionCode' };
    }

    ctx.onOutput('Executing Python code...');

    const result = await testPythonTool(
      ctx,
      tool.functionCode,
      {},
      tool.dependencies || [],
      tool.timeout || 30
    );

    if (result.success) {
      ctx.onOutput(`✓ Test passed (${result.executionTimeMs}ms)`);
      if (result.stdout) {
        ctx.onOutput('\nOutput:');
        ctx.onOutput(result.stdout);
      }
      if (result.result !== undefined) {
        ctx.onOutput(`\nResult: ${JSON.stringify(result.result, null, 2)}`);
      }
    } else {
      ctx.onOutput(`✗ Test failed`);
      if (result.errorMessage) {
        ctx.onOutput(`Error: ${result.errorMessage}`);
      }
      if (result.stderr) {
        ctx.onOutput('\nStderr:');
        ctx.onOutput(result.stderr);
      }
    }

    return { success: result.success };
  } else if (tool.type === 'KustoTool') {
    if (!tool.connector || !tool.database || !tool.query) {
      return { success: false, message: 'KustoTool requires connector, database, and query' };
    }

    ctx.onOutput(`Connector: ${tool.connector}`);
    ctx.onOutput(`Database: ${tool.database}`);
    ctx.onOutput('Executing query...');

    const result = await testKustoTool(
      ctx,
      toolName,
      tool.query,
      tool.connector,
      tool.database
    );

    if (result.success) {
      ctx.onOutput(`✓ Query successful (${result.executionTimeMs}ms)`);
      ctx.onOutput(`Rows: ${result.rowCount}`);
      if (result.columns && result.columns.length > 0) {
        ctx.onOutput(`Columns: ${result.columns.join(', ')}`);
      }
      if (result.rows && result.rows.length > 0) {
        ctx.onOutput('\nSample results (first 5 rows):');
        result.rows.slice(0, 5).forEach((row, i) => {
          ctx.onOutput(`  ${i + 1}. ${JSON.stringify(row)}`);
        });
      }
    } else {
      ctx.onOutput(`✗ Query failed`);
      if (result.errorMessage) {
        ctx.onOutput(`Error: ${result.errorMessage}`);
      }
    }

    return { success: result.success };
  } else if (tool.type === 'LinkTool') {
    if (!tool.template) {
      return { success: false, message: 'LinkTool requires template' };
    }

    // Just validate the template
    const placeholders = tool.template.match(/\{([^}]+)\}/g) || [];

    ctx.onOutput(`✓ LinkTool template is valid`);
    ctx.onOutput(`Template: ${tool.template}`);
    if (placeholders.length > 0) {
      ctx.onOutput(`Placeholders: ${placeholders.join(', ')}`);
    }

    return { success: true };
  }

  return { success: false, message: `Testing not supported for type: ${tool.type}` };
}

/**
 * Diff local vs remote tool
 */
async function diffTool(ctx: CommandContext, toolName: string): Promise<CommandResult> {
  // Get local
  const filePath = await findToolFile(toolName);
  if (!filePath) {
    return { success: false, message: `Local tool not found: ${toolName}` };
  }

  const localContent = await fs.readFile(filePath, 'utf-8');
  const localParsed = yaml.parse(localContent);

  // Get remote
  const result = await fetchToolFromServer(ctx, toolName);
  if (!result.success || !result.tool) {
    return { success: false, message: `Remote tool not found: ${result.error}` };
  }

  const remoteParsed = result.tool;

  // Normalize for comparison
  const localYaml = yaml.stringify(localParsed);
  const remoteYaml = yaml.stringify(remoteParsed);

  if (localYaml === remoteYaml) {
    ctx.onOutput(`✓ Tool '${toolName}' is in sync with server`);
    return { success: true };
  }

  ctx.onOutput(`Tool '${toolName}' differs from server:\n`);
  ctx.onOutput('--- Local');
  ctx.onOutput('+++ Server');
  ctx.onOutput('');

  // Simple line-by-line diff
  const localLines = localYaml.split('\n');
  const remoteLines = remoteYaml.split('\n');
  const maxLines = Math.max(localLines.length, remoteLines.length);

  for (let i = 0; i < maxLines; i++) {
    const localLine = localLines[i] || '';
    const remoteLine = remoteLines[i] || '';

    if (localLine !== remoteLine) {
      if (localLine) ctx.onOutput(`- ${localLine}`);
      if (remoteLine) ctx.onOutput(`+ ${remoteLine}`);
    }
  }

  return { success: true };
}

/**
 * Migrate V1 tools to V2 format
 */
async function migrateTool(ctx: CommandContext, toolName: string, dryRun: boolean): Promise<CommandResult> {
  const filePath = await findToolFile(toolName);
  if (!filePath) {
    return { success: false, message: `Tool not found: ${toolName}` };
  }

  const content = await fs.readFile(filePath, 'utf-8');
  const parsed = yaml.parse(content);

  // Check if already V2 (has 'type' field at top level)
  if (parsed.type && ['KustoTool', 'LinkTool', 'PythonTool'].includes(parsed.type)) {
    ctx.onOutput(`Tool '${toolName}' is already V2 format`);
    return { success: true };
  }

  // V1 detection and conversion
  let v2Tool: ToolV2;

  if (parsed.kustoQuery || parsed.query) {
    // V1 Kusto tool
    v2Tool = {
      name: parsed.name || toolName,
      type: 'KustoTool',
      description: parsed.description,
      connector: parsed.connector || parsed.cluster,
      database: parsed.database,
      query: parsed.kustoQuery || parsed.query,
      parameters: parsed.parameters,
    };
  } else if (parsed.linkTemplate || parsed.template) {
    // V1 Link tool
    v2Tool = {
      name: parsed.name || toolName,
      type: 'LinkTool',
      description: parsed.description,
      template: parsed.linkTemplate || parsed.template,
      parameters: parsed.parameters,
    };
  } else if (parsed.pythonCode || parsed.functionCode) {
    // V1 Python tool
    v2Tool = {
      name: parsed.name || toolName,
      type: 'PythonTool',
      description: parsed.description,
      functionCode: parsed.pythonCode || parsed.functionCode,
      timeout: parsed.timeout,
      dependencies: parsed.dependencies,
      parameters: parsed.parameters,
    };
  } else {
    return { success: false, message: `Cannot determine tool type for migration` };
  }

  const newContent = yaml.stringify(v2Tool);

  if (dryRun) {
    ctx.onOutput(`Would migrate '${toolName}' to V2 format:\n`);
    ctx.onOutput(newContent);
    return { success: true };
  }

  // Write migrated file
  await fs.writeFile(filePath, newContent, 'utf-8');
  ctx.onOutput(`✓ Migrated '${toolName}' to V2 format`);

  return { success: true };
}

/**
 * Show available tool types
 */
async function showToolTypes(ctx: CommandContext, specificType?: string): Promise<CommandResult> {
  const types = [
    {
      name: 'KustoTool',
      description: 'Execute Kusto queries against Azure Data Explorer clusters',
      sample: generateToolTemplate('example_kusto', 'KustoTool'),
    },
    {
      name: 'LinkTool',
      description: 'Generate URLs based on templates with parameter substitution',
      sample: generateToolTemplate('example_link', 'LinkTool'),
    },
    {
      name: 'PythonTool',
      description: 'Execute custom Python code with configurable dependencies',
      sample: generateToolTemplate('example_python', 'PythonTool'),
    },
  ];

  if (specificType) {
    const type = types.find((t) => t.name.toLowerCase() === specificType.toLowerCase());
    if (!type) {
      return { success: false, message: `Unknown tool type: ${specificType}` };
    }

    ctx.onOutput(`\n${type.name}\n${'─'.repeat(type.name.length)}`);
    ctx.onOutput(type.description);
    ctx.onOutput('\nSample YAML:\n');
    ctx.onOutput(type.sample);
  } else {
    ctx.onOutput('\nAvailable Tool Types:\n');
    for (const type of types) {
      ctx.onOutput(`  ${type.name}`);
      ctx.onOutput(`    ${type.description}`);
      ctx.onOutput('');
    }
    ctx.onOutput('Use /tool show-types <type> for sample YAML');
  }

  return { success: true };
}

/**
 * Show available connectors
 */
async function showConnectors(ctx: CommandContext): Promise<CommandResult> {
  ctx.onOutput('Fetching connectors from server...');

  const result = await fetchConnectors(ctx);

  if (!result.success) {
    return { success: false, message: `Failed to fetch connectors: ${result.error}` };
  }

  if (!result.connectors || result.connectors.length === 0) {
    ctx.onOutput('No data connectors configured on server.');
  } else {
    ctx.onOutput('\nConfigured Data Connectors:\n');
    for (const connector of result.connectors) {
      ctx.onOutput(`  • ${connector}`);
    }
  }

  ctx.onOutput('\nAvailable Connector Types:');
  ctx.onOutput('  • Kusto - Azure Data Explorer');
  ctx.onOutput('  • LogAnalytics - Azure Log Analytics workspace');
  ctx.onOutput('  • SqlServer - SQL Server database');

  return { success: true };
}

/**
 * /tool command handler
 */
async function handleToolCommand(ctx: CommandContext): Promise<CommandResult> {
  const subCommand = ctx.args[0]?.toLowerCase();
  const toolName = ctx.args[1];

  // No subcommand - show interactive menu
  if (!subCommand) {
    const tools = await getLocalTools();

    const steps: WizardStep[] = [
      {
        id: 'action',
        title: 'Tool Management',
        prompt: 'What would you like to do?',
        type: 'select',
        options: [
          { key: 'create', label: 'Create new tool', description: 'Create a new tool configuration' },
          { key: 'list', label: 'List tools', description: 'View local and server tools' },
          { key: 'edit', label: 'Edit tool', description: 'Modify an existing tool' },
          { key: 'apply', label: 'Apply tool', description: 'Deploy a tool to the server' },
          { key: 'test', label: 'Test tool', description: 'Test a tool execution' },
          { key: 'delete', label: 'Delete tool', description: 'Remove a tool' },
          { key: 'validate', label: 'Validate tools', description: 'Check tool configurations' },
          { key: 'diff', label: 'Compare with server', description: 'See differences from remote' },
          { key: 'sync', label: 'Sync from server', description: 'Download tools from server' },
          { key: 'types', label: 'Show tool types', description: 'View available tool types' },
          { key: 'connectors', label: 'Show connectors', description: 'View data connectors' },
        ],
      },
    ];

    return {
      success: true,
      silent: true,
      wizard: {
        id: 'tool-menu',
        title: 'Tool Management',
        steps,
        currentStep: 0,
        data: {},
        onComplete: async (data) => {
          const action = data.action;

          switch (action) {
            case 'create':
              return { success: true, wizard: createToolWizard(ctx) };

            case 'list':
              return handleListCommand(ctx);

            case 'edit':
              if (tools.length === 0) {
                return { success: false, message: 'No local tools found. Create one first with /tool create' };
              }
              return {
                success: true,
                wizard: {
                  id: 'tool-edit-select',
                  title: 'Edit Tool',
                  steps: [{
                    id: 'tool',
                    title: 'Select Tool',
                    prompt: 'Which tool do you want to edit?',
                    type: 'select',
                    options: tools.map((t) => ({
                      key: t.name,
                      label: t.name,
                      description: t.type,
                    })),
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => handleEditCommand(ctx, d.tool),
                },
              };

            case 'apply':
              if (tools.length === 0) {
                return { success: false, message: 'No local tools found.' };
              }
              return { success: true, wizard: createApplyWizard(ctx, tools) };

            case 'test':
              if (tools.length === 0) {
                return { success: false, message: 'No local tools found.' };
              }
              return {
                success: true,
                wizard: {
                  id: 'tool-test-select',
                  title: 'Test Tool',
                  steps: [{
                    id: 'tool',
                    title: 'Select Tool',
                    prompt: 'Which tool do you want to test?',
                    type: 'select',
                    options: tools.map((t) => ({
                      key: t.name,
                      label: t.name,
                      description: t.type,
                    })),
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => testTool(ctx, d.tool),
                },
              };

            case 'delete':
              if (tools.length === 0) {
                return { success: false, message: 'No local tools found.' };
              }
              return {
                success: true,
                wizard: {
                  id: 'tool-delete-select',
                  title: 'Delete Tool',
                  steps: [{
                    id: 'tool',
                    title: 'Select Tool',
                    prompt: 'Which tool do you want to delete?',
                    type: 'select',
                    options: tools.map((t) => ({
                      key: t.name,
                      label: t.name,
                      description: t.type,
                    })),
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => ({
                    success: true,
                    wizard: createDeleteWizard(ctx, d.tool),
                  }),
                },
              };

            case 'validate':
              return validateAllTools(ctx);

            case 'diff':
              if (tools.length === 0) {
                return { success: false, message: 'No local tools found.' };
              }
              return {
                success: true,
                wizard: {
                  id: 'tool-diff-select',
                  title: 'Compare Tool',
                  steps: [{
                    id: 'tool',
                    title: 'Select Tool',
                    prompt: 'Which tool do you want to compare with server?',
                    type: 'select',
                    options: tools.map((t) => ({
                      key: t.name,
                      label: t.name,
                      description: t.type,
                    })),
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => diffTool(ctx, d.tool),
                },
              };

            case 'sync':
              return { success: true, wizard: createSyncWizard(ctx) };

            case 'types':
              return showToolTypes(ctx);

            case 'connectors':
              return showConnectors(ctx);

            default:
              return { success: false, message: `Unknown action: ${action}` };
          }
        },
      },
    };
  }

  // Handle subcommands
  switch (subCommand) {
    case 'create': {
      // If no args, show wizard
      if (!toolName) {
        return { success: true, silent: true, wizard: createToolWizard(ctx) };
      }
      // If type specified as first arg
      const type = toolName as ToolType;
      const name = ctx.args[2];
      if (['kustotool', 'linktool', 'pythontool'].includes(type.toLowerCase())) {
        const actualType = type.charAt(0).toUpperCase() + type.slice(1).toLowerCase() as ToolType;
        const content = generateToolTemplate(name || 'new_tool', actualType);
        const toolsDir = path.join(process.cwd(), 'tools');
        await fs.mkdir(toolsDir, { recursive: true });
        const filePath = path.join(toolsDir, `${name || 'new_tool'}.yaml`);
        await fs.writeFile(filePath, content, 'utf-8');
        return {
          success: true,
          editor: { content, filename: `${name || 'new_tool'}.yaml`, filePath, fileType: 'yaml' },
        };
      }
      return { success: true, silent: true, wizard: createToolWizard(ctx) };
    }

    case 'list':
      return handleListCommand(ctx);

    case 'edit':
      if (!toolName) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: false, message: 'No tools found. Create one with /tool create' };
        }
        return {
          success: true,
          silent: true,
          wizard: {
            id: 'tool-edit-select',
            title: 'Edit Tool',
            steps: [{
              id: 'tool',
              title: 'Select Tool',
              prompt: 'Which tool do you want to edit?',
              type: 'select',
              options: tools.map((t) => ({
                key: t.name,
                label: t.name,
                description: t.type,
              })),
            }],
            currentStep: 0,
            data: {},
            onComplete: async (d) => handleEditCommand(ctx, d.tool),
          },
        };
      }
      return handleEditCommand(ctx, toolName);

    case 'apply': {
      const dryRun = ctx.args.includes('--dry-run');
      if (!toolName) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: false, message: 'No tools found.' };
        }
        return { success: true, silent: true, wizard: createApplyWizard(ctx, tools) };
      }

      const filePath = await findToolFile(toolName);
      if (!filePath) {
        return { success: false, message: `Tool not found: ${toolName}` };
      }

      const { tool, error } = await parseToolFile(filePath);
      if (error || !tool) {
        return { success: false, message: `Failed to parse tool: ${error}` };
      }

      ctx.onOutput(`Applying tool '${toolName}'${dryRun ? ' (dry run)' : ''}...`);
      const result = await applyToolToServer(ctx, tool, dryRun);

      if (result.success) {
        ctx.onOutput(`✓ ${result.message}`);
        return { success: true };
      }
      return { success: false, message: result.error };
    }

    case 'delete':
      if (!toolName) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: false, message: 'No tools found.' };
        }
        return {
          success: true,
          silent: true,
          wizard: {
            id: 'tool-delete-select',
            title: 'Delete Tool',
            steps: [{
              id: 'tool',
              title: 'Select Tool',
              prompt: 'Which tool do you want to delete?',
              type: 'select',
              options: tools.map((t) => ({
                key: t.name,
                label: t.name,
                description: t.type,
              })),
            }],
            currentStep: 0,
            data: {},
            onComplete: async (d) => ({
              success: true,
              wizard: createDeleteWizard(ctx, d.tool),
            }),
          },
        };
      }
      return { success: true, silent: true, wizard: createDeleteWizard(ctx, toolName) };

    case 'validate': {
      const all = ctx.args.includes('--all') || ctx.args.includes('-a');
      if (all || !toolName) {
        return validateAllTools(ctx);
      }
      return validateSingleTool(ctx, toolName);
    }

    case 'test':
      if (!toolName) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: false, message: 'No tools found.' };
        }
        return {
          success: true,
          silent: true,
          wizard: {
            id: 'tool-test-select',
            title: 'Test Tool',
            steps: [{
              id: 'tool',
              title: 'Select Tool',
              prompt: 'Which tool do you want to test?',
              type: 'select',
              options: tools.map((t) => ({
                key: t.name,
                label: t.name,
                description: t.type,
              })),
            }],
            currentStep: 0,
            data: {},
            onComplete: async (d) => testTool(ctx, d.tool),
          },
        };
      }
      return testTool(ctx, toolName);

    case 'diff':
      if (!toolName) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: false, message: 'No tools found.' };
        }
        return {
          success: true,
          silent: true,
          wizard: {
            id: 'tool-diff-select',
            title: 'Compare Tool',
            steps: [{
              id: 'tool',
              title: 'Select Tool',
              prompt: 'Which tool do you want to compare with server?',
              type: 'select',
              options: tools.map((t) => ({
                key: t.name,
                label: t.name,
                description: t.type,
              })),
            }],
            currentStep: 0,
            data: {},
            onComplete: async (d) => diffTool(ctx, d.tool),
          },
        };
      }
      return diffTool(ctx, toolName);

    case 'migrate': {
      const all = ctx.args.includes('--all') || ctx.args.includes('-a');
      const dryRun = ctx.args.includes('--dry-run');

      if (all) {
        const tools = await getLocalTools();
        if (tools.length === 0) {
          return { success: true, message: 'No tools found to migrate.' };
        }

        ctx.onOutput(`Migrating ${tools.length} tool(s)${dryRun ? ' (dry run)' : ''}...\n`);
        let migratedCount = 0;
        for (const { name } of tools) {
          const result = await migrateTool(ctx, name, dryRun);
          if (result.success) migratedCount++;
        }
        return { success: true, message: `\nMigrated ${migratedCount}/${tools.length} tools` };
      }

      if (!toolName) {
        return { success: false, message: 'Usage: /tool migrate <name> [--dry-run] or /tool migrate --all' };
      }
      return migrateTool(ctx, toolName, dryRun);
    }

    case 'sync':
      return { success: true, silent: true, wizard: createSyncWizard(ctx) };

    case 'show-types':
    case 'types':
      return showToolTypes(ctx, toolName);

    case 'show-connectors':
    case 'connectors':
      return showConnectors(ctx);

    default:
      return {
        success: false,
        message: `Unknown subcommand: ${subCommand}\n\nAvailable: create, list, edit, apply, delete, validate, test, diff, migrate, sync, show-types, show-connectors`,
      };
  }
}

/**
 * /tool command definition
 */
const toolCommand: SlashCommand = {
  name: 'tool',
  description: 'Create, manage, and test tools',
  usage: '/tool [subcommand] [args]',
  examples: [
    '/tool                    # Interactive menu',
    '/tool create             # Create wizard',
    '/tool list [--server]    # List tools',
    '/tool edit <name>        # Edit in VimEditor',
    '/tool apply <name>       # Deploy to server',
    '/tool delete <name>      # Delete tool',
    '/tool validate [--all]   # Validate YAML',
    '/tool test <name>        # Test execution',
    '/tool diff <name>        # Compare local/remote',
    '/tool migrate [--all]    # Convert V1 to V2',
    '/tool sync               # Download from server',
    '/tool show-types         # Available types',
    '/tool show-connectors    # Data connectors',
  ],
  execute: handleToolCommand,
};

/**
 * Register /tool command
 */
export function registerToolCommand(): void {
  commandRegistry.register(toolCommand);
}

export default toolCommand;

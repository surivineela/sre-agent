/**
 * /agent Command - Complete Agent Management
 *
 * Full feature parity with Agent.Cli C# implementation:
 * - create: Interactive wizard with AI-assisted mode
 * - list: List local and server agents
 * - edit: Inline VimEditor
 * - delete: Delete local and/or server agents
 * - apply: Deploy to server
 * - validate: Validate locally and against server
 * - test: Test agent with a message
 * - diff: Compare local vs remote
 * - sync: Download all agents from server
 */
import * as fs from 'fs/promises';
import * as path from 'path';
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult, WizardConfig } from './types';
import { getAgentTemplate } from '../utils/examples';
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
// API HELPERS
// ============================================================================

interface AgentFromServer {
  name: string;
  handoffDescription?: string;
  instructions?: string;
  tools?: string[];
  handoffs?: string[];
  allowParallelToolCalls?: boolean;
  maxReflectionCount?: number;
  temperature?: number;
  enableSkills?: boolean;
  vanillaMode?: boolean;
}

async function fetchAgentsFromServer(serverUrl: string): Promise<{
  success: boolean;
  agents?: AgentFromServer[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/agents`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json();
    const agents = Array.isArray(data) ? data : data.value || [];
    return { success: true, agents };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function fetchAgentFromServer(serverUrl: string, name: string): Promise<{
  success: boolean;
  agent?: AgentFromServer;
  yaml?: string;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/agents/${name}`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      if (response.status === 404) {
        return { success: false, error: 'Agent not found on server' };
      }
      return { success: false, error: `HTTP ${response.status}` };
    }

    const agent = await response.json();
    const yaml = agentToYaml(agent);
    return { success: true, agent, yaml };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function applyAgentToServer(serverUrl: string, name: string, yamlContent: string, dryRun = false): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const url = `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/agents/${name}${dryRun ? '?dryRun=true' : ''}`;

    const response = await fetch(url, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/x-yaml',
        Accept: 'application/json',
        ...authHeaders,
      },
      body: yamlContent,
    });

    if (!response.ok) {
      const errorText = await response.text();
      return { success: false, error: errorText || `HTTP ${response.status}` };
    }

    return { success: true };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function deleteAgentFromServer(serverUrl: string, name: string): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/agents/${name}`,
      {
        method: 'DELETE',
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      const errorText = await response.text();
      return { success: false, error: errorText || `HTTP ${response.status}` };
    }

    return { success: true };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function generateSmartAgent(
  serverUrl: string,
  agentName: string,
  userInstructions?: string
): Promise<{
  success: boolean;
  instructions?: string;
  suggestedTools?: string[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v1/incidentplayground/generateInstructions`,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Accept: 'application/json',
          ...authHeaders,
        },
        body: JSON.stringify({ agentName, userInstructions }),
      }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json();
    return {
      success: true,
      instructions: data.instructions || data.Instructions,
      suggestedTools: data.suggestedTools || data.SuggestedTools || [],
    };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function fetchToolsFromServer(serverUrl: string): Promise<string[]> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/tools`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) return [];

    const data = await response.json();
    const tools = Array.isArray(data) ? data : data.value || [];
    return tools.map((t: { name?: string; Name?: string }) => t.name || t.Name || '');
  } catch {
    return [];
  }
}

// ============================================================================
// YAML HELPERS
// ============================================================================

function agentToYaml(agent: AgentFromServer): string {
  const tools = agent.tools?.length ? agent.tools.map(t => `    - ${t}`).join('\n') : '    []';
  const handoffs = agent.handoffs?.length ? agent.handoffs.map(h => `    - ${h}`).join('\n') : '    []';

  return `apiVersion: srectl.agent/v2
kind: ExtendedAgent
metadata:
  name: ${agent.name}
spec:
  instructions: |
${(agent.instructions || '').split('\n').map(l => '    ' + l).join('\n')}
  handoffDescription: ${agent.handoffDescription || ''}
  handoffs:
${handoffs}
  tools:
${tools}
  allowParallelToolCalls: ${agent.allowParallelToolCalls ?? true}
  maxReflectionCount: ${agent.maxReflectionCount ?? 3}
  temperature: ${agent.temperature ?? 0.7}
  vanillaMode: ${agent.vanillaMode ?? false}
  enableSkills: ${agent.enableSkills ?? false}
`;
}

function parseAgentYaml(content: string): {
  name?: string;
  instructions?: string;
  handoffDescription?: string;
  tools?: string[];
  handoffs?: string[];
} {
  const nameMatch = content.match(/name:\s*(.+)/);
  const handoffDescMatch = content.match(/handoffDescription:\s*(.+)/);
  const instructionsMatch = content.match(/instructions:\s*\|\n([\s\S]*?)(?=\n\s*[a-zA-Z]|$)/);
  const toolsMatch = content.match(/tools:\s*\n((?:\s*-\s*.+\n)*)/);
  const handoffsMatch = content.match(/handoffs:\s*\n((?:\s*-\s*.+\n)*)/);

  return {
    name: nameMatch?.[1]?.trim(),
    instructions: instructionsMatch?.[1]?.split('\n').map(l => l.replace(/^\s{4}/, '')).join('\n').trim(),
    handoffDescription: handoffDescMatch?.[1]?.trim(),
    tools: toolsMatch?.[1]?.match(/-\s*(.+)/g)?.map(t => t.replace(/-\s*/, '').trim()) || [],
    handoffs: handoffsMatch?.[1]?.match(/-\s*(.+)/g)?.map(h => h.replace(/-\s*/, '').trim()) || [],
  };
}

// ============================================================================
// LOCAL FILE HELPERS
// ============================================================================

async function getLocalAgents(): Promise<Array<{ name: string; file: string; content: string }>> {
  const agentsDir = path.join(process.cwd(), 'agents');
  const agents: Array<{ name: string; file: string; content: string }> = [];

  try {
    const files = await fs.readdir(agentsDir);
    for (const file of files) {
      if (file.endsWith('.yaml') || file.endsWith('.yml')) {
        const filePath = path.join(agentsDir, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const parsed = parseAgentYaml(content);
        agents.push({
          name: parsed.name || file.replace(/\.ya?ml$/, ''),
          file,
          content,
        });
      }
    }
  } catch {
    // Directory doesn't exist
  }

  return agents;
}

async function getLocalAgent(name: string): Promise<{ exists: boolean; content?: string; filePath?: string }> {
  const agentsDir = path.join(process.cwd(), 'agents');
  const filePath = path.join(agentsDir, `${name}.yaml`);

  try {
    const content = await fs.readFile(filePath, 'utf-8');
    return { exists: true, content, filePath };
  } catch {
    return { exists: false };
  }
}

// ============================================================================
// WIZARD HELPERS
// ============================================================================

function createAgentWizard(ctx: CommandContext, availableTools: string[]): WizardConfig {
  const toolOptions = availableTools.slice(0, 10).map(t => ({
    key: t,
    label: t,
  }));

  return {
    id: 'create-agent',
    title: 'Create New Agent',
    steps: [
      {
        id: 'name',
        title: 'Agent Name',
        prompt: 'Enter a name for your agent (no spaces, use underscores):',
        type: 'input',
        placeholder: 'my_agent',
        defaultValue: '',
      },
      {
        id: 'method',
        title: 'Creation Method',
        prompt: 'How would you like to create the agent?',
        type: 'select',
        options: [
          { key: 'smart', label: 'AI-Assisted (Recommended)', description: 'Generate instructions from description' },
          { key: 'manual', label: 'Manual Template', description: 'Start with a blank template' },
        ],
      },
      {
        id: 'description',
        title: 'Agent Purpose',
        prompt: 'Describe what this agent should do:',
        type: 'input',
        placeholder: 'An agent that helps with...',
        defaultValue: '',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => createAgentFromWizard(ctx, data),
  };
}

async function createAgentFromWizard(
  ctx: CommandContext,
  data: Record<string, string>
): Promise<CommandResult> {
  const agentName = data.name?.replace(/\s+/g, '_') || 'new_agent';
  const method = data.method || 'manual';
  const description = data.description;

  // Validate name
  if (!agentName || agentName.includes(' ')) {
    return { success: false, message: 'Agent name cannot contain spaces.' };
  }

  const agentsDir = path.join(process.cwd(), 'agents');
  await fs.mkdir(agentsDir, { recursive: true });
  const filePath = path.join(agentsDir, `${agentName}.yaml`);

  // Check if exists
  try {
    await fs.access(filePath);
    return {
      success: false,
      message: `Agent "${agentName}" already exists. Use /agent edit ${agentName} to modify.`,
    };
  } catch {
    // Good - doesn't exist
  }

  let content: string;

  if (method === 'smart' && description) {
    const serverUrl = ctx.services.config.getServerUrl?.() || '';
    if (serverUrl) {
      const result = await generateSmartAgent(serverUrl, agentName, description);
      if (result.success && result.instructions) {
        content = `apiVersion: srectl.agent/v2
kind: ExtendedAgent
metadata:
  name: ${agentName}
spec:
  instructions: |
${result.instructions.split('\n').map(l => '    ' + l).join('\n')}
  handoffDescription: ${description}
  handoffs: []
  tools:
${(result.suggestedTools || []).map(t => `    - ${t}`).join('\n') || '    []'}
  allowParallelToolCalls: true
  maxReflectionCount: 3
  criticOnHandoff: false
  temperature: 0.7
  vanillaMode: false
  enableSkills: false
`;
      } else {
        content = getAgentTemplate(agentName);
      }
    } else {
      content = getAgentTemplate(agentName);
    }
  } else {
    content = getAgentTemplate(agentName);
  }

  await fs.writeFile(filePath, content, 'utf-8');

  return {
    success: true,
    message: `✓ Created agent: ${agentName}`,
    editor: {
      content,
      filename: `${agentName}.yaml`,
      filePath,
      fileType: 'yaml',
    },
  };
}

function createApplyWizard(ctx: CommandContext, localAgents: string[]): WizardConfig {
  return {
    id: 'apply-agent',
    title: 'Apply Agent to Server',
    steps: [
      {
        id: 'name',
        title: 'Select Agent',
        prompt: 'Which agent would you like to apply to the server?',
        type: 'select',
        options: localAgents.map(name => ({ key: name, label: name })),
      },
      {
        id: 'dryRun',
        title: 'Validation',
        prompt: 'Would you like to validate first (dry-run)?',
        type: 'select',
        options: [
          { key: 'yes', label: 'Yes - Validate first', description: 'Check for errors before applying' },
          { key: 'no', label: 'No - Apply directly', description: 'Apply changes immediately' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => applyAgentFromWizard(ctx, data),
  };
}

async function applyAgentFromWizard(
  ctx: CommandContext,
  data: Record<string, string>
): Promise<CommandResult> {
  const agentName = data.name;
  const dryRun = data.dryRun === 'yes';

  const serverUrl = ctx.services.config.getServerUrl?.() || '';
  if (!serverUrl) {
    return { success: false, message: 'Server URL not configured. Run /init first.' };
  }

  const local = await getLocalAgent(agentName);
  if (!local.exists || !local.content) {
    return { success: false, message: `Agent "${agentName}" not found locally.` };
  }

  const result = await applyAgentToServer(serverUrl, agentName, local.content, dryRun);

  if (result.success) {
    if (dryRun) {
      return { success: true, message: `✓ Validation passed for "${agentName}". Use /agent apply ${agentName} to deploy.` };
    }
    return { success: true, message: `✓ Agent "${agentName}" applied to server successfully.` };
  }

  return { success: false, message: `Failed to apply agent: ${result.error}` };
}

function createDeleteWizard(ctx: CommandContext, localAgents: string[]): WizardConfig {
  return {
    id: 'delete-agent',
    title: 'Delete Agent',
    steps: [
      {
        id: 'name',
        title: 'Select Agent',
        prompt: 'Which agent would you like to delete?',
        type: 'select',
        options: localAgents.map(name => ({ key: name, label: name })),
      },
      {
        id: 'target',
        title: 'Delete From',
        prompt: 'Where should the agent be deleted from?',
        type: 'select',
        options: [
          { key: 'local', label: 'Local only', description: 'Delete local file' },
          { key: 'server', label: 'Server only', description: 'Delete from server' },
          { key: 'both', label: 'Both', description: 'Delete local and server' },
        ],
      },
      {
        id: 'confirm',
        title: 'Confirm',
        prompt: 'Are you sure you want to delete this agent?',
        type: 'confirm',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => deleteAgentFromWizard(ctx, data),
  };
}

async function deleteAgentFromWizard(
  ctx: CommandContext,
  data: Record<string, string>
): Promise<CommandResult> {
  const agentName = data.name;
  const target = data.target;
  const confirm = data.confirm;

  if (confirm !== 'yes') {
    return { success: true, message: 'Delete cancelled.' };
  }

  const messages: string[] = [];
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  // Delete from server
  if ((target === 'server' || target === 'both') && serverUrl) {
    const result = await deleteAgentFromServer(serverUrl, agentName);
    if (result.success) {
      messages.push(`✓ Deleted "${agentName}" from server`);
    } else {
      messages.push(`⚠ Failed to delete from server: ${result.error}`);
    }
  }

  // Delete local
  if (target === 'local' || target === 'both') {
    const filePath = path.join(process.cwd(), 'agents', `${agentName}.yaml`);
    try {
      await fs.unlink(filePath);
      messages.push(`✓ Deleted local file: ${agentName}.yaml`);
    } catch (error) {
      messages.push(`⚠ Failed to delete local file: ${error}`);
    }
  }

  return { success: true, message: messages.join('\n') };
}

function createSyncWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'sync-agents',
    title: 'Sync Agents from Server',
    steps: [
      {
        id: 'confirm',
        title: 'Confirm Sync',
        prompt: 'This will download all agents from the server to your local agents/ directory. Existing files will be overwritten. Continue?',
        type: 'confirm',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => syncAgentsFromWizard(ctx, data),
  };
}

async function syncAgentsFromWizard(
  ctx: CommandContext,
  data: Record<string, string>
): Promise<CommandResult> {
  if (data.confirm !== 'yes') {
    return { success: true, message: 'Sync cancelled.' };
  }

  const serverUrl = ctx.services.config.getServerUrl?.() || '';
  if (!serverUrl) {
    return { success: false, message: 'Server URL not configured. Run /init first.' };
  }

  const result = await fetchAgentsFromServer(serverUrl);
  if (!result.success || !result.agents) {
    return { success: false, message: `Failed to fetch agents: ${result.error}` };
  }

  const agentsDir = path.join(process.cwd(), 'agents');
  await fs.mkdir(agentsDir, { recursive: true });

  let synced = 0;
  for (const agent of result.agents) {
    const yaml = agentToYaml(agent);
    const filePath = path.join(agentsDir, `${agent.name}.yaml`);
    await fs.writeFile(filePath, yaml, 'utf-8');
    synced++;
  }

  return { success: true, message: `✓ Synced ${synced} agent(s) from server to agents/ directory.` };
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleAgentCommand(ctx: CommandContext): Promise<CommandResult> {
  const subCommand = ctx.args[0]?.toLowerCase();
  const agentName = ctx.args[1];
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  // -------------------------------------------------------------------------
  // No subcommand - show interactive menu
  // -------------------------------------------------------------------------
  if (!subCommand) {
    const localAgents = await getLocalAgents();
    const agentNames = localAgents.map(a => a.name);

    return {
      success: true,
      silent: true,
      wizard: {
        id: 'agent-menu',
        title: 'Agent Management',
        steps: [
          {
            id: 'action',
            title: 'Choose Action',
            prompt: 'What would you like to do?',
            type: 'select',
            options: [
              { key: 'use', label: 'Use agent', description: 'Select agent for chat' },
              { key: 'create', label: 'Create new agent', description: 'Create a new agent with wizard' },
              { key: 'list', label: 'List agents', description: 'Show local and server agents' },
              { key: 'edit', label: 'Edit agent', description: 'Edit an existing agent' },
              { key: 'apply', label: 'Apply to server', description: 'Deploy agent to server' },
            ],
          },
        ],
        currentStep: 0,
        data: {},
        onComplete: async (data) => {
          switch (data.action) {
            case 'use': {
              if (!serverUrl) {
                return { success: false, message: 'Server URL not configured. Run /init first.' };
              }
              const serverAgents = await fetchAgentsFromServer(serverUrl);
              if (!serverAgents.success || !serverAgents.agents || serverAgents.agents.length === 0) {
                return { success: false, message: 'No agents found on server.' };
              }
              return {
                success: true,
                silent: true,
                wizard: {
                  id: 'use-agent',
                  title: 'Select Agent for Chat',
                  steps: [{
                    id: 'name',
                    title: 'Select Agent',
                    prompt: 'Which agent would you like to use for this chat session?',
                    type: 'select',
                    options: [
                      { key: '', label: '(Default agent)', description: 'Use the default server agent' },
                      ...serverAgents.agents.map(a => ({
                        key: a.name,
                        label: a.name,
                        description: a.handoffDescription || '',
                      })),
                    ],
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => {
                    const selectedAgent = d.name || '';
                    ctx.onStateChange?.({ agentName: selectedAgent || undefined });
                    if (selectedAgent) {
                      return { success: true, message: `✓ Now using agent: ${selectedAgent}` };
                    }
                    return { success: true, message: '✓ Using default agent' };
                  },
                },
              };
            }
            case 'create': {
              const tools = serverUrl ? await fetchToolsFromServer(serverUrl) : [];
              return { success: true, silent: true, wizard: createAgentWizard(ctx, tools) };
            }
            case 'list':
              return handleListCommand(ctx, serverUrl);
            case 'edit': {
              if (agentNames.length === 0) {
                return { success: false, message: 'No local agents found. Create one first with /agent create.' };
              }
              return {
                success: true,
                silent: true,
                wizard: {
                  id: 'edit-agent',
                  title: 'Edit Agent',
                  steps: [{
                    id: 'name',
                    title: 'Select Agent',
                    prompt: 'Which agent would you like to edit?',
                    type: 'select',
                    options: agentNames.map(n => ({ key: n, label: n })),
                  }],
                  currentStep: 0,
                  data: {},
                  onComplete: async (d) => handleEditCommand(ctx, d.name),
                },
              };
            }
            case 'apply': {
              if (agentNames.length === 0) {
                return { success: false, message: 'No local agents found. Create one first.' };
              }
              return { success: true, silent: true, wizard: createApplyWizard(ctx, agentNames) };
            }
            default:
              return { success: false, message: 'Unknown action.' };
          }
        },
      },
    };
  }

  // -------------------------------------------------------------------------
  // /agent use [name]
  // -------------------------------------------------------------------------
  if (subCommand === 'use') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }

    // If agent name provided directly
    if (agentName) {
      ctx.onStateChange?.({ agentName });
      return { success: true, message: `✓ Now using agent: ${agentName}` };
    }

    // Show selection wizard
    const serverAgents = await fetchAgentsFromServer(serverUrl);
    if (!serverAgents.success || !serverAgents.agents || serverAgents.agents.length === 0) {
      return { success: false, message: 'No agents found on server.' };
    }

    return {
      success: true,
      silent: true,
      wizard: {
        id: 'use-agent',
        title: 'Select Agent for Chat',
        steps: [{
          id: 'name',
          title: 'Select Agent',
          prompt: 'Which agent would you like to use for this chat session?',
          type: 'select',
          options: [
            { key: '', label: '(Default agent)', description: 'Use the default server agent' },
            ...serverAgents.agents.map(a => ({
              key: a.name,
              label: a.name,
              description: a.handoffDescription || '',
            })),
          ],
        }],
        currentStep: 0,
        data: {},
        onComplete: async (d) => {
          const selectedAgent = d.name || '';
          ctx.onStateChange?.({ agentName: selectedAgent || undefined });
          if (selectedAgent) {
            return { success: true, message: `✓ Now using agent: ${selectedAgent}` };
          }
          return { success: true, message: '✓ Using default agent' };
        },
      },
    };
  }

  // -------------------------------------------------------------------------
  // /agent create
  // -------------------------------------------------------------------------
  if (subCommand === 'create') {
    if (agentName && !agentName.startsWith('--')) {
      // Quick create with name
      const isSmart = ctx.args.includes('--smart');
      const desc = ctx.args.slice(isSmart ? 3 : 2).join(' ');
      return createAgentFromWizard(ctx, {
        name: agentName,
        method: isSmart ? 'smart' : 'manual',
        description: desc,
      });
    }
    // Start wizard
    const tools = serverUrl ? await fetchToolsFromServer(serverUrl) : [];
    return { success: true, silent: true, wizard: createAgentWizard(ctx, tools) };
  }

  // -------------------------------------------------------------------------
  // /agent list
  // -------------------------------------------------------------------------
  if (subCommand === 'list') {
    return handleListCommand(ctx, serverUrl);
  }

  // -------------------------------------------------------------------------
  // /agent edit <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'edit') {
    if (!agentName) {
      const localAgents = await getLocalAgents();
      if (localAgents.length === 0) {
        return { success: false, message: 'No local agents found. Create one first with /agent create.' };
      }
      return {
        success: true,
        silent: true,
        wizard: {
          id: 'edit-agent',
          title: 'Edit Agent',
          steps: [{
            id: 'name',
            title: 'Select Agent',
            prompt: 'Which agent would you like to edit?',
            type: 'select',
            options: localAgents.map(a => ({ key: a.name, label: a.name })),
          }],
          currentStep: 0,
          data: {},
          onComplete: async (data) => handleEditCommand(ctx, data.name),
        },
      };
    }
    return handleEditCommand(ctx, agentName);
  }

  // -------------------------------------------------------------------------
  // /agent delete <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'delete') {
    const localAgents = await getLocalAgents();
    const agentNames = localAgents.map(a => a.name);

    if (!agentName) {
      if (agentNames.length === 0) {
        return { success: false, message: 'No local agents found.' };
      }
      return { success: true, silent: true, wizard: createDeleteWizard(ctx, agentNames) };
    }

    // Direct delete with confirmation wizard
    return {
      success: true,
      silent: true,
      wizard: {
        id: 'delete-confirm',
        title: 'Delete Agent',
        steps: [
          {
            id: 'target',
            title: 'Delete From',
            prompt: `Where should "${agentName}" be deleted from?`,
            type: 'select',
            options: [
              { key: 'local', label: 'Local only' },
              { key: 'server', label: 'Server only' },
              { key: 'both', label: 'Both local and server' },
            ],
          },
          {
            id: 'confirm',
            title: 'Confirm',
            prompt: `Are you sure you want to delete "${agentName}"?`,
            type: 'confirm',
          },
        ],
        currentStep: 0,
        data: { name: agentName },
        onComplete: async (data) => deleteAgentFromWizard(ctx, { ...data, name: agentName }),
      },
    };
  }

  // -------------------------------------------------------------------------
  // /agent apply <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'apply') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }

    const localAgents = await getLocalAgents();
    const agentNames = localAgents.map(a => a.name);

    if (!agentName) {
      if (agentNames.length === 0) {
        return { success: false, message: 'No local agents found. Create one first.' };
      }
      return { success: true, silent: true, wizard: createApplyWizard(ctx, agentNames) };
    }

    // Direct apply with dry-run option
    const dryRun = ctx.args.includes('--dry-run');
    const local = await getLocalAgent(agentName);

    if (!local.exists || !local.content) {
      return { success: false, message: `Agent "${agentName}" not found locally.` };
    }

    const result = await applyAgentToServer(serverUrl, agentName, local.content, dryRun);

    if (result.success) {
      if (dryRun) {
        return { success: true, message: `✓ Validation passed for "${agentName}". Run without --dry-run to deploy.` };
      }
      return { success: true, message: `✓ Agent "${agentName}" applied to server successfully.` };
    }

    return { success: false, message: `Failed to apply: ${result.error}` };
  }

  // -------------------------------------------------------------------------
  // /agent validate <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'validate') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }

    const localAgents = await getLocalAgents();

    if (!agentName && !ctx.args.includes('--all')) {
      const agentNames = localAgents.map(a => a.name);
      if (agentNames.length === 0) {
        return { success: false, message: 'No local agents found.' };
      }
      return {
        success: true,
        silent: true,
        wizard: {
          id: 'validate-agent',
          title: 'Validate Agent',
          steps: [{
            id: 'name',
            title: 'Select Agent',
            prompt: 'Which agent would you like to validate?',
            type: 'select',
            options: [
              { key: '--all', label: 'All agents', description: 'Validate all local agents' },
              ...agentNames.map(n => ({ key: n, label: n })),
            ],
          }],
          currentStep: 0,
          data: {},
          onComplete: async (data) => {
            if (data.name === '--all') {
              return validateAllAgents(ctx, serverUrl, localAgents);
            }
            return validateSingleAgent(ctx, serverUrl, data.name);
          },
        },
      };
    }

    if (ctx.args.includes('--all')) {
      return validateAllAgents(ctx, serverUrl, localAgents);
    }

    return validateSingleAgent(ctx, serverUrl, agentName);
  }

  // -------------------------------------------------------------------------
  // /agent test <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'test') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }

    const localAgents = await getLocalAgents();

    if (!agentName) {
      const agentNames = localAgents.map(a => a.name);
      if (agentNames.length === 0) {
        return { success: false, message: 'No local agents found.' };
      }
      return {
        success: true,
        silent: true,
        wizard: {
          id: 'test-agent',
          title: 'Test Agent',
          steps: [
            {
              id: 'name',
              title: 'Select Agent',
              prompt: 'Which agent would you like to test?',
              type: 'select',
              options: agentNames.map(n => ({ key: n, label: n })),
            },
            {
              id: 'message',
              title: 'Test Message',
              prompt: 'Enter a test message to send to the agent:',
              type: 'input',
              placeholder: 'Hello, can you help me?',
            },
          ],
          currentStep: 0,
          data: {},
          onComplete: async (data) => testAgent(ctx, serverUrl, data.name, data.message),
        },
      };
    }

    const message = ctx.args.slice(2).join(' ');
    if (!message) {
      return {
        success: true,
        silent: true,
        wizard: {
          id: 'test-message',
          title: 'Test Agent',
          steps: [{
            id: 'message',
            title: 'Test Message',
            prompt: `Enter a test message to send to "${agentName}":`,
            type: 'input',
            placeholder: 'Hello, can you help me?',
          }],
          currentStep: 0,
          data: { name: agentName },
          onComplete: async (data) => testAgent(ctx, serverUrl, agentName, data.message),
        },
      };
    }

    return testAgent(ctx, serverUrl, agentName, message);
  }

  // -------------------------------------------------------------------------
  // /agent diff <name>
  // -------------------------------------------------------------------------
  if (subCommand === 'diff') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }

    const localAgents = await getLocalAgents();

    if (!agentName) {
      const agentNames = localAgents.map(a => a.name);
      if (agentNames.length === 0) {
        return { success: false, message: 'No local agents found.' };
      }
      return {
        success: true,
        silent: true,
        wizard: {
          id: 'diff-agent',
          title: 'Compare Agent',
          steps: [{
            id: 'name',
            title: 'Select Agent',
            prompt: 'Which agent would you like to compare local vs server?',
            type: 'select',
            options: agentNames.map(n => ({ key: n, label: n })),
          }],
          currentStep: 0,
          data: {},
          onComplete: async (data) => diffAgent(ctx, serverUrl, data.name),
        },
      };
    }

    return diffAgent(ctx, serverUrl, agentName);
  }

  // -------------------------------------------------------------------------
  // /agent sync
  // -------------------------------------------------------------------------
  if (subCommand === 'sync') {
    if (!serverUrl) {
      return { success: false, message: 'Server URL not configured. Run /init first.' };
    }
    return { success: true, silent: true, wizard: createSyncWizard(ctx) };
  }

  // -------------------------------------------------------------------------
  // Unknown subcommand - show help
  // -------------------------------------------------------------------------
  ctx.onOutput(`
Agent Management Commands:

  /agent                    Interactive menu
  /agent use [name]         Select agent for chat session
  /agent create [name]      Create new agent (wizard)
  /agent list               List local agents
  /agent list --server      List server agents
  /agent edit <name>        Edit agent in inline editor
  /agent delete <name>      Delete agent (local/server)
  /agent apply <name>       Deploy agent to server
  /agent validate <name>    Validate agent against server
  /agent test <name>        Test agent with a message
  /agent diff <name>        Compare local vs server
  /agent sync               Download all agents from server

Options:
  --smart                   Use AI to generate instructions (create)
  --dry-run                 Validate without applying (apply)
  --server                  Include server agents (list)
  --all                     Process all agents (validate)
`);
  return { success: true };
}

// ============================================================================
// SUBCOMMAND IMPLEMENTATIONS
// ============================================================================

async function handleListCommand(ctx: CommandContext, serverUrl: string): Promise<CommandResult> {
  const showServer = ctx.args.includes('--server');
  const localAgents = await getLocalAgents();
  const lines: string[] = [];

  // Local agents
  lines.push('Local Agents:');
  if (localAgents.length === 0) {
    lines.push('  (none)');
  } else {
    for (const agent of localAgents) {
      const parsed = parseAgentYaml(agent.content);
      lines.push(`  • ${agent.name}`);
      if (parsed.handoffDescription) {
        lines.push(`    ${parsed.handoffDescription}`);
      }
      if (parsed.tools && parsed.tools.length > 0) {
        lines.push(`    Tools: ${parsed.tools.slice(0, 3).join(', ')}${parsed.tools.length > 3 ? '...' : ''}`);
      }
    }
  }

  // Server agents
  if (showServer && serverUrl) {
    lines.push('');
    lines.push('Server Agents:');
    const result = await fetchAgentsFromServer(serverUrl);
    if (result.success && result.agents) {
      if (result.agents.length === 0) {
        lines.push('  (none)');
      } else {
        for (const agent of result.agents) {
          lines.push(`  • ${agent.name}`);
          if (agent.handoffDescription) {
            lines.push(`    ${agent.handoffDescription}`);
          }
        }
      }
    } else {
      lines.push(`  (failed to fetch: ${result.error})`);
    }
  }

  ctx.onOutput(lines.join('\n'));
  return { success: true, silent: true };
}

async function handleEditCommand(ctx: CommandContext, name: string): Promise<CommandResult> {
  const local = await getLocalAgent(name);

  if (!local.exists || !local.content || !local.filePath) {
    return { success: false, message: `Agent "${name}" not found locally.` };
  }

  return {
    success: true,
    silent: true,
    editor: {
      content: local.content,
      filename: `${name}.yaml`,
      filePath: local.filePath,
      fileType: 'yaml',
    },
  };
}

async function validateSingleAgent(
  ctx: CommandContext,
  serverUrl: string,
  name: string
): Promise<CommandResult> {
  const local = await getLocalAgent(name);

  if (!local.exists || !local.content) {
    return { success: false, message: `Agent "${name}" not found locally.` };
  }

  const result = await applyAgentToServer(serverUrl, name, local.content, true);

  if (result.success) {
    return { success: true, message: `✓ Agent "${name}" is valid.` };
  }

  return { success: false, message: `✗ Agent "${name}" validation failed:\n${result.error}` };
}

async function validateAllAgents(
  ctx: CommandContext,
  serverUrl: string,
  localAgents: Array<{ name: string; content: string }>
): Promise<CommandResult> {
  const results: string[] = [];
  let passed = 0;
  let failed = 0;

  for (const agent of localAgents) {
    const result = await applyAgentToServer(serverUrl, agent.name, agent.content, true);
    if (result.success) {
      results.push(`✓ ${agent.name}`);
      passed++;
    } else {
      results.push(`✗ ${agent.name}: ${result.error}`);
      failed++;
    }
  }

  results.push('');
  results.push(`Summary: ${passed} passed, ${failed} failed`);

  ctx.onOutput(results.join('\n'));
  return { success: failed === 0, silent: true };
}

async function testAgent(
  ctx: CommandContext,
  serverUrl: string,
  name: string,
  message: string
): Promise<CommandResult> {
  if (!message) {
    return { success: false, message: 'Please provide a test message.' };
  }

  // Create a test thread with the agent
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(`${serverUrl}/api/v1/threads`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Accept: 'application/json',
        ...authHeaders,
      },
      body: JSON.stringify({
        startMessage: message,
        agentName: name,
        userId: process.env.USER || 'cli-user',
        displayName: process.env.USER || 'CLI User',
      }),
    });

    if (!response.ok) {
      const error = await response.text();
      return { success: false, message: `Failed to create test thread: ${error}` };
    }

    const thread = await response.json();
    return {
      success: true,
      message: `✓ Test thread created with agent "${name}"\nThread ID: ${thread.id || thread.Id}\n\nUse the chat to continue the conversation or check thread status.`,
    };
  } catch (error) {
    return { success: false, message: `Test failed: ${error}` };
  }
}

async function diffAgent(
  ctx: CommandContext,
  serverUrl: string,
  name: string
): Promise<CommandResult> {
  // Get local
  const local = await getLocalAgent(name);
  if (!local.exists || !local.content) {
    return { success: false, message: `Agent "${name}" not found locally.` };
  }

  // Get remote
  const remote = await fetchAgentFromServer(serverUrl, name);
  if (!remote.success || !remote.yaml) {
    return { success: false, message: `Agent "${name}" not found on server: ${remote.error}` };
  }

  // Simple inline diff
  const localLines = local.content.split('\n');
  const remoteLines = remote.yaml.split('\n');

  const diffLines: string[] = [];
  diffLines.push(`Comparing "${name}": Local vs Server`);
  diffLines.push('─'.repeat(50));
  diffLines.push('');

  const maxLines = Math.max(localLines.length, remoteLines.length);
  let differences = 0;

  for (let i = 0; i < maxLines; i++) {
    const localLine = localLines[i] || '';
    const remoteLine = remoteLines[i] || '';

    if (localLine !== remoteLine) {
      differences++;
      if (localLine && !remoteLine) {
        diffLines.push(`+ ${localLine} (local only)`);
      } else if (!localLine && remoteLine) {
        diffLines.push(`- ${remoteLine} (server only)`);
      } else {
        diffLines.push(`~ Local:  ${localLine}`);
        diffLines.push(`~ Server: ${remoteLine}`);
      }
    }
  }

  if (differences === 0) {
    diffLines.push('✓ Local and server configurations are identical.');
  } else {
    diffLines.push('');
    diffLines.push(`Found ${differences} difference(s).`);
    diffLines.push('');
    diffLines.push('Use /agent apply to push local changes to server.');
  }

  ctx.onOutput(diffLines.join('\n'));
  return { success: true, silent: true };
}

// ============================================================================
// COMMAND REGISTRATION
// ============================================================================

const agentCommand: SlashCommand = {
  name: 'agent',
  aliases: ['agents'],
  description: 'Create, edit, deploy, and manage agents',
  usage: '/agent [create|list|edit|delete|apply|validate|test|diff|sync] [args]',
  examples: [
    '/agent',
    '/agent create my_agent',
    '/agent create --smart k8s_expert',
    '/agent list --server',
    '/agent edit my_agent',
    '/agent apply my_agent',
    '/agent validate --all',
    '/agent test my_agent "Hello"',
    '/agent diff my_agent',
    '/agent sync',
  ],
  execute: handleAgentCommand,
};

export function registerAgentCommand(): void {
  commandRegistry.register(agentCommand);
}

export default agentCommand;

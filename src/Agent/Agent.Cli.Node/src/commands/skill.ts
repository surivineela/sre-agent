/**
 * /skill Command - Skill Management
 *
 * Create, edit, list, and manage reusable prompt patterns
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
// TYPES
// ============================================================================

interface SkillFromServer {
  name: string;
  description?: string;
  prompt?: string;
  parameters?: unknown[];
}

// ============================================================================
// API HELPERS
// ============================================================================

async function fetchSkillsFromServer(serverUrl: string): Promise<{
  success: boolean;
  skills?: SkillFromServer[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/skills`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json() as { value?: SkillFromServer[] } | SkillFromServer[];
    const skills = Array.isArray(data) ? data : data.value || [];
    return { success: true, skills };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}


async function deleteSkillFromServer(serverUrl: string, name: string): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/skills/${name}`,
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

async function applySkillToServer(serverUrl: string, name: string, yamlContent: string): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const url = `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/skills/${name}`;

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

// ============================================================================
// LOCAL FILE HELPERS
// ============================================================================

async function getLocalSkills(): Promise<Array<{ name: string; file: string; content: string }>> {
  const skillsDir = path.join(process.cwd(), 'skills');
  const skills: Array<{ name: string; file: string; content: string }> = [];

  try {
    const files = await fs.readdir(skillsDir);
    for (const file of files) {
      if (file.endsWith('.yaml') || file.endsWith('.yml')) {
        const filePath = path.join(skillsDir, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const nameMatch = content.match(/name:\s*(.+)/);
        skills.push({
          name: nameMatch?.[1]?.trim() || file.replace(/\.ya?ml$/, ''),
          file,
          content,
        });
      }
    }
  } catch {
    // Directory doesn't exist
  }

  return skills;
}

async function getLocalSkill(name: string): Promise<{ exists: boolean; content?: string; filePath?: string }> {
  const skillsDir = path.join(process.cwd(), 'skills');
  const filePath = path.join(skillsDir, `${name}.yaml`);

  try {
    const content = await fs.readFile(filePath, 'utf-8');
    return { exists: true, content, filePath };
  } catch {
    return { exists: false };
  }
}

// ============================================================================
// YAML HELPERS
// ============================================================================

function getSkillTemplate(name: string): string {
  return `apiVersion: srectl.skill/v2
kind: Skill
metadata:
  name: ${name}
spec:
  description: |
    A reusable skill for...
  prompt: |
    You are helping with a specific task.

    Instructions:
    1. Analyze the input
    2. Provide structured output

    Input: {{input}}
  parameters:
    - name: input
      type: string
      required: true
      description: The input to process
`;
}


// ============================================================================
// WIZARDS
// ============================================================================

function createSkillMenuWizard(ctx: CommandContext, localSkills: string[], serverSkills: string[]): WizardConfig {
  return {
    id: 'skill-menu',
    title: 'Skill Management',
    steps: [
      {
        id: 'action',
        title: 'Choose Action',
        prompt: 'What would you like to do?',
        type: 'select',
        options: [
          { key: 'create', label: 'Create New Skill', description: 'Create a new reusable prompt pattern' },
          { key: 'list', label: 'List Skills', description: `View all skills (${localSkills.length} local, ${serverSkills.length} server)` },
          { key: 'edit', label: 'Edit Skill', description: 'Modify an existing skill' },
          { key: 'delete', label: 'Delete Skill', description: 'Remove a skill' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      switch (data.action) {
        case 'create':
          return { success: true, silent: true, wizard: createSkillCreateWizard(ctx) };
        case 'list':
          return await handleListSkills(ctx);
        case 'edit':
          return { success: true, silent: true, wizard: createSkillEditWizard(ctx, localSkills) };
        case 'delete':
          return { success: true, silent: true, wizard: createSkillDeleteWizard(ctx, localSkills, serverSkills) };
        default:
          return { success: false, message: 'Unknown action' };
      }
    },
  };
}

function createSkillCreateWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'skill-create',
    title: 'Create New Skill',
    steps: [
      {
        id: 'name',
        title: 'Skill Name',
        prompt: 'Enter a name for your skill (no spaces, use underscores):',
        type: 'input',
        placeholder: 'my_skill',
        defaultValue: '',
      },
      {
        id: 'method',
        title: 'Creation Method',
        prompt: 'How would you like to create the skill?',
        type: 'select',
        options: [
          { key: 'template', label: 'Start with Template', description: 'Use a basic template and customize' },
          { key: 'guided', label: 'Guided Creation', description: 'Answer questions to build the skill' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => createSkillFromWizard(ctx, data),
  };
}

function createSkillEditWizard(ctx: CommandContext, localSkills: string[]): WizardConfig {
  const skillOptions = localSkills.map(s => ({
    key: s,
    label: s,
    description: 'Edit this skill',
  }));

  if (skillOptions.length === 0) {
    skillOptions.push({
      key: 'none',
      label: 'No local skills found',
      description: 'Create a skill first with /skill create',
    });
  }

  return {
    id: 'skill-edit',
    title: 'Edit Skill',
    steps: [
      {
        id: 'skill',
        title: 'Select Skill',
        prompt: 'Which skill would you like to edit?',
        type: 'select',
        options: skillOptions,
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.skill === 'none') {
        return { success: false, message: 'No skill selected.' };
      }
      return await handleEditSkill(ctx, data.skill);
    },
  };
}

function createSkillDeleteWizard(ctx: CommandContext, localSkills: string[], serverSkills: string[]): WizardConfig {
  const allSkills = [...new Set([...localSkills, ...serverSkills])];
  const skillOptions = allSkills.map(s => ({
    key: s,
    label: s,
    description: localSkills.includes(s) && serverSkills.includes(s)
      ? 'Local + Server'
      : localSkills.includes(s)
        ? 'Local only'
        : 'Server only',
  }));

  if (skillOptions.length === 0) {
    skillOptions.push({
      key: 'none',
      label: 'No skills found',
      description: 'Nothing to delete',
    });
  }

  return {
    id: 'skill-delete',
    title: 'Delete Skill',
    steps: [
      {
        id: 'skill',
        title: 'Select Skill',
        prompt: 'Which skill would you like to delete?',
        type: 'select',
        options: skillOptions,
      },
      {
        id: 'confirm',
        title: 'Confirm Deletion',
        prompt: 'Are you sure you want to delete this skill? This cannot be undone.',
        type: 'confirm',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.skill === 'none') {
        return { success: false, message: 'No skill selected.' };
      }
      if (data.confirm !== 'yes') {
        return { success: false, message: 'Deletion cancelled.' };
      }
      return await handleDeleteSkill(ctx, data.skill, localSkills.includes(data.skill), serverSkills.includes(data.skill));
    },
  };
}

// ============================================================================
// SUBCOMMAND HANDLERS
// ============================================================================

async function createSkillFromWizard(ctx: CommandContext, data: Record<string, string>): Promise<CommandResult> {
  const { onOutput } = ctx;
  const skillName = data.name?.replace(/\s+/g, '_') || 'new_skill';

  if (!skillName || skillName.includes(' ')) {
    return { success: false, message: 'Skill name cannot contain spaces.' };
  }

  const skillsDir = path.join(process.cwd(), 'skills');
  await fs.mkdir(skillsDir, { recursive: true });
  const filePath = path.join(skillsDir, `${skillName}.yaml`);

  // Check if exists
  try {
    await fs.access(filePath);
    return {
      success: false,
      message: `Skill "${skillName}" already exists. Use /skill edit ${skillName} to modify.`,
    };
  } catch {
    // Good - doesn't exist
  }

  // Create from template
  const content = getSkillTemplate(skillName);
  await fs.writeFile(filePath, content, 'utf-8');

  onOutput(`\n✓ Created skill: ${filePath}`);
  onOutput('  Use /skill edit ' + skillName + ' to customize.\n');

  // Open in editor
  return {
    success: true,
    silent: true,
    editor: {
      content,
      filename: `${skillName}.yaml`,
      filePath,
      fileType: 'yaml',
    },
  };
}

async function handleListSkills(ctx: CommandContext): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput('\n┌─ Skills');
  onOutput('│');

  // Local skills
  const localSkills = await getLocalSkills();
  onOutput('│  Local Skills:');
  if (localSkills.length === 0) {
    onOutput('│    (none)');
  } else {
    for (const skill of localSkills) {
      onOutput(`│    • ${skill.name} (${skill.file})`);
    }
  }

  // Server skills
  onOutput('│');
  onOutput('│  Server Skills:');
  if (serverUrl) {
    const result = await fetchSkillsFromServer(serverUrl);
    if (result.success && result.skills) {
      if (result.skills.length === 0) {
        onOutput('│    (none)');
      } else {
        for (const skill of result.skills) {
          onOutput(`│    • ${skill.name}`);
        }
      }
    } else {
      onOutput(`│    Error: ${result.error}`);
    }
  } else {
    onOutput('│    (server not configured)');
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleEditSkill(_ctx: CommandContext, name: string): Promise<CommandResult> {
  const local = await getLocalSkill(name);

  if (!local.exists || !local.content || !local.filePath) {
    return {
      success: false,
      message: `Skill "${name}" not found locally. Create it first with /skill create.`,
    };
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

async function handleDeleteSkill(
  ctx: CommandContext,
  name: string,
  hasLocal: boolean,
  hasServer: boolean
): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput(`\n┌─ Deleting Skill: ${name}`);
  onOutput('│');

  // Delete local
  if (hasLocal) {
    const skillsDir = path.join(process.cwd(), 'skills');
    const filePath = path.join(skillsDir, `${name}.yaml`);
    try {
      await fs.unlink(filePath);
      onOutput('│  ✓ Deleted local file');
    } catch (error) {
      onOutput(`│  ✗ Failed to delete local: ${error}`);
    }
  }

  // Delete from server
  if (hasServer && serverUrl) {
    const result = await deleteSkillFromServer(serverUrl, name);
    if (result.success) {
      onOutput('│  ✓ Deleted from server');
    } else {
      onOutput(`│  ✗ Failed to delete from server: ${result.error}`);
    }
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleApplySkill(ctx: CommandContext, name: string): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  if (!serverUrl) {
    return { success: false, message: 'Server not configured. Run /init first.' };
  }

  const local = await getLocalSkill(name);
  if (!local.exists || !local.content) {
    return { success: false, message: `Skill "${name}" not found locally.` };
  }

  onOutput(`\n┌─ Applying Skill: ${name}`);
  onOutput('│');

  const result = await applySkillToServer(serverUrl, name, local.content);

  if (result.success) {
    onOutput('│  ✓ Successfully applied to server');
  } else {
    onOutput(`│  ✗ Error: ${result.error}`);
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleSkillCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args } = ctx;
  const subCommand = args[0]?.toLowerCase();
  const skillName = args[1];
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  // No subcommand - show interactive menu
  if (!subCommand) {
    const localSkills = (await getLocalSkills()).map(s => s.name);
    let serverSkills: string[] = [];
    if (serverUrl) {
      const result = await fetchSkillsFromServer(serverUrl);
      if (result.success && result.skills) {
        serverSkills = result.skills.map(s => s.name);
      }
    }

    return {
      success: true,
      silent: true,
      wizard: createSkillMenuWizard(ctx, localSkills, serverSkills),
    };
  }

  // Subcommand routing
  switch (subCommand) {
    case 'list':
    case 'ls':
      return await handleListSkills(ctx);

    case 'create':
    case 'new':
      if (skillName) {
        return await createSkillFromWizard(ctx, { name: skillName, method: 'template' });
      }
      return {
        success: true,
        silent: true,
        wizard: createSkillCreateWizard(ctx),
      };

    case 'edit':
      if (!skillName) {
        const localSkills = (await getLocalSkills()).map(s => s.name);
        return {
          success: true,
          silent: true,
          wizard: createSkillEditWizard(ctx, localSkills),
        };
      }
      return await handleEditSkill(ctx, skillName);

    case 'delete':
    case 'rm':
      if (!skillName) {
        const localSkills = (await getLocalSkills()).map(s => s.name);
        let serverSkills: string[] = [];
        if (serverUrl) {
          const result = await fetchSkillsFromServer(serverUrl);
          if (result.success && result.skills) {
            serverSkills = result.skills.map(s => s.name);
          }
        }
        return {
          success: true,
          silent: true,
          wizard: createSkillDeleteWizard(ctx, localSkills, serverSkills),
        };
      }
      // Direct delete - need confirmation
      const localSkills = (await getLocalSkills()).map(s => s.name);
      let serverSkills: string[] = [];
      if (serverUrl) {
        const result = await fetchSkillsFromServer(serverUrl);
        if (result.success && result.skills) {
          serverSkills = result.skills.map(s => s.name);
        }
      }
      return await handleDeleteSkill(
        ctx,
        skillName,
        localSkills.includes(skillName),
        serverSkills.includes(skillName)
      );

    case 'apply':
    case 'deploy':
      if (!skillName) {
        return { success: false, message: 'Usage: /skill apply <name>' };
      }
      return await handleApplySkill(ctx, skillName);

    default:
      return {
        success: false,
        message: `Unknown subcommand: ${subCommand}\n\nUsage: /skill [list|create|edit|delete|apply] [name]`,
      };
  }
}

/**
 * Skill command definition
 */
const skillCommand: SlashCommand = {
  name: 'skill',
  aliases: ['skills'],
  description: 'Create and manage reusable prompt patterns',
  usage: '/skill [list|create|edit|delete|apply] [name]',
  examples: [
    '/skill',
    '/skill list',
    '/skill create',
    '/skill create my_skill',
    '/skill edit my_skill',
    '/skill delete my_skill',
    '/skill apply my_skill',
  ],
  execute: handleSkillCommand,
};

/**
 * Register the skill command
 */
export function registerSkillCommand(): void {
  commandRegistry.register(skillCommand);
}

/**
 * /scheduled Command - Scheduled Task Management
 *
 * Create, enable, disable, and manage scheduled tasks
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

interface ScheduledTask {
  name: string;
  description?: string;
  schedule: string; // Cron expression
  enabled: boolean;
  agentName?: string;
  prompt?: string;
  lastRun?: string;
  nextRun?: string;
}

// ============================================================================
// API HELPERS
// ============================================================================

async function fetchScheduledTasksFromServer(serverUrl: string): Promise<{
  success: boolean;
  tasks?: ScheduledTask[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/scheduledTasks`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json() as { value?: ScheduledTask[] } | ScheduledTask[];
    const tasks = Array.isArray(data) ? data : data.value || [];
    return { success: true, tasks };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function enableScheduledTask(serverUrl: string, name: string, enable: boolean): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/scheduledTasks/${name}/${enable ? 'enable' : 'disable'}`,
      {
        method: 'POST',
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

async function deleteScheduledTaskFromServer(serverUrl: string, name: string): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/scheduledTasks/${name}`,
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


// ============================================================================
// LOCAL FILE HELPERS
// ============================================================================

async function getLocalScheduledTasks(): Promise<Array<{ name: string; file: string; content: string }>> {
  const scheduledDir = path.join(process.cwd(), 'scheduled');
  const tasks: Array<{ name: string; file: string; content: string }> = [];

  try {
    const files = await fs.readdir(scheduledDir);
    for (const file of files) {
      if (file.endsWith('.yaml') || file.endsWith('.yml')) {
        const filePath = path.join(scheduledDir, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const nameMatch = content.match(/name:\s*(.+)/);
        tasks.push({
          name: nameMatch?.[1]?.trim() || file.replace(/\.ya?ml$/, ''),
          file,
          content,
        });
      }
    }
  } catch {
    // Directory doesn't exist
  }

  return tasks;
}

async function getLocalScheduledTask(name: string): Promise<{ exists: boolean; content?: string; filePath?: string }> {
  const scheduledDir = path.join(process.cwd(), 'scheduled');
  const filePath = path.join(scheduledDir, `${name}.yaml`);

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

function getScheduledTaskTemplate(name: string): string {
  return `apiVersion: srectl.scheduled/v2
kind: ScheduledTask
metadata:
  name: ${name}
spec:
  description: |
    A scheduled task that runs periodically
  # Cron expression: minute hour day-of-month month day-of-week
  # Examples:
  #   "0 * * * *"     - Every hour
  #   "0 0 * * *"     - Every day at midnight
  #   "0 9 * * 1-5"   - Weekdays at 9am
  #   "*/15 * * * *"  - Every 15 minutes
  schedule: "0 * * * *"
  enabled: false
  agentName: default_agent
  prompt: |
    Check the system health and report any issues.
`;
}

// ============================================================================
// DISPLAY HELPERS
// ============================================================================

function formatScheduleDescription(cron: string): string {
  // Simple cron description
  const parts = cron.split(' ');
  if (parts.length !== 5) return cron;

  const [min, hour, dom, month, dow] = parts;

  if (min === '0' && hour === '*') return 'Every hour';
  if (min === '0' && hour === '0' && dom === '*' && month === '*' && dow === '*') return 'Daily at midnight';
  if (min.startsWith('*/')) return `Every ${min.slice(2)} minutes`;
  if (hour.startsWith('*/')) return `Every ${hour.slice(2)} hours`;

  return cron;
}

// ============================================================================
// WIZARDS
// ============================================================================

function createScheduledMenuWizard(
  ctx: CommandContext,
  localTasks: string[],
  serverTasks: ScheduledTask[]
): WizardConfig {
  const enabledCount = serverTasks.filter(t => t.enabled).length;

  return {
    id: 'scheduled-menu',
    title: 'Scheduled Task Management',
    steps: [
      {
        id: 'action',
        title: 'Choose Action',
        prompt: 'What would you like to do?',
        type: 'select',
        options: [
          { key: 'list', label: 'List Tasks', description: `View all tasks (${enabledCount} enabled, ${serverTasks.length - enabledCount} disabled)` },
          { key: 'create', label: 'Create New Task', description: 'Create a new scheduled task' },
          { key: 'enable', label: 'Enable/Disable Task', description: 'Toggle task enabled state' },
          { key: 'delete', label: 'Delete Task', description: 'Remove a scheduled task' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      switch (data.action) {
        case 'list':
          return await handleListScheduledTasks(ctx);
        case 'create':
          return { success: true, silent: true, wizard: createScheduledCreateWizard(ctx) };
        case 'enable':
          return { success: true, silent: true, wizard: createScheduledToggleWizard(ctx, serverTasks) };
        case 'delete':
          return { success: true, silent: true, wizard: createScheduledDeleteWizard(ctx, localTasks, serverTasks) };
        default:
          return { success: false, message: 'Unknown action' };
      }
    },
  };
}

function createScheduledCreateWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'scheduled-create',
    title: 'Create Scheduled Task',
    steps: [
      {
        id: 'name',
        title: 'Task Name',
        prompt: 'Enter a name for your scheduled task:',
        type: 'input',
        placeholder: 'health_check',
        defaultValue: '',
      },
      {
        id: 'schedule',
        title: 'Schedule',
        prompt: 'How often should this task run?',
        type: 'select',
        options: [
          { key: '*/15 * * * *', label: 'Every 15 minutes', description: 'Runs 4 times per hour' },
          { key: '0 * * * *', label: 'Every hour', description: 'Runs at the top of each hour' },
          { key: '0 0 * * *', label: 'Daily at midnight', description: 'Runs once per day' },
          { key: '0 9 * * 1-5', label: 'Weekdays at 9am', description: 'Runs on work days' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => createScheduledTaskFromWizard(ctx, data),
  };
}

function createScheduledToggleWizard(ctx: CommandContext, serverTasks: ScheduledTask[]): WizardConfig {
  const taskOptions = serverTasks.map(t => ({
    key: t.name,
    label: `${t.name} ${t.enabled ? '●' : '○'}`,
    description: t.enabled ? 'Currently enabled - click to disable' : 'Currently disabled - click to enable',
  }));

  if (taskOptions.length === 0) {
    taskOptions.push({
      key: 'none',
      label: 'No tasks found',
      description: 'Create a task first',
    });
  }

  return {
    id: 'scheduled-toggle',
    title: 'Enable/Disable Task',
    steps: [
      {
        id: 'task',
        title: 'Select Task',
        prompt: 'Which task would you like to toggle?',
        type: 'select',
        options: taskOptions,
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.task === 'none') {
        return { success: false, message: 'No task selected.' };
      }
      const task = serverTasks.find(t => t.name === data.task);
      if (!task) {
        return { success: false, message: 'Task not found.' };
      }
      return await handleToggleScheduledTask(ctx, data.task, !task.enabled);
    },
  };
}

function createScheduledDeleteWizard(
  ctx: CommandContext,
  localTasks: string[],
  serverTasks: ScheduledTask[]
): WizardConfig {
  const allNames = [...new Set([...localTasks, ...serverTasks.map(t => t.name)])];
  const taskOptions = allNames.map(name => {
    const hasLocal = localTasks.includes(name);
    const hasServer = serverTasks.some(t => t.name === name);
    return {
      key: name,
      label: name,
      description: hasLocal && hasServer ? 'Local + Server' : hasLocal ? 'Local only' : 'Server only',
    };
  });

  if (taskOptions.length === 0) {
    taskOptions.push({
      key: 'none',
      label: 'No tasks found',
      description: 'Nothing to delete',
    });
  }

  return {
    id: 'scheduled-delete',
    title: 'Delete Scheduled Task',
    steps: [
      {
        id: 'task',
        title: 'Select Task',
        prompt: 'Which task would you like to delete?',
        type: 'select',
        options: taskOptions,
      },
      {
        id: 'confirm',
        title: 'Confirm Deletion',
        prompt: 'Are you sure you want to delete this task?',
        type: 'confirm',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.task === 'none') {
        return { success: false, message: 'No task selected.' };
      }
      if (data.confirm !== 'yes') {
        return { success: false, message: 'Deletion cancelled.' };
      }
      return await handleDeleteScheduledTask(
        ctx,
        data.task,
        localTasks.includes(data.task),
        serverTasks.some(t => t.name === data.task)
      );
    },
  };
}

// ============================================================================
// SUBCOMMAND HANDLERS
// ============================================================================

async function createScheduledTaskFromWizard(ctx: CommandContext, data: Record<string, string>): Promise<CommandResult> {
  const { onOutput } = ctx;
  const taskName = data.name?.replace(/\s+/g, '_') || 'new_task';
  const schedule = data.schedule || '0 * * * *';

  if (!taskName || taskName.includes(' ')) {
    return { success: false, message: 'Task name cannot contain spaces.' };
  }

  const scheduledDir = path.join(process.cwd(), 'scheduled');
  await fs.mkdir(scheduledDir, { recursive: true });
  const filePath = path.join(scheduledDir, `${taskName}.yaml`);

  // Check if exists
  try {
    await fs.access(filePath);
    return {
      success: false,
      message: `Task "${taskName}" already exists. Use /scheduled edit ${taskName} to modify.`,
    };
  } catch {
    // Good - doesn't exist
  }

  // Create from template with custom schedule
  let content = getScheduledTaskTemplate(taskName);
  content = content.replace(/schedule: ".*"/, `schedule: "${schedule}"`);

  await fs.writeFile(filePath, content, 'utf-8');

  onOutput(`\n✓ Created scheduled task: ${filePath}`);
  onOutput('  Edit the file and use /apply to deploy.\n');

  return {
    success: true,
    silent: true,
    editor: {
      content,
      filename: `${taskName}.yaml`,
      filePath,
      fileType: 'yaml',
    },
  };
}

async function handleListScheduledTasks(ctx: CommandContext): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput('\n┌─ Scheduled Tasks');
  onOutput('│');

  // Local tasks
  const localTasks = await getLocalScheduledTasks();
  onOutput('│  Local Tasks:');
  if (localTasks.length === 0) {
    onOutput('│    (none)');
  } else {
    for (const task of localTasks) {
      onOutput(`│    • ${task.name} (${task.file})`);
    }
  }

  // Server tasks
  onOutput('│');
  onOutput('│  Server Tasks:');
  if (serverUrl) {
    const result = await fetchScheduledTasksFromServer(serverUrl);
    if (result.success && result.tasks) {
      if (result.tasks.length === 0) {
        onOutput('│    (none)');
      } else {
        for (const task of result.tasks) {
          const status = task.enabled ? '●' : '○';
          const scheduleDesc = formatScheduleDescription(task.schedule);
          onOutput(`│    ${status} ${task.name}`);
          onOutput(`│      Schedule: ${scheduleDesc}`);
          if (task.nextRun) {
            onOutput(`│      Next run: ${task.nextRun}`);
          }
        }
      }
    } else {
      onOutput(`│    Error: ${result.error}`);
    }
  } else {
    onOutput('│    (server not configured)');
  }

  onOutput('│');
  onOutput('│  Legend: ● enabled  ○ disabled');
  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleToggleScheduledTask(ctx: CommandContext, name: string, enable: boolean): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  if (!serverUrl) {
    return { success: false, message: 'Server not configured. Run /init first.' };
  }

  onOutput(`\n┌─ ${enable ? 'Enabling' : 'Disabling'} Task: ${name}`);
  onOutput('│');

  const result = await enableScheduledTask(serverUrl, name, enable);

  if (result.success) {
    onOutput(`│  ✓ Task ${enable ? 'enabled' : 'disabled'}`);
  } else {
    onOutput(`│  ✗ Error: ${result.error}`);
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleDeleteScheduledTask(
  ctx: CommandContext,
  name: string,
  hasLocal: boolean,
  hasServer: boolean
): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput(`\n┌─ Deleting Scheduled Task: ${name}`);
  onOutput('│');

  // Delete local
  if (hasLocal) {
    const scheduledDir = path.join(process.cwd(), 'scheduled');
    const filePath = path.join(scheduledDir, `${name}.yaml`);
    try {
      await fs.unlink(filePath);
      onOutput('│  ✓ Deleted local file');
    } catch (error) {
      onOutput(`│  ✗ Failed to delete local: ${error}`);
    }
  }

  // Delete from server
  if (hasServer && serverUrl) {
    const result = await deleteScheduledTaskFromServer(serverUrl, name);
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

async function handleEditScheduledTask(_ctx: CommandContext, name: string): Promise<CommandResult> {
  const local = await getLocalScheduledTask(name);

  if (!local.exists || !local.content || !local.filePath) {
    return {
      success: false,
      message: `Task "${name}" not found locally. Create it first with /scheduled create.`,
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

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleScheduledCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args } = ctx;
  const subCommand = args[0]?.toLowerCase();
  const taskName = args[1];
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  // No subcommand - show interactive menu
  if (!subCommand) {
    const localTasks = (await getLocalScheduledTasks()).map(t => t.name);
    let serverTasks: ScheduledTask[] = [];
    if (serverUrl) {
      const result = await fetchScheduledTasksFromServer(serverUrl);
      if (result.success && result.tasks) {
        serverTasks = result.tasks;
      }
    }

    return {
      success: true,
      silent: true,
      wizard: createScheduledMenuWizard(ctx, localTasks, serverTasks),
    };
  }

  // Subcommand routing
  switch (subCommand) {
    case 'list':
    case 'ls':
      return await handleListScheduledTasks(ctx);

    case 'create':
    case 'new':
      if (taskName) {
        return await createScheduledTaskFromWizard(ctx, { name: taskName, schedule: '0 * * * *' });
      }
      return {
        success: true,
        silent: true,
        wizard: createScheduledCreateWizard(ctx),
      };

    case 'edit':
      if (!taskName) {
        return { success: false, message: 'Usage: /scheduled edit <name>' };
      }
      return await handleEditScheduledTask(ctx, taskName);

    case 'enable':
      if (!taskName) {
        return { success: false, message: 'Usage: /scheduled enable <name>' };
      }
      return await handleToggleScheduledTask(ctx, taskName, true);

    case 'disable':
      if (!taskName) {
        return { success: false, message: 'Usage: /scheduled disable <name>' };
      }
      return await handleToggleScheduledTask(ctx, taskName, false);

    case 'delete':
    case 'rm':
      if (!taskName) {
        const localTasks = (await getLocalScheduledTasks()).map(t => t.name);
        let serverTasks: ScheduledTask[] = [];
        if (serverUrl) {
          const result = await fetchScheduledTasksFromServer(serverUrl);
          if (result.success && result.tasks) {
            serverTasks = result.tasks;
          }
        }
        return {
          success: true,
          silent: true,
          wizard: createScheduledDeleteWizard(ctx, localTasks, serverTasks),
        };
      }
      const localTasks = (await getLocalScheduledTasks()).map(t => t.name);
      let serverTasks: ScheduledTask[] = [];
      if (serverUrl) {
        const result = await fetchScheduledTasksFromServer(serverUrl);
        if (result.success && result.tasks) {
          serverTasks = result.tasks;
        }
      }
      return await handleDeleteScheduledTask(
        ctx,
        taskName,
        localTasks.includes(taskName),
        serverTasks.some(t => t.name === taskName)
      );

    default:
      return {
        success: false,
        message: `Unknown subcommand: ${subCommand}\n\nUsage: /scheduled [list|create|edit|enable|disable|delete] [name]`,
      };
  }
}

/**
 * Scheduled command definition
 */
const scheduledCommand: SlashCommand = {
  name: 'scheduled',
  aliases: ['schedule', 'task', 'tasks', 'cron'],
  description: 'Manage scheduled tasks',
  usage: '/scheduled [list|create|edit|enable|disable|delete] [name]',
  examples: [
    '/scheduled',
    '/scheduled list',
    '/scheduled create',
    '/scheduled enable health_check',
    '/scheduled disable health_check',
    '/scheduled delete old_task',
  ],
  execute: handleScheduledCommand,
};

/**
 * Register the scheduled command
 */
export function registerScheduledCommand(): void {
  commandRegistry.register(scheduledCommand);
}

/**
 * /filter Command - Incident Filter Management
 *
 * Configure and manage incident filters for routing and processing
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

interface IncidentFilter {
  name: string;
  description?: string;
  enabled: boolean;
  conditions: FilterCondition[];
  actions: FilterAction[];
}

interface FilterCondition {
  field: string;
  operator: 'equals' | 'contains' | 'startsWith' | 'regex';
  value: string;
}

interface FilterAction {
  type: 'route' | 'tag' | 'notify' | 'suppress';
  target?: string;
  value?: string;
}

// ============================================================================
// API HELPERS
// ============================================================================

async function fetchFiltersFromServer(serverUrl: string): Promise<{
  success: boolean;
  filters?: IncidentFilter[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/filters`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json() as { value?: IncidentFilter[] } | IncidentFilter[];
    const filters = Array.isArray(data) ? data : data.value || [];
    return { success: true, filters };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function deleteFilterFromServer(serverUrl: string, name: string): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/filters/${name}`,
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

async function toggleFilterOnServer(serverUrl: string, name: string, enable: boolean): Promise<{
  success: boolean;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/extendedAgent/filters/${name}/${enable ? 'enable' : 'disable'}`,
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


// ============================================================================
// LOCAL FILE HELPERS
// ============================================================================

async function getLocalFilters(): Promise<Array<{ name: string; file: string; content: string }>> {
  const filtersDir = path.join(process.cwd(), 'filters');
  const filters: Array<{ name: string; file: string; content: string }> = [];

  try {
    const files = await fs.readdir(filtersDir);
    for (const file of files) {
      if (file.endsWith('.yaml') || file.endsWith('.yml')) {
        const filePath = path.join(filtersDir, file);
        const content = await fs.readFile(filePath, 'utf-8');
        const nameMatch = content.match(/name:\s*(.+)/);
        filters.push({
          name: nameMatch?.[1]?.trim() || file.replace(/\.ya?ml$/, ''),
          file,
          content,
        });
      }
    }
  } catch {
    // Directory doesn't exist
  }

  return filters;
}

async function getLocalFilter(name: string): Promise<{ exists: boolean; content?: string; filePath?: string }> {
  const filtersDir = path.join(process.cwd(), 'filters');
  const filePath = path.join(filtersDir, `${name}.yaml`);

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

function getFilterTemplate(name: string): string {
  return `apiVersion: srectl.filter/v2
kind: IncidentFilter
metadata:
  name: ${name}
spec:
  description: |
    Filter for routing specific incidents
  enabled: false
  # Conditions to match (all must be true)
  conditions:
    - field: severity
      operator: equals
      value: "Sev1"
    - field: title
      operator: contains
      value: "database"
  # Actions to take when conditions match
  actions:
    - type: route
      target: database_agent
    - type: tag
      value: "database-incident"
    # - type: notify
    #   target: "#database-alerts"
    # - type: suppress
`;
}

// ============================================================================
// WIZARDS
// ============================================================================

function createFilterMenuWizard(
  ctx: CommandContext,
  localFilters: string[],
  serverFilters: IncidentFilter[]
): WizardConfig {
  const enabledCount = serverFilters.filter(f => f.enabled).length;

  return {
    id: 'filter-menu',
    title: 'Incident Filter Management',
    steps: [
      {
        id: 'action',
        title: 'Choose Action',
        prompt: 'What would you like to do?',
        type: 'select',
        options: [
          { key: 'list', label: 'List Filters', description: `View all filters (${enabledCount} enabled)` },
          { key: 'create', label: 'Create New Filter', description: 'Create a new incident filter' },
          { key: 'edit', label: 'Edit Filter', description: 'Modify an existing filter' },
          { key: 'toggle', label: 'Enable/Disable Filter', description: 'Toggle filter state' },
          { key: 'delete', label: 'Delete Filter', description: 'Remove a filter' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      switch (data.action) {
        case 'list':
          return await handleListFilters(ctx);
        case 'create':
          return { success: true, silent: true, wizard: createFilterCreateWizard(ctx) };
        case 'edit':
          return { success: true, silent: true, wizard: createFilterEditWizard(ctx, localFilters) };
        case 'toggle':
          return { success: true, silent: true, wizard: createFilterToggleWizard(ctx, serverFilters) };
        case 'delete':
          return { success: true, silent: true, wizard: createFilterDeleteWizard(ctx, localFilters, serverFilters) };
        default:
          return { success: false, message: 'Unknown action' };
      }
    },
  };
}

function createFilterCreateWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'filter-create',
    title: 'Create Incident Filter',
    steps: [
      {
        id: 'name',
        title: 'Filter Name',
        prompt: 'Enter a name for your filter:',
        type: 'input',
        placeholder: 'database_routing',
        defaultValue: '',
      },
      {
        id: 'type',
        title: 'Filter Type',
        prompt: 'What type of filter would you like to create?',
        type: 'select',
        options: [
          { key: 'route', label: 'Routing Filter', description: 'Route incidents to specific agents' },
          { key: 'tag', label: 'Tagging Filter', description: 'Add tags to matching incidents' },
          { key: 'suppress', label: 'Suppression Filter', description: 'Suppress low-priority incidents' },
          { key: 'notify', label: 'Notification Filter', description: 'Send alerts for matching incidents' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => createFilterFromWizard(ctx, data),
  };
}

function createFilterEditWizard(ctx: CommandContext, localFilters: string[]): WizardConfig {
  const filterOptions = localFilters.map(f => ({
    key: f,
    label: f,
    description: 'Edit this filter',
  }));

  if (filterOptions.length === 0) {
    filterOptions.push({
      key: 'none',
      label: 'No local filters found',
      description: 'Create a filter first with /filter create',
    });
  }

  return {
    id: 'filter-edit',
    title: 'Edit Filter',
    steps: [
      {
        id: 'filter',
        title: 'Select Filter',
        prompt: 'Which filter would you like to edit?',
        type: 'select',
        options: filterOptions,
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.filter === 'none') {
        return { success: false, message: 'No filter selected.' };
      }
      return await handleEditFilter(ctx, data.filter);
    },
  };
}

function createFilterToggleWizard(ctx: CommandContext, serverFilters: IncidentFilter[]): WizardConfig {
  const filterOptions = serverFilters.map(f => ({
    key: f.name,
    label: `${f.name} ${f.enabled ? '●' : '○'}`,
    description: f.enabled ? 'Enabled - click to disable' : 'Disabled - click to enable',
  }));

  if (filterOptions.length === 0) {
    filterOptions.push({
      key: 'none',
      label: 'No filters found on server',
      description: 'Deploy a filter first with /apply',
    });
  }

  return {
    id: 'filter-toggle',
    title: 'Enable/Disable Filter',
    steps: [
      {
        id: 'filter',
        title: 'Select Filter',
        prompt: 'Which filter would you like to toggle?',
        type: 'select',
        options: filterOptions,
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.filter === 'none') {
        return { success: false, message: 'No filter selected.' };
      }
      const filter = serverFilters.find(f => f.name === data.filter);
      if (!filter) {
        return { success: false, message: 'Filter not found.' };
      }
      return await handleToggleFilter(ctx, data.filter, !filter.enabled);
    },
  };
}

function createFilterDeleteWizard(
  ctx: CommandContext,
  localFilters: string[],
  serverFilters: IncidentFilter[]
): WizardConfig {
  const allNames = [...new Set([...localFilters, ...serverFilters.map(f => f.name)])];
  const filterOptions = allNames.map(name => {
    const hasLocal = localFilters.includes(name);
    const hasServer = serverFilters.some(f => f.name === name);
    return {
      key: name,
      label: name,
      description: hasLocal && hasServer ? 'Local + Server' : hasLocal ? 'Local only' : 'Server only',
    };
  });

  if (filterOptions.length === 0) {
    filterOptions.push({
      key: 'none',
      label: 'No filters found',
      description: 'Nothing to delete',
    });
  }

  return {
    id: 'filter-delete',
    title: 'Delete Filter',
    steps: [
      {
        id: 'filter',
        title: 'Select Filter',
        prompt: 'Which filter would you like to delete?',
        type: 'select',
        options: filterOptions,
      },
      {
        id: 'confirm',
        title: 'Confirm Deletion',
        prompt: 'Are you sure you want to delete this filter?',
        type: 'confirm',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (data.filter === 'none') {
        return { success: false, message: 'No filter selected.' };
      }
      if (data.confirm !== 'yes') {
        return { success: false, message: 'Deletion cancelled.' };
      }
      return await handleDeleteFilter(
        ctx,
        data.filter,
        localFilters.includes(data.filter),
        serverFilters.some(f => f.name === data.filter)
      );
    },
  };
}

// ============================================================================
// SUBCOMMAND HANDLERS
// ============================================================================

async function createFilterFromWizard(ctx: CommandContext, data: Record<string, string>): Promise<CommandResult> {
  const { onOutput } = ctx;
  const filterName = data.name?.replace(/\s+/g, '_') || 'new_filter';

  if (!filterName || filterName.includes(' ')) {
    return { success: false, message: 'Filter name cannot contain spaces.' };
  }

  const filtersDir = path.join(process.cwd(), 'filters');
  await fs.mkdir(filtersDir, { recursive: true });
  const filePath = path.join(filtersDir, `${filterName}.yaml`);

  // Check if exists
  try {
    await fs.access(filePath);
    return {
      success: false,
      message: `Filter "${filterName}" already exists. Use /filter edit ${filterName} to modify.`,
    };
  } catch {
    // Good - doesn't exist
  }

  // Create from template
  const content = getFilterTemplate(filterName);
  await fs.writeFile(filePath, content, 'utf-8');

  onOutput(`\n✓ Created filter: ${filePath}`);
  onOutput('  Edit the file and use /apply to deploy.\n');

  return {
    success: true,
    silent: true,
    editor: {
      content,
      filename: `${filterName}.yaml`,
      filePath,
      fileType: 'yaml',
    },
  };
}

async function handleListFilters(ctx: CommandContext): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput('\n┌─ Incident Filters');
  onOutput('│');

  // Local filters
  const localFilters = await getLocalFilters();
  onOutput('│  Local Filters:');
  if (localFilters.length === 0) {
    onOutput('│    (none)');
  } else {
    for (const filter of localFilters) {
      onOutput(`│    • ${filter.name} (${filter.file})`);
    }
  }

  // Server filters
  onOutput('│');
  onOutput('│  Server Filters:');
  if (serverUrl) {
    const result = await fetchFiltersFromServer(serverUrl);
    if (result.success && result.filters) {
      if (result.filters.length === 0) {
        onOutput('│    (none)');
      } else {
        for (const filter of result.filters) {
          const status = filter.enabled ? '●' : '○';
          onOutput(`│    ${status} ${filter.name}`);
          if (filter.description) {
            const desc = filter.description.split('\n')[0].slice(0, 40);
            onOutput(`│      ${desc}${filter.description.length > 40 ? '...' : ''}`);
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

async function handleEditFilter(_ctx: CommandContext, name: string): Promise<CommandResult> {
  const local = await getLocalFilter(name);

  if (!local.exists || !local.content || !local.filePath) {
    return {
      success: false,
      message: `Filter "${name}" not found locally. Create it first with /filter create.`,
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

async function handleToggleFilter(ctx: CommandContext, name: string, enable: boolean): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  if (!serverUrl) {
    return { success: false, message: 'Server not configured. Run /init first.' };
  }

  onOutput(`\n┌─ ${enable ? 'Enabling' : 'Disabling'} Filter: ${name}`);
  onOutput('│');

  const result = await toggleFilterOnServer(serverUrl, name, enable);

  if (result.success) {
    onOutput(`│  ✓ Filter ${enable ? 'enabled' : 'disabled'}`);
  } else {
    onOutput(`│  ✗ Error: ${result.error}`);
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleDeleteFilter(
  ctx: CommandContext,
  name: string,
  hasLocal: boolean,
  hasServer: boolean
): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput(`\n┌─ Deleting Filter: ${name}`);
  onOutput('│');

  // Delete local
  if (hasLocal) {
    const filtersDir = path.join(process.cwd(), 'filters');
    const filePath = path.join(filtersDir, `${name}.yaml`);
    try {
      await fs.unlink(filePath);
      onOutput('│  ✓ Deleted local file');
    } catch (error) {
      onOutput(`│  ✗ Failed to delete local: ${error}`);
    }
  }

  // Delete from server
  if (hasServer && serverUrl) {
    const result = await deleteFilterFromServer(serverUrl, name);
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

async function handleTestFilter(ctx: CommandContext, name: string): Promise<CommandResult> {
  const { onOutput } = ctx;

  onOutput('\n┌─ Testing Filter: ' + name);
  onOutput('│');
  onOutput('│  Enter a test incident to see if it matches.');
  onOutput('│  Format: { "title": "...", "severity": "Sev1", ... }');
  onOutput('│');
  onOutput('│  Or type /filter test ' + name + ' <incident-json>');
  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleFilterCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args } = ctx;
  const subCommand = args[0]?.toLowerCase();
  const filterName = args[1];
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  // No subcommand - show interactive menu
  if (!subCommand) {
    const localFilters = (await getLocalFilters()).map(f => f.name);
    let serverFilters: IncidentFilter[] = [];
    if (serverUrl) {
      const result = await fetchFiltersFromServer(serverUrl);
      if (result.success && result.filters) {
        serverFilters = result.filters;
      }
    }

    return {
      success: true,
      silent: true,
      wizard: createFilterMenuWizard(ctx, localFilters, serverFilters),
    };
  }

  // Subcommand routing
  switch (subCommand) {
    case 'list':
    case 'ls':
      return await handleListFilters(ctx);

    case 'create':
    case 'new':
      if (filterName) {
        return await createFilterFromWizard(ctx, { name: filterName, type: 'route' });
      }
      return {
        success: true,
        silent: true,
        wizard: createFilterCreateWizard(ctx),
      };

    case 'edit':
      if (!filterName) {
        const localFilters = (await getLocalFilters()).map(f => f.name);
        return {
          success: true,
          silent: true,
          wizard: createFilterEditWizard(ctx, localFilters),
        };
      }
      return await handleEditFilter(ctx, filterName);

    case 'enable':
      if (!filterName) {
        return { success: false, message: 'Usage: /filter enable <name>' };
      }
      return await handleToggleFilter(ctx, filterName, true);

    case 'disable':
      if (!filterName) {
        return { success: false, message: 'Usage: /filter disable <name>' };
      }
      return await handleToggleFilter(ctx, filterName, false);

    case 'delete':
    case 'rm':
      if (!filterName) {
        const localFilters = (await getLocalFilters()).map(f => f.name);
        let serverFilters: IncidentFilter[] = [];
        if (serverUrl) {
          const result = await fetchFiltersFromServer(serverUrl);
          if (result.success && result.filters) {
            serverFilters = result.filters;
          }
        }
        return {
          success: true,
          silent: true,
          wizard: createFilterDeleteWizard(ctx, localFilters, serverFilters),
        };
      }
      const localFilters = (await getLocalFilters()).map(f => f.name);
      let serverFilters: IncidentFilter[] = [];
      if (serverUrl) {
        const result = await fetchFiltersFromServer(serverUrl);
        if (result.success && result.filters) {
          serverFilters = result.filters;
        }
      }
      return await handleDeleteFilter(
        ctx,
        filterName,
        localFilters.includes(filterName),
        serverFilters.some(f => f.name === filterName)
      );

    case 'test':
      if (!filterName) {
        return { success: false, message: 'Usage: /filter test <name>' };
      }
      return await handleTestFilter(ctx, filterName);

    default:
      return {
        success: false,
        message: `Unknown subcommand: ${subCommand}\n\nUsage: /filter [list|create|edit|enable|disable|delete|test] [name]`,
      };
  }
}

/**
 * Filter command definition
 */
const filterCommand: SlashCommand = {
  name: 'filter',
  aliases: ['filters', 'incident-filter', 'incfilter'],
  description: 'Configure incident filters for routing and processing',
  usage: '/filter [list|create|edit|enable|disable|delete|test] [name]',
  examples: [
    '/filter',
    '/filter list',
    '/filter create',
    '/filter create database_routing',
    '/filter edit database_routing',
    '/filter enable database_routing',
    '/filter disable database_routing',
    '/filter delete database_routing',
    '/filter test database_routing',
  ],
  execute: handleFilterCommand,
};

/**
 * Register the filter command
 */
export function registerFilterCommand(): void {
  commandRegistry.register(filterCommand);
}

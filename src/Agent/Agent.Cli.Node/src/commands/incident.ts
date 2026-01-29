/**
 * /incident Command - Incident Management
 *
 * List, view, and work with incidents
 */
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

interface Incident {
  id: string;
  title: string;
  severity: 'Sev0' | 'Sev1' | 'Sev2' | 'Sev3' | 'Sev4';
  status: 'Active' | 'Mitigated' | 'Resolved';
  createdTime: string;
  owningTeam?: string;
  impactedServices?: string[];
  description?: string;
}

// ============================================================================
// API HELPERS
// ============================================================================

async function fetchIncidentsFromServer(serverUrl: string, filter?: string): Promise<{
  success: boolean;
  incidents?: Incident[];
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    let url = `${serverUrl.replace(/\/$/, '')}/api/v2/incidents`;
    if (filter) {
      url += `?$filter=${encodeURIComponent(filter)}`;
    }

    const response = await fetch(url, {
      headers: { Accept: 'application/json', ...authHeaders },
    });

    if (!response.ok) {
      return { success: false, error: `HTTP ${response.status}` };
    }

    const data = await response.json() as { value?: Incident[] } | Incident[];
    const incidents = Array.isArray(data) ? data : data.value || [];
    return { success: true, incidents };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

async function fetchIncidentFromServer(serverUrl: string, id: string): Promise<{
  success: boolean;
  incident?: Incident;
  error?: string;
}> {
  try {
    const authHeaders = await getAuthHeaders();
    const response = await fetch(
      `${serverUrl.replace(/\/$/, '')}/api/v2/incidents/${id}`,
      {
        headers: { Accept: 'application/json', ...authHeaders },
      }
    );

    if (!response.ok) {
      if (response.status === 404) {
        return { success: false, error: 'Incident not found' };
      }
      return { success: false, error: `HTTP ${response.status}` };
    }

    const incident = await response.json() as Incident;
    return { success: true, incident };
  } catch (error) {
    return { success: false, error: String(error) };
  }
}

// ============================================================================
// DISPLAY HELPERS
// ============================================================================

function getSeverityColor(severity: string): string {
  switch (severity) {
    case 'Sev0': return '🔴';
    case 'Sev1': return '🟠';
    case 'Sev2': return '🟡';
    case 'Sev3': return '🔵';
    case 'Sev4': return '⚪';
    default: return '⚫';
  }
}

function getStatusIndicator(status: string): string {
  switch (status) {
    case 'Active': return '🔥';
    case 'Mitigated': return '🔧';
    case 'Resolved': return '✅';
    default: return '❓';
  }
}

function formatIncidentRow(incident: Incident): string {
  const sev = getSeverityColor(incident.severity);
  const status = getStatusIndicator(incident.status);
  const title = incident.title.length > 50 ? incident.title.slice(0, 47) + '...' : incident.title;
  return `${sev} ${status} ${incident.id.padEnd(12)} ${title}`;
}

function formatIncidentDetails(incident: Incident): string {
  const lines = [
    '',
    '┌─ Incident Details',
    '│',
    `│  ID:       ${incident.id}`,
    `│  Title:    ${incident.title}`,
    `│  Severity: ${getSeverityColor(incident.severity)} ${incident.severity}`,
    `│  Status:   ${getStatusIndicator(incident.status)} ${incident.status}`,
    `│  Created:  ${incident.createdTime}`,
  ];

  if (incident.owningTeam) {
    lines.push(`│  Team:     ${incident.owningTeam}`);
  }

  if (incident.impactedServices?.length) {
    lines.push('│');
    lines.push('│  Impacted Services:');
    for (const service of incident.impactedServices) {
      lines.push(`│    • ${service}`);
    }
  }

  if (incident.description) {
    lines.push('│');
    lines.push('│  Description:');
    const descLines = incident.description.split('\n');
    for (const line of descLines.slice(0, 5)) {
      lines.push(`│    ${line}`);
    }
    if (descLines.length > 5) {
      lines.push(`│    ... (${descLines.length - 5} more lines)`);
    }
  }

  lines.push('│');
  lines.push('└─');
  lines.push('');

  return lines.join('\n');
}

// ============================================================================
// TEST MODE
// ============================================================================

const TEST_INCIDENTS: Incident[] = [
  {
    id: 'INC001',
    title: 'Database connection timeout in production',
    severity: 'Sev1',
    status: 'Active',
    createdTime: new Date().toISOString(),
    owningTeam: 'Database Team',
    impactedServices: ['API Gateway', 'User Service', 'Order Service'],
    description: 'Multiple services reporting database connection timeouts. Initial investigation shows connection pool exhaustion.',
  },
  {
    id: 'INC002',
    title: 'Elevated error rates in payment processing',
    severity: 'Sev2',
    status: 'Mitigated',
    createdTime: new Date(Date.now() - 3600000).toISOString(),
    owningTeam: 'Payments Team',
    impactedServices: ['Payment Gateway'],
    description: 'Payment failures increased to 5%. Root cause identified as third-party API latency.',
  },
  {
    id: 'INC003',
    title: 'Scheduled maintenance notification',
    severity: 'Sev4',
    status: 'Resolved',
    createdTime: new Date(Date.now() - 86400000).toISOString(),
    owningTeam: 'Platform Team',
    impactedServices: [],
    description: 'Planned maintenance completed successfully.',
  },
];

// ============================================================================
// WIZARDS
// ============================================================================

function createIncidentMenuWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'incident-menu',
    title: 'Incident Management',
    steps: [
      {
        id: 'action',
        title: 'Choose Action',
        prompt: 'What would you like to do?',
        type: 'select',
        options: [
          { key: 'list', label: 'List Incidents', description: 'View active and recent incidents' },
          { key: 'view', label: 'View Incident', description: 'See details for a specific incident' },
          { key: 'filter', label: 'Filter Incidents', description: 'Search incidents by criteria' },
          { key: 'retro', label: 'Incident Retro Mode', description: 'Enter retro mode for testing' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      switch (data.action) {
        case 'list':
          return await handleListIncidents(ctx, 'Active');
        case 'view':
          return { success: true, silent: true, wizard: createIncidentViewWizard(ctx) };
        case 'filter':
          return { success: true, silent: true, wizard: createIncidentFilterWizard(ctx) };
        case 'retro':
          return await handleRetroMode(ctx);
        default:
          return { success: false, message: 'Unknown action' };
      }
    },
  };
}

function createIncidentViewWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'incident-view',
    title: 'View Incident',
    steps: [
      {
        id: 'id',
        title: 'Incident ID',
        prompt: 'Enter the incident ID:',
        type: 'input',
        placeholder: 'INC001',
        defaultValue: '',
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      if (!data.id) {
        return { success: false, message: 'Incident ID is required.' };
      }
      return await handleViewIncident(ctx, data.id);
    },
  };
}

function createIncidentFilterWizard(ctx: CommandContext): WizardConfig {
  return {
    id: 'incident-filter',
    title: 'Filter Incidents',
    steps: [
      {
        id: 'status',
        title: 'Status',
        prompt: 'Filter by status:',
        type: 'select',
        options: [
          { key: 'Active', label: 'Active', description: 'Currently active incidents' },
          { key: 'Mitigated', label: 'Mitigated', description: 'Mitigated but not resolved' },
          { key: 'Resolved', label: 'Resolved', description: 'Resolved incidents' },
          { key: 'all', label: 'All', description: 'All incidents' },
        ],
      },
      {
        id: 'severity',
        title: 'Severity',
        prompt: 'Filter by severity:',
        type: 'select',
        options: [
          { key: 'all', label: 'All Severities', description: 'Show all severity levels' },
          { key: 'Sev0,Sev1', label: 'Critical (Sev0-1)', description: 'Critical incidents only' },
          { key: 'Sev2,Sev3', label: 'Medium (Sev2-3)', description: 'Medium priority' },
          { key: 'Sev4', label: 'Low (Sev4)', description: 'Low priority' },
        ],
      },
    ],
    currentStep: 0,
    data: {},
    onComplete: async (data) => {
      const status = data.status === 'all' ? undefined : data.status;
      return await handleListIncidents(ctx, status);
    },
  };
}

// ============================================================================
// SUBCOMMAND HANDLERS
// ============================================================================

async function handleListIncidents(ctx: CommandContext, statusFilter?: string): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  onOutput('\n┌─ Incidents');
  onOutput('│');

  let incidents: Incident[] = [];

  if (serverUrl) {
    const filter = statusFilter ? `status eq '${statusFilter}'` : undefined;
    const result = await fetchIncidentsFromServer(serverUrl, filter);
    if (result.success && result.incidents) {
      incidents = result.incidents;
    } else {
      onOutput(`│  Error fetching incidents: ${result.error}`);
      onOutput('│  Showing test data instead...');
      onOutput('│');
      incidents = TEST_INCIDENTS;
    }
  } else {
    onOutput('│  (Server not configured - showing test data)');
    onOutput('│');
    incidents = TEST_INCIDENTS;
  }

  if (statusFilter) {
    incidents = incidents.filter(i => i.status === statusFilter);
  }

  if (incidents.length === 0) {
    onOutput('│  No incidents found.');
  } else {
    onOutput('│  Legend: 🔴Sev0 🟠Sev1 🟡Sev2 🔵Sev3 ⚪Sev4 | 🔥Active 🔧Mitigated ✅Resolved');
    onOutput('│');
    for (const incident of incidents.slice(0, 20)) {
      onOutput(`│  ${formatIncidentRow(incident)}`);
    }
    if (incidents.length > 20) {
      onOutput(`│  ... and ${incidents.length - 20} more`);
    }
  }

  onOutput('│');
  onOutput('└─\n');

  return { success: true, silent: true };
}

async function handleViewIncident(ctx: CommandContext, id: string): Promise<CommandResult> {
  const { onOutput } = ctx;
  const serverUrl = ctx.services.config.getServerUrl?.() || '';

  let incident: Incident | undefined;

  if (serverUrl) {
    const result = await fetchIncidentFromServer(serverUrl, id);
    if (result.success && result.incident) {
      incident = result.incident;
    } else {
      // Fall back to test data
      incident = TEST_INCIDENTS.find(i => i.id === id);
    }
  } else {
    incident = TEST_INCIDENTS.find(i => i.id === id);
  }

  if (!incident) {
    return { success: false, message: `Incident "${id}" not found.` };
  }

  onOutput(formatIncidentDetails(incident));
  return { success: true, silent: true };
}

async function handleRetroMode(ctx: CommandContext): Promise<CommandResult> {
  const { onOutput, onStateChange } = ctx;

  onOutput('\n┌─ Incident Retro Mode');
  onOutput('│');
  onOutput('│  You are now in incident retro mode.');
  onOutput('│  This mode allows you to test incident handling without');
  onOutput('│  affecting production incidents.');
  onOutput('│');
  onOutput('│  Test incidents are available:');
  for (const incident of TEST_INCIDENTS) {
    onOutput(`│    • ${incident.id}: ${incident.title}`);
  }
  onOutput('│');
  onOutput('│  Commands:');
  onOutput('│    /incident view <id>  - View incident details');
  onOutput('│    /incident list       - List test incidents');
  onOutput('│    Type any message to chat about an incident');
  onOutput('│');
  onOutput('└─\n');

  // Set state to indicate retro mode
  onStateChange({ currentPlan: 'incident-retro' });

  return { success: true, silent: true };
}

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleIncidentCommand(ctx: CommandContext): Promise<CommandResult> {
  const { args } = ctx;
  const subCommand = args[0]?.toLowerCase();
  const incidentId = args[1];

  // No subcommand - show interactive menu
  if (!subCommand) {
    return {
      success: true,
      silent: true,
      wizard: createIncidentMenuWizard(ctx),
    };
  }

  // Subcommand routing
  switch (subCommand) {
    case 'list':
    case 'ls':
      const statusArg = args[1];
      return await handleListIncidents(ctx, statusArg);

    case 'view':
    case 'get':
    case 'show':
      if (!incidentId) {
        return {
          success: true,
          silent: true,
          wizard: createIncidentViewWizard(ctx),
        };
      }
      return await handleViewIncident(ctx, incidentId);

    case 'retro':
    case 'test':
      return await handleRetroMode(ctx);

    case 'active':
      return await handleListIncidents(ctx, 'Active');

    case 'mitigated':
      return await handleListIncidents(ctx, 'Mitigated');

    case 'resolved':
      return await handleListIncidents(ctx, 'Resolved');

    default:
      // Try to interpret as incident ID
      if (subCommand.match(/^[A-Za-z0-9]+$/)) {
        return await handleViewIncident(ctx, subCommand);
      }
      return {
        success: false,
        message: `Unknown subcommand: ${subCommand}\n\nUsage: /incident [list|view|retro|active|mitigated|resolved] [id]`,
      };
  }
}

/**
 * Incident command definition
 */
const incidentCommand: SlashCommand = {
  name: 'incident',
  aliases: ['inc', 'incidents'],
  description: 'Work with incidents',
  usage: '/incident [list|view|retro|active] [id]',
  examples: [
    '/incident',
    '/incident list',
    '/incident view INC001',
    '/incident INC001',
    '/incident active',
    '/incident retro',
  ],
  execute: handleIncidentCommand,
};

/**
 * Register the incident command
 */
export function registerIncidentCommand(): void {
  commandRegistry.register(incidentCommand);
}

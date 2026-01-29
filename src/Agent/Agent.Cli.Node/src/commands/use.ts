/**
 * /use Command - Select agent for chat session
 * Interactive command to choose which agent to use
 */
import { commandRegistry } from './registry';
import type { SlashCommand, CommandContext, CommandResult } from './types';
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

// ============================================================================
// COMMAND HANDLER
// ============================================================================

async function handleUseCommand(ctx: CommandContext): Promise<CommandResult> {
  const serverUrl = ctx.services.config.getServerUrl?.() || '';
  const agentName = ctx.args[0];

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

// ============================================================================
// COMMAND REGISTRATION
// ============================================================================

const useCommand: SlashCommand = {
  name: 'use',
  aliases: [],
  description: 'Select which agent to use for chat',
  usage: '/use [agent_name]',
  examples: [
    '/use',
    '/use my_agent',
  ],
  execute: handleUseCommand,
};

export function registerUseCommand(): void {
  commandRegistry.register(useCommand);
}

export default useCommand;

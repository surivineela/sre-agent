/**
 * Agent management tools
 */
import type { ToolDefinition, ToolContext } from '../../types';

/**
 * Create agent tool
 */
export const agentCreate: ToolDefinition = {
  name: 'agent_create',
  description: 'Create a new SRE agent with specified configuration',
  inputSchema: {
    type: 'object',
    properties: {
      name: {
        type: 'string',
        description: 'Agent name (lowercase, no spaces)',
      },
      instructions: {
        type: 'string',
        description: 'Agent instructions (minimum 50 characters)',
      },
      tools: {
        type: 'array',
        items: { type: 'string' },
        description: 'List of tool names to include',
      },
      handoffs: {
        type: 'array',
        items: { type: 'string' },
        description: 'List of agent names this agent can hand off to',
      },
      temperature: {
        type: 'number',
        description: 'Temperature for response generation (0-2)',
      },
    },
    required: ['name', 'instructions'],
  },
  execute: async (input, context: ToolContext) => {
    const { name, instructions, tools, handoffs, temperature } = input as {
      name: string;
      instructions: string;
      tools?: string[];
      handoffs?: string[];
      temperature?: number;
    };

    const result = await context.api.createAgent({
      name,
      instructions,
      tools,
      handoffs,
      temperature,
    });

    return {
      success: true,
      agent: result,
      message: `Created agent: ${result.name}`,
    };
  },
  requiresPermission: 'session',
  category: 'agent_management',
};

/**
 * List agents tool
 */
export const agentList: ToolDefinition = {
  name: 'agent_list',
  description: 'List all available agents',
  inputSchema: {
    type: 'object',
    properties: {},
  },
  execute: async (_, context: ToolContext) => {
    const agents = await context.api.listAgents();
    return {
      agents,
      count: agents.length,
    };
  },
  requiresPermission: 'none',
  category: 'agent_management',
};

/**
 * Get agent tool
 */
export const agentGet: ToolDefinition = {
  name: 'agent_get',
  description: 'Get details of a specific agent',
  inputSchema: {
    type: 'object',
    properties: {
      name: {
        type: 'string',
        description: 'Agent name',
      },
    },
    required: ['name'],
  },
  execute: async (input, context: ToolContext) => {
    const { name } = input as { name: string };
    const agent = await context.api.getAgent(name);
    return { agent };
  },
  requiresPermission: 'none',
  category: 'agent_management',
};

/**
 * Delete agent tool
 */
export const agentDelete: ToolDefinition = {
  name: 'agent_delete',
  description: 'Delete an agent',
  inputSchema: {
    type: 'object',
    properties: {
      name: {
        type: 'string',
        description: 'Agent name to delete',
      },
    },
    required: ['name'],
  },
  execute: async (input, context: ToolContext) => {
    const { name } = input as { name: string };
    await context.api.deleteAgent(name);
    return {
      success: true,
      message: `Deleted agent: ${name}`,
    };
  },
  requiresPermission: 'always',
  category: 'agent_management',
};

/**
 * Generate smart agent tool
 */
export const agentGenerateSmart: ToolDefinition = {
  name: 'agent_generate_smart',
  description: 'Use AI to generate agent instructions based on a description',
  inputSchema: {
    type: 'object',
    properties: {
      name: {
        type: 'string',
        description: 'Name for the new agent',
      },
      description: {
        type: 'string',
        description: 'Description of what the agent should do',
      },
    },
    required: ['name'],
  },
  execute: async (input, context: ToolContext) => {
    const { name, description } = input as { name: string; description?: string };
    const result = await context.api.generateSmartAgent(name, description);
    return {
      success: true,
      generatedAgent: result,
      message: `Generated instructions for agent: ${name}`,
    };
  },
  requiresPermission: 'session',
  category: 'agent_management',
};

/**
 * All agent tools
 */
export const agentTools: ToolDefinition[] = [
  agentCreate,
  agentList,
  agentGet,
  agentDelete,
  agentGenerateSmart,
];

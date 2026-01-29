/**
 * Thread/conversation management tools
 */
import type { ToolDefinition, ToolContext } from '../../types';

/**
 * Create thread tool
 */
export const threadCreate: ToolDefinition = {
  name: 'thread_create',
  description: 'Create a new conversation thread with an agent',
  inputSchema: {
    type: 'object',
    properties: {
      agentName: {
        type: 'string',
        description: 'Name of the agent to create a thread with',
      },
      message: {
        type: 'string',
        description: 'Initial message to send to the agent',
      },
    },
    required: ['agentName', 'message'],
  },
  execute: async (input, context: ToolContext) => {
    const { agentName, message } = input as {
      agentName: string;
      message: string;
    };

    const thread = await context.api.createThread(agentName, message);

    return {
      threadId: thread.id,
      status: thread.status,
      agentName: thread.agentName,
      message: `Created thread ${thread.id} with agent ${agentName}`,
    };
  },
  requiresPermission: 'once',
  category: 'agent_management',
};

/**
 * Send message to thread tool
 */
export const threadSend: ToolDefinition = {
  name: 'thread_send',
  description: 'Send a message to an existing thread',
  inputSchema: {
    type: 'object',
    properties: {
      threadId: {
        type: 'string',
        description: 'Thread ID to send the message to',
      },
      message: {
        type: 'string',
        description: 'Message to send',
      },
    },
    required: ['threadId', 'message'],
  },
  execute: async (input, context: ToolContext) => {
    const { threadId, message } = input as {
      threadId: string;
      message: string;
    };

    const response = await context.api.sendMessage(threadId, message);

    return {
      messageId: response.id,
      role: response.role,
      content: response.content,
    };
  },
  requiresPermission: 'once',
  category: 'agent_management',
};

/**
 * Track thread tool
 */
export const threadTrack: ToolDefinition = {
  name: 'thread_track',
  description: 'Track and wait for messages from a thread',
  inputSchema: {
    type: 'object',
    properties: {
      threadId: {
        type: 'string',
        description: 'Thread ID to track',
      },
      maxWaitSeconds: {
        type: 'number',
        description: 'Maximum time to wait in seconds (default: 60)',
      },
    },
    required: ['threadId'],
  },
  execute: async (input, context: ToolContext) => {
    const { threadId, maxWaitSeconds = 60 } = input as {
      threadId: string;
      maxWaitSeconds?: number;
    };

    const messages = await context.api.trackThread(threadId, maxWaitSeconds);

    return {
      threadId,
      messages,
      count: messages.length,
    };
  },
  requiresPermission: 'none',
  category: 'agent_management',
};

/**
 * All thread tools
 */
export const threadTools: ToolDefinition[] = [
  threadCreate,
  threadSend,
  threadTrack,
];

/**
 * Context assembly for API calls
 */
import type { Message, APIContext, APIMessage, APIToolDefinition, Config } from '../types';
import { VERSION } from '../version';

/**
 * Default system prompt for the SRE agent
 */
export const CORE_SYSTEM_PROMPT = `You are an SRE (Site Reliability Engineering) assistant helping users manage agents, tools, and infrastructure.

## Capabilities
- Create and manage SRE agents
- Execute tools for infrastructure operations
- Help with Kubernetes, Azure, and other cloud operations
- Assist with incident investigation using Kusto queries
- Manage scheduled tasks and workflows

## Guidelines
- Be concise and accurate in your responses
- When using tools, explain what you're doing
- If unsure, ask clarifying questions
- For dangerous operations, explain the risks before proceeding
- Format output appropriately for the terminal

## Tool Usage
When you need to perform an action, use the available tools. Wait for tool results before continuing.
Always explain what you're about to do before invoking a tool.`;

export interface ContextAssemblerOptions {
  config: Config;
  tools: APIToolDefinition[];
  maxContextMessages?: number;
}

/**
 * Context assembler for building API request context
 */
export class ContextAssembler {
  private config: Config;
  private tools: APIToolDefinition[];
  private maxContextMessages: number;

  constructor(options: ContextAssemblerOptions) {
    this.config = options.config;
    this.tools = options.tools;
    this.maxContextMessages = options.maxContextMessages ?? 50;
  }

  /**
   * Assemble full context for API call
   */
  assembleContext(messages: Message[]): APIContext {
    return {
      systemPrompt: this.buildSystemPrompt(),
      messages: this.formatMessages(messages),
      tools: this.tools,
      maxTokens: this.config.agent.maxTokens,
      temperature: this.config.agent.temperature,
    };
  }

  /**
   * Build the complete system prompt
   */
  buildSystemPrompt(): string {
    const parts = [
      CORE_SYSTEM_PROMPT,
      this.getEnvironmentContext(),
      this.getToolDescriptions(),
    ];

    return parts.filter(Boolean).join('\n\n');
  }

  /**
   * Get environment context
   */
  private getEnvironmentContext(): string {
    return `## Environment
- Working Directory: ${process.cwd()}
- Platform: ${process.platform}
- Node Version: ${process.version}
- CLI Version: ${VERSION}
- Current Profile: ${this.config.currentProfile || 'default'}
- Server URL: ${this.config.server.url}
- Current Time: ${new Date().toISOString()}`;
  }

  /**
   * Get tool descriptions for the system prompt
   */
  private getToolDescriptions(): string {
    if (this.tools.length === 0) {
      return '';
    }

    const toolList = this.tools
      .map((tool) => `- **${tool.name}**: ${tool.description}`)
      .join('\n');

    return `## Available Tools
${toolList}`;
  }

  /**
   * Format messages for API
   */
  private formatMessages(messages: Message[]): APIMessage[] {
    // Take last N messages to stay within context
    const recentMessages = messages.slice(-this.maxContextMessages);

    return recentMessages.map((msg) => this.formatMessage(msg));
  }

  /**
   * Format a single message for API
   */
  private formatMessage(message: Message): APIMessage {
    // Tool messages become user messages with special formatting
    if (message.role === 'tool') {
      return {
        role: 'user',
        content: `[Tool Result]\n${message.content}`,
      };
    }

    // System messages are passed through
    if (message.role === 'system') {
      return {
        role: 'system',
        content: message.content,
      };
    }

    return {
      role: message.role as 'user' | 'assistant',
      content: message.content,
    };
  }

  /**
   * Update tools list
   */
  setTools(tools: APIToolDefinition[]): void {
    this.tools = tools;
  }

  /**
   * Update config
   */
  setConfig(config: Config): void {
    this.config = config;
  }

  /**
   * Estimate token count (rough approximation)
   */
  estimateTokens(messages: Message[]): number {
    const systemPrompt = this.buildSystemPrompt();
    const formattedMessages = this.formatMessages(messages);

    let totalChars = systemPrompt.length;
    for (const msg of formattedMessages) {
      totalChars += typeof msg.content === 'string' ? msg.content.length : 0;
    }

    // Rough estimate: ~4 characters per token
    return Math.ceil(totalChars / 4);
  }
}

/**
 * Create a new context assembler
 */
export function createContextAssembler(
  options: ContextAssemblerOptions
): ContextAssembler {
  return new ContextAssembler(options);
}

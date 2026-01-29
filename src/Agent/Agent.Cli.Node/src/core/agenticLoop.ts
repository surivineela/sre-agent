/**
 * Agentic loop - Core processing cycle for user input
 *
 * The agentic loop manages the cycle between:
 * 1. User input
 * 2. AI processing
 * 3. Tool execution
 * 4. Response generation
 */
import { EventEmitter } from 'events';
import type {
  Message,
  ToolCall,
  ToolResult,
  AgenticLoopConfig,
  AgenticLoopCallbacks,
  LoopState,
  LoopStatus,
  Services,
  APIContext,
  APIMessage,
} from '../types';
import { logger } from '../utils/logger';
import { generateId } from '../utils/formatting';
import { TimeoutError } from '../utils/errors';

const DEFAULT_CONFIG: AgenticLoopConfig = {
  maxIterations: 10,
  maxTokens: 8192,
  timeoutMs: 120000,
  enableExtendedThinking: true,
};

const CORE_SYSTEM_PROMPT = `You are an SRE (Site Reliability Engineering) assistant. You help users manage agents, tools, and infrastructure operations.

You have access to various tools to help accomplish tasks. When a tool is needed, you will invoke it and wait for the result before continuing.

Be helpful, concise, and accurate. If you're unsure about something, say so rather than making up information.`;

export class AgenticLoop extends EventEmitter {
  private state: LoopState;
  private config: AgenticLoopConfig;
  private services: Services;
  private abortController: AbortController | null = null;

  constructor(services: Services, config: Partial<AgenticLoopConfig> = {}) {
    super();
    this.services = services;
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.state = this.createInitialState();
  }

  private createInitialState(): LoopState {
    return {
      messages: [],
      pendingTools: [],
      iteration: 0,
      status: 'idle',
    };
  }

  /**
   * Reset the loop state
   */
  reset(): void {
    this.state = this.createInitialState();
    this.abortController?.abort();
    this.abortController = null;
  }

  /**
   * Cancel the current operation
   */
  cancel(): void {
    this.abortController?.abort();
    this.setStatus('idle');
  }

  /**
   * Get current status
   */
  getStatus(): LoopStatus {
    return this.state.status;
  }

  /**
   * Process user input through the agentic loop
   */
  async processUserInput(
    input: string,
    callbacks?: AgenticLoopCallbacks
  ): Promise<{ response: string; toolCalls: ToolCall[] }> {
    logger.debug('Processing user input', { input: input.slice(0, 100) });

    // Create new abort controller
    this.abortController = new AbortController();

    // Add user message
    this.addMessage({ role: 'user', content: input });

    // Track all tool calls in this turn
    const allToolCalls: ToolCall[] = [];
    let finalResponse = '';

    try {
      // Enter the agentic loop
      while (this.state.iteration < this.config.maxIterations) {
        this.state.iteration++;
        logger.debug(`Agentic loop iteration ${this.state.iteration}`);

        // Check for cancellation
        if (this.abortController.signal.aborted) {
          throw new Error('Operation cancelled');
        }

        // Assemble context for API call
        const context = this.assembleContext();

        // Call backend API
        this.setStatus('thinking');
        callbacks?.onThinking?.();

        const response = await this.callBackend(context);

        // Handle streaming response
        this.setStatus('streaming');
        const { text, toolCalls } = await this.handleResponse(response, callbacks);
        finalResponse = text;

        // If no tool calls, we're done
        if (!toolCalls || toolCalls.length === 0) {
          break;
        }

        // Execute tools
        allToolCalls.push(...toolCalls);
        this.setStatus('tool_execution');

        const toolResults = await this.executeTools(toolCalls, callbacks);

        // Add tool results to context
        this.addToolResults(toolResults);

        // Continue loop for follow-up
      }

      return { response: finalResponse, toolCalls: allToolCalls };
    } catch (error) {
      const err = error instanceof Error ? error : new Error(String(error));
      callbacks?.onError?.(err);
      logger.error('Agentic loop error', err);
      throw error;
    } finally {
      this.setStatus('idle');
      this.state.iteration = 0;
    }
  }

  /**
   * Set the loop status and emit event
   */
  private setStatus(status: LoopStatus): void {
    this.state.status = status;
    this.emit('status', status);
  }

  /**
   * Add a message to the history
   */
  private addMessage(message: Omit<Message, 'id' | 'timestamp'>): void {
    const fullMessage: Message = {
      ...message,
      id: generateId(),
      timestamp: new Date(),
    };
    this.state.messages.push(fullMessage);
    this.emit('message', fullMessage);
  }

  /**
   * Assemble context for API call
   */
  private assembleContext(): APIContext {
    const systemPrompt = this.buildSystemPrompt();
    const messages = this.formatMessagesForAPI();
    const tools = this.services.tools.getToolsForAPI();

    return {
      systemPrompt,
      messages,
      tools,
      maxTokens: this.config.maxTokens,
      temperature: 0.7,
    };
  }

  /**
   * Build the system prompt with environment context
   */
  private buildSystemPrompt(): string {
    const envContext = `
## Environment
- Working Directory: ${process.cwd()}
- Platform: ${process.platform}
- Node Version: ${process.version}
- CLI Version: 1.0.0
- Current Time: ${new Date().toISOString()}
`.trim();

    return `${CORE_SYSTEM_PROMPT}\n\n${envContext}`;
  }

  /**
   * Format messages for API call
   */
  private formatMessagesForAPI(): APIMessage[] {
    return this.state.messages.map((msg) => ({
      role: msg.role === 'tool' ? 'user' : msg.role,
      content: msg.content,
    }));
  }

  /**
   * Call the backend API
   */
  private async callBackend(context: APIContext): Promise<Response> {
    const config = this.services.config.get();
    const serverUrl = config.server.url;

    // Create a timeout promise
    const timeoutPromise = new Promise<never>((_, reject) => {
      setTimeout(
        () => reject(new TimeoutError('API call', this.config.timeoutMs)),
        this.config.timeoutMs
      );
    });

    // Get auth token
    let authToken: string | undefined;
    try {
      authToken = await this.services.auth.getToken();
    } catch {
      logger.warn('Could not get auth token');
    }

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };

    if (authToken) {
      headers['Authorization'] = `Bearer ${authToken}`;
    }

    // Make API call with timeout
    const fetchPromise = fetch(`${serverUrl}/api/v1/chat/completions`, {
      method: 'POST',
      headers,
      body: JSON.stringify({
        messages: [
          { role: 'system', content: context.systemPrompt },
          ...context.messages,
        ],
        tools: context.tools.length > 0 ? context.tools : undefined,
        max_tokens: context.maxTokens,
        temperature: context.temperature,
        stream: true,
      }),
      signal: this.abortController?.signal,
    });

    return Promise.race([fetchPromise, timeoutPromise]);
  }

  /**
   * Handle streaming response from API
   */
  private async handleResponse(
    response: Response,
    callbacks?: AgenticLoopCallbacks
  ): Promise<{ text: string; toolCalls: ToolCall[] }> {
    // For now, handle as non-streaming response
    // TODO: Implement proper SSE streaming

    if (!response.ok) {
      const errorText = await response.text();
      throw new Error(`API error: ${response.status} - ${errorText}`);
    }

    const data = await response.json() as {
      choices?: Array<{
        message?: {
          content?: string;
          tool_calls?: Array<{
            id: string;
            name?: string;
            function?: { name: string; arguments: string };
            arguments?: string;
          }>;
        };
      }>;
    };

    // Extract text content
    let text = '';
    if (data.choices?.[0]?.message?.content) {
      text = data.choices[0].message.content;
    }

    // Add assistant message
    this.addMessage({ role: 'assistant', content: text });

    // Emit streaming chunks for UI
    if (callbacks?.onStream) {
      // Simulate streaming for better UX
      const messageId = this.state.messages[this.state.messages.length - 1].id;
      callbacks.onStream(text, messageId);
    }

    // Extract tool calls
    const toolCalls: ToolCall[] = [];
    if (data.choices?.[0]?.message?.tool_calls) {
      for (const tc of data.choices[0].message.tool_calls) {
        toolCalls.push({
          id: tc.id,
          name: tc.function?.name || tc.name || 'unknown',
          input: JSON.parse(tc.function?.arguments || tc.arguments || '{}'),
        });
      }
    }

    return { text, toolCalls };
  }

  /**
   * Execute tools with permission checks
   */
  private async executeTools(
    toolCalls: ToolCall[],
    callbacks?: AgenticLoopCallbacks
  ): Promise<ToolResult[]> {
    const results: ToolResult[] = [];

    for (const call of toolCalls) {
      callbacks?.onToolStart?.(call);
      this.emit('tool_start', call);

      const startTime = Date.now();

      try {
        // Check permissions
        const hasPermission = await this.checkPermission(call, callbacks);

        if (!hasPermission) {
          const result: ToolResult = {
            id: call.id,
            success: false,
            error: 'Permission denied',
            duration: Date.now() - startTime,
          };
          results.push(result);
          callbacks?.onToolComplete?.(call, result);
          this.emit('tool_denied', call);
          continue;
        }

        // Execute tool
        const output = await this.services.tools.execute(
          call.name,
          call.input,
          {
            api: this.services.api,
            config: this.services.config.get(),
            cwd: process.cwd(),
            abortSignal: this.abortController?.signal,
          }
        );

        const result: ToolResult = {
          id: call.id,
          success: true,
          output,
          duration: Date.now() - startTime,
        };

        results.push(result);
        callbacks?.onToolComplete?.(call, result);
        this.emit('tool_complete', { call, result });
      } catch (error) {
        const errorMessage = error instanceof Error ? error.message : String(error);
        const result: ToolResult = {
          id: call.id,
          success: false,
          error: errorMessage,
          duration: Date.now() - startTime,
        };

        results.push(result);
        callbacks?.onToolComplete?.(call, result);
        this.emit('tool_error', { call, error });
      }
    }

    return results;
  }

  /**
   * Check if a tool call is permitted
   */
  private async checkPermission(
    call: ToolCall,
    callbacks?: AgenticLoopCallbacks
  ): Promise<boolean> {
    // Get tool definition
    const tool = this.services.tools.get(call.name);

    if (!tool) {
      return false;
    }

    // Check if permission is required
    if (tool.requiresPermission === 'none') {
      return true;
    }

    // TODO: Check stored permissions

    // Request permission from user
    if (callbacks?.onPermissionRequest) {
      this.setStatus('awaiting_permission');
      return callbacks.onPermissionRequest(call);
    }

    // Default: deny if no callback
    return false;
  }

  /**
   * Add tool results to message history
   */
  private addToolResults(results: ToolResult[]): void {
    for (const result of results) {
      this.addMessage({
        role: 'tool',
        content: result.success
          ? JSON.stringify(result.output)
          : `Error: ${result.error}`,
        toolResults: [result],
      });
    }
  }
}

/**
 * Create a new agentic loop instance
 */
export function createAgenticLoop(
  services: Services,
  config?: Partial<AgenticLoopConfig>
): AgenticLoop {
  return new AgenticLoop(services, config);
}

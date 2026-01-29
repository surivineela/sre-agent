/**
 * SignalR Streaming Service
 *
 * Real SignalR connection to the SRE Agent backend, matching Agent.Web implementation.
 * Uses WebSockets with LongPolling fallback and automatic reconnection.
 */
import * as signalR from '@microsoft/signalr';
import { EventEmitter } from 'events';
import { logger } from '../utils/logger';
import { getAuthService } from './auth';

/**
 * Streaming message content types (matches Microsoft.Extensions.AI format)
 * Note: Server uses $type as JSON discriminator
 */
export interface StreamingMessageContent {
  // Server uses $type as discriminator
  '$type'?: 'text' | 'functionCall' | null;
  // Also try plain 'type' for fallback
  type?: 'text' | 'tool_call' | 'tool_result';
  // Text content
  text?: string | null;
  Text?: string | null;  // PascalCase variant
  // Function call info
  name?: string | null;
  arguments?: string | null;
  // Additional properties
  additionalProperties?: {
    userDescription?: string | null;
    functionCallDescription?: string | null;
  } | null;
}

/**
 * Streaming message from SignalR (matches Agent.Web format)
 */
export interface StreamingMessage {
  finishReason?: 'stop' | 'tool_calls' | 'length' | null;
  FinishReason?: 'stop' | 'tool_calls' | 'length' | null;  // PascalCase
  authorName?: string | null;
  AuthorName?: string | null;  // PascalCase
  role?: 'user' | 'assistant' | 'tool' | 'system' | null;
  Role?: string | null;  // PascalCase
  contents?: StreamingMessageContent[] | null;
  Contents?: StreamingMessageContent[] | null;  // PascalCase
  createdAt?: string | null;
  CreatedAt?: string | null;  // PascalCase
  additionalProperties?: {
    actionName?: string | null;
    connectionId?: string | null;
    threadId?: string | null;
    messageId?: string | null;
    streamMessageType?: string | null;
    isCancelled?: boolean | null;
    userId?: string;
  } | null;
  AdditionalProperties?: Record<string, unknown> | null;  // PascalCase
}

/**
 * Thread create request
 */
export interface ThreadCreateRequest {
  message: string;
  userId: string;
  displayName: string;
  agentName?: string;
  conversationModifier?: string;
}

/**
 * Message create request
 */
export interface MessageCreateRequest {
  text: string;
  userId: string;
  displayName: string;
  conversationModifier?: string;
  agent?: string;
}

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Enhanced connection state with detailed reconnection info (SPEC-011)
 */
export interface EnhancedConnectionState {
  status: 'connected' | 'connecting' | 'disconnected' | 'reconnecting' | 'auth-required';
  attempt: number;
  maxAttempts: number;
  nextRetryIn: number; // seconds until next retry
  lastError?: string;
  serverUrl?: string;
}

/**
 * SignalR Hub method names (matching Agent.Web)
 */
const HubMethods = {
  CreateThread: 'CreateThread',
  CreateMessage: 'CreateMessage',
  CancelThread: 'CancelThread',
  SubmitUserQuestionResponse: 'SubmitUserQuestionResponse',
} as const;

/**
 * SignalR Hub event names (matching Agent.Web)
 */
const HubEvents = {
  MessageUpdate: 'MessageUpdate',
  ThreadUpdate: 'ThreadUpdate',
  TaskUpdate: 'TaskUpdate',
  TodoPlanUpdate: 'TodoPlanUpdate',
  IncidentUpdate: 'IncidentUpdate',
  Error: 'Error',
  Pong: 'Pong',
} as const;

/**
 * SignalR Streaming Service
 *
 * Provides real-time communication with the SRE Agent backend using SignalR
 */
export class StreamingService extends EventEmitter {
  private serverUrl: string;
  private hubUrl: string;
  private connection: signalR.HubConnection | null = null;
  private connectionState: ConnectionState = 'disconnected';
  private currentThreadId: string | null = null;
  private userId: string;
  private displayName: string;

  // Enhanced connection state (SPEC-011)
  private enhancedState: EnhancedConnectionState = {
    status: 'disconnected',
    attempt: 0,
    maxAttempts: 5,
    nextRetryIn: 0,
  };
  private countdownTimer: NodeJS.Timeout | null = null;
  private manualReconnect: boolean = false;

  // Event names
  static readonly EVENTS = {
    MESSAGE_UPDATE: 'messageUpdate',
    THREAD_UPDATE: 'threadUpdate',
    TASK_UPDATE: 'taskUpdate',
    TODO_PLAN_UPDATE: 'todoPlanUpdate',
    CONNECTION_STATE: 'connectionState',
    ENHANCED_CONNECTION_STATE: 'enhancedConnectionState',
    ERROR: 'error',
  };

  constructor(serverUrl: string, userId?: string, displayName?: string) {
    super();
    this.serverUrl = serverUrl.replace(/\/$/, '');
    this.hubUrl = `${this.serverUrl}/agentHub`;
    this.userId = userId || process.env.USER || process.env.USERNAME || 'cli-user';
    this.displayName = displayName || this.userId;
  }

  /**
   * Get authentication token for SignalR connection
   */
  private async getAuthToken(): Promise<string> {
    try {
      const authService = getAuthService();
      const token = await authService.getToken();
      return token || '';
    } catch (error) {
      logger.debug('Could not get auth token for SignalR', { error });
      return '';
    }
  }

  /**
   * Get current connection state
   */
  get state(): ConnectionState {
    return this.connectionState;
  }

  /**
   * Check if connected
   */
  get isConnected(): boolean {
    return this.connectionState === 'connected';
  }

  /**
   * Set connection state and emit event
   */
  private setConnectionState(state: ConnectionState): void {
    this.connectionState = state;
    this.emit(StreamingService.EVENTS.CONNECTION_STATE, state);
  }

  /**
   * Update enhanced connection state and emit event (SPEC-011)
   */
  private updateEnhancedState(update: Partial<EnhancedConnectionState>): void {
    this.enhancedState = { ...this.enhancedState, ...update };
    this.emit(StreamingService.EVENTS.ENHANCED_CONNECTION_STATE, this.enhancedState);
  }

  /**
   * Get enhanced connection state (SPEC-011)
   */
  getEnhancedState(): EnhancedConnectionState {
    return { ...this.enhancedState };
  }

  /**
   * Handle connection error and schedule reconnection (SPEC-011)
   */
  private handleConnectionError(error: Error): void {
    const isAuthError = error.message.includes('401') ||
                        error.message.includes('Unauthorized') ||
                        error.message.includes('403');

    if (isAuthError) {
      this.updateEnhancedState({
        status: 'auth-required',
        lastError: 'Authentication required or token expired',
      });
      this.setConnectionState('disconnected');
      return;
    }

    this.updateEnhancedState({
      status: 'disconnected',
      lastError: error.message,
    });
    this.setConnectionState('disconnected');

    // Schedule reconnection if not at max attempts
    if (this.enhancedState.attempt < this.enhancedState.maxAttempts) {
      this.scheduleReconnect();
    }
  }

  /**
   * Schedule a reconnection attempt with exponential backoff (SPEC-011)
   */
  private scheduleReconnect(): void {
    if (this.enhancedState.attempt >= this.enhancedState.maxAttempts) {
      this.updateEnhancedState({
        status: 'disconnected',
        lastError: `Failed after ${this.enhancedState.maxAttempts} attempts`,
        nextRetryIn: 0,
      });
      return;
    }

    // Exponential backoff: 2, 4, 8, 16, 32 seconds
    const delay = Math.pow(2, this.enhancedState.attempt + 1);
    this.updateEnhancedState({
      status: 'reconnecting',
      nextRetryIn: delay,
    });

    // Start countdown timer
    this.countdownTimer = setInterval(() => {
      const remaining = this.enhancedState.nextRetryIn - 1;
      if (remaining <= 0) {
        this.clearCountdown();
        this.attemptReconnect();
      } else {
        this.updateEnhancedState({ nextRetryIn: remaining });
      }
    }, 1000);
  }

  /**
   * Clear countdown timer (SPEC-011)
   */
  private clearCountdown(): void {
    if (this.countdownTimer) {
      clearInterval(this.countdownTimer);
      this.countdownTimer = null;
    }
  }

  /**
   * Attempt reconnection (SPEC-011)
   */
  private async attemptReconnect(): Promise<void> {
    this.updateEnhancedState({
      status: 'connecting',
      attempt: this.enhancedState.attempt + 1,
      nextRetryIn: 0,
    });
    this.setConnectionState('connecting');

    try {
      await this.connect();
      this.updateEnhancedState({
        status: 'connected',
        attempt: 0,
        nextRetryIn: 0,
        lastError: undefined,
      });
    } catch (error) {
      const err = error instanceof Error ? error : new Error(String(error));
      this.handleConnectionError(err);
    }
  }

  /**
   * Retry connection immediately (SPEC-011)
   */
  retryNow(): void {
    this.clearCountdown();
    this.manualReconnect = true;
    this.attemptReconnect();
  }

  /**
   * Cancel reconnection attempts (SPEC-011)
   */
  cancelReconnect(): void {
    this.clearCountdown();
    this.updateEnhancedState({
      status: 'disconnected',
      lastError: 'Reconnection cancelled',
      nextRetryIn: 0,
    });
    this.setConnectionState('disconnected');
  }

  /**
   * Connect to the SignalR hub
   */
  async connect(): Promise<void> {
    if (this.connectionState === 'connected' && this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    // Clean up existing connection if any
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch {
        // Ignore stop errors
      }
      this.connection = null;
    }

    this.setConnectionState('connecting');
    this.updateEnhancedState({
      status: 'connecting',
      serverUrl: this.serverUrl,
      attempt: this.manualReconnect ? this.enhancedState.attempt : 1,
    });
    this.manualReconnect = false;

    try {
      // Build SignalR connection (matching Agent.Web implementation)
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(this.hubUrl, {
          accessTokenFactory: () => this.getAuthToken(),
          transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling,
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Exponential backoff: 0, 2s, 4s, 8s, 16s, 30s max
            if (retryContext.previousRetryCount >= 10) {
              return null; // Stop retrying after 10 attempts
            }
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          },
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      // Set up event handlers
      this.setupEventHandlers();

      // Start connection
      await this.connection.start();

      this.setConnectionState('connected');
      this.updateEnhancedState({
        status: 'connected',
        attempt: 0,
        nextRetryIn: 0,
        lastError: undefined,
      });
      logger.info('Connected to SignalR hub', { hubUrl: this.hubUrl });
    } catch (error) {
      this.setConnectionState('disconnected');
      const message = error instanceof Error ? error.message : String(error);
      logger.error('Failed to connect to SignalR hub', { error: message });
      this.emit(StreamingService.EVENTS.ERROR, { type: 'connection', message });
      // Update enhanced state with error
      this.updateEnhancedState({
        status: 'disconnected',
        lastError: message,
      });
      throw error;
    }
  }

  /**
   * Set up SignalR event handlers
   */
  private setupEventHandlers(): void {
    if (!this.connection) return;

    // Connection lifecycle events
    this.connection.onreconnecting((error) => {
      this.setConnectionState('reconnecting');
      this.updateEnhancedState({
        status: 'reconnecting',
        attempt: this.enhancedState.attempt + 1,
        lastError: error?.message,
      });
      logger.warn('SignalR reconnecting', { error: error?.message });
    });

    this.connection.onreconnected((connectionId) => {
      this.setConnectionState('connected');
      this.updateEnhancedState({
        status: 'connected',
        attempt: 0,
        nextRetryIn: 0,
        lastError: undefined,
      });
      logger.info('SignalR reconnected', { connectionId });
    });

    this.connection.onclose((error) => {
      this.setConnectionState('disconnected');
      this.updateEnhancedState({
        status: 'disconnected',
        lastError: error?.message || 'Connection closed',
      });
      logger.warn('SignalR connection closed', { error: error?.message });
    });

    // Message events (matching Agent.Web)
    this.connection.on(HubEvents.MessageUpdate, (message: StreamingMessage) => {
      logger.debug('MessageUpdate received', { message });
      this.emit(StreamingService.EVENTS.MESSAGE_UPDATE, {
        threadId: this.currentThreadId,
        message,
      });
    });

    this.connection.on(HubEvents.ThreadUpdate, (update: unknown) => {
      logger.debug('ThreadUpdate received', { update });
      this.emit(StreamingService.EVENTS.THREAD_UPDATE, update);
    });

    this.connection.on(HubEvents.TaskUpdate, (update: unknown) => {
      logger.debug('TaskUpdate received', { update });
      this.emit(StreamingService.EVENTS.TASK_UPDATE, update);
    });

    this.connection.on(HubEvents.TodoPlanUpdate, (update: unknown) => {
      logger.debug('TodoPlanUpdate received', { update });
      this.emit(StreamingService.EVENTS.TODO_PLAN_UPDATE, update);
    });

    // Error event from server
    this.connection.on(HubEvents.Error, (error: unknown) => {
      logger.error('Server Error received', { error });
      this.emit(StreamingService.EVENTS.ERROR, {
        type: 'server',
        message: JSON.stringify(error),
        error,
      });
    });

    // Pong event for connection testing
    this.connection.on(HubEvents.Pong, (timestamp: unknown) => {
      logger.debug('Pong received', { timestamp });
    });

    // IncidentUpdate event (suppress warning, log for debugging)
    this.connection.on(HubEvents.IncidentUpdate, (update: unknown) => {
      logger.debug('IncidentUpdate received', { update });
    });
  }

  /**
   * Disconnect from the hub
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop();
      } catch (error) {
        logger.warn('Error stopping SignalR connection', { error });
      }
      this.connection = null;
    }
    this.setConnectionState('disconnected');
    this.currentThreadId = null;
    logger.info('Disconnected from SignalR hub');
  }

  /**
   * Create a new thread and start streaming
   */
  async createThread(request: ThreadCreateRequest): Promise<string> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }

    const threadId = crypto.randomUUID();
    this.currentThreadId = threadId;

    logger.debug('Creating thread via SignalR', { threadId, message: request.message, agentName: request.agentName });

    // Match Agent.Web's ThreadCreateRequest format exactly (camelCase)
    const threadCreateRequest: {
      startMessage: {
        text: string;
        userId: string;
        displayName: string;
        agent?: string;
        conversationModifier?: string;
      };
      startingAgent?: string;
    } = {
      startMessage: {
        text: request.message,
        userId: request.userId || this.userId,
        displayName: request.displayName || this.displayName,
      },
    };

    // Only add agent if specified
    if (request.agentName) {
      threadCreateRequest.startMessage.agent = request.agentName;
    }

    // Add conversation modifier if specified
    if (request.conversationModifier) {
      threadCreateRequest.startMessage.conversationModifier = request.conversationModifier;
    }

    try {
      await this.connection!.invoke(
        HubMethods.CreateThread,
        threadId,
        threadCreateRequest,
        false // textOnly
      );

      logger.debug('Thread created', { threadId });
      return threadId;
    } catch (error) {
      const errorMsg = error instanceof Error ? error.message : String(error);
      logger.error('Failed to create thread via SignalR', { error: errorMsg, threadId });
      throw error;
    }
  }

  /**
   * Send a message to an existing thread
   */
  async sendMessage(threadId: string, request: MessageCreateRequest): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }

    this.currentThreadId = threadId;

    const messageCreateRequest = {
      text: request.text,
      userId: request.userId || this.userId,
      displayName: request.displayName || this.displayName,
      agent: request.agent,
    };

    try {
      await this.connection!.invoke(
        HubMethods.CreateMessage,
        threadId,
        messageCreateRequest,
        false // skipQueuing
      );

      logger.debug('Message sent', { threadId });
    } catch (error) {
      logger.error('Failed to send message via SignalR', { error });
      throw error;
    }
  }

  /**
   * Cancel streaming for a thread
   */
  async cancelThread(threadId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    try {
      await this.connection.invoke(HubMethods.CancelThread, threadId);
      logger.debug('Thread cancelled', { threadId });
    } catch (error) {
      logger.warn('Failed to cancel thread', { threadId, error });
    }
  }

  /**
   * Submit a response to a user question
   */
  async submitUserQuestionResponse(
    threadId: string,
    questionId: string,
    response: { answer: string; values?: Record<string, unknown> }
  ): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      await this.connect();
    }

    try {
      await this.connection!.invoke(
        HubMethods.SubmitUserQuestionResponse,
        threadId,
        questionId,
        response
      );
      logger.debug('Question response submitted', { threadId, questionId });
    } catch (error) {
      logger.error('Failed to submit question response', { error });
      throw error;
    }
  }

  /**
   * Approve or deny a tool call
   * This sends the approval decision back to the server
   */
  async approveToolCall(threadId: string, toolCallId: string, approved: boolean): Promise<void> {
    // Use the respondToApproval method for now
    // The server should handle tool approval through the approval endpoint
    try {
      await this.respondToApproval(threadId, toolCallId, approved, this.displayName);
      logger.debug('Tool call approval sent', { threadId, toolCallId, approved });
    } catch (error) {
      // If the approval endpoint doesn't exist, try submitting as a question response
      logger.debug('Falling back to question response for tool approval', { error });
      try {
        await this.submitUserQuestionResponse(threadId, toolCallId, {
          answer: approved ? 'approved' : 'denied',
          values: { approved },
        });
      } catch (fallbackError) {
        logger.warn('Could not send tool approval to server', { fallbackError });
        // Don't throw - the UI should still continue
      }
    }
  }

  /**
   * Get messages from a thread (REST fallback)
   */
  async getMessages(
    threadId: string,
    options?: {
      skip?: number;
      top?: number;
      descending?: boolean;
    }
  ): Promise<StreamingMessage[]> {
    const params = new URLSearchParams();
    if (options?.skip) params.set('$skip', String(options.skip));
    if (options?.top) params.set('$top', String(options.top));
    if (options?.descending) params.set('$orderby', 'timestamp desc');

    const url = `${this.serverUrl}/api/v1/threads/${threadId}/messages?${params}`;

    const token = await this.getAuthToken();
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      throw new Error(`Failed to get messages: ${response.status}`);
    }

    const data = await response.json();
    const messages = Array.isArray(data)
      ? data
      : data.value || data.messages || data.items || data.results || [];
    return messages;
  }

  /**
   * Get thread status (REST fallback)
   */
  async getThreadStatus(threadId: string): Promise<{
    id: string;
    status: string;
    isComplete: boolean;
  }> {
    const token = await this.getAuthToken();
    const headers: Record<string, string> = { Accept: 'application/json' };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const response = await fetch(`${this.serverUrl}/api/v1/threads/${threadId}`, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      throw new Error(`Failed to get thread: ${response.status}`);
    }

    const thread = await response.json();
    const statusValue = thread.status || thread.Status || thread.state || thread.State || 'unknown';
    const statusLower = statusValue.toLowerCase();

    const completeStates = ['completed', 'complete', 'done', 'finished', 'failed', 'cancelled', 'canceled', 'error'];
    const isComplete = completeStates.some(s => statusLower.includes(s));

    return {
      id: thread.id || thread.Id || thread.threadId || thread.ThreadId || threadId,
      status: statusValue,
      isComplete,
    };
  }

  /**
   * Approve or deny an approval request (REST)
   */
  async respondToApproval(
    threadId: string,
    approvalId: string,
    approved: boolean,
    user?: string
  ): Promise<void> {
    const endpoint = approved ? 'approve' : 'cancel';
    const token = await this.getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const res = await fetch(
      `${this.serverUrl}/api/v1/approvals/${approvalId}/${endpoint}`,
      {
        method: 'POST',
        headers,
        body: JSON.stringify({
          user: user || this.displayName,
        }),
      }
    );

    if (!res.ok) {
      throw new Error(`Failed to ${endpoint} approval: ${res.status}`);
    }
  }

  /**
   * Post execution action for azcli/kubectl/psql (REST)
   * Matches Agent.Web's ThreadClient.postExecutionAction
   */
  async postExecutionAction(
    basePath: 'azCliExecution' | 'kubectlExecution' | 'psqlExecution',
    threadId: string,
    executionId: string,
    action: 'run' | 'cancel',
    user?: string
  ): Promise<void> {
    const token = await this.getAuthToken();
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'application/json',
    };
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    const url = `${this.serverUrl}/api/v1/${basePath}/${threadId}/${executionId}/action`;
    const body = {
      action,
      user: user || this.displayName,
    };

    logger.info('Posting execution action', { url, basePath, threadId, executionId, action, user: body.user });

    const res = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      logger.error('Execution action failed', { status: res.status, statusText: res.statusText, errorText });
      throw new Error(`Failed to ${action} execution: ${res.status} ${res.statusText} - ${errorText}`);
    }

    const result = await res.json().catch(() => null);
    logger.info('Execution action successful', { result });
  }
}

/**
 * Create streaming service instance
 */
export function createStreamingService(
  serverUrl: string,
  userId?: string,
  displayName?: string
): StreamingService {
  return new StreamingService(serverUrl, userId, displayName);
}

export default StreamingService;

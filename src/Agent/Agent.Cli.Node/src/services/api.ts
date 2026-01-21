/**
 * API service for communicating with the SRE Agent backend
 */
import { EventEmitter } from 'events';
import type {
  APIServiceInterface,
  Agent,
  AgentSpec,
  Tool,
  Thread,
  ThreadMessage,
  Skill,
  ScheduledTask,
  ScheduledTaskSpec,
  SmartAgentResult,
  ConnectionStatus,
} from '../types';
import type { AuthServiceInterface } from '../types';
import { APIError, ConnectionError, TimeoutError } from '../utils/errors';
import { logger } from '../utils/logger';

interface APIConfig {
  baseUrl: string;
  timeout: number;
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  body?: unknown;
  headers?: Record<string, string>;
  timeout?: number;
}

export class APIService extends EventEmitter implements APIServiceInterface {
  private config: APIConfig;
  private authService: AuthServiceInterface;
  private _connectionStatus: ConnectionStatus = 'disconnected';

  constructor(config: APIConfig, authService: AuthServiceInterface) {
    super();
    this.config = config;
    this.authService = authService;
  }

  get connectionStatus(): ConnectionStatus {
    return this._connectionStatus;
  }

  private setConnectionStatus(status: ConnectionStatus): void {
    this._connectionStatus = status;
    this.emit('status', status);
  }

  /**
   * Connect to the API server
   */
  async connect(): Promise<void> {
    this.setConnectionStatus('connecting');

    try {
      // Test connection
      const result = await this.testConnection();

      if (result.success) {
        this.setConnectionStatus('connected');
        logger.info('Connected to API server', { url: this.config.baseUrl });
      } else {
        this.setConnectionStatus('disconnected');
        throw new ConnectionError(`Failed to connect: ${result.message}`);
      }
    } catch (err) {
      this.setConnectionStatus('disconnected');
      throw err;
    }
  }

  /**
   * Test the API connection
   */
  async testConnection(): Promise<{ success: boolean; message: string }> {
    try {
      // Use agents endpoint which is known to exist (health endpoint may not)
      await this.request('/api/v2/extendedAgent/agents', { timeout: 5000 });
      return { success: true, message: 'Connected' };
    } catch (err) {
      return {
        success: false,
        message: err instanceof Error ? err.message : 'Unknown error',
      };
    }
  }

  /**
   * Get agent metadata (name, subscription, resource group)
   */
  async getAgentMetadata(): Promise<{
    name: string;
    subscriptionId: string;
    resourceGroup: string;
  } | null> {
    try {
      const response = await this.request<{
        name: string;
        subscriptionId: string;
        resourceGroup: string;
      }>('/api/v1/metadata', { timeout: 5000 });
      return response;
    } catch (err) {
      logger.warn('Could not fetch agent metadata', err);
      return null;
    }
  }

  /**
   * Fetch App Insights App ID from ARM resource
   * Uses the agent metadata to construct the ARM resource ID and fetch it directly from Azure
   */
  async getAppInsightsAppIdFromArm(): Promise<string | null> {
    try {
      // First get the agent metadata to know the resource location
      const metadata = await this.getAgentMetadata();
      if (!metadata) {
        logger.warn('Could not get agent metadata for ARM query');
        return null;
      }

      const { name, subscriptionId, resourceGroup } = metadata;

      // Construct ARM resource ID
      const resourceId = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.App/agents/${name}`;

      // Get token for ARM API
      let authToken: string | undefined;
      try {
        authToken = await this.authService.getToken();
      } catch (err) {
        logger.warn('Could not get auth token for ARM query', err);
        return null;
      }

      if (!authToken) {
        logger.warn('No auth token available for ARM query');
        return null;
      }

      // Call ARM API directly
      const armUrl = `https://management.azure.com${resourceId}?api-version=2025-05-01-preview`;
      const response = await fetch(armUrl, {
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${authToken}`,
          'Content-Type': 'application/json',
        },
      });

      if (!response.ok) {
        logger.warn(`ARM API returned ${response.status}: ${response.statusText}`);
        return null;
      }

      const armResource = await response.json() as {
        properties?: {
          logConfiguration?: {
            applicationInsightsConfiguration?: {
              appId?: string;
            };
          };
        };
      };

      const appId = armResource?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId;
      if (appId) {
        logger.info('Successfully fetched App Insights App ID from ARM', { appId });
        return appId;
      }

      logger.warn('ARM resource does not have App Insights configuration');
      return null;
    } catch (err) {
      logger.warn('Could not fetch App Insights App ID from ARM', err);
      return null;
    }
  }

  /**
   * Make an API request
   */
  private async request<T>(path: string, options: RequestOptions = {}): Promise<T> {
    const { method = 'GET', body, headers = {}, timeout = this.config.timeout } = options;

    const url = `${this.config.baseUrl}${path}`;
    const startTime = Date.now();

    // Get auth token
    let authToken: string | undefined;
    try {
      authToken = await this.authService.getToken();
    } catch (err) {
      logger.warn('Could not get auth token', err);
    }

    const requestHeaders: Record<string, string> = {
      'Content-Type': 'application/json',
      Accept: 'application/json',
      ...headers,
    };

    if (authToken) {
      requestHeaders['Authorization'] = `Bearer ${authToken}`;
    }

    logger.apiRequest(method, url, body);

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeout);

    try {
      const response = await fetch(url, {
        method,
        headers: requestHeaders,
        body: body ? JSON.stringify(body) : undefined,
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      const duration = Date.now() - startTime;
      logger.apiResponse(method, url, response.status, duration);

      if (!response.ok) {
        const errorBody = await response.text();
        let errorMessage = `API error: ${response.status} ${response.statusText}`;

        try {
          const errorJson = JSON.parse(errorBody);
          errorMessage = errorJson.message || errorJson.error || errorMessage;
        } catch {
          // Use status text
        }

        throw new APIError(errorMessage, response.status);
      }

      // Handle empty responses
      const text = await response.text();
      if (!text) {
        return undefined as T;
      }

      return JSON.parse(text);
    } catch (err) {
      clearTimeout(timeoutId);

      if (err instanceof APIError) {
        throw err;
      }

      if (err instanceof Error && err.name === 'AbortError') {
        throw new TimeoutError(`API request to ${path}`, timeout);
      }

      throw new ConnectionError(`Failed to connect to ${url}: ${err}`);
    }
  }

  // ============================================================================
  // Agent Management
  // ============================================================================

  async createAgent(spec: AgentSpec): Promise<Agent> {
    return this.request<Agent>('/api/v2/agents', {
      method: 'POST',
      body: spec,
    });
  }

  async listAgents(): Promise<Agent[]> {
    return this.request<Agent[]>('/api/v2/agents');
  }

  async getAgent(name: string): Promise<Agent> {
    return this.request<Agent>(`/api/v2/agents/${encodeURIComponent(name)}`);
  }

  async deleteAgent(name: string): Promise<void> {
    await this.request(`/api/v2/agents/${encodeURIComponent(name)}`, {
      method: 'DELETE',
    });
  }

  async updateAgent(name: string, spec: Partial<AgentSpec>): Promise<Agent> {
    return this.request<Agent>(`/api/v2/agents/${encodeURIComponent(name)}`, {
      method: 'PUT',
      body: spec,
    });
  }

  async generateSmartAgent(name: string, instructions?: string): Promise<SmartAgentResult> {
    return this.request<SmartAgentResult>('/api/v1/incidentplayground/generateInstructions', {
      method: 'POST',
      body: {
        agentName: name,
        userInstructions: instructions,
      },
    });
  }

  // ============================================================================
  // Tool Management
  // ============================================================================

  async listTools(): Promise<Tool[]> {
    return this.request<Tool[]>('/api/v2/tools');
  }

  async getTool(name: string): Promise<Tool> {
    return this.request<Tool>(`/api/v2/tools/${encodeURIComponent(name)}`);
  }

  async createTool(spec: Partial<Tool>): Promise<Tool> {
    return this.request<Tool>('/api/v2/tools', {
      method: 'POST',
      body: spec,
    });
  }

  async deleteTool(name: string): Promise<void> {
    await this.request(`/api/v2/tools/${encodeURIComponent(name)}`, {
      method: 'DELETE',
    });
  }

  // ============================================================================
  // Thread/Conversation Management
  // ============================================================================

  async createThread(agentName: string, message: string): Promise<Thread> {
    return this.request<Thread>('/api/v1/threads', {
      method: 'POST',
      body: {
        agentName,
        message,
        userId: await this.getCurrentUserId(),
        displayName: await this.getCurrentUserName(),
      },
    });
  }

  async sendMessage(threadId: string, message: string): Promise<ThreadMessage> {
    return this.request<ThreadMessage>(`/api/v1/threads/${encodeURIComponent(threadId)}/messages`, {
      method: 'POST',
      body: {
        message,
        userId: await this.getCurrentUserId(),
        displayName: await this.getCurrentUserName(),
      },
    });
  }

  async getThread(threadId: string): Promise<Thread> {
    return this.request<Thread>(`/api/v1/threads/${encodeURIComponent(threadId)}`);
  }

  async listThreadMessages(threadId: string): Promise<ThreadMessage[]> {
    return this.request<ThreadMessage[]>(`/api/v1/threads/${encodeURIComponent(threadId)}/messages`);
  }

  async trackThread(threadId: string, maxWaitSeconds = 60): Promise<ThreadMessage[]> {
    const messages: ThreadMessage[] = [];
    const endTime = Date.now() + maxWaitSeconds * 1000;

    while (Date.now() < endTime) {
      const thread = await this.getThread(threadId);

      if (thread.status === 'completed' || thread.status === 'failed') {
        return this.listThreadMessages(threadId);
      }

      // Poll every 2 seconds
      await new Promise((resolve) => setTimeout(resolve, 2000));
    }

    return messages;
  }

  async *streamThreadMessages(threadId: string): AsyncGenerator<ThreadMessage> {
    // Note: This is a placeholder for SSE streaming
    // In production, this would use EventSource
    const messages = await this.listThreadMessages(threadId);
    for (const message of messages) {
      yield message;
    }
  }

  // ============================================================================
  // Skill Management
  // ============================================================================

  async listSkills(): Promise<Skill[]> {
    return this.request<Skill[]>('/api/v2/skills');
  }

  async getSkill(name: string): Promise<Skill> {
    return this.request<Skill>(`/api/v2/skills/${encodeURIComponent(name)}`);
  }

  async convertAgentToSkill(agentName: string): Promise<Skill> {
    return this.request<Skill>(`/api/v2/agents/${encodeURIComponent(agentName)}/convert-to-skill`, {
      method: 'POST',
    });
  }

  // ============================================================================
  // Scheduled Tasks
  // ============================================================================

  async listScheduledTasks(): Promise<ScheduledTask[]> {
    return this.request<ScheduledTask[]>('/api/v1/scheduledtasks');
  }

  async createScheduledTask(task: ScheduledTaskSpec): Promise<ScheduledTask> {
    return this.request<ScheduledTask>('/api/v1/scheduledtasks', {
      method: 'POST',
      body: task,
    });
  }

  async deleteScheduledTask(id: string): Promise<void> {
    await this.request(`/api/v1/scheduledtasks/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
  }

  // ============================================================================
  // Helper Methods
  // ============================================================================

  private async getCurrentUserId(): Promise<string> {
    // In a real implementation, this would extract from the token
    return process.env.USER || process.env.USERNAME || 'cli-user';
  }

  private async getCurrentUserName(): Promise<string> {
    return process.env.USER || process.env.USERNAME || 'CLI User';
  }

  /**
   * Update the base URL
   */
  setBaseUrl(url: string): void {
    this.config.baseUrl = url;
    this.setConnectionStatus('disconnected');
  }
}

// Factory function
export function createAPIService(
  config: APIConfig,
  authService: AuthServiceInterface
): APIService {
  return new APIService(config, authService);
}

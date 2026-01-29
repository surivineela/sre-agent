/**
 * Trace Service - Fetches trace data from App Insights or backend API
 * Based on Agent.Web client's useTracePanel implementation
 */

import type { ThreadEventLog, ISpan, EventName } from '../types/trace';
import { generateSpans } from './traceParser';
import { getAuthService } from './auth';
import { logger } from '../utils/logger';

/**
 * Maximum number of spans to process to prevent memory exhaustion
 * CLI has limited memory compared to browser, so keep this conservative
 */
const MAX_SPANS = 500;

/**
 * App Insights query for thread traces
 */
export function getThreadTracesDataQuery(threadId: string): string {
  return `
    let threadId = '${threadId}';
    customEvents
    | where customDimensions contains threadId
    | extend
        ThreadId = coalesce(tostring(customDimensions.ThreadId), tostring(customDimensions.ChatThreadId)),
        AgentName = coalesce(tostring(customDimensions.AgentName), tostring(customDimensions.SubAgentName)),
        EventType = tostring(customDimensions.EventType),
        TaskType = tostring(customDimensions.TaskType),
        Message = tostring(customDimensions.Message),
        FromAgent = tostring(customDimensions.FromAgent),
        ToAgent = tostring(customDimensions.ToAgent),
        ToolName = tostring(customDimensions.ToolName),
        ToolDescription = tostring(customDimensions.ToolDescription),
        ToolInput = tostring(customDimensions.ToolInput),
        ToolOutput = tostring(customDimensions.ToolOutput),
        UserId = tostring(customDimensions.UserId),
        DisplayName = tostring(customDimensions.DisplayName),
        ModelId = tostring(customDimensions.ModelId),
        Temperature = tostring(customDimensions.Temperature),
        InputTokens = tostring(customDimensions.InputTokens),
        ModelInput = tostring(customDimensions.ModelInput),
        OutputTokens = tostring(customDimensions.OutputTokens),
        ModelOutput = tostring(customDimensions.ModelOutput),
        SystemPrompt = tostring(customDimensions.SystemPrompt),
        ModelThinking = tostring(customDimensions.ModelThinking),
        Reasoning = tostring(customDimensions.Reasoning),
        Response = tostring(customDimensions.Response),
        Result = tostring(customDimensions.Result),
        HandoffReasoning = tostring(customDimensions.HandoffReasoning),
        SpanId = tostring(customDimensions.SpanId),
        ParentSpanId = tostring(customDimensions.ParentSpanId),
        TraceId = tostring(customDimensions.TraceId)
    | extend
        EventName = iff(name == 'MetaAgent', iff(isempty(Message), 'Incident', 'UserMessage'), name)
    | project
        timestamp,
        Event = bag_pack(
            'eventName', EventName,
            'threadId', ThreadId,
            'agentName', AgentName,
            'eventType', EventType,
            'taskType', TaskType,
            'message', Message,
            'fromAgent', FromAgent,
            'toAgent', ToAgent,
            'toolName', ToolName,
            'toolDescription', ToolDescription,
            'toolInput', ToolInput,
            'toolOutput', ToolOutput,
            'userId', UserId,
            'displayName', DisplayName,
            'modelId', ModelId,
            'temperature', Temperature,
            'inputTokens', InputTokens,
            'modelInput', ModelInput,
            'outputTokens', OutputTokens,
            'modelOutput', ModelOutput,
            'systemPrompt', SystemPrompt,
            'modelThinking', ModelThinking,
            'reasoning', Reasoning,
            'response', Response,
            'result', Result,
            'handoffReasoning', HandoffReasoning,
            'spanId', SpanId,
            'parentSpanId', ParentSpanId,
            'traceId', TraceId
        )
    | order by timestamp asc
    `;
}

export interface FetchTracesResult {
  spans: ISpan[];
  events: ThreadEventLog[];
  parseWarnings: string[];
  parseErrors: string[];
  error?: string;
}

/**
 * Trace service for fetching and parsing trace data
 */
export class TraceService {
  private baseUrl: string;
  private appInsightsAppId?: string;

  constructor(baseUrl: string, appInsightsAppId?: string) {
    this.baseUrl = baseUrl;
    this.appInsightsAppId = appInsightsAppId;
  }

  /**
   * Set the App Insights App ID for direct queries
   */
  setAppInsightsAppId(appId: string): void {
    this.appInsightsAppId = appId;
  }

  /**
   * Fetch traces for a thread
   * Tries App Insights first if configured, then falls back to backend API
   */
  async fetchTraces(threadId: string, isIncidentThread = false): Promise<FetchTracesResult> {
    // Try App Insights direct query first
    if (this.appInsightsAppId) {
      try {
        return await this.fetchFromAppInsights(threadId, isIncidentThread);
      } catch (err) {
        logger.warn('App Insights query failed, trying backend API', err);
      }
    }

    // Try backend trace API
    try {
      return await this.fetchFromBackendApi(threadId, isIncidentThread);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : String(err);
      return {
        spans: [],
        events: [],
        parseWarnings: [],
        parseErrors: [],
        error: `Failed to fetch traces: ${errorMessage}`,
      };
    }
  }

  /**
   * Fetch traces directly from App Insights REST API
   */
  private async fetchFromAppInsights(threadId: string, isIncidentThread: boolean): Promise<FetchTracesResult> {
    const authService = getAuthService();
    const token = await authService.getToken();

    if (!token) {
      throw new Error('No auth token available for App Insights');
    }

    const query = getThreadTracesDataQuery(threadId);
    const url = `https://api.applicationinsights.io/v1/apps/${this.appInsightsAppId}/query`;

    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`,
      },
      body: JSON.stringify({ query }),
    });

    if (!response.ok) {
      throw new Error(`App Insights query failed: ${response.status} ${response.statusText}`);
    }

    const result = await response.json() as {
      tables: Array<{ columns: Array<{ name: string }>; rows: unknown[][] }>;
    };

    const rows = result.tables?.[0]?.rows ?? [];

    // Limit rows to prevent memory issues
    const limitedRows = rows.slice(0, MAX_SPANS);
    if (rows.length > MAX_SPANS) {
      logger.warn(`App Insights returned ${rows.length} events, truncating to ${MAX_SPANS} to prevent memory issues`);
    }

    const events = this.parseQueryResults(limitedRows);
    const { spans, parseWarnings, parseErrors } = generateSpans(events);

    // Debug: Log span kinds to help diagnose missing user messages
    const spanKindCounts: Record<string, number> = {};
    for (const span of spans) {
      spanKindCounts[span.kind] = (spanKindCounts[span.kind] || 0) + 1;
    }
    logger.debug('Trace spans by kind from App Insights:', spanKindCounts);

    // Mark first span as Incident if this is an incident thread
    if (isIncidentThread && spans.length && spans[0].kind !== 'Incident') {
      spans[0].kind = 'Incident';
    }

    return { spans, events, parseWarnings, parseErrors };
  }

  /**
   * Fetch traces from backend trace API
   */
  private async fetchFromBackendApi(threadId: string, isIncidentThread: boolean): Promise<FetchTracesResult> {
    const authService = getAuthService();
    const token = await authService.getToken();

    const url = `${this.baseUrl}/api/trace/fetch`;

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify({ threadId, maxSpans: MAX_SPANS }),
    });

    if (!response.ok) {
      // Try alternate endpoint for OpenTelemetry activities
      return this.fetchFromActivitiesApi(threadId, isIncidentThread);
    }

    const result = await response.json() as Array<{
      traceId: string;
      totalDuration: number;
      spanCount: number;
      metadata: Record<string, unknown>;
      spans: Array<{
        spanId: string;
        parentSpanId?: string;
        operationName: string;
        duration: number;
        status: string;
        attributes: Record<string, unknown>;
        events: Array<{ name: string; timestamp: string; attributes: Record<string, unknown> }>;
      }>;
    }>;

    // Convert backend format to ISpan format (limit to prevent memory issues)
    const spans: ISpan[] = [];
    let totalSpanCount = 0;
    outer: for (const trace of result) {
      for (const span of trace.spans) {
        if (totalSpanCount >= MAX_SPANS) {
          logger.warn(`Trace has more than ${MAX_SPANS} spans, truncating to prevent memory issues`);
          break outer;
        }
        spans.push(this.convertBackendSpanToISpan(span));
        totalSpanCount++;
      }
    }

    // Post-process to establish proper hierarchy (mimics Web client traceParser)
    this.establishSpanHierarchy(spans);

    // Mark first span as Incident if this is an incident thread
    if (isIncidentThread && spans.length && spans[0].kind !== 'Incident') {
      spans[0].kind = 'Incident';
    }

    return { spans, events: [], parseWarnings: [], parseErrors: [] };
  }

  /**
   * Try to fetch from activities/OpenTelemetry endpoint
   */
  private async fetchFromActivitiesApi(threadId: string, isIncidentThread: boolean): Promise<FetchTracesResult> {
    const authService = getAuthService();
    const token = await authService.getToken();

    // Try the thread messages endpoint to get some data
    const url = `${this.baseUrl}/api/v1/threads/${encodeURIComponent(threadId)}/messages`;

    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
    };
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(url, {
      method: 'GET',
      headers,
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch thread messages: ${response.status}`);
    }

    const messages = await response.json() as Array<{
      id: string;
      role: string;
      content: string;
      createdAt: string;
      metadata?: Record<string, unknown>;
    }>;

    // Convert messages to basic spans for display
    logger.debug('Thread messages from API:', { count: messages.length, roles: messages.map(m => m.role) });

    const events: ThreadEventLog[] = messages.map((msg, index) => ({
      timeStamp: new Date(msg.createdAt),
      eventName: (msg.role === 'user' ? 'UserMessage' : 'AgentResponse') as EventName,
      threadId,
      message: msg.content,
      displayName: msg.role === 'user' ? 'User' : 'Agent',
    }));

    const { spans, parseWarnings, parseErrors } = generateSpans(events);

    // Debug: Log span kinds
    const spanKindCounts: Record<string, number> = {};
    for (const span of spans) {
      spanKindCounts[span.kind] = (spanKindCounts[span.kind] || 0) + 1;
    }
    logger.debug('Trace spans by kind from thread messages:', spanKindCounts);

    if (isIncidentThread && spans.length && spans[0].kind !== 'Incident') {
      spans[0].kind = 'Incident';
    }

    return { spans, events, parseWarnings, parseErrors };
  }

  /**
   * Parse App Insights query results into ThreadEventLog array
   */
  private parseQueryResults(rows: unknown[][]): ThreadEventLog[] {
    return rows.map(row => {
      const timeStamp = new Date(row[0] as string);
      const eventObj = JSON.parse(row[1] as string) as Omit<
        ThreadEventLog,
        'timeStamp' | 'modelInput' | 'modelOutput' | 'message'
      > & { modelInput?: string; modelOutput?: string; message?: string };

      const { modelInput, modelOutput, message, ...rest } = eventObj;

      let modelInputObj: ThreadEventLog['modelInput'] = undefined;
      let modelOutputObj: ThreadEventLog['modelOutput'] = undefined;

      try {
        modelInputObj = modelInput ? JSON.parse(modelInput) : undefined;
      } catch {
        modelInputObj = modelInput ? [{ role: 'unknown', contentLength: modelInput.length, contentPreview: modelInput }] : undefined;
      }

      try {
        modelOutputObj = modelOutput ? JSON.parse(modelOutput) : undefined;
      } catch {
        modelOutputObj = modelOutput ? [{ role: 'unknown', contentLength: modelOutput.length, contentPreview: modelOutput }] : undefined;
      }

      return {
        ...rest,
        modelInput: modelInputObj,
        modelOutput: modelOutputObj,
        message,
        timeStamp,
      } as ThreadEventLog;
    });
  }

  /**
   * Convert backend span format to ISpan
   */
  private convertBackendSpanToISpan(span: {
    spanId: string;
    parentSpanId?: string;
    operationName: string;
    duration: number;
    status: string;
    attributes: Record<string, unknown>;
    events: Array<{ name: string; timestamp: string; attributes: Record<string, unknown> }>;
  }): ISpan {
    const attrs = span.attributes;
    const kind = this.inferSpanKind(span.operationName, attrs);

    // Extract tool input/output - try multiple attribute name patterns
    const toolInput = (attrs['tool.input'] || attrs['toolInput'] || attrs['ToolInput'] || attrs['tool_input']) as string | undefined;
    const toolOutput = (attrs['tool.output'] || attrs['toolOutput'] || attrs['ToolOutput'] || attrs['tool_output']) as string | undefined;
    const toolDescription = (attrs['tool.description'] || attrs['toolDescription'] || attrs['ToolDescription']) as string | undefined;
    const command = (attrs['command'] || attrs['Command'] || attrs['tool.command']) as string | undefined;
    const executedBy = (attrs['executedBy'] || attrs['ExecutedBy'] || attrs['executed_by']) as string | undefined;

    // Extract tool name - fall back to operationName if it's a Tool kind
    let toolName = (attrs['tool.name'] || attrs['toolName'] || attrs['ToolName']) as string | undefined;
    if (!toolName && kind === 'Tool') {
      // Use operationName as tool name, cleaning up common prefixes
      const op = span.operationName;
      if (op && !op.toLowerCase().startsWith('tool')) {
        toolName = op;
      } else if (op && op.toLowerCase().startsWith('tool_')) {
        toolName = op.substring(5); // Remove "tool_" prefix
      } else if (op && op.toLowerCase().startsWith('tool.')) {
        toolName = op.substring(5); // Remove "tool." prefix
      } else if (op) {
        toolName = op;
      }
    }

    // Extract user message - try multiple patterns including triggered.message from reasoning.loop
    const userMessage = (
      attrs['triggered.message'] ||  // reasoning.loop stores user message here
      attrs['message'] ||
      attrs['Message'] ||
      attrs['user.message'] ||
      attrs['input'] ||
      attrs['user_input'] ||
      attrs['content']
    ) as string | undefined;

    // Extract triggered by info (for reasoning.loop spans)
    const triggeredBy = (attrs['triggered.by'] || attrs['triggeredBy']) as string | undefined;

    // Extract model input/output - try multiple attribute name patterns
    const modelInput = attrs['model.input'] || attrs['modelInput'] || attrs['ModelInput'] || attrs['model_input'];
    const modelOutput = attrs['model.output'] || attrs['modelOutput'] || attrs['ModelOutput'] || attrs['model_output'];
    const systemPrompt = (attrs['system.prompt'] || attrs['systemPrompt'] || attrs['SystemPrompt']) as string | undefined;
    const modelThinking = (attrs['model.thinking'] || attrs['modelThinking'] || attrs['ModelThinking']) as string | undefined;
    const reasoning = (attrs['reasoning'] || attrs['Reasoning']) as string | undefined;
    const response = (attrs['response'] || attrs['Response']) as string | undefined;

    // Parse model input/output if they're JSON strings
    let parsedModelInput: unknown[] | undefined;
    let parsedModelOutput: unknown[] | undefined;
    try {
      if (typeof modelInput === 'string') {
        parsedModelInput = JSON.parse(modelInput);
      } else if (Array.isArray(modelInput)) {
        parsedModelInput = modelInput;
      }
    } catch { /* ignore parse errors */ }
    try {
      if (typeof modelOutput === 'string') {
        parsedModelOutput = JSON.parse(modelOutput);
      } else if (Array.isArray(modelOutput)) {
        parsedModelOutput = modelOutput;
      }
    } catch { /* ignore parse errors */ }

    // UserMessage and Incident spans should always be top-level (no parent)
    const isTopLevelKind = kind === 'UserMessage' || kind === 'Incident';

    return {
      kind,
      context: {
        span_id: span.spanId,
        span_id_number: parseInt(span.spanId, 10) || 0,
      },
      parent_id: isTopLevelKind ? undefined : span.parentSpanId,
      start_time: new Date(), // Would need actual start time from backend
      end_time: new Date(),
      attributes: {
        agentName: (attrs['agent.name'] || attrs['agentName'] || attrs['AgentName']) as string,
        toolName,
        toolDescription,
        toolInput,
        toolOutput,
        command,
        executedBy,
        message: userMessage,
      },
      usage_info: {
        modelName: (attrs['model.name'] || attrs['modelName'] || attrs['ModelName'] || attrs['modelId'] || attrs['ModelId']) as string,
        prompt_tokens: (attrs['model.input.tokens.count'] || attrs['inputTokens'] || attrs['InputTokens']) as number,
        completion_tokens: (attrs['model.output.tokens.count'] || attrs['outputTokens'] || attrs['OutputTokens']) as number,
        total_tokens: (attrs['model.total.tokens.count'] || attrs['totalTokens'] || attrs['TotalTokens']) as number,
        model_input: parsedModelInput as Array<{ role: string; contentLength: number; contentPreview: string }>,
        model_output: parsedModelOutput as Array<{ role: string; contentLength: number; contentPreview: string }>,
        systemPrompt,
        modelThinking,
        reasoning,
        response,
      },
      status: span.status === 'Ok' ? 'completed' : span.status === 'Error' ? 'failed' : 'running',
    };
  }

  /**
   * Establish proper span hierarchy to match Web client tree structure:
   * - UserMessage and Agent are both at root level (siblings)
   * - Tool, ModelGeneration, etc. as children of Agent
   *
   * This mimics the Web client's traceParser structure where user sees:
   *   ❯ User Message
   *   ◆ Agent (meta_agent)
   *   ├── ⟡ Model
   *   ├── ⚙ Tool
   *   └── ...
   */
  private establishSpanHierarchy(spans: ISpan[]): void {
    if (spans.length === 0) return;

    // Build a set of valid span IDs
    const validSpanIds = new Set(spans.map(s => s.context.span_id));

    // Find root-level spans and Agent spans
    const hasUserMessage = spans.some(s => s.kind === 'UserMessage');
    const agentSpans = spans.filter(s => s.kind === 'Agent');
    const subAgentSpans = spans.filter(s => s.kind === 'SubAgent');

    // If no UserMessage exists, create a synthetic one
    if (!hasUserMessage && agentSpans.length > 0) {
      const firstAgent = agentSpans[0];

      // Try to extract user message from various sources:
      // 1. From an agent span with triggered.message attribute (reasoning.loop)
      // 2. From model input of the first model call
      let extractedUserMessage = '(User message not available in trace data)';

      // Check if any agent span has the triggered message
      const spanWithTriggeredMessage = spans.find(s => s.attributes?.message && s.kind === 'Agent');
      if (spanWithTriggeredMessage?.attributes?.message) {
        extractedUserMessage = spanWithTriggeredMessage.attributes.message;
      } else {
        // Fallback: extract from model input
        const firstModelSpan = spans.find(s => s.kind === 'ModelGeneration' && s.usage_info?.model_input?.length);
        if (firstModelSpan?.usage_info?.model_input) {
          const userInput = firstModelSpan.usage_info.model_input.find(m => m.role === 'user');
          if (userInput?.contentPreview) {
            extractedUserMessage = userInput.contentPreview;
          }
        }
      }

      const syntheticUserMessage: ISpan = {
        kind: 'UserMessage',
        context: {
          span_id: `synthetic_user_${Date.now()}`,
          span_id_number: -1,
        },
        parent_id: undefined,
        start_time: firstAgent.start_time,
        attributes: {
          displayName: 'User',
          message: extractedUserMessage,
        },
      };
      spans.unshift(syntheticUserMessage);
      validSpanIds.add(syntheticUserMessage.context.span_id);
    }

    // Top-level kinds - UserMessage, Incident, and Agent are all at root level
    const topLevelKinds = new Set(['UserMessage', 'Incident', 'Agent']);

    // Ensure top-level spans have no parent
    for (const span of spans) {
      if (topLevelKinds.has(span.kind)) {
        span.parent_id = undefined;
      }
    }

    // SubAgent spans should be children of their parent Agent
    for (const subAgent of subAgentSpans) {
      const parentAgent = agentSpans.find(a =>
        a.attributes?.agentName && subAgent.attributes?.parentAgent === a.attributes.agentName
      ) || agentSpans[0];

      if (parentAgent) {
        subAgent.parent_id = parentAgent.context.span_id;
      }
    }

    // Child span kinds that should be under an Agent
    const childKinds = new Set(['Tool', 'ModelGeneration', 'AgentThinking', 'AgentResponse', 'Execution', 'AgentHandoff', 'AgentHandback']);

    for (const span of spans) {
      // Skip top-level and SubAgent kinds (already handled)
      if (topLevelKinds.has(span.kind) || span.kind === 'SubAgent') {
        continue;
      }

      // Child spans should be parented to an Agent
      if (childKinds.has(span.kind)) {
        // If parent_id doesn't exist in our spans, it's orphaned
        if (!span.parent_id || !validSpanIds.has(span.parent_id)) {
          // Try to find an appropriate Agent parent by agent name
          const spanAgentName = span.attributes?.agentName;
          const matchingAgent = [...agentSpans, ...subAgentSpans].find(a => a.attributes?.agentName === spanAgentName);

          if (matchingAgent) {
            span.parent_id = matchingAgent.context.span_id;
          } else if (agentSpans.length > 0) {
            // Fall back to the first Agent span
            span.parent_id = agentSpans[0].context.span_id;
          }
          // If no agent, leave as orphan (will show at root)
        }
      }
    }
  }

  /**
   * Infer span kind from operation name and attributes
   */
  private inferSpanKind(operationName: string, attrs: Record<string, unknown>): ISpan['kind'] {
    const op = operationName.toLowerCase();

    // Check attributes for explicit kind/type indicators
    const spanKind = (attrs['span.kind'] || attrs['spanKind'] || attrs['kind']) as string | undefined;
    if (spanKind) {
      const kindLower = spanKind.toLowerCase();
      if (kindLower === 'usermessage' || kindLower === 'user_message' || kindLower === 'user') return 'UserMessage';
      if (kindLower === 'modelgeneration' || kindLower === 'model_generation') return 'ModelGeneration';
      if (kindLower === 'tool') return 'Tool';
      if (kindLower === 'agent') return 'Agent';
      if (kindLower === 'subagent' || kindLower === 'sub_agent') return 'SubAgent';
      if (kindLower === 'incident') return 'Incident';
    }

    // Check role attribute - if role is 'user', it's a user message
    const role = (attrs['role'] || attrs['message.role']) as string | undefined;
    if (role?.toLowerCase() === 'user') return 'UserMessage';

    // Handle known OpenTelemetry operation names from TraceConstants.cs
    // user.message - explicit user message span
    if (op === 'user.message' || op === 'usermessage') return 'UserMessage';
    // reasoning.loop - agent's think-act loop iteration (treat as Agent execution context)
    if (op === 'reasoning.loop') return 'Agent';
    // invoke.agent - agent invocation
    if (op === 'invoke.agent' || op === 'invokeagent') return 'Agent';
    // model.generation - LLM call
    if (op === 'model.generation' || op === 'modelgeneration') return 'ModelGeneration';
    // tool - tool execution
    if (op === 'tool') return 'Tool';
    // handoff - agent handoff
    if (op === 'handoff') return 'AgentHandoff';

    // Check operation name patterns (fallback)
    if (op.includes('usermessage') || op.includes('user_message') || op === 'user') return 'UserMessage';
    if (op.includes('user') && op.includes('input')) return 'UserMessage';
    if (op.includes('user') && op.includes('message')) return 'UserMessage';
    if (op.includes('model') || op.includes('generation')) return 'ModelGeneration';
    if (op.includes('tool')) return 'Tool';
    if (op.includes('agent') && op.includes('handoff')) return 'AgentHandoff';
    if (op.includes('agent') && op.includes('handback')) return 'AgentHandback';
    if (op.includes('agent') && op.includes('thinking')) return 'AgentThinking';
    if (op.includes('agent') && op.includes('response')) return 'AgentResponse';
    if (op.includes('subagent')) return 'SubAgent';
    if (op.includes('agent')) return 'Agent';
    if (op.includes('incident')) return 'Incident';
    if (attrs['tool.name'] || attrs['toolName']) return 'Tool';

    // Default to 'Tool' for unknown operations (most unrecognized spans are tool-like)
    // This aligns better with the Web client's approach
    return 'Tool';
  }
}

// Singleton instance
let traceServiceInstance: TraceService | null = null;

/**
 * Get or create the trace service instance
 */
export function getTraceService(baseUrl?: string, appInsightsAppId?: string): TraceService {
  if (!traceServiceInstance && baseUrl) {
    traceServiceInstance = new TraceService(baseUrl, appInsightsAppId);
  }
  if (!traceServiceInstance) {
    throw new Error('TraceService not initialized. Call getTraceService with baseUrl first.');
  }
  return traceServiceInstance;
}

/**
 * Initialize the trace service
 */
export function initTraceService(baseUrl: string, appInsightsAppId?: string): TraceService {
  traceServiceInstance = new TraceService(baseUrl, appInsightsAppId);
  return traceServiceInstance;
}

/**
 * Trace types for CLI trace view
 * Aligned with Agent.Web client's trace implementation
 */

// ============================================================================
// Event Types (from App Insights customEvents)
// ============================================================================

export type EventName =
  | 'UserMessage'
  | 'Incident'
  | 'AgentExecution'
  | 'AgentResponse'
  | 'ModelGeneration'
  | 'AgentHandoff'
  | 'AgentToolExecution';

export type EventType =
  | 'AgentStart'
  | 'AgentEnd'
  | 'AgentHandoff'
  | 'ModelGenerationStart'
  | 'ModelGenerationEnd'
  | 'ToolStart'
  | 'ToolEnd';

/**
 * Raw event log from App Insights customEvents
 */
export interface ThreadEventLog {
  timeStamp: Date;
  eventName: EventName;
  threadId: string;
  agentName?: string;
  eventType?: EventType;
  taskType?: string;
  message?: string;
  fromAgent?: string;
  toAgent?: string;
  toolName?: string;
  toolDescription?: string;
  toolInput?: string;
  toolOutput?: string;
  userId?: string;
  displayName?: string;
  modelId?: string;
  temperature?: number;
  inputTokens?: number;
  modelInput?: { role: string; contentLength: number; contentPreview: string }[];
  outputTokens?: number;
  modelOutput?: { role: string; contentLength: number; contentPreview: string }[];
  systemPrompt?: string;
  modelThinking?: string;
  reasoning?: string;
  response?: string;
  result?: string;
  handoffReasoning?: string;
  spanId?: string;
  parentSpanId?: string;
  traceId?: string;
}

// ============================================================================
// Span Types (parsed from events)
// ============================================================================

export type SpanKind =
  | 'Incident'
  | 'Agent'
  | 'SubAgent'
  | 'Tool'
  | 'UserMessage'
  | 'AgentHandoff'
  | 'AgentHandback'
  | 'ModelGeneration'
  | 'AgentResponse'
  | 'AgentThinking'
  | 'Execution';  // CLI-specific: AzCli, Kubectl, etc.

export interface ITokenUsageInfo {
  modelName?: string;
  temperature?: number;
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
  model_input?: { role: string; contentLength: number; contentPreview: string }[];
  model_output?: { role: string; contentLength: number; contentPreview: string }[];
  systemPrompt?: string;
  modelThinking?: string;
  reasoning?: string;
  response?: string;
}

export interface SpanAttributes {
  // Common
  message?: string;
  result?: string;

  // Agent/SubAgent
  agentName?: string;

  // User
  displayName?: string;
  userId?: string;

  // Tool/Execution
  toolName?: string;
  toolDescription?: string;
  toolInput?: string;
  toolOutput?: string;
  executionType?: 'azCli' | 'kubectl' | 'psql' | 'bash';
  command?: string;
  executedBy?: string;

  // Handoff
  fromAgent?: string;
  toAgent?: string;
  handoffReasoning?: string;

  // Thinking
  thinkingSteps?: { timestamp: Date; message: string }[];

  // Duration (calculated)
  duration?: number;
}

export interface ITraceContext {
  span_id: string;
  span_id_number: number;
  trace_id?: string;
  thread_id?: string;
}

/**
 * A span represents a single operation in the trace
 */
export interface ISpan {
  kind: SpanKind;
  context: ITraceContext;
  parent_id: string | undefined;
  start_time: Date;
  end_time?: Date;
  attributes?: SpanAttributes;
  usage_info?: ITokenUsageInfo;
  status?: 'running' | 'completed' | 'failed' | 'cancelled';
  error?: string;
}

/**
 * Tree node for UI rendering (span with children)
 */
export interface ISpanTreeNode extends ISpan {
  children: ISpanTreeNode[];
  depth: number;
  isExpanded?: boolean;
}

// ============================================================================
// Trace View State
// ============================================================================

export interface TraceViewState {
  spans: ISpan[];
  selectedSpanId?: string;
  expandedSpanIds: Set<string>;
  isLoading: boolean;
  error?: string;
  parseWarnings?: string[];
  parseErrors?: string[];
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Build tree from flat spans using parent_id relationships
 */
export function buildSpanTree(spans: ISpan[]): ISpanTreeNode[] {
  const spanMap = new Map<string, ISpanTreeNode>();
  const roots: ISpanTreeNode[] = [];

  // Create tree nodes
  for (const span of spans) {
    spanMap.set(span.context.span_id, { ...span, children: [], depth: 0 });
  }

  // Link children to parents
  for (const span of spans) {
    const node = spanMap.get(span.context.span_id)!;
    if (span.parent_id && spanMap.has(span.parent_id)) {
      const parent = spanMap.get(span.parent_id)!;
      node.depth = parent.depth + 1;
      parent.children.push(node);
    } else {
      roots.push(node);
    }
  }

  // Sort children by start time
  const sortChildren = (nodes: ISpanTreeNode[]) => {
    nodes.sort((a, b) => a.start_time.getTime() - b.start_time.getTime());
    for (const node of nodes) {
      sortChildren(node.children);
    }
  };
  sortChildren(roots);

  return roots;
}

/**
 * Get span duration in milliseconds
 */
export function getSpanDuration(span: ISpan): number | undefined {
  if (!span.end_time) return undefined;
  return span.end_time.getTime() - span.start_time.getTime();
}

/**
 * Format duration for display
 */
export function formatDuration(ms: number | undefined): string {
  if (ms === undefined) return '-';
  if (ms < 1000) return `${Math.round(ms)}ms`;
  const seconds = ms / 1000;
  if (seconds < 60) return `${seconds.toFixed(1)}s`;
  return `${Math.floor(seconds / 60)}m ${Math.round(seconds % 60)}s`;
}

/**
 * Get display title for span
 */
export function getSpanTitle(span: ISpan): string {
  switch (span.kind) {
    case 'Incident':
      return 'Incident';
    case 'Agent':
    case 'SubAgent':
      return span.attributes?.agentName || span.kind;
    case 'Tool':
      return span.attributes?.toolName || 'Tool';
    case 'Execution':
      const cmd = span.attributes?.command?.slice(0, 40) || '';
      return `${span.attributes?.executionType || 'Command'}: ${cmd}${cmd.length >= 40 ? '...' : ''}`;
    case 'UserMessage':
      return span.attributes?.displayName || 'User';
    case 'AgentHandoff':
      return `${span.attributes?.fromAgent} → ${span.attributes?.toAgent}`;
    case 'AgentHandback':
      return `${span.attributes?.toAgent} ← ${span.attributes?.fromAgent}`;
    case 'ModelGeneration':
      return span.usage_info?.modelName || 'Model';
    case 'AgentResponse':
      return 'Response';
    case 'AgentThinking':
      const steps = span.attributes?.thinkingSteps?.length || 0;
      return `Thinking (${steps} steps)`;
    default:
      // For unknown kinds, try to use attributes or show the kind itself
      return span.attributes?.message?.slice(0, 50) || span.kind || 'Unknown';
  }
}

/**
 * Get icon/symbol for span kind
 */
export function getSpanIcon(span: ISpan): string {
  switch (span.kind) {
    case 'Incident':
      return '⚠';
    case 'Agent':
      return '◆';
    case 'SubAgent':
      return '◇';
    case 'Tool':
      return '⚙';
    case 'Execution':
      return '●';
    case 'UserMessage':
      return '❯';
    case 'AgentHandoff':
    case 'AgentHandback':
      return '↔';
    case 'ModelGeneration':
      return '⟡';
    case 'AgentResponse':
      return '●';
    case 'AgentThinking':
      return '◆';
    default:
      return '•';
  }
}

/**
 * Get color for span kind
 */
export function getSpanColor(span: ISpan): string {
  if (span.status === 'failed') return 'red';
  if (span.status === 'cancelled') return 'gray';

  switch (span.kind) {
    case 'Incident':
      return 'yellow';
    case 'Agent':
    case 'SubAgent':
      return 'cyan';
    case 'Tool':
    case 'Execution':
      return 'green';
    case 'UserMessage':
      return 'white';
    case 'AgentHandoff':
    case 'AgentHandback':
      return 'magenta';
    case 'ModelGeneration':
      return 'blue';
    case 'AgentResponse':
      return '#F8BBD9'; // BABY_PINK
    case 'AgentThinking':
      return 'gray';
    default:
      return 'gray';  // Unknown kinds shown in gray like Web client
  }
}

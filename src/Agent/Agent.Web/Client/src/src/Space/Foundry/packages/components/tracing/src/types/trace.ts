export interface ITokenUsageInfo {
    modelName?: string;
    temperature?: number;
    prompt_tokens?: number;
    completion_tokens?: number;
    total_tokens?: number;
    model_input?: [{ role: string; contentLength: number; contentPreview: string }];
    model_output?: [{ role: string; contentLength: number; contentPreview: string }];
    systemPrompt?: string;
    modelThinking?: string;
    reasoning?: string;
    response?: string;
    errorMessage?: string;
    errorType?: string;
}

interface Attributes {
    agentName?: string;
    displayName?: string;
    userId?: string;
    message?: string;
    result?: string;
    fromAgent?: string;
    toAgent?: string;
    toolName?: string;
    toolDescription?: string;
    toolInput?: string;
    toolOutput?: string;
    duration?: number;
    handoffReasoning?: string;
    thinkingSteps?: { timestamp: Date; message: string }[];
}

export interface ISpan {
    kind: SpanKind;
    context: ITraceContext;
    parent_id: string | undefined;
    start_time: Date;
    end_time?: Date;
    attributes?: Attributes;
    usage_info?: ITokenUsageInfo;
}

interface ITraceContext {
    span_id: string;
    span_id_number: number;
    trace_id?: string;
    trace_state?: string;
    thread_id?: string;
}

export interface ISpanTreeNode extends ISpan {
    uiChildren: ISpanTreeNode[];
}

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
    | 'AgentThinking'; // Grouped thinking/intermediate messages

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
    | 'ModelGenerationError'
    | 'ToolStart'
    | 'ToolEnd';

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
    modelInput?: [{ role: string; contentLength: number; contentPreview: string }];
    outputTokens?: number;
    modelOutput?: [{ role: string; contentLength: number; contentPreview: string }];
    systemPrompt?: string; // Complete system prompt sent to the model
    modelThinking?: string; // Model's internal "thinking" from Responses API
    reasoning?: string; // Agent's structured reasoningScratchPad
    response?: string; // Agent's structured notifyUserMessage
    result?: string;
    handoffReasoning?: string;
    spanId?: string;
    parentSpanId?: string;
    traceId?: string;
    errorMessage?: string;
    errorType?: string;
}

export interface INodeDetailProps {
    selectedSpan: ISpan;
}

import { ISpan, ThreadEventLog } from '../../../../../packages/components/tracing/src/types/trace';

/**
 * Attempts to parse reasoning from model output content preview as a fallback
 * when the backend doesn't provide separate Reasoning/Response fields.
 * This is best-effort since content is truncated.
 */
const parseReasoningFromOutputFallback = (
    modelOutput?: { role: string; contentLength: number; contentPreview: string }[]
): { reasoning?: string; response?: string } => {
    if (!modelOutput || modelOutput.length === 0) {
        return {};
    }

    // Look for assistant messages that might contain reasoning
    const assistantOutput = modelOutput.find(msg => msg.role.toLowerCase() === 'assistant');
    if (!assistantOutput?.contentPreview) {
        return {};
    }

    const content = assistantOutput.contentPreview;

    // Try to parse as JSON to extract reasoningScratchPad (best effort - may be truncated)
    try {
        if (content.trim().startsWith('{')) {
            const parsed = JSON.parse(content);
            if (parsed.reasoningScratchPad || parsed.ReasoningScratchPad) {
                return {
                    reasoning: parsed.reasoningScratchPad || parsed.ReasoningScratchPad,
                    response: parsed.notifyUserMessage || parsed.NotifyUserMessage || parsed.response || undefined,
                };
            }
        }
    } catch {
        // JSON parsing failed (likely truncated), return empty
    }

    return {};
};

export const generateSpans = (events: ThreadEventLog[]) => {
    const spans: ISpan[] = [];
    const parseWarnings: string[] = [];
    const parseErrors: string[] = [];

    let metaAgentName: string | undefined;

    const unclosedAgentSpanIds: { [agentName: string]: number } = {};
    const unclosedModelGenerationSpanIds: { [agentName: string]: number } = {};
    const unclosedToolCallSpanIds: { [toolAndAgentName: string]: number } = {};
    // Map from telemetry spanId to UI span index for parent lookups
    const telemetrySpanIdToUiSpanIndex: { [spanId: string]: number } = {};

    // Helper to find parent span using telemetry parentSpanId, with fallback to agent name lookup
    const findParentSpanId = (event: ThreadEventLog): number | undefined => {
        // First try to use telemetry parentSpanId
        if (event.parentSpanId && telemetrySpanIdToUiSpanIndex[event.parentSpanId] !== undefined) {
            return telemetrySpanIdToUiSpanIndex[event.parentSpanId];
        }
        // Fallback to agent name lookup
        const agentName = event.agentName ?? metaAgentName ?? 'unknown';
        return unclosedAgentSpanIds[agentName];
    };

    // Helper to register a span's telemetry spanId for parent lookups
    const registerSpanId = (event: ThreadEventLog, uiSpanIndex: number) => {
        if (event.spanId) {
            telemetrySpanIdToUiSpanIndex[event.spanId] = uiSpanIndex;
        }
    };

    for (let index = 0; index < events.length; index++) {
        const event = events[index];

        if (event.eventName === 'UserMessage') {
            const newSpan: ISpan = {
                kind: 'UserMessage',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: undefined,
                start_time: event.timeStamp,
                attributes: {
                    displayName: event.displayName,
                    userId: event.userId,
                    message: event.message,
                },
            };
            spans.push(newSpan);
            continue;
        }

        if (event.eventName === 'Incident') {
            const newSpan: ISpan = {
                kind: 'Incident',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: undefined,
                start_time: event.timeStamp,
            };
            spans.push(newSpan);
            continue;
        }

        if (event.eventName === 'AgentResponse') {
            const message = event.message ?? '';
            // Use telemetry parentSpanId for proper hierarchy, fallback to agent name
            const parentSpanIndex = findParentSpanId(event);

            // Determine if this is a thinking step or final response
            // Check explicit fields first, then fall back to heuristic detection from message content
            // Messages starting with **Header** pattern are intermediate thinking steps
            const hasExplicitThinking = !!(event.modelThinking || event.reasoning);
            const messageStartsWithHeader = message.trimStart().startsWith('**') && !message.trimStart().startsWith('**Error');
            const isThinkingStep = hasExplicitThinking || messageStartsWithHeader;

            if (isThinkingStep) {
                // Group consecutive thinking steps into AgentThinking span
                const lastSpan = spans[spans.length - 1];
                const canGroup =
                    lastSpan?.kind === 'AgentThinking' &&
                    lastSpan.attributes?.thinkingSteps &&
                    lastSpan.parent_id === parentSpanIndex?.toString();

                if (canGroup) {
                    lastSpan.attributes!.thinkingSteps!.push({ timestamp: event.timeStamp, message });
                } else {
                    const newSpan: ISpan = {
                        kind: 'AgentThinking',
                        context: {
                            span_id: spans.length.toString(),
                            span_id_number: spans.length,
                        },
                        parent_id: parentSpanIndex?.toString(),
                        start_time: event.timeStamp,
                        attributes: {
                            thinkingSteps: [{ timestamp: event.timeStamp, message }],
                        },
                    };
                    spans.push(newSpan);
                    registerSpanId(event, newSpan.context.span_id_number);
                }
            } else {
                // Final response - create AgentResponse span
                const newSpan: ISpan = {
                    kind: 'AgentResponse',
                    context: {
                        span_id: spans.length.toString(),
                        span_id_number: spans.length,
                    },
                    parent_id: parentSpanIndex?.toString(),
                    start_time: event.timeStamp,
                    attributes: {
                        message,
                    },
                };
                spans.push(newSpan);
                registerSpanId(event, newSpan.context.span_id_number);
            }
            continue;
        }

        if (event.eventName === 'AgentExecution' && event.eventType === 'AgentStart') {
            if (!event.agentName) {
                parseErrors.push(`[AgentStart] ${event.timeStamp.toUTCString()} AgentStart found but there is no agent name.`);
                continue;
            }

            if (metaAgentName === undefined && event.agentName) {
                // This is the first AgentStart we have seen, so this must be the MetaAgent
                metaAgentName = event.agentName;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId !== undefined) {
                parseWarnings.push(
                    `[AgentStart] ${event.timeStamp.toUTCString()} AgentStart found for ${agentType} ${event.agentName} but there is already an open span for this agent.`
                );
            }

            const newSpan: ISpan = {
                kind: agentType,
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: undefined,
                start_time: event.timeStamp,
                attributes: {
                    agentName: event.agentName,
                },
            };

            unclosedAgentSpanIds[event.agentName] = newSpan.context.span_id_number;
            spans.push(newSpan);
            registerSpanId(event, newSpan.context.span_id_number);
            continue;
        }

        if (event.eventName === 'AgentExecution' && event.eventType === 'AgentEnd') {
            if (!event.agentName) {
                parseErrors.push(`[AgentEnd] ${event.timeStamp.toUTCString()} AgentEnd found but there is no agent name.`);
                continue;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            let existingAgentSpanId = unclosedAgentSpanIds[event.agentName];

            // If no open span exists, create a synthetic one (AgentStart event may have been missed)
            if (existingAgentSpanId === undefined) {
                parseWarnings.push(
                    `[AgentEnd] ${event.timeStamp.toUTCString()} AgentEnd found for ${agentType} ${event.agentName} but there is no open span for this agent. Creating synthetic span.`
                );

                // Create a synthetic span - we don't know the real start time, so use the end time
                const syntheticSpan: ISpan = {
                    kind: agentType,
                    context: {
                        span_id: spans.length.toString(),
                        span_id_number: spans.length,
                    },
                    parent_id: undefined,
                    start_time: event.timeStamp, // Best we can do without the start event
                    attributes: {
                        agentName: event.agentName,
                    },
                };
                existingAgentSpanId = syntheticSpan.context.span_id_number;
                spans.push(syntheticSpan);
            }

            const existingAgentSpan = spans[existingAgentSpanId];
            if (existingAgentSpan === undefined) {
                parseErrors.push(
                    `[AgentEnd] ${event.timeStamp.toUTCString()} Internal error: ${agentType} ${event.agentName} span with id ${existingAgentSpanId} not found.`
                );
                delete unclosedAgentSpanIds[event.agentName];
                continue;
            }

            existingAgentSpan.end_time = event.timeStamp;
            existingAgentSpan.attributes = { ...existingAgentSpan.attributes, result: event.result };
            delete unclosedAgentSpanIds[event.agentName];
            continue;
        }

        if (event.eventName === 'ModelGeneration' && event.eventType === 'ModelGenerationStart') {
            if (event.agentName === undefined) {
                parseErrors.push(
                    `[ModelGenerationStart] ${event.timeStamp.toUTCString()} ModelGenerationStart found but there is no agent name.`
                );
                continue;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[ModelGenerationStart] ${event.timeStamp.toUTCString()} ModelGenerationStart found for ${agentType} ${event.agentName} but there is no open span for this agent.`
                );
                delete unclosedModelGenerationSpanIds[event.agentName];
                continue;
            }

            const existingModelGenerationSpanId = unclosedModelGenerationSpanIds[event.agentName];
            if (existingModelGenerationSpanId !== undefined) {
                parseWarnings.push(
                    `[ModelGenerationStart] ${event.timeStamp.toUTCString()} ModelGenerationStart found for ${agentType} ${event.agentName} but there is already an open ModelGeneration span for this agent.`
                );
            }

            const newSpan: ISpan = {
                kind: 'ModelGeneration',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: existingAgentSpanId.toString(),
                start_time: event.timeStamp,
                attributes: {
                    agentName: event.agentName,
                },
                usage_info: {
                    model_input: event.modelInput,
                    systemPrompt: event.systemPrompt,
                    temperature: event.temperature,
                },
            };
            spans.push(newSpan);
            registerSpanId(event, newSpan.context.span_id_number);

            unclosedModelGenerationSpanIds[event.agentName] = newSpan.context.span_id_number;
            continue;
        }

        if (event.eventName === 'ModelGeneration' && event.eventType === 'ModelGenerationEnd') {
            if (event.agentName === undefined) {
                parseErrors.push(
                    `[ModelGenerationEnd] ${event.timeStamp.toUTCString()} ModelGenerationEnd found but there is no agent name.`
                );
                continue;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[ModelGenerationEnd] ${event.timeStamp.toUTCString()} ModelGenerationEnd found for ${agentType} ${event.agentName} but there is no open span for this agent.`
                );
            }

            const existingModelGenerationSpanId = unclosedModelGenerationSpanIds[event.agentName];
            if (existingModelGenerationSpanId === undefined) {
                parseErrors.push(
                    `[ModelGenerationEnd] ${event.timeStamp.toUTCString()} ModelGenerationEnd found for ${agentType} ${event.agentName} but there is no open ModelGeneration span for this agent.`
                );
                continue;
            }

            const existingModelGenerationSpan = spans[existingModelGenerationSpanId];
            if (existingModelGenerationSpan === undefined) {
                parseWarnings.push(
                    `[ModelGenerationEnd] ${event.timeStamp.toUTCString()} Internal error: ${agentType} ${event.agentName} ModelGeneration span with id ${existingModelGenerationSpanId} not found.`
                );
                delete unclosedModelGenerationSpanIds[event.agentName];
                continue;
            }

            // Prefer backend-provided reasoning/response fields, fall back to parsing from output
            let reasoning = event.reasoning;
            let response = event.response;
            if (!reasoning && !response) {
                const fallback = parseReasoningFromOutputFallback(event.modelOutput);
                reasoning = fallback.reasoning;
                response = fallback.response;
            }

            existingModelGenerationSpan.end_time = event.timeStamp;
            existingModelGenerationSpan.usage_info = {
                ...existingModelGenerationSpan.usage_info,
                modelName: event.modelId,
                prompt_tokens: event.inputTokens,
                completion_tokens: event.outputTokens,
                total_tokens:
                    event.inputTokens !== undefined && event.outputTokens !== undefined
                        ? event.inputTokens + event.outputTokens
                        : undefined,
                model_output: event.modelOutput,
                modelThinking: event.modelThinking, // Model's internal thinking from Responses API
                reasoning,
                response,
            };
            delete unclosedModelGenerationSpanIds[event.agentName];
            continue;
        }

        if (event.eventName === 'ModelGeneration' && event.eventType === 'ModelGenerationError') {
            if (event.agentName === undefined) {
                parseErrors.push(
                    `[ModelGenerationError] ${event.timeStamp.toUTCString()} ModelGenerationError found but there is no agent name.`
                );
                continue;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[ModelGenerationError] ${event.timeStamp.toUTCString()} ModelGenerationError found for ${agentType} ${event.agentName} but there is no open span for this agent.`
                );
            }

            const existingModelGenerationSpanId = unclosedModelGenerationSpanIds[event.agentName];
            if (existingModelGenerationSpanId === undefined) {
                // Create a synthetic span for the error since we don't have a start event
                parseWarnings.push(
                    `[ModelGenerationError] ${event.timeStamp.toUTCString()} ModelGenerationError found for ${agentType} ${event.agentName} but there is no open ModelGeneration span. Creating synthetic span.`
                );

                const syntheticSpan: ISpan = {
                    kind: 'ModelGeneration',
                    context: {
                        span_id: spans.length.toString(),
                        span_id_number: spans.length,
                    },
                    parent_id: existingAgentSpanId?.toString(),
                    start_time: event.timeStamp,
                    end_time: event.timeStamp,
                    attributes: {
                        agentName: event.agentName,
                    },
                    usage_info: {
                        errorMessage: event.errorMessage,
                        errorType: event.errorType,
                    },
                };
                spans.push(syntheticSpan);
                continue;
            }

            const existingModelGenerationSpan = spans[existingModelGenerationSpanId];
            if (existingModelGenerationSpan === undefined) {
                parseWarnings.push(
                    `[ModelGenerationError] ${event.timeStamp.toUTCString()} Internal error: ${agentType} ${event.agentName} ModelGeneration span with id ${existingModelGenerationSpanId} not found.`
                );
                delete unclosedModelGenerationSpanIds[event.agentName];
                continue;
            }

            existingModelGenerationSpan.end_time = event.timeStamp;
            existingModelGenerationSpan.usage_info = {
                ...existingModelGenerationSpan.usage_info,
                errorMessage: event.errorMessage,
                errorType: event.errorType,
            };
            delete unclosedModelGenerationSpanIds[event.agentName];
            continue;
        }

        if (event.eventName === 'AgentHandoff') {
            const metaAgentSpanId = metaAgentName ? unclosedAgentSpanIds[metaAgentName] : undefined;
            if (metaAgentSpanId === undefined) {
                parseErrors.push(`[AgentHandoff] ${event.timeStamp.toUTCString()} AgentHandoff found but there is no open MetaAgent span.`);
                continue;
            }

            const existingMetaAgentSpan = spans[metaAgentSpanId];
            if (existingMetaAgentSpan === undefined) {
                parseErrors.push(
                    `[AgentHandoff] ${event.timeStamp.toUTCString()} Internal error: MetaAgent span with id ${metaAgentSpanId} not found.`
                );
                if (metaAgentName) {
                    delete unclosedAgentSpanIds[metaAgentName];
                }
                continue;
            }

            existingMetaAgentSpan.end_time = event.timeStamp;
            existingMetaAgentSpan.attributes = { ...existingMetaAgentSpan.attributes, result: event.result };
            if (metaAgentName) {
                delete unclosedAgentSpanIds[metaAgentName];
            }

            const newSpan: ISpan = {
                kind: 'AgentHandoff',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: metaAgentSpanId.toString(),
                start_time: event.timeStamp,
                attributes: {
                    agentName: event.fromAgent,
                    fromAgent: event.fromAgent,
                    toAgent: event.toAgent,
                    handoffReasoning: event.handoffReasoning,
                },
            };
            spans.push(newSpan);
            continue;
        }

        if (event.eventName === 'AgentToolExecution' && event.eventType === 'ToolStart' && event.toolName === 'HandoffBack') {
            // Special tool name to indicate handoff back to MetaAgent
            if (!event.agentName) {
                parseErrors.push(`[HandoffBack] ${event.timeStamp.toUTCString()} HandoffBack found but there is no SubAgent name.`);
                continue;
            }

            const subAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (subAgentSpanId === undefined) {
                parseWarnings.push(
                    `[HandoffBack] ${event.timeStamp.toUTCString()} HandoffBack found for SubAgent ${event.agentName} but there is no open span for this agent.`
                );
            } else {
                const existingSubAgentSpan = spans[subAgentSpanId];
                if (existingSubAgentSpan === undefined) {
                    parseWarnings.push(
                        `[HandoffBack] ${event.timeStamp.toUTCString()} Internal error: SubAgent span with id ${subAgentSpanId} not found.`
                    );
                } else {
                    existingSubAgentSpan.end_time = event.timeStamp;
                    existingSubAgentSpan.attributes = { ...existingSubAgentSpan.attributes, result: event.result };
                }
                delete unclosedAgentSpanIds[event.agentName];
            }

            const newSpan: ISpan = {
                kind: 'AgentHandback',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: subAgentSpanId?.toString(),
                start_time: event.timeStamp,
                attributes: {
                    agentName: event.agentName,
                    fromAgent: event.agentName,
                    toAgent: metaAgentName,
                },
            };
            spans.push(newSpan);
            continue;
        }

        if (event.eventName === 'AgentToolExecution' && event.eventType === 'ToolStart' && event.toolName !== 'HandoffBack') {
            if (event.agentName === undefined || event.toolName === undefined) {
                parseErrors.push(
                    `[ToolStart] ${event.timeStamp.toUTCString()} ToolStart found but there is no agent name '${event.agentName}' or tool name '${event.toolName}'.`
                );
                continue;
            }

            const unclosedToolCallSpanIdKey = `${event.toolName}@@${event.agentName}`;
            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[ToolStart] ${event.timeStamp.toUTCString()} ToolStart found for tool ${event.toolName} on ${agentType} ${event.agentName} but there is no open span for this this agent.`
                );
                delete unclosedToolCallSpanIds[unclosedToolCallSpanIdKey];
                continue;
            }

            const existingToolCallSpanId = unclosedToolCallSpanIds[unclosedToolCallSpanIdKey];
            if (existingToolCallSpanId !== undefined) {
                parseWarnings.push(
                    `[ToolStart] ${event.timeStamp.toUTCString()} ToolStart found for tool ${event.toolName} on ${agentType} ${event.agentName} but there is already an open ToolCall span for this tool+agent.`
                );
            }

            const newSpan: ISpan = {
                kind: 'Tool',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: existingAgentSpanId?.toString(),
                start_time: event.timeStamp,
                attributes: {
                    agentName: event.agentName,
                    toolName: event.toolName,
                    toolDescription: event.toolDescription,
                    toolInput: event.toolInput,
                },
            };
            spans.push(newSpan);
            registerSpanId(event, newSpan.context.span_id_number);

            unclosedToolCallSpanIds[unclosedToolCallSpanIdKey] = newSpan.context.span_id_number;
            continue;
        }

        if (event.eventName === 'AgentToolExecution' && event.eventType === 'ToolEnd' && event.toolName !== 'HandoffBack') {
            if (event.agentName === undefined || event.toolName === undefined) {
                parseErrors.push(
                    `[ToolEnd] ${event.timeStamp.toUTCString()} ToolEnd found but there is no agent name '${event.agentName}' or tool name '${event.toolName}'.`
                );
                continue;
            }

            const unclosedToolCallSpanIdKey = `${event.toolName}@@${event.agentName}`;
            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[ToolEnd] ${event.timeStamp.toUTCString()} ToolEnd found for tool ${event.toolName} on ${agentType} ${event.agentName} but there is no open span for this this agent.`
                );
            }

            const existingToolCallSpanId = unclosedToolCallSpanIds[unclosedToolCallSpanIdKey];
            if (existingToolCallSpanId === undefined) {
                parseErrors.push(
                    `[ToolEnd] ${event.timeStamp.toUTCString()} ToolEnd found for tool ${event.toolName} on ${agentType} ${event.agentName} but there is no open ToolCall span for this tool+agent.`
                );
                continue;
            }

            const existingToolCallSpan = spans[existingToolCallSpanId];
            if (existingToolCallSpan === undefined) {
                parseWarnings.push(
                    `[ToolEnd] ${event.timeStamp.toUTCString()} Internal error: ToolCall span with id ${existingToolCallSpanId} not found for tool ${event.toolName} on ${agentType} ${event.agentName}.`
                );
                delete unclosedToolCallSpanIds[unclosedToolCallSpanIdKey];
                continue;
            }

            existingToolCallSpan.end_time = event.timeStamp;
            existingToolCallSpan.attributes = { ...existingToolCallSpan.attributes, toolOutput: event.toolOutput };
            delete unclosedToolCallSpanIds[unclosedToolCallSpanIdKey];
            continue;
        }
    }
    return { spans, parseWarnings, parseErrors };
};

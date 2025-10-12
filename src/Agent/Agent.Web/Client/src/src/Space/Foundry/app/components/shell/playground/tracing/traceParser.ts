import { ISpan, ThreadEventLog } from '../../../../../packages/components/tracing/src/types/trace';

export const generateSpans = (events: ThreadEventLog[]) => {
    const spans: ISpan[] = [];
    const parseWarnings: string[] = [];
    const parseErrors: string[] = [];

    let metaAgentName: string | undefined;

    const unclosedAgentSpanIds: { [agentName: string]: number } = {};
    const unclosedModelGenerationSpanIds: { [agentName: string]: number } = {};
    const unclosedToolCallSpanIds: { [toolAndAgentName: string]: number } = {};

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
            const newSpan: ISpan = {
                kind: 'AgentResponse',
                context: {
                    span_id: spans.length.toString(),
                    span_id_number: spans.length,
                },
                parent_id: undefined,
                start_time: event.timeStamp,
                attributes: {
                    message: event.message,
                },
            };
            spans.push(newSpan);
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
            continue;
        }

        if (event.eventName === 'AgentExecution' && event.eventType === 'AgentEnd') {
            if (!event.agentName) {
                parseErrors.push(`[AgentEnd] ${event.timeStamp.toUTCString()} AgentEnd found but there is no agent name.`);
                continue;
            }

            const agentType = event.agentName === metaAgentName ? 'Agent' : 'SubAgent';
            const existingAgentSpanId = unclosedAgentSpanIds[event.agentName];
            if (existingAgentSpanId === undefined) {
                parseErrors.push(
                    `[AgentEnd] ${event.timeStamp.toUTCString()} AgentEnd found for ${agentType} ${event.agentName} but there is no open span for this agent.`
                );
                continue;
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
                    temperature: event.temperature,
                },
            };
            spans.push(newSpan);

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
                },
            };
            spans.push(newSpan);

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

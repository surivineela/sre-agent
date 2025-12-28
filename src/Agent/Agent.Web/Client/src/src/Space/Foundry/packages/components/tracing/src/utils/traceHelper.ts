import { Thread } from '../../../../../../../Common/Contracts/DataPlane/Thread';
import { TraceBadgeTypes } from '../../../../../common/components/src/TraceBadge/TraceBadge';
import type { ISpan } from '../types/trace';

export const getBadgeType = (span: ISpan): TraceBadgeTypes | undefined => {
    let badgeType: TraceBadgeTypes | undefined;
    switch (span.kind) {
        case 'Incident': {
            badgeType = 'incident';
            break;
        }
        case 'Agent': {
            badgeType = 'agent';
            break;
        }
        case 'SubAgent': {
            badgeType = 'subagent';
            break;
        }
        case 'Tool': {
            badgeType = 'tool';
            break;
        }
        case 'UserMessage': {
            badgeType = 'userMessage';
            break;
        }
        case 'AgentHandoff': {
            badgeType = 'agentHandoff';
            break;
        }
        case 'AgentHandback': {
            badgeType = 'agentHandback';
            break;
        }
        case 'ModelGeneration': {
            badgeType = 'modelGeneration';
            break;
        }
        case 'AgentResponse': {
            badgeType = 'agentResponse';
            break;
        }
        case 'AgentThinking': {
            badgeType = 'agentThinking';
            break;
        }
        case undefined:
        default: {
            badgeType = undefined;
            break;
        }
    }
    return badgeType;
};

export const getSpanTotalTokens = (span: ISpan): number | undefined => {
    return span.usage_info?.total_tokens;
};

export const getSpanDurationSeconds = (span: ISpan): number | undefined => {
    const startTime = span.start_time ? new Date(span.start_time) : undefined;
    const endTime = span.end_time ? new Date(span.end_time) : undefined;
    const durationMs = startTime && endTime ? endTime.getTime() - startTime.getTime() : undefined;

    const attributeDurationNumber = span.attributes?.duration === undefined ? undefined : Number(span.attributes.duration);

    if (durationMs === undefined || Number.isNaN(durationMs)) {
        return Number.isNaN(attributeDurationNumber) ? undefined : attributeDurationNumber;
    }
    return Math.round(durationMs / 1000);
};

export const formatStartTime = (startTime?: string): string | undefined => {
    try {
        if (!startTime) {
            return undefined;
        }

        const date = new Date(startTime);

        if (Number.isNaN(date.getTime())) {
            return undefined;
        }

        return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}`;
    } catch {
        return undefined;
    }
};

export const getSpanTitle = (span: ISpan, thread: Thread): string => {
    {
        switch (span.kind) {
            case 'Incident':
                return thread?.title;
            case 'Agent':
                return span.attributes?.agentName || '';
            case 'SubAgent':
                return span.attributes?.agentName || '';
            case 'Tool':
                return span.attributes?.toolName || '';
            case 'UserMessage':
                return span.attributes?.displayName || '';
            case 'AgentHandoff':
            case 'AgentHandback':
                return span.attributes?.fromAgent && span.attributes?.toAgent
                    ? `${span.attributes?.fromAgent} → ${span.attributes?.toAgent}`
                    : '';
            case 'ModelGeneration':
                return span.usage_info?.modelName || '';
            case 'AgentThinking': {
                const steps = span.attributes?.thinkingSteps;
                if (!steps?.length) return '';
                const firstMessage = steps[0].message;
                if (firstMessage.trimStart().startsWith('**')) {
                    const start = firstMessage.indexOf('**');
                    const end = firstMessage.indexOf('**', start + 2);
                    if (end !== -1) return firstMessage.substring(start + 2, end);
                }
                return `${steps.length} thinking step${steps.length !== 1 ? 's' : ''}`;
            }
            case 'AgentResponse':
            default:
                return '';
        }
    }
};

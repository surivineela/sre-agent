import { getAgentHeaders } from '../../../Common/Helpers/headers';

export type PlaygroundInsightSeverity = 'info' | 'warning' | 'error';

export interface PlaygroundInsightEvidence {
    title: string;
    detail?: string;
    severity?: PlaygroundInsightSeverity;
}

export interface PlaygroundInsightsRequest {
    prompt: string;
    agentName?: string;
    agentGoal?: string;
    tools: string[];
    systemTools: string[];
    availableTools?: string[];
    availableSystemTools?: string[];
    chatFindings: PlaygroundInsightEvidence[];
    toolFindings: PlaygroundInsightEvidence[];
    transcriptSummary?: string;
    recentMessages?: string[];
}

export interface PlaygroundInsightImpact {
    scoreIncrease: number;
    dimension: string;
    description: string;
    level: 'high' | 'medium' | 'low';
}

export interface PlaygroundInsightAction {
    id: string;
    type: string;
    title: string;
    detail?: string;
    severity: PlaygroundInsightSeverity;
    impact?: PlaygroundInsightImpact;
    patch?: string;
    autoApplicable?: boolean;
    conflicts?: string[];
    requires?: string[];
}

export interface PlaygroundSubScore {
    id: string;
    label: string;
    score: number;
    evidence: string;
    improvements?: string[];
}

export interface PlaygroundInsightsResponse {
    confidenceScore: number;
    confidenceLabel: string;
    subScores: PlaygroundSubScore[];
    promptInsights: string[];
    toolSuggestions: string[];
    chatDiagnostics: string[];
    actionItems: PlaygroundInsightAction[];
    notes: string[];
    suggestedSequence?: string[];
}

export const fetchPlaygroundInsights = async (
    sreAgentEndpoint: string,
    payload: PlaygroundInsightsRequest
): Promise<PlaygroundInsightsResponse> => {
    const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/playground-insights`, {
        method: 'POST',
        headers: {
            ...getAgentHeaders(),
            'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(payload),
    });

    if (!response.ok) {
        const errorBody = await response.text().catch(() => undefined);
        throw new Error(
            `Failed to generate playground insights: ${response.status} ${response.statusText}${errorBody ? ` - ${errorBody}` : ''}`
        );
    }

    return response.json();
};

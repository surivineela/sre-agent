import { getAgentHeaders } from '../../../../Common/Helpers/headers';

export interface PromptImprovementRequest {
    prompt: string;
}

export interface PromptImprovementResponse {
    improvedPrompt: string;
    warnings: string[];
    suggestions: string[];
    handoffDescription?: string;
}

export const improvePrompt = async (sreAgentEndpoint: string, prompt: string): Promise<PromptImprovementResponse> => {
    const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/prompt-improvement`, {
        method: 'POST',
        headers: {
            ...getAgentHeaders(),
            'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({
            prompt,
        } as PromptImprovementRequest),
    });

    if (!response.ok) {
        let errorMessage = `Failed to improve prompt: ${response.status} ${response.statusText}`;

        try {
            const errorData = await response.text();
            if (errorData) {
                errorMessage += ` - ${errorData}`;
            }
        } catch (e) {
            // If we can't parse the error response, just use the status
        }

        throw new Error(errorMessage);
    }

    return response.json();
};

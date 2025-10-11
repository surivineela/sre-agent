import { getAgentHeaders } from '../../../../Common/Helpers/headers';
import { ScheduledTaskPromptImprovementResponse } from '../../../Contracts/ScheduledTasks';

export const improveScheduledTaskPrompt = async (
    sreAgentEndpoint: string,
    prompt: string
): Promise<ScheduledTaskPromptImprovementResponse> => {
    const response = await fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/prompt/improve`, {
        method: 'POST',
        headers: {
            ...getAgentHeaders(),
            'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify({ prompt }),
    });

    if (!response.ok) {
        let errorMessage = `Failed to improve scheduled task prompt: ${response.status} ${response.statusText}`;

        try {
            const errorText = await response.text();
            if (errorText) {
                errorMessage += ` - ${errorText}`;
            }
        } catch (error) {
            // ignore parsing errors
        }

        throw new Error(errorMessage);
    }

    return response.json();
};

import { getAgentHeaders } from '../../../../Common/Helpers/headers';
import { CronExpressionGenerationRequest, CronExpressionGenerationResponse } from '../../../Contracts/ScheduledTasks';

export const generateCronExpression = async (
    sreAgentEndpoint: string,
    request: CronExpressionGenerationRequest
): Promise<CronExpressionGenerationResponse> => {
    const response = await fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/cron/generate`, {
        method: 'POST',
        headers: {
            ...getAgentHeaders(),
            'Content-Type': 'application/json',
        },
        credentials: 'include',
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        let errorMessage = `Failed to generate cron expression: ${response.status} ${response.statusText}`;

        try {
            const errorData = await response.text();
            if (errorData) {
                errorMessage += ` - ${errorData}`;
            }
        } catch (error) {
            // ignore parsing errors, use default message
        }

        throw new Error(errorMessage);
    }

    return response.json();
};

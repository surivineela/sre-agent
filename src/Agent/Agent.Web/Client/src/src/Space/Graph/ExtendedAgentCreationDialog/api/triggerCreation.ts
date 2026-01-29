import { getAgentHeaders } from '../../../../Common/Helpers/headers';
import { CreateScheduledTaskRequest } from '../../../Contracts/ScheduledTasks';
import { TriggerState } from '../types';

export interface TriggerCreationResult {
    success: boolean;
    id?: string;
    error?: string;
}

export interface FilterFieldOption {
    fieldName: string;
    displayName: string;
    options: { key: string; value: string }[];
    fieldInputType: 'Dropdown' | 'TextField';
    isRequired: boolean;
}

export interface CreateIncidentHandlerRequest {
    id: string;
    name: string;
    description: string;
    incidentFilterId: string;
    incidentProcessingGuide: string[];
    tools: string[];
    incidents: string[];
    customInstructions: string;
}

export const createTriggerFromState = async (trigger: TriggerState, sreAgentEndpoint: string): Promise<TriggerCreationResult> => {
    try {
        if (trigger.mode === 'scheduled') {
            return await createScheduledTaskTrigger(trigger, sreAgentEndpoint);
        } else if (trigger.mode === 'incident') {
            return await createIncidentHandlerTrigger(trigger, sreAgentEndpoint);
        } else {
            return { success: false, error: 'Invalid trigger mode' };
        }
    } catch (error: any) {
        return {
            success: false,
            error: `Failed to create trigger: ${error.message || 'Unknown error'}`,
        };
    }
};

const createScheduledTaskTrigger = async (trigger: TriggerState, sreAgentEndpoint: string): Promise<TriggerCreationResult> => {
    if (trigger.strategy === 'existing') {
        // For existing scheduled tasks, we don't create a new one
        return {
            success: true,
            id: trigger.existingId,
        };
    }

    // Validate required fields for new scheduled task
    if (!trigger.name || !trigger.schedule.cronExpression || !trigger.instructions) {
        return {
            success: false,
            error: 'Missing required fields: name, schedule, and instructions are required',
        };
    }

    const request: CreateScheduledTaskRequest = {
        name: trigger.name,
        description: trigger.description || '',
        cronExpression: trigger.schedule.cronExpression,
        agentPrompt: trigger.instructions,
        agent: trigger.agentName,
        createdBy: 'Sub-Agent Builder',
        startTime: trigger.schedule.startTime,
        // Optional fields that may not exist in trigger state but could be extended
        endTime: undefined,
        threadId: undefined,
        maxExecutions: undefined,
        notificationChannel: undefined,
        executionContext: {},
    };

    const response = await fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks`, {
        method: 'POST',
        headers: getAgentHeaders(),
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        const errorText = await response.text();
        return {
            success: false,
            error: `HTTP ${response.status}: ${errorText}`,
        };
    }

    const result = await response.json();
    return {
        success: true,
        id: result.taskId || result.id,
    };
};

const createIncidentHandlerTrigger = async (trigger: TriggerState, sreAgentEndpoint: string): Promise<TriggerCreationResult> => {
    if (trigger.strategy === 'existing') {
        // For existing incident handlers, we don't create a new one
        return {
            success: true,
            id: trigger.existingId,
        };
    }

    // Validate required fields for new incident handler
    if (!trigger.name || !trigger.instructions || !trigger.incidentPriorities || !trigger.incidentType) {
        return {
            success: false,
            error: 'Missing required fields: name, instructions, priorities, and type are required',
        };
    }

    // Generate a unique ID for the incident handler
    const handlerId = generateHandlerId(trigger.name);

    // Create incident filter first
    const incidentFilterId = await createIncidentFilter(trigger, sreAgentEndpoint);
    if (!incidentFilterId) {
        return {
            success: false,
            error: 'Failed to create incident filter',
        };
    }

    const request: CreateIncidentHandlerRequest = {
        id: handlerId,
        name: trigger.name,
        description: trigger.description || '',
        incidentFilterId,
        incidentProcessingGuide: [trigger.instructions],
        tools: [], // TODO: Extract tools from agent if needed
        incidents: [],
        customInstructions: trigger.instructions,
    };

    const response = await fetch(`${sreAgentEndpoint}/api/v1/incidentPlayground/handlers/${handlerId}`, {
        method: 'PUT',
        headers: getAgentHeaders(),
        body: JSON.stringify(request),
    });

    if (!response.ok) {
        const errorText = await response.text();
        return {
            success: false,
            error: `HTTP ${response.status}: ${errorText}`,
        };
    }

    return {
        success: true,
        id: handlerId,
    };
};

const createIncidentFilter = async (trigger: TriggerState, sreAgentEndpoint: string): Promise<string | null> => {
    try {
        const filterId = generateFilterId(trigger.name);

        // Build the filter object directly from trigger state
        const filter: any = {
            id: filterId,
            name: `${trigger.name} Filter`,
            priorities: trigger.incidentPriorities || [],
            incidentType: trigger.incidentType || '',
            handlingAgent: trigger.agentName || '',
            agentMode: 'autonomous',
        };

        // Include additional filter fields from the trigger state
        if (trigger.additionalFilterFields) {
            for (const [key, value] of Object.entries(trigger.additionalFilterFields)) {
                // A workaround of field name mismatch between additional filter fields and filter creation payload fields.
                const payloadFieldName = key === 'owningTeam' ? 'owningTeamId' : key;
                filter[payloadFieldName] = value;
            }
        }

        const response = await fetch(`${sreAgentEndpoint}/api/v1/incidentPlayground/filters/${filterId}`, {
            method: 'PUT',
            headers: getAgentHeaders(),
            body: JSON.stringify(filter),
        });

        if (!response.ok) {
            console.error('Failed to create incident filter:', await response.text());
            return null;
        }

        return filterId;
    } catch (error) {
        console.error('Error creating incident filter:', error);
        return null;
    }
};

const generateHandlerId = (name: string): string => {
    // Generate a unique ID based on the name and timestamp
    const cleanName = name.replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
    const timestamp = Date.now().toString(36);
    return `${cleanName}-${timestamp}`;
};

const generateFilterId = (name: string): string => {
    // Generate a unique filter ID based on the name and timestamp
    const cleanName = name.replace(/[^a-zA-Z0-9]/g, '').toLowerCase();
    const timestamp = Date.now().toString(36);
    return `filter-${cleanName}-${timestamp}`;
};

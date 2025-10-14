export interface ScheduledTask {
    id: string;
    name: string;
    description: string;
    status: ScheduledTaskStatus;
    cronExpression: string;
    agentPrompt: string;
    threadId?: string;
    createdBy: string;
    createdAt: string;
    startTime?: string;
    endTime?: string;
    lastExecutionTime?: string;
    nextExecutionTime?: string;
    executionCount: number;
    maxExecutions?: number;
    notificationChannel?: string;
    executionContext?: Record<string, any>;
    executionHistory?: ScheduledTaskExecution[];
}

export interface ScheduledTaskExecution {
    executionTime: string;
    threadId?: string;
    success: boolean;
    errorMessage?: string;
    executionMetadata?: Record<string, any>;
}

export interface CreateScheduledTaskRequest {
    name: string;
    description: string;
    cronExpression: string;
    agentPrompt: string;
    agent?: string; // The agent name to associate with this scheduled task
    createdBy: string; // Required field for tracking who created the task
    startTime?: string;
    endTime?: string;
    threadId?: string;
    executionContext?: Record<string, any>;
    maxExecutions?: number;
    notificationChannel?: string;
}

export interface UpdateScheduledTaskRequest {
    name?: string;
    description?: string;
    cronExpression?: string;
    agentPrompt?: string;
    startTime?: string;
    endTime?: string;
    status?: ScheduledTaskStatus;
    executionContext?: Record<string, any>;
    maxExecutions?: number;
    notificationChannel?: string;
}

export interface CronExpressionGenerationRequest {
    description: string;
    timezone?: string;
    startTime?: string;
    format?: string;
}

export interface CronExpressionGenerationResponse {
    cronExpression: string;
    humanReadableDescription: string;
    timezone?: string;
    assumptions: string[];
    warnings: string[];
    examples: string[];
}

export interface ScheduledTaskPromptImprovementRequest {
    prompt: string;
}

export interface ScheduledTaskPromptImprovementResponse {
    improvedPrompt: string;
    warnings: string[];
    suggestions: string[];
    followUpQuestions: string[];
}

export enum ScheduledTaskStatus {
    Active = 'Active',
    Paused = 'Paused',
    Completed = 'Completed',
    Failed = 'Failed',
}

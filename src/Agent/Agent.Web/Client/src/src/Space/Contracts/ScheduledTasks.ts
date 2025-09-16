export interface ScheduledTask {
    id: string;
    name: string;
    description: string;
    status: 'Active' | 'Paused' | 'Completed' | 'Failed';
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
    status?: 'Active' | 'Paused' | 'Completed' | 'Failed';
    executionContext?: Record<string, any>;
    maxExecutions?: number;
    notificationChannel?: string;
}

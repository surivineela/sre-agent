export interface AgentTask {
    id: string;
    status: AgentTaskStatus;
    title?: string;
    type: AgentTaskType;
}

export enum AgentTaskStatus {
    InProgress = 'InProgress',
    Complete = 'Complete',
    Failed = 'Failed',
    Canceled = 'Canceled',
}

export enum AgentTaskType {
    IncidentInvestigation = 'IncidentInvestigation',
}

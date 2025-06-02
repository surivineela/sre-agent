export interface Agent {
    provisioningState: ProvisioningState;
    agentEndpoint: string;
    runningState: string;
    vnetConfiguration?: VnetConfiguration;
    knowledgeGraphConfiguration?: KnowledgeGraphConfiguration;
    actionConfiguration?: ActionConfiguration;
    outboundConnectionConfiguration?: OutboundConnectionConfiguration;
    mcpServers?: string[];
    logConfiguration?: LogConfiguration;
    incidentManagementConfiguration?: IncidentManagementConfiguration | null;
    dashboardConfiguration: DashboardConfiguration;
}

export enum ProvisioningState {
    InProgress = 'InProgress',
    Succeeded = 'Succeeded',
    Failed = 'Failed',
    Canceled = 'Canceled',
    Deleting = 'Deleting',
}

export interface DashboardConfiguration {
    grafanaUrl?: string;
    azureMonitorWorkspaceQueryEndpoint?: string;
    identity?: string;
    azureMonitorWorkspaceMetricsIngestionEndpoint?: string;
}

export interface VnetConfiguration {
    subnetResourceId?: string;
    vNetGuid?: string;
}

export interface KnowledgeGraphConfiguration {
    identity?: string;
    managedResources?: string[];
}

export interface ActionConfiguration {
    identity?: string;
    mode?: string;
}

export interface OutboundConnectionConfiguration {
    azureBotConfiguration?: {
        identity: string;
    };
}

export interface LogAnalyticsConfiguration {
    workspaceId: string;
    sharedKey: string;
}

export interface LogConfiguration {
    logAnalyticsConfiguration: LogAnalyticsConfiguration;
}

export interface IncidentManagementConfiguration {
    type: IncidentManagementType;
    connectionName?: string;
    connectionUrl?: string;
    connectionKey?: string;
}

export enum IncidentManagementType {
    PagerDuty = 'PagerDuty',
    AzMonitor = 'AzMonitor',
}

export enum IncidentStatus {
    active = 'active',
    acknowledged = 'acknowledged',
    mitigated = 'mitigated',
    triggered = 'triggered',
    closed = 'closed',
    resolved = 'resolved',
}

export enum ThreadSource {
    conversation = 'Conversation',
    incident = 'Incident',
    Portal = 'Portal', // legacy
}

export interface Thread {
    id: string;
    title: string;
    startMessage: Message;
    createdTimestamp: string;
    modifiedTimestamp: string;
    lastMessage: Message;
    status?: AgentStatus;
    incidentSource?: any;
    source?: ThreadSource;
}

export interface ThreadContext {
    isThreadActive: boolean;
    orchestrationState: {
        orchestrationInstanceId: string;
        reasoningState: ThreadOrchestrationReasoningState;
    };
}

export enum ThreadOrchestrationReasoningState {
    NotStarted = 'NotStarted',
    OrchestrationInitialized = 'OrchestrationInitialized',
    Waiting = 'Waiting',
    PlanningNextAction = 'PlanningNextAction',
    RunningFunctionCall = 'RunningFunctionCall',
    OrchestrationCompleted = 'OrchestrationCompleted',
    Error = 'Error',
}

export interface AgentStatus {
    actionsStatus?: {
        hasCriticalActions: boolean;
        hasWarningActions: boolean;
    };
    incidentStatus?: {
        incidentId: string;
        status: string;
    };
}

export interface Message {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    text: string;
    title?: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    isDailyReport?: boolean;
}

export interface KnowledgeGraphBuildStatus {
    crawledCount: number;
    hasCompletedInitialGraphCrawl: boolean;
    isCrawling: boolean;
    properties: any;
    totalVisibleResources: number;
}

export interface AzCliExecution {
    id: string;
    command: string;
    description: string;
    status: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';
    output?: string;
    error?: string;
    createdTimestamp: string;
    startedTimestamp?: string;
    completedTimestamp?: string;
    executedBy?: {
        displayName: string;
        userId: string;
        role: string;
    };
}

export enum ApprovalDecision {
    Pending = 'Pending',
    Approved = 'Approved',
    Rejected = 'Rejected',
}

export interface Approval {
    id: string;
    title: string;
    description: string;
    status: ApprovalDecision;
    createdTimestamp: string;
    decisionTimestamp?: string;
    decisionUser?: MessageAuthor;
}

export interface MessageAuthor {
    role: 'SREAgent' | 'User';
    userId: string;
    displayName: string;
}

export enum ActionStatus {
    Pending = 'Pending',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Failed = 'Failed',
    All = 'All',
}

export interface Action {
    id: string;
    title: string;
    timeStamp: Date;
    status: ActionStatus;
}

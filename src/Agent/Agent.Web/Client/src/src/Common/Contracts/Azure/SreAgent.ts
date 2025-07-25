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

export enum AgentMode {
    autonomous = 'autonomous',
    review = 'review',
    /** renamed to "chat" (but back-compatible for now); double check usage in incident handlers (API: IncidentPlayground/filterFieldOptions) */
    readonly = 'readonly',
}

export enum AgentAccessLevel {
    low = 'Low',
    high = 'High',
}

export enum LowercaseAgentAccessLevel {
    low = 'low',
    high = 'high',
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
    accessLevel?: AgentAccessLevel;
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

export interface ApplicationInsightsConfiguration {
    appId: string;
    connectionString: string;
}

export interface LogConfiguration {
    logAnalyticsConfiguration: LogAnalyticsConfiguration;
    applicationInsightsConfiguration: ApplicationInsightsConfiguration;
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
    Icm = 'Icm',
    ServiceNow = 'ServiceNow',
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
    welcomeMessage = 'WelcomeMessage',
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
    lastReadTime?: string;
    agentMode?: string;
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

export interface MessageMetaData {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    title?: string;
}

export type ChatMessageError = 'PermissionDenied' | 'UnknownError';

export interface MessageContent {
    text: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    kubectlExecution?: KubectlExecution;
    isDailyReport?: boolean;
}

// ToDo: Replace this with interface Message extends MessageMetaData, MessageContent{} after shipping
// streaming message experience out. Right now let's keep the definition separate to avoid breaking changes.
export interface Message {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    text: string;
    title?: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    kubectlExecution?: KubectlExecution;
    isDailyReport?: boolean;
}

export interface KnowledgeGraphBuildStatus {
    isCrawling: boolean;
    hasCompletedInitialGraphCrawl: boolean;
    crawledCount: number;
    totalVisibleResources: number;
    properties: Record<string, any>;
    progressByResourceType: Record<string, CrawlProgress>;
}

export interface CrawlProgress {
    crawledCount: number;
    totalCount: number;
}

export interface AzCliExecution {
    id: string;
    command: string;
    description: string;
    status: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'PendingAuthorization';
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

export interface KubectlExecution {
    id: string;
    command: string;
    stdin?: string;
    description: string;
    status: 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled' | 'PendingAuthorization';
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
    Cancelled = 'Cancelled',
    PendingAuthorization = 'PendingAuthorization',
    Authorized = 'Authorized',
}

export interface Approval {
    id: string;
    title: string;
    description: string;
    status: ApprovalDecision;
    createdTimestamp: string;
    decisionTimestamp?: string;
    decisionUser?: MessageAuthor;
    oboTokenScope?: string;
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

export const SREAgentUserId = 'agent-default';

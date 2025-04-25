export interface Agent {
    provisioningState: string;
    agentEndpoint: string;
    runningState: string;
    vnetConfiguration?: VnetConfiguration;
    knowledgeGraphConfiguration?: KnowledgeGraphConfiguration;
    outboundConnectionConfiguration?: OutboundConnectionConfiguration;
    mcpServers?: string[];
    logConfiguration?: LogConfiguration;
    incidentManagementConfiguration?: IncidentManagementConfiguration | null;
}

export interface VnetConfiguration {
    subnetResourceId?: string;
    vNetGuid?: string;
}

export interface KnowledgeGraphConfiguration {
    identity?: string;
    managedResources?: string[];
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
}

export enum IncidentStatus {
    error = 'error',
    warning = 'warning',
    success = 'success',
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
    incidentStatus?: IncidentStatus;
    source?: ThreadSource;
}

export interface Message {
    id: string;
    timeStamp: string;
    author: MessageAuthor;
    text: string;
    approval?: Approval;
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

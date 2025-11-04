export interface Connector {
    name: string;
    dataConnectorType: string;
    /** Secret value - must be fetched through ListSecrets endpoints */
    dataSource?: string;
    keyVaultUri?: string;
    identity: string;
    source?: string;
}

export interface Agent {
    provisioningState: ProvisioningState;
    agentEndpoint: string;
    agentSpaceId?: string;
    runningState: string;
    powerState?: 'Running' | 'Stopped';
    vnetConfiguration?: VnetConfiguration;
    knowledgeGraphConfiguration?: KnowledgeGraphConfiguration;
    actionConfiguration?: ActionConfiguration;
    outboundConnectionConfiguration?: OutboundConnectionConfiguration;
    mcpServers?: string[];
    logConfiguration?: LogConfiguration;
    incidentManagementConfiguration?: IncidentManagementConfiguration | null;
    dashboardConfiguration: DashboardConfiguration;
    dataConnectors?: Connector[];
    upgradeChannel?: UpgradeChannel;
    monthlyAgentUnitLimit?: number;
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

export interface ApplicationInsightsConfiguration {
    appId: string;
    connectionString: string;
}

export interface LogConfiguration {
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
    None = 'None',
}

export enum UpgradeChannel {
    Stable = 'Stable',
    Preview = 'Preview',
}

export enum IncidentStatus {
    active = 'active',
    new = 'new',
    assigned = 'assigned',
    inProgress = 'in progress',
    acknowledged = 'acknowledged',
    mitigated = 'mitigated',
    triggered = 'triggered',
    closed = 'closed',
    resolved = 'resolved',
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

interface UsageName {
    localizedValue: string;
    value: 'TotalAgentUnits' | 'AgentUnits';
}

export interface MonthlyUsage {
    currentValue: number;
    limit: number;
    name: UsageName;
    unit: 'Count';
}

export interface DailyUsage {
    date: Date | string;
    name: UsageName;
    value: number;
}

export const SREAgentUserId = 'agent-default';

export type AgentPowerState = 'Running' | 'Stopped';

export interface SreAgentArgItem {
    id: string;
    name: string;
    location: string;
    type: string;
    subscriptionId: string;
    resourceGroup: string;
    agentSpaceId?: string | null;
    powerState?: AgentPowerState;
}

export enum ProvisioningState {
    InProgress = 'InProgress',
    Succeeded = 'Succeeded',
    Failed = 'Failed',
    Canceled = 'Canceled',
    Deleting = 'Deleting',
}

export enum AgentMode {
    Autonomous = 'autonomous',
    Review = 'review',
    ReadOnly = 'readonly',
}

export enum AgentAccessLevel {
    low = 'Low',
    high = 'High',
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

export interface DashboardConfiguration {
    grafanaUrl?: string;
    azureMonitorWorkspaceQueryEndpoint?: string;
    identity?: string;
    azureMonitorWorkspaceMetricsIngestionEndpoint?: string;
}

export interface DataConnector {
    name: string;
    dataConnectorType: string;
    /** Secret value - must be fetched through ListSecrets endpoints */
    dataSource?: string;
    keyVaultUri?: string;
    identity: string;
    source?: string;
}

export enum ModelProvider {
    Anthropic = 'Anthropic',
    MicrosoftFoundry = 'MicrosoftFoundry',
}

export interface Model {
    provider: ModelProvider;
    model?: string; // (NOTE (wangcynthia): optional for now, since for GA the user can only select provider.
}

export interface Agent {
    provisioningState: ProvisioningState;
    agentEndpoint: string;
    agentSpaceId?: string | null;
    runningState: string;
    vnetConfiguration?: VnetConfiguration;
    knowledgeGraphConfiguration?: KnowledgeGraphConfiguration;
    actionConfiguration?: ActionConfiguration;
    outboundConnectionConfiguration?: OutboundConnectionConfiguration;
    mcpServers?: string[];
    logConfiguration?: LogConfiguration;
    incidentManagementConfiguration?: IncidentManagementConfiguration | null;
    dashboardConfiguration: DashboardConfiguration;
    upgradeChannel?: UpgradeChannel;
    powerState?: AgentPowerState;
    defaultModel?: Model;
}

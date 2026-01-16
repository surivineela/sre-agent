export interface Identity {
    type: string;
    userAssignedIdentities?: Record<string, any>;
}

export interface ArmTemplate {
    $schema: string;
    contentVersion: string;
    parameters: Record<string, ArmTemplateParameter>;
    variables: Record<string, object>;
    resources: ArmTemplateResourceFragment<any>[];
}

export interface DeploymentFragment {
    mode: string;
    template: any;
}

export interface ArmTemplateParameter {
    type: string;
    allowedValues?: string[];
    minLength?: number;
    maxLength?: number;
    defaultValue?: any;
    metadata?: object;
}

export interface ArmTemplateResourceFragment<T> {
    apiVersion: string;
    name: string;
    type: string;
    kind?: string;
    tags?: Record<string, string>;
    location?: string;
    dependsOn?: string[];
    scope?: string;
    properties: T;
    resources?: ArmTemplateResourceFragment<any>[];
    sku?: object;
    identity?: Identity;
    resourceGroup?: string;
    subscriptionId?: string;
    copy?: {
        name: string;
        count: string;
    };
}

export enum ArmTemplateParameterName {
    SubscriptionId = 'subscriptionId',
    Location = 'location',
    ResourceGroupName = 'resourceGroupName',
}

export enum SreAgentParameterName {
    OidcUserIdentityName = 'oidcUserIdentity',
    ResourceGroups = 'resourceGroups',
    Subscriptions = 'subscriptions',
    WorkspaceName = 'workspaceName',
    WorkspaceSku = 'workspaceSku',
    AgentName = 'agentName',
    AccessLevel = 'accessLevel',
    AgentMode = 'agentMode',
    AzureBotName = 'azureBotName',
    AzureBotSku = 'azureBotSku',
    UserObjectId = 'userObjectId',
}

export enum AgentSpaceParameterName {
    AgentSpaceName = 'agentSpaceName',
    AgentSpaceDescription = 'agentSpaceDescription',
    AgentSpaceMaxCount = 'agentSpaceMaxCount',
}

export enum AppInsightsParameterName {
    AppInsightsName = 'appInsightsName',
    AppInsightsResourceId = 'appInsightsResourceId',
    AppInsightsApplicationType = 'appInsightsApplicationType',
    AppInsightsRequestSource = 'appInsightsRequestSource',
    WorkspaceName = 'workspaceName',
}

export enum ArmServiceType {
    Agents = 'Microsoft.App/agents',
    AgentSpaces = 'Microsoft.App/agentSpaces',
    BotServices = 'Microsoft.BotService/botServices',
    Site = 'Microsoft.Web/Sites',
    ServerFarm = 'Microsoft.Web/Serverfarms',
    Workspace = 'Microsoft.OperationalInsights/workspaces',
    SiteRbac = 'Microsoft.Authorization/roleAssignments',
    OidcUserIdentity = 'oidcUserIdentities',
    UserIdentity = 'Microsoft.ManagedIdentity/userAssignedIdentities',
    AppInsights = 'Microsoft.Insights/components',
    Deployments = 'Microsoft.Resources/deployments',
    AzureMonitorWorkspace = 'Microsoft.Monitor/accounts',
    DataCollectionRule = 'Microsoft.Insights/dataCollectionRules',
}

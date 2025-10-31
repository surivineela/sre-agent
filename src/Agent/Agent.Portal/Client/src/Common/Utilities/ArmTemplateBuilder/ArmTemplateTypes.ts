/**
 * Core type definitions and interfaces for ARM templates
 */

/**
 * Identity configuration for ARM resources
 */
export interface Identity {
    type: string;
    userAssignedIdentities?: Record<string, any>;
}

/**
 * Complete ARM template structure
 */
export interface ArmTemplate {
    $schema: string;
    contentVersion: string;
    parameters: Record<string, ArmTemplateParameter>;
    variables: Record<string, object>;
    resources: ArmTemplateResourceFragment<any>[];
}

/**
 * Deployment properties fragment
 */
export interface DeploymentFragment {
    mode: string;
    template: any;
}

/**
 * Template parameter definition
 */
export interface ArmTemplateParameter {
    type: string;
    allowedValues?: string[];
    minLength?: number;
    maxLength?: number;
    defaultValue?: any;
    metadata?: object;
}

/**
 * Generic ARM resource fragment
 */
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

/**
 * Standard ARM template parameter names
 */
export enum ArmTemplateParameterName {
    SubscriptionId = 'subscriptionId',
    Location = 'location',
    ResourceGroupName = 'resourceGroupName',
}

/**
 * SRE Agent specific parameter names
 */
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

/**
 * Application Insights parameter names
 */
export enum AppInsightsParameterName {
    AppInsightsName = 'appInsightsName',
    AppInsightsApplicationType = 'appInsightsApplicationType',
    AppInsightsRequestSource = 'appInsightsRequestSource',
    WorkspaceName = 'workspaceName',
}

/**
 * ARM service type identifiers
 */
export enum ArmServiceType {
    Agents = 'Microsoft.App/agents',
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

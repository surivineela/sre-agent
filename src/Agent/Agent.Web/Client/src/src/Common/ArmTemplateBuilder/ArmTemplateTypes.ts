import { Identity } from '../Contracts/Azure/Identity';

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
    administratorLogin = 'administratorLogin',
    administratorLoginPassword = 'administratorLoginPassword',
}

export enum ArmServiceType {
    DashboardGrafana = 'Microsoft.Dashboard/grafana',
    AzureMonitorWorkspace = 'Microsoft.Monitor/accounts',
    DataCollectionRule = 'Microsoft.Insights/dataCollectionRules',
}

export enum GrafanaParameterName {
    GrafanaName = 'grafanaName',
}

export enum AzureMonitorWorkspaceParameterName {
    WorkspaceName = 'workspaceName',
}

export enum DataCollectionRuleParameterName {
    DataCollectionRuleName = 'dataCollectionRuleName',
    AzureMonitorWorkspaceId = 'azureMonitorWorkspaceId',
}

import { ProvisioningState } from './SreAgent';

export interface AgentSpaceArgItem {
    id: string;
    name: string;
    location: string;
    type: string;
    subscriptionId: string;
    resourceGroup: string;
}

export interface AgentSpaceAllowedAction {
    actionName: string;
    extension: string;
    approvalRequired: boolean;
}

export interface GenevaActionsConfiguration {
    acisEndpoint?: string;
    clientId?: string;
    certificateSubjectName?: string;
    authenticationMode?: string;
    extensionName?: string;
    allowedActions?: AgentSpaceAllowedAction[];
    certificateSubjectAlternativeName?: string;
}

export interface AgentSpacePolicies {
    genevaActionsConfiguration?: GenevaActionsConfiguration;
}

export interface AgentSpace {
    provisioningState?: ProvisioningState;
    description?: string;
    /** Read-only: current number of agents in the space */
    currentAgentCount?: number;
    /** Maximum allowed agents in the space */
    maxAgentCount?: number;
    /** Resource IDs of member agents */
    memberAgents?: string[];
    policies?: AgentSpacePolicies;
    serviceTreeId?: string;
}

export interface AgentSpaceConnector {
    name: string;
    dataConnectorType: string;
    /** Secret value - only populated via ListSecrets endpoint */
    dataSource?: string;
    keyVaultUri?: string;
    /** Managed Identity resource ID */
    identity: string;
}

/**
 * Allowed action row for form editing (includes unique ID for table management)
 */
export interface AllowedActionRow {
    id: string;
    actionName: string;
    extension: string;
    approvalRequired: boolean;
}

/**
 * Form values for Agent Space creation
 */
export interface AgentSpaceCreateFormValues {
    subscriptionId: string;
    resourceGroupId: string;
    isResourceGroupNew: boolean;
    name: string;
    location: string;
    description: string;
    maxAgentCount: number;
    enableGenevaActions: boolean;
    endpoint: string;
    clientId: string;
    extensionName: string;
    allowedActions: AllowedActionRow[];
}

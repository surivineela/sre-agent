export interface AlertConfig {
    alertingId: string;
    incidentTitle: string;
    teamId: number;
    defaultHumanInterventionLoop: string;
}

export interface TeamConfig {
    teamId: string;
    teamName: string;
    routingId: string;
}

export interface AlertInfo {
    id: string;
    serviceName: string;
    serviceId: string;
    title: string;
    teamAssignedTo: string;
    teamId: number;
    severity?: number;
}

export interface IcmTeamInfo {
    icmServiceId: number;
    icmServiceName: string;
    icmTeamName: string;
    icmTeamId: number;
    teamPublicId: string;
}

export interface IcmService {
    id: number;
    name: string;
}

export interface IcmTeams {
    id: string;
    serviceId: number;
    teams: IcmTeam[];
    timestamp: number;
    datetime: string;
}

export interface IcmTeam {
    id: number;
    name: string;
    publicId: string;
}

export interface AgentDeployment {
    id: string;
    teamId: number;
    subscriptionId: string;
    resourceGroup: string;
    name: string;
    location: string;
}

// For ARM list responses
export interface ArmListResponse<T> {
    value: T[];
    nextLink?: string; // For pagination
}

// Updated Subscription interface
export interface Subscription {
    id: string; // Full ARM ID e.g., "/subscriptions/xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    subscriptionId: string; // GUID e.g., "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
    displayName: string;
    state?: string; // e.g., "Enabled", "Warned", "PastDue", "Disabled"
    // tenantId?: string; // Might be useful in some contexts
}

// Updated ResourceGroup interface
export interface ResourceGroup {
    id: string; // Full ARM ID e.g., "/subscriptions/.../resourceGroups/myRG"
    name: string;
    location: string;
    properties?: {
        provisioningState?: string; // e.g., "Succeeded", "Failed"
    };
    tags?: { [key: string]: string };
}

// Updated Location interface
export interface Location {
    id: string; // Full ARM ID e.g., "/subscriptions/.../locations/westus"
    name: string; // The short name for the location e.g., "westus"
    displayName: string; // The display name for the location e.g., "West US"
    regionalDisplayName?: string; // Regional display name e.g., "(US) West US"
    metadata?: {
        regionType?: string; // "Physical" or "Logical"
        regionCategory?: string; // "Recommended", "Other"
        geographyGroup?: string;
        longitude?: string;
        latitude?: string;
        physicalLocation?: string;
        pairedRegion?: Array<{ name: string, id: string }>;
    };
}

export interface IcmIncident {
    title: string;
    severity: number;
    state: "ACTIVE" | "MITIGATED" | "RESOLVED";
    id: number;
    // ISO Datetime string
    createdDate: string;
}

export interface GenerateInstructionsRequest {
    incidentIds: number[];
    customInstructions: string;
}

export interface GenerateInstructionsResponse {
    instructions: string[]; 
    // For debugging purposes
    troubleshootingGuide: string;
}

export interface AlertStreamPostBody {
    source: string;
    IncidentId: string;
    agentMode: string | null;
    customAlertConfig: any
}

export interface DeployAgentPostBody {
    resourceName: string,
    subscriptionId: string,
    resourceGroup: string,
    location: string
}
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

export interface AgentDeployment {
    id: string;
    teamId: number;
    subscriptionId: string;
    resourceGroup: string;
    name: string;
    location: string;
}

export interface Subscription {
    subscriptionId: string; 
    displayName: string
}

export interface ResourceGroup {
    name: string;
    location: string;
}

export interface Location {
    name: string;
    displayName: string;
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
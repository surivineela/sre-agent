export interface InstructionGenerationRequest {
    agentName: string;
    customInstructions: string;
    incidents: string[];
    tools: string[];
    existingInstructions?: string;
}

export interface InstructionGenerationResponse {
    agentName: string;
    generatedInstructions: string;
    incidents: string[];
    tools: string[];
}

export interface IncidentQueryRequest {
    keywords?: string[];
    durationInDays?: number;
    filter?: IncidentFilterDocumentPayload;
}

export interface IncidentFilterDocumentPayload {
    id?: string;
    impactedService?: string;
    priority?: string;
    incidentType?: string;
    alertId?: string;
    titleContains?: string;
}

export interface IIncidentDocument {
    createdAt: string;
    updatedAt: string;
    impactedServiceId: string;
    impactedServiceName: string;
    id: string;
    status: string;
    incidentType: string;
    priority: string;
    title: string;
    description: string;
    extractedKnowledge: string;
}

export interface ToolInfo {
    name: string;
    description: string;
    parameters: string[];
}

export interface IncidentHandler {
    id: string;
    name: string;
    description: string;
    incidentFilterId: string;
    incidentProcessingGuide: string[];
    tools: string[];
    incidents: string[];
    customInstructions: string;
}

export interface IncidentFilter {
    id: string;
    alertId: string;
    createdAt: string;
    updatedAt: string;
    documentType: string;
    impactedService: string;
    incidentType: string;
    isDeleted: boolean;
    isEnabled: boolean;
    partitionKey: string;
    priority: string;
    titleContains: string;
}

export interface IncidentFilterPayload {
    Id: string;
    ImpactedService?: string;
    Priority?: string;
    IncidentType?: string;
    AlertId?: string;
    TitleContains?: string;
}

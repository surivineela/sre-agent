import { AgentMode, IncidentManagementType, IncidentStatus } from './SreAgent';

/**
 * Defines the events that can trigger agent processing for an incident.
 * ICM provider only.
 */
export enum IncidentTriggerEvent {
    /** Default trigger. Fires when incident created OR transferred to team. */
    IncidentCreatedOrTransferred = 'IncidentCreatedOrTransferred',
    /** Fires when discussion entry added with @sreagent mention by current on-call. */
    DiscussionEntry = 'DiscussionEntry',
    /** Fires when incident state changes to Mitigated. */
    IncidentMitigated = 'IncidentMitigated',
    /** Fires when incident state changes from Mitigated/Resolved to Active. */
    IncidentReactivated = 'IncidentReactivated',
    /** Fires when incident state changes to Resolved. */
    IncidentResolved = 'IncidentResolved',
}

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
    pageSize?: number;
    pageNumber?: number; //1-based index
    statuses?: IncidentStatus[];
    searchTerm?: string;
}

export interface IncidentQueryResponse {
    items: IncidentDocument[];
    totalCount: number;
}

export interface IncidentFilterDocumentPayload {
    id?: string;
    impactedService?: string;
    priorities?: string[];
    incidentType?: string;
    alertId?: string;
    titleContains?: string;
    agentMode?: AgentMode;
    deepInvestigationEnabled?: boolean;
    handlingAgent?: string;
    owningTeamId?: string; // only for IcM
    createdBy?: string; // only for IcM
    monitorId?: string; // only for IcM
    /** List of trigger events that this filter responds to. ICM only. */
    triggers?: IncidentTriggerEvent[];
    /** List of handling agents for multi-agent parallel processing. ICM only (Phase 2). */
    handlingAgents?: string[];
}

export type IncidentDocumentType = 'ServiceNowIncident' | 'PagerDutyIncident' | 'IcmIncident' | 'AzureMonitorIncident';

export interface IncidentDocument {
    createdAt: string;
    updatedAt: string;
    impactedServiceId: string;
    impactedServiceName: string;
    id: string;
    alertId?: string;
    status: string;
    incidentType: string;
    priority: string;
    title: string;
    description: string;
    extractedKnowledge: string;
    documentType: IncidentDocumentType;
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
    priorities?: string[];
    titleContains: string;
    /** If no value, will be empty string */
    agentMode?: AgentMode;
    deepInvestigationEnabled?: boolean;
    handlingAgent?: string;
    owningTeamId?: string; // only for IcM
    /** List of trigger events that this filter responds to. ICM only. */
    triggers?: IncidentTriggerEvent[];
    /** List of handling agents for multi-agent parallel processing. ICM only (Phase 2). */
    handlingAgents?: string[];
    createdBy?: string; // only for IcM
    monitorId?: string; // only for IcM
}

export interface TestHandlerPayload {
    incidentId?: string;
    severity?: string;
    isTest?: boolean;
    incidentHandler?: IncidentHandler;
    incidentFilter?: IncidentFilterDocumentPayload;
}

export interface TestHandlerResponse {
    statusCode: number;
    message?: string;
    incidentId: string;
    threadId?: string;
}

export interface IncidentPlatformTypeResponse {
    incidentPlatformType: IncidentManagementType;
}

export type WithSelection<T> = T & {
    selected: boolean;
};

export interface IncidentTeamSearchResponse {
    id: number;
    name: string;
    description: string;
    teamPublicId: string;
    tenant?: {
        id: number;
        name: string;
        tenantPublicId: string;
        description: string;
    };
}

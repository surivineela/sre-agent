import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';

export interface IncidentHandlerCreateFormValues {
    filterName?: string;
    incidentType?: string;
    impactedService?: string;
    priority?: string;
    titleContains?: string;
    agentMode?: AgentMode;
    owningTeamId?: string;
    createdBy?: string;
    monitorId?: string;

    incidentIds?: string[];
    customInstructions?: string;
    toolNames?: string[];
    incidentProcessingGuide?: string;

    useCustomHandler?: boolean;
    deepInvestigationEnabled?: boolean;
    includePastIncidents?: boolean;
}

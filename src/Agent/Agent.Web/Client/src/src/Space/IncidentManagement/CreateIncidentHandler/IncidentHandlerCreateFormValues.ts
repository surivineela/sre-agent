import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';

export interface IncidentHandlerCreateFormValues {
    filterName?: string;
    incidentType?: string;
    impactedService?: string;
    priority?: string;
    titleContains?: string;
    agentMode?: AgentMode;

    incidentIds?: string[];
    customInstructions?: string;
    toolNames?: string[];
    incidentProcessingGuide?: string;

    useCustomHandler?: boolean;
    includePastIncidents?: boolean;
}

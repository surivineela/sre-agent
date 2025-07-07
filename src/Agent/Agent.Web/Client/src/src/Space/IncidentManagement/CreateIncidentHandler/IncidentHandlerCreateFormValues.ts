export interface IncidentHandlerCreateFormValues {
    filterName?: string;
    incidentType?: string;
    impactedService?: string;
    priority?: string;
    titleContains?: string;

    incidentIds?: string[];
    customInstructions?: string;
    toolNames?: string[];
    incidentProcessingGuide?: string;

    useCustomHandler?: boolean;
    includePastIncidents?: boolean;
}

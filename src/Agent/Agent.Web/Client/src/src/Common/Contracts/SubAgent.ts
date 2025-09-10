export interface SubAgent {
    agentName: string;
    logicAppWorkflowId: string;
    workflowConfiguration: {
        connections: Record<string, any>;
        workflowDefinition: Record<string, any>;
    };
    agentCardUrl: string;
    authType: string;
}

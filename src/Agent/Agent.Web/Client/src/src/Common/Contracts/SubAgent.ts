export interface SubAgent {
    agentCardUrl: string;
    agentName: string;
    authType: string;
    logicAppWorkflowDefinition: Record<string, unknown>;
    logicAppWorkflowId: string;
}

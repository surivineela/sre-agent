import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { McpConnection } from '../../Graph/ExtendedAgentCreationDialog/api/mcpConnectionsApi';

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
    handlingAgent?: string;

    incidentIds?: string[];
    customInstructions?: string;
    toolNames?: string[];
    incidentProcessingGuide?: string;

    useCustomHandler?: boolean;
    deepInvestigationEnabled?: boolean;
    includePastIncidents?: boolean;

    isIncidentTriggerWithLearnings?: boolean;

    subagentName?: string;
    subagentInstructions?: string;
    subagentHandoffInstructions?: string;
    subagentHandoffSubagents?: string[];
    subagentAutonomyLevel?: AgentMode;
    subagentToolNames?: string[];

    extendedAgents?: ExtendedAgent[];
    systemTools?: SystemTool[];
    extendedTools?: ExtendedTool[];
    mcpConnections?: McpConnection[];
}

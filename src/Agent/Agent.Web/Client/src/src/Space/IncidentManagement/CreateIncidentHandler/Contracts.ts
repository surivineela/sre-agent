import { IncidentFilter } from '../../../Common/Contracts/Azure/IncidentHandler';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { McpConnection } from '../../Graph/ExtendedAgentCreationDialog/api/mcpConnectionsApi';

export const HANDLER_TOOL_LIMIT = 30;

export enum TimeDuration {
    Last1Day = 1,
    Last7Days = 7,
    Last15Days = 15,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90,
}

export enum TimeDurationKey {
    Last1Day = 'last1Day',
    Last7Days = 'last7Days',
    Last15Days = 'last15Days',
    Last30Days = 'last30Days',
    Last60Days = 'last60Days',
    Last90Days = 'last90Days',
}

export enum IncidentTableFieldNames {
    Priority = 'priority',
    CreatedAt = 'createdAt',
    Title = 'title',
    Id = 'id',
    Status = 'status',
}

export enum ToolTableFieldNames {
    Name = 'name',
    Description = 'description',
}

export type FilterMode = 'create' | 'edit';
export type HandlerMode = 'create' | 'edit' | 'quickEdit';
export type OperationStatus = 'inprogress' | 'succeeded' | 'failed';

export interface HandlerCreateOrEditInfo {
    filter?: IncidentFilter;
    handlerId?: string;
    quickEdit?: boolean;
    subAgentTriggerInfo?: {
        preSelectedAgent?: string;
        agents: string[];
    };
    incidentTriggerWithLearningsInfo?: {
        mcpConnections?: McpConnection[];
        extendedAgents: ExtendedAgent[];
        extendedTools?: ExtendedTool[];
        systemTools?: SystemTool[];
    };
}

export enum IncidentsListColumnKey {
    incidentId = 'incidentId',
    title = 'title',
    priority = 'priority',
    incidentStatus = 'incidentStatus',
    agentStatus = 'agentStatus',
    createdTimestamp = 'createdTimestamp',
    impactedService = 'impactedService',
    handler = 'handler',
}

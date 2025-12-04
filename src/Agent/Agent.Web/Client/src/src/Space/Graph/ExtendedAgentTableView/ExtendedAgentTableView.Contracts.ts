import { ExtendedAgent, ExtendedAgentNodeType, ExtendedTool, ExtendedTrigger, Skill } from '../../Contracts/ExtendedAgentGraph';

export type AgentItem = {
    name: string;
    trigger: string;
    tools: string;
    systemToolsCount: number;
    kustoToolsCount: number;
    handoff: string;
    data: ExtendedAgent;
};

export type IncidentTriggerItem = {
    name: string;
    status: string;
    subAgent: string;
    severity: string;
    incidentType: string;
    impactedService: string;
    description: string;
    titleContains: string;
    data: ExtendedTrigger;
};

export type ScheduledTaskItem = {
    id: string;
    name: string;
    status: string;
    schedule: string;
    completedRuns: number;
    data: ExtendedTrigger;
};

export type KustoToolItem = {
    name: string;
    connector: string;
    database: string;
    parameters: string;
    connectorStatus: (typeof CONNECTOR_STATUS)[keyof typeof CONNECTOR_STATUS];
    data: ExtendedTool;
};

export type SkillItem = {
    name: string;
    description: string;
    toolsCount: number;
    hasContent: boolean;
    filesCount: number;
    data: Skill;
};

export type TableViewTabValue = 'agents' | 'incidentTriggers' | 'scheduledTasks' | 'kustoTools' | 'skills';

export type BaseTableItem = {
    name: string;
    [key: string]: any;
};

export interface EntityTableProps {
    openInfoPanel?: (itemName: string, itemType: ExtendedAgentNodeType) => void;
    refresh: () => void;
    lastUpdated?: string;
    isLoading?: boolean;
}

export interface EntityToolbarProps extends EntityTableProps {
    searchText?: string;
    setSearchText: (searchText: string) => void;
    statusFilter?: any;
    setStatusFilter?: (statusFilter: any) => void;
}

export const STATUS = {
    ACTIVE: 'active',
    DISABLED: 'disabled',
    COMPLETED: 'completed',
} as const;

export const CONNECTOR_STATUS = {
    CONNECTED: 'connected',
    NOT_CONNECTED: 'not-connected',
} as const;

export const EMPTY_DISPLAY = '-' as const;

export const ALL_FILTER_KEY = 'all';

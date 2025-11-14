import { createContext } from 'react';
import { EntityType } from '../Graph/ExtendedAgentCreationDialog/types';

// Extended Agent Types
export type ExtendedAgent = {
    name: string;
    instructions: string;
    handoffDescription?: string;
    handoffs?: string[];
    tools?: string[];
    systemTools?: string[];
    mcpTools?: string[];
    connectors?: string[];
    allowParallelToolCalls?: boolean;
    agentsAsTools?: AgentAsToolReference[];
    maxReflectionCount?: number;
    criticPromptPath?: string;
    criticOnHandOff?: boolean;
    commonPrompts?: string[];
    commonTools?: string[];
    temperature?: number;
    llmModelName?: string;
    agentType?: 'Autonomous' | 'Orchestrator' | 'Activity';
    outputType?: string;
    metaAgentOverride?: boolean;
    enableMemory?: boolean;
    metadata?: Record<string, any>;
};

export type AgentAsToolReference = {
    agentName: string;
    toolName: string;
    toolDescription: string;
    inputDescription: string;
};

// Tool Types
export type KustoDisplayOptions = {
    showTable?: boolean;
    showChart?: boolean;
    maxTableRows?: number;
    maxChartPoints?: number;
    chartTitle?: string;
    xField?: string;
    seriesFields?: string[];
};

export type ExtendedTool = {
    name: string;
    type: string;
    connector?: string;
    description?: string;
    parameters?: ToolParameter[];
    attributes?: string[];
    metadata?: Record<string, any>;
    // Kusto tool specific
    mode?: string;
    function?: string;
    query?: string;
    file?: string;
    database?: string;
    clusterUri?: string;
    regionalClusterGroups?: Record<string, string[]>;
    displayOptions?: KustoDisplayOptions;
    // Link tool specific
    template?: string;
};

export type ToolParameter = {
    name: string;
    type: string;
    description?: string;
    required?: boolean;
    mapTo?: string;
    target?: string;
    value?: string | number | boolean | null;
};

// Connector Types
export type ExtendedConnector = {
    name: string;
    description?: string;
    type?: string;
    [key: string]: any;
};

export type ExtendedTrigger = {
    name: string;
    description?: string;
    type: 'incident' | 'scheduled';
    agentName?: string;
    subAgent?: string;
    status?: 'Active' | 'Paused' | 'Disabled';
    priority?: string;
    incidentType?: string;
    severity?: string;
    service?: string;
    impactedService?: string;
    titleContains?: string;
    cronExpression?: string;
    schedule?: string;
    timezone?: string;
    createdAt?: string;
    enabled?: boolean;
    data?: any;
    [key: string]: any;
};

export type ConnectorAuth = {
    type?: string;
    [key: string]: any;
};

// Paginated Response Types
export type PaginatedResponse<T> = {
    data: T[];
    pageIndex: number;
    totalPages: number;
    pageSize: number;
    totalCount: number;
    hasPreviousPage: boolean;
    hasNextPage: boolean;
};

// System Tool Types
export type SystemTool = {
    name: string;
    category: string;
    resourceType: string;
    pluginName: string;
    description?: string;
    parameters?: string[];
    isIncidentHandlerTool?: boolean;
    incidentHandlerPlatform?: string;
};

// Graph Node Types
export enum ExtendedAgentNodeType {
    Agent = 'AGENT',
    Tool = 'TOOL',
    SystemTool = 'SYSTEM_TOOL',
    Connector = 'CONNECTOR',
    Trigger = 'TRIGGER',
}

export type ExtendedAgentGraphNode = {
    id: string;
    name: string;
    type: ExtendedAgentNodeType;
    agentType?: 'Autonomous' | 'Orchestrator' | 'Activity';
    toolType?: string;
    connectorType?: string;
    triggerType?: 'incident' | 'scheduled';
    data?: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger | SystemTool;
};

export type ExtendedAgentGraphEdge = {
    source: string;
    target: string;
    sourceType: EntityType;
    targetType: EntityType;
};

export type TriggerQuickAction = 'editTrigger';

export type AgentQuickAction =
    | 'addIncidentTrigger'
    | 'addScheduledTask'
    | 'addHandoffSourceExistingAgent'
    | 'addHandoffTargetExistingAgent'
    | 'addHandoff'
    | 'addTool'
    | 'createHandoffSourceAgent'
    | 'createHandoffTargetAgent'
    | 'createAgent'
    | 'editAgent'
    | 'createTool';

export enum ExtendedAgentGraphView {
    Grid = 'grid',
    Visual = 'visual',
}

// Graph Context
interface ExtendedAgentGraphContextProps {
    selectedNode?: ExtendedAgentGraphNode;
    setSelectedNode: (_?: ExtendedAgentGraphNode) => void;
    expandInfoPanel: () => void;
    hoveredNodeId?: string;
    hoverNode: (nodeId: string) => void;
    unHoverNode: () => void;
    nodesToHighlight: string[];
    edgesToHighlight: string[];
    openRelationshipDialog?: (agentName: string) => void;
    triggerAgentQuickAction: (agentName: string, action: AgentQuickAction) => void;
    triggerTriggerQuickAction: (triggerName: string, action: TriggerQuickAction) => void;
    onEntitySelect: (anchorEntity?: ExtendedAgentAnchorEntity | undefined) => void;
    onViewChange: (viewType: ExtendedAgentGraphView) => void;
}

export const ExtendedAgentGraphContext = createContext<ExtendedAgentGraphContextProps>({
    setSelectedNode: (_?: ExtendedAgentGraphNode) => {},
    expandInfoPanel: () => {},
    hoverNode: () => {},
    unHoverNode: () => {},
    nodesToHighlight: [],
    edgesToHighlight: [],
    openRelationshipDialog: () => {},
    triggerAgentQuickAction: () => {},
    triggerTriggerQuickAction: () => {},
    onEntitySelect: () => {},
    onViewChange: () => {},
});

// Node Size Configuration
export class ExtendedAgentNodeSize {
    static readonly agentWidth = 320;
    static readonly agentHeight = 118;
    static readonly toolWidth = 320;
    static readonly toolHeight = 40;
    static readonly connectorWidth = 320;
    static readonly connectorHeight = 118;
    static readonly triggerWidth = 320;
    static readonly triggerHeight = 118;
}

export type ExtendedAgentAnchorEntity = {
    entityType: 'Agent' | 'Trigger';
    entityName: string;
};

export interface PromptImprovementRequest {
    prompt: string;
}

export interface PromptImprovementResponse {
    improvedPrompt: string;
    warnings: string[];
    suggestions: string[];
    handoffDescription?: string;
}

export const INFO_PANEL_MIN_WIDTH = 360;
export const INFO_PANEL_MAX_WIDTH = 720;
export const INFO_PANEL_DEFAULT_WIDTH = 600;

import { createContext } from 'react';

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
    status?: 'Active' | 'Paused' | 'Disabled';
    priority?: string;
    incidentType?: string;
    cronExpression?: string;
    timezone?: string;
    createdAt?: string;
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

// Graph Edge Types
export enum ExtendedAgentRelationType {
    UsesTool = 'USES_TOOL',
    UsesSystemTool = 'USES_SYSTEM_TOOL',
    ToolUsesConnector = 'TOOL_USES_CONNECTOR',
    AgentAsTool = 'AGENT_AS_TOOL',
    HandoffTo = 'HANDOFF_TO',
    TriggerStartsAgent = 'TRIGGER_STARTS_AGENT',
    MetaAgentConnectsTo = 'META_AGENT_CONNECTS_TO',
}

export type ExtendedAgentGraphEdge = {
    source: string;
    target: string;
    label?: string;
    relationType: ExtendedAgentRelationType;
};

export type AgentQuickAction = 'addIncidentTrigger' | 'addHandoff' | 'addTool' | 'createAgent' | 'createTool';

// Graph Context
interface ExtendedAgentGraphContextProps {
    selectedNode?: ExtendedAgentGraphNode;
    setSelectedNode: (_?: ExtendedAgentGraphNode) => void;
    hoveredNodeId?: string;
    hoverNode: (nodeId: string) => void;
    unHoverNode: () => void;
    nodesToHighlight: string[];
    edgesToHighlight: string[];
    openRelationshipDialog?: (agentName: string) => void;
    triggerAgentQuickAction?: (agentName: string, action: AgentQuickAction) => void;
}

export const ExtendedAgentGraphContext = createContext<ExtendedAgentGraphContextProps>({
    setSelectedNode: (_?: ExtendedAgentGraphNode) => {},
    hoverNode: () => {},
    unHoverNode: () => {},
    nodesToHighlight: [],
    edgesToHighlight: [],
    openRelationshipDialog: () => {},
    triggerAgentQuickAction: () => {},
});

// Node Size Configuration
export class ExtendedAgentNodeSize {
    static readonly agentWidth = 320;
    static readonly agentHeight = 118;
    static readonly toolWidth = 320;
    static readonly toolHeight = 40;
    static readonly connectorWidth = 264; // 220 * 1.2
    static readonly connectorHeight = 108; // 90 * 1.2
    static readonly triggerWidth = 320;
    static readonly triggerHeight = 118;
}

// Filter Types
export type ExtendedAgentFilters = {
    agentName?: string;
    agentType?: 'Autonomous' | 'Orchestrator' | 'Activity' | 'All';
    toolType?: string;
    triggerType?: 'incident' | 'scheduled' | 'All';
    searchQuery?: string;
};

import { createContext } from 'react';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { McpTool } from '../Graph/ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { EntityType } from '../Graph/ExtendedAgentCreationDialog/types';

// Skill Types
export type SkillFile = {
    fileName: string;
    filePath: string;
    content: string;
};

export type Skill = {
    name: string;
    description?: string;
    tools?: string[];
    skillMdContent?: string;
    additionalFiles?: SkillFile[];
};

export type SkillGroupData = {
    skillCount: number;
    skills: Skill[];
};

// Toolbox Types
export type ToolboxToolItem = {
    tool: ExtendedTool | SystemTool;
    connector?: ExtendedConnector;
    isSystemTool: boolean;
};

export type ToolboxData = {
    agentName: string;
    toolCount: number;
    tools: ToolboxToolItem[];
};

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
    enableVanillaMode?: boolean;
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
    toolMode?: string;
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
    // Python function tool specific
    functionCode?: string;
    timeoutSeconds?: number;
    dependencies?: string[];
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
    executionCount?: number;
    id?: string;
    data?: any;
    agentMode?: AgentMode;
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
    Skill = 'SKILL',
    SkillGroup = 'SKILL_GROUP',
    Toolbox = 'TOOLBOX',
}

export type ExtendedAgentGraphNode = {
    id: string;
    name: string;
    type: ExtendedAgentNodeType;
    agentType?: 'Autonomous' | 'Orchestrator' | 'Activity';
    toolType?: string;
    connectorType?: string;
    triggerType?: 'incident' | 'scheduled';
    isLastInGroup?: boolean;
    data?: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger | SystemTool | Skill | SkillGroupData | ToolboxData;
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
    | 'createTool'
    | 'createPythonTool';

export enum ExtendedAgentGraphView {
    Grid = 'grid',
    Visual = 'visual',
    Playground = 'playground',
}

// Graph Context
interface ExtendedAgentGraphContextProps {
    selectedNodeId?: string;
    setSelectedNodeId: (_?: string) => void;
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
    hasSkills: boolean;
    isSkillGroupExpanded?: boolean;
    toggleSkillGroupExpanded?: () => void;
    expandedToolboxes: Set<string>;
    toggleToolboxExpanded: (agentName: string) => void;
    setPlaygroundEntity: React.Dispatch<React.SetStateAction<PlaygroundEntity | undefined>>;
}

export const ExtendedAgentGraphContext = createContext<ExtendedAgentGraphContextProps>({
    setSelectedNodeId: (_?: string) => {},
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
    hasSkills: false,
    isSkillGroupExpanded: false,
    toggleSkillGroupExpanded: () => {},
    expandedToolboxes: new Set<string>(),
    toggleToolboxExpanded: () => {},
    setPlaygroundEntity: () => {},
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
    static readonly skillWidth = 320;
    static readonly skillHeight = 40;
    static readonly skillGroupWidth = 320;
    static readonly skillGroupHeight = 80;
    static readonly toolboxWidth = 280;

    static readonly toolsBasePadding = 20;
    static readonly toolsExpandLinkHeight = 28;
    static readonly toolsCollapsedMaxRows = 5;
    static readonly toolsExpandedMaxRows = 10;
    static readonly toolsRowHeight = 48;
    static readonly toolsRowGap = 4;

    // Height calculation for collapsed toolbox: padding + (tool height + gap) * count + collapse link
    static getCollapsedToolboxHeight(toolCount: number): number {
        const expandLinkHeight = toolCount <= this.toolsCollapsedMaxRows ? 0 : this.toolsExpandLinkHeight; // expand link row
        const rowCount = Math.min(toolCount, this.toolsCollapsedMaxRows);
        return this.toolsBasePadding * 2 + rowCount * this.toolsRowHeight + Math.max(0, rowCount - 1) * this.toolsRowGap + expandLinkHeight;
    }

    // Height calculation for expanded toolbox tools container: (tool height + gap) * count
    static getExpandedToolsContainerHeight(toolCount: number): number {
        const rowCount = Math.min(toolCount, this.toolsExpandedMaxRows);
        return rowCount * this.toolsRowHeight + Math.max(0, rowCount - 1) * this.toolsRowGap;
    }

    // Height calculation for expanded toolbox: base padding + (tool height + gap) * count + collapse link
    static getExpandedToolboxHeight(toolCount: number): number {
        const collapseLinkHeight = toolCount <= this.toolsCollapsedMaxRows ? 0 : this.toolsExpandLinkHeight; // collapse link row
        const rowCount = Math.min(toolCount, this.toolsExpandedMaxRows);
        return (
            this.toolsBasePadding * 2 + rowCount * this.toolsRowHeight + Math.max(0, rowCount - 1) * this.toolsRowGap + collapseLinkHeight
        );
    }

    // Height calculation for expanded skill group: base padding + (skill height + gap) * count + collapse link
    static getExpandedSkillGroupHeight(skillCount: number): number {
        const basePadding = 40; // top/bottom padding
        const skillCardHeight = 48; // skill card height
        const gap = 8; // gap between cards
        const collapseHeight = 32; // collapse link row
        return basePadding + skillCount * skillCardHeight + (skillCount - 1) * gap + collapseHeight;
    }
}

export type ExtendedAgentAnchorEntity = {
    entityType: 'Agent' | 'Trigger';
    entityName: string;
};

interface PlaygroundAgentEntity {
    entityType: 'Agent';
    entity: ExtendedAgent;
}

interface PlaygroundSystemToolEntity {
    entityType: 'SystemTool';
    entity: SystemTool;
}

interface PlaygroundExtendedToolEntity {
    entityType: 'ExtendedTool';
    entity: ExtendedTool;
}

interface PlaygroundMcpToolEntity {
    entityType: 'McpTool';
    entity: McpTool;
}

export type PlaygroundEntity = PlaygroundAgentEntity | PlaygroundSystemToolEntity | PlaygroundExtendedToolEntity | PlaygroundMcpToolEntity;

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

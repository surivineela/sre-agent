import { ChatTelemetrySnapshot } from '../../../Contracts/Activities';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { ToolsPickerProps } from '../../Common/ToolsPicker/ToolsPicker';
import { UseToolsPickerReturn } from '../../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { UseHandoffAgentsReturn } from './Hooks/useHandoffAgents';
import { UseImprovementsAndSuggestionsReturn } from './Hooks/useImprovementsAndSuggestions';

export interface AgentPlaygroundFormValues {
    agentName: string;
    instructions: string;
    handoffInstructions: string;
    handoffSubagents: string[];
    tools: string[];
    mcpTools: string[];
    enableMemory?: boolean;
    enableVanillaMode?: boolean;
}

export interface AgentPlaygroundProps extends Omit<AgentPlaygroundFormikProps, 'excludedHandoffAgent' | 'mode' | 'setMode'> {
    refresh: (selectedAgent?: string) => void;
    agent: ExtendedAgent;
}

export type AgentPlaygroundMode = 'edit' | 'test';

export interface AgentPlaygroundFormikProps {
    agent: ExtendedAgent;
    agents?: ExtendedAgent[];
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    excludedHandoffAgent?: string;
    additionalHandoffAgents?: string[];
    isExistingAgent?: boolean;
    existingAgentGuid?: string;
    isOverrideScenario?: boolean;
}

export interface FormViewProps {
    disableControls?: boolean;
    handoffAgentsHook: UseHandoffAgentsReturn;
    improvementsAndSuggestionsHook: UseImprovementsAndSuggestionsReturn;
    showSuggestionsArea: boolean;
    setShowSuggestionsArea: (value: boolean) => void;
    toolsPickerHook: UseToolsPickerReturn;
    isExistingAgent?: boolean;
    isOverrideScenario?: boolean;
}

export interface YamlViewProps {
    yamlContent: string;
    handleYamlChange: (value: string | undefined) => void;
    disabled: boolean;
}

export interface YamlDiffViewProps {
    yamlContent: string;
    originalYamlContent: string;
}

export interface TestPanelProps {
    mode: AgentPlaygroundMode;
    agentName: string | undefined;
    threadId: string | undefined;
    restartTest: () => void;
    threadAutoTerminated: boolean;
    testStarted: boolean;
    addThread: (threadId: string) => void;
    selectThread: (threadId: string | null) => void;
    chatKey: string | undefined;
    onTelemetryUpdate?: (snapshot: ChatTelemetrySnapshot) => void;
    hidden?: boolean;
}

export interface SuggestionsAreaProps {
    isLoading: boolean;
    suggestions: string[] | undefined;
    warnings: string[] | undefined;
    improvedPrompt: string | undefined;
    handoffDescription: string | undefined;
}

export interface ToolsPanelProps extends ToolsPickerProps {
    close: () => void;
}

// Evaluation related types
export type QualityStatus = 'notAnalyzed' | 'running' | 'analyzed';

export type QualitySubscore = {
    id: string;
    label: string;
    score: number;
    evidence: string;
};

export type QualityFindingPayload =
    | { type: 'instructions'; addition: string }
    | { type: 'tool'; toolName: string; action: 'add' | 'update'; description?: string }
    | { type: 'newTool'; toolName: string; description?: string }
    | { type: 'prompt-rewrite'; fullPromptRewrite?: string }
    | { type: 'promptPatch'; patch: string };

export type QualityFinding = {
    id: string;
    title: string;
    rationale: string;
    expectedLift: number;
    impactLabel: string;
    autoApply: boolean;
    patch?: string;
    shortDiff?: string;
    payload?: QualityFindingPayload;
    toolHint?: string;
    safetyNote?: string;
};

export type QualityResult = {
    overallScore: number;
    evidence: string;
    hint: string;
    subScores: QualitySubscore[];
    findings: QualityFinding[];
};

export const PREVIEW_UPDATE_BADGE_TIMEOUT = 6000;

export const RelativeTimeCutoffs = {
    oneMinute: 60,
    oneHour: 3600,
    oneDay: 86400,
};
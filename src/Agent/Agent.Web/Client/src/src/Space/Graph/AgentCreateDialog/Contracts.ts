import { ExtendedAgent, ExtendedTool, Skill, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { ToolsPickerProps } from '../Common/ToolsPicker/ToolsPicker';
import { UseToolsPickerReturn } from '../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { UseHandoffAgentsReturn } from './Hooks/useHandoffAgents';
import { UseImprovementsAndSuggestionsReturn } from './Hooks/useImprovementsAndSuggestions';

export interface AgentCreateFormValues {
    agentName: string;
    instructions: string;
    handoffInstructions: string;
    handoffSubagents: string[];
    tools: string[];
    mcpTools: string[];
    enableMemory?: boolean;
    enableVanillaMode?: boolean;
    enableSkills?: boolean;
    allowedSkills?: string[];
}

export type PanelType = 'tools' | 'suggestions' | 'test' | undefined;

interface AgentCreateInfoWithHandoff {
    mode: 'createSource' | 'createTarget';
    agent: ExtendedAgent;
}

interface AgentCreateInfoWithoutHandoff {
    mode: 'create';
    agent: undefined;
}

interface MetaAgentCreateInfo {
    mode: 'createMetaAgent';
    agent: undefined;
}

interface AgentEditInfo {
    mode: 'edit';
    agent: ExtendedAgent;
}

export type AgentCreateOrEditInfo = AgentCreateInfoWithHandoff | AgentCreateInfoWithoutHandoff | MetaAgentCreateInfo | AgentEditInfo;

export interface AgentCreateDialogProps extends Omit<AgentCreateDialogFormikProps, 'excludedHandoffAgent'> {
    refresh: (selectedAgent?: string) => void;
    agentCreateOrEditInfo?: AgentCreateOrEditInfo;
}

export interface AgentCreateDialogFormikProps {
    onDismiss: () => void;
    agents?: ExtendedAgent[];
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    mcpConnections?: McpConnection[];
    skills?: Skill[];
    excludedHandoffAgent?: string;
    additionalHandoffAgents?: string[];
    isEditScenario?: boolean;
    existingAgentGuid?: string;
    isOverrideScenario?: boolean;
    submissionError?: string;
    setSubmissionError?: (error: string | undefined) => void;
}

export interface FormViewProps {
    disableControls?: boolean;
    handoffAgentsHook: UseHandoffAgentsReturn;
    improvementsAndSuggestionsHook: UseImprovementsAndSuggestionsReturn;
    toolsPickerHook: UseToolsPickerReturn;
    openedPanel: PanelType;
    openPanel: (panel: PanelType) => void;
    closePanel: () => void;
    isEditScenario?: boolean;
    isOverrideScenario?: boolean;
    testPanelProps: TestPanelProps;
    skills?: Skill[];
}
export interface YamlViewProps {
    yamlContent: string;
    handleYamlChange: (value: string | undefined) => void;
    disabled: boolean;

    openedPanel: PanelType;
    testPanelProps: TestPanelProps;
}

export interface TestPanelProps {
    agentName: string | undefined;
    threadId: string | undefined;
    restartTest: () => void;
    threadAutoTerminated: boolean;
    testStarted: boolean;
    addThread: (threadId: string) => void;
    selectThread: (threadId: string | null) => void;
    chatKey: string | undefined;
    onClose: () => void;
}

export interface SuggestionsPanelProps {
    close: () => void;
    isLoading: boolean;
    suggestions: string[] | undefined;
    warnings: string[] | undefined;
    improvedPrompt: string | undefined;
    handoffDescription: string | undefined;
}

export interface ToolsPanelProps extends ToolsPickerProps {
    close: () => void;
}

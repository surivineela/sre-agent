import { ExtendedAgent, ExtendedTool, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { ToolsPickerProps } from '../Common/ToolsPicker/ToolsPicker';
import { UseToolsPickerReturn } from '../Common/ToolsPicker/useToolsPicker';
import { UseHandoffAgentsReturn } from './Hooks/useHandoffAgents';
import { UseImprovementsAndSuggestionsReturn } from './Hooks/useImprovementsAndSuggestions';

export interface AgentCreateFormValues {
    agentName: string;
    instructions: string;
    handoffInstructions: string;
    handoffSubagents: string[];
    tools: string[];
}

export type PanelType = 'tools' | 'suggestions' | undefined;

interface AgentCreateInfoWithHandoff {
    mode: 'createSource' | 'createTarget';
    agent: ExtendedAgent;
}

interface AgentCreateInfoWithoutHandoff {
    mode: 'create';
    agent: undefined;
}

interface AgentEditInfo {
    mode: 'edit';
    agent: ExtendedAgent;
}

export type AgentCreateOrEditInfo = AgentCreateInfoWithHandoff | AgentCreateInfoWithoutHandoff | AgentEditInfo;

export interface AgentCreateDialogProps extends Omit<AgentCreateDialogFormikProps, 'excludedHandoffAgent'> {
    refresh: (selectedAgent?: string) => void;
    agentCreateOrEditInfo?: AgentCreateOrEditInfo;
}

export interface AgentCreateDialogFormikProps {
    onDismiss: () => void;
    agents?: ExtendedAgent[];
    existingTools?: ExtendedTool[];
    systemTools?: SystemTool[];
    excludedHandoffAgent?: string;
    isEditScenario?: boolean;
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
}

export interface YamlViewProps {
    yamlContent: string;
    handleYamlChange: (value: string | undefined) => void;
    disabled: boolean;
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

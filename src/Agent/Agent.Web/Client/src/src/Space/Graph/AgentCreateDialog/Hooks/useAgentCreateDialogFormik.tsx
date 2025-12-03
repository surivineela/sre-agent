import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Guid } from '../../../../Common/Helpers/Guid';
import { ExtendedAgent, ExtendedTool, PromptImprovementResponse, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { tryParseAgentYaml } from '../../../Playground/PlaygroundYamlUtils';
import { useToolsPicker } from '../../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { buildAgentConfigurationYaml } from '../../ExtendedAgentYamlUtils';
import { AgentCreateFormValues, PanelType } from '../Contracts';
import { useHandoffAgents } from '../Hooks/useHandoffAgents';
import { useImprovementsAndSuggestions } from '../Hooks/useImprovementsAndSuggestions';

export enum TabNames {
    Form = 'form',
    Yaml = 'yaml',
}

export const useAgentCreateDialogFormik = (
    agents: ExtendedAgent[] | undefined,
    existingTools: ExtendedTool[] | undefined,
    systemTools: SystemTool[] | undefined,
    mcpConnections: McpConnection[] | undefined,
    excludedHandoffAgent: string | undefined,
    additionalHandoffAgents: string[] | undefined,
    isEditScenario: boolean = false,
    existingAgentGuid: string | undefined,
    isOverrideScenario: boolean = false
) => {
    const { values, setFieldValue, setValues, isValid, dirty, submitForm, resetForm } = useFormikContext<AgentCreateFormValues>();

    const [view, setView] = useState<TabNames>(TabNames.Form);
    const [openedPanel, setOpenedPanel] = useState<PanelType>();
    const [yamlContent, setYamlContent] = useState<string>('');
    const [needsYamlSync, setNeedsYamlSync] = useState<boolean>(true);
    const [testThreadId, setTestThreadId] = useState<string | undefined>(undefined);
    const testThreadIdRef = useRef<string | undefined>(undefined);
    const [testThreadAutoTerminated, setTestThreadAutoTerminated] = useState<boolean>(false);
    const [testStarted, setTestStarted] = useState<boolean>(false);
    const [chatKey, setChatKey] = useState<string>();

    const handoffAgentsHook = useHandoffAgents(
        values.handoffSubagents,
        (value: string[]) => setFieldValue('handoffSubagents', value),
        agents,
        excludedHandoffAgent,
        additionalHandoffAgents
    );

    const toolsPickerHook = useToolsPicker({
        selectedToolNames: [...values.tools, ...values.mcpTools],
        setSelectedToolNames: (value: string[]) => setFieldValue('tools', value),
        existingTools,
        systemTools,
        mcpConnections,
    });

    const improvementsResultHandler = useCallback(
        (result: PromptImprovementResponse | undefined) => {
            if (result) {
                setFieldValue('instructions', result.improvedPrompt);
                setFieldValue('handoffInstructions', result.handoffDescription);
            }
        },
        [setFieldValue]
    );

    const improvementsAndSuggestionsHook = useImprovementsAndSuggestions(values.instructions, improvementsResultHandler);

    const disableControls = useMemo(() => {
        return improvementsAndSuggestionsHook.loadingImprovements;
    }, [improvementsAndSuggestionsHook.loadingImprovements]);

    useEffect(() => {
        if (view === 'form' || needsYamlSync) {
            const agentObj: Partial<ExtendedAgent> = {
                name: values.agentName,
                instructions: values.instructions,
                handoffDescription: values.handoffInstructions,
                handoffs: values.handoffSubagents,
                tools: values.tools,
                mcpTools: values.mcpTools,
                enableMemory: values.enableMemory,
                enableVanillaMode: values.enableVanillaMode,
            };
            const agentYaml = buildAgentConfigurationYaml(agentObj, true);
            setYamlContent(agentYaml);
            setNeedsYamlSync(false);
        }
    }, [view, values, setYamlContent, needsYamlSync]);

    const handleYamlChange = useCallback(
        (newYaml: string | undefined) => {
            if (!newYaml) {
                setValues({
                    agentName: isOverrideScenario || isEditScenario ? values.agentName : '',
                    instructions: '',
                    handoffInstructions: '',
                    handoffSubagents: [],
                    tools: [],
                    mcpTools: [],
                    enableMemory: false,
                    enableVanillaMode: false,
                });
                return;
            }

            setYamlContent(newYaml);

            // Pass current form values as previous to preserve fields not in YAML
            const currentAgent: Partial<ExtendedAgent> = {
                name: values.agentName,
                instructions: values.instructions,
                handoffDescription: values.handoffInstructions,
                handoffs: values.handoffSubagents,
                tools: values.tools,
                mcpTools: values.mcpTools,
                enableMemory: values.enableMemory,
                enableVanillaMode: values.enableVanillaMode,
            };

            const parsedYaml = tryParseAgentYaml(newYaml, currentAgent);

            if (!parsedYaml.error && parsedYaml.agent) {
                const agent = parsedYaml.agent;
                setValues({
                    agentName: isOverrideScenario || isEditScenario ? values.agentName : agent.name || '',
                    instructions: agent.instructions || '',
                    handoffInstructions: agent.handoffDescription || '',
                    handoffSubagents: agent.handoffs || [],
                    tools: agent.tools || [],
                    mcpTools: agent.mcpTools || [],
                    enableMemory: agent.enableMemory ?? false,
                    enableVanillaMode: agent.enableVanillaMode ?? false,
                });
            }
        },
        [values, setValues, setYamlContent, isOverrideScenario, isEditScenario]
    );

    const onDiscard = useCallback(() => {
        resetForm();
        if (view === 'yaml') {
            setTimeout(() => setNeedsYamlSync(true), 0);
        }
    }, [view, resetForm, setNeedsYamlSync]);

    const resetTestThread = useCallback(
        (autoTerminated?: boolean) => {
            setTestThreadAutoTerminated(!!autoTerminated && testThreadIdRef.current !== undefined);
            setTestStarted(false);
            testThreadIdRef.current = undefined;
            setTestThreadId(undefined);
            setChatKey(existingAgentGuid ? `${existingAgentGuid}-${Guid.newGuid()}` : undefined);
        },
        [existingAgentGuid]
    );

    const addTestThread = useCallback((threadId: string) => {
        setTestStarted(true);
        testThreadIdRef.current = threadId;
    }, []);

    const handleOpenedPanelChange = useCallback(
        (panel: PanelType | undefined) => {
            setTestThreadId(testThreadIdRef.current);
            setOpenedPanel(panel);
        },
        [setOpenedPanel, setTestThreadId]
    );

    const handleViewChange = useCallback(
        (newView: TabNames) => {
            setTestThreadId(testThreadIdRef.current);
            setView(newView);
        },
        [setView]
    );

    useEffect(() => {
        resetTestThread(true);
    }, [resetTestThread]);

    return {
        view,
        setView: handleViewChange,
        openedPanel,
        setOpenedPanel: handleOpenedPanelChange,
        yamlContent,
        handoffAgentsHook,
        toolsPickerHook,
        improvementsAndSuggestionsHook,
        disableControls,
        handleYamlChange,
        saveDisabled: !isValid || (!isOverrideScenario && !isEditScenario && !dirty) || disableControls,
        discardDisabled: !dirty || disableControls,
        onSubmit: submitForm,
        onDiscard: onDiscard,
        testPanelProps: {
            agentName: existingAgentGuid ? values.agentName : undefined,
            threadId: testThreadId,
            restartTest: resetTestThread,
            threadAutoTerminated: testThreadAutoTerminated,
            testStarted: testStarted,
            addThread: addTestThread,
            chatKey: chatKey,
            onClose: () => setOpenedPanel(undefined),
        },
    };
};

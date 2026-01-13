import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { Guid } from '../../../../../Common/Helpers/Guid';
import { DirtyStateContext } from '../../../../Contracts/Context';
import { ExtendedAgent, ExtendedTool, PromptImprovementResponse, SystemTool } from '../../../../Contracts/ExtendedAgentGraph';
import { tryParseAgentYaml } from '../../../../Playground/PlaygroundYamlUtils';
import { useToolsPicker } from '../../../Common/ToolsPicker/useToolsPicker';
import { McpConnection } from '../../../ExtendedAgentCreationDialog/api/mcpConnectionsApi';
import { buildAgentConfigurationYaml } from '../../../ExtendedAgentYamlUtils';
import { AgentPlaygroundFormValues, AgentPlaygroundMode } from '../Contracts';
import useEvaluation from './useEvaluation';
import { useHandoffAgents } from './useHandoffAgents';
import { useImprovementsAndSuggestions } from './useImprovementsAndSuggestions';

export enum EditorTabNames {
    Form = 'form',
    Yaml = 'yaml',
}

export enum TestTabNames {
    Chat = 'chat',
    Evaluation = 'evaluation',
}

export const useAgentPlaygroundFormik = (
    agents: ExtendedAgent[] | undefined,
    existingTools: ExtendedTool[] | undefined,
    systemTools: SystemTool[] | undefined,
    mcpConnections: McpConnection[] | undefined,
    excludedHandoffAgent: string | undefined,
    additionalHandoffAgents: string[] | undefined,
    isExistingAgent: boolean = false,
    existingAgentGuid: string | undefined,
    isOverrideScenario: boolean = false
) => {
    const { values, setFieldValue, setValues, isValid, dirty, submitForm, resetForm } = useFormikContext<AgentPlaygroundFormValues>();
    const { setIsDirty } = useContext(DirtyStateContext);

    const [editorPanelView, setEditorPanelView] = useState<EditorTabNames>(EditorTabNames.Form);
    const [testPanelView, setTestPanelView] = useState<TestTabNames>(TestTabNames.Chat);
    const [yamlContent, setYamlContent] = useState<string>('');
    const [needsYamlSync, setNeedsYamlSync] = useState<boolean>(true);
    const [testThreadId, setTestThreadId] = useState<string | undefined>(undefined);
    const testThreadIdRef = useRef<string | undefined>(undefined);
    const [testThreadAutoTerminated, setTestThreadAutoTerminated] = useState<boolean>(false);
    const [testStarted, setTestStarted] = useState<boolean>(false);
    const [chatKey, setChatKey] = useState<string>();
    const [showSuggestionsArea, setShowSuggestionsArea] = useState<boolean>(false);
    const [showTestToggleDialog, setShowTestToggleDialog] = useState<boolean>(false);
    const mode = useMemo<AgentPlaygroundMode>(() => {
        return dirty ? 'edit' : 'test';
    }, [dirty]);

    const evaluationHook = useEvaluation({
        mode: mode,
        tools: existingTools || [],
        systemTools: systemTools || [],
    });

    const handoffAgentsHook = useHandoffAgents(
        values.handoffSubagents,
        (value: string[]) => setFieldValue('handoffSubagents', value),
        agents,
        excludedHandoffAgent,
        additionalHandoffAgents
    );

    const toolsPickerHook = useToolsPicker({
        selectedToolNames: values.tools,
        setSelectedToolNames: (value: string[]) => setFieldValue('tools', value),
        selectedMcpToolNames: values.mcpTools,
        setSelectedMcpToolNames: (value: string[]) => setFieldValue('mcpTools', value),
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
        setIsDirty(dirty);
    }, [dirty, setIsDirty]);

    useEffect(() => {
        return () => {
            setIsDirty(false);
        };
    }, []);

    useEffect(() => {
        if (editorPanelView === 'form' || needsYamlSync) {
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
    }, [editorPanelView, values, setYamlContent, needsYamlSync]);

    const handleYamlChange = useCallback(
        (newYaml: string | undefined) => {
            if (!newYaml) {
                setValues({
                    agentName: isOverrideScenario || isExistingAgent ? values.agentName : '',
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
                    agentName: isOverrideScenario || isExistingAgent ? values.agentName : agent.name || '',
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
        [values, setValues, setYamlContent, isOverrideScenario, isExistingAgent]
    );

    const onDiscard = useCallback(() => {
        resetForm();
        if (editorPanelView === 'yaml') {
            setTimeout(() => setNeedsYamlSync(true), 0);
        }
    }, [editorPanelView, resetForm, setNeedsYamlSync]);

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

    const handleTestPanelViewChange = useCallback(
        (newView: TestTabNames) => {
            setTestThreadId(testThreadIdRef.current);
            setTestPanelView(newView);
        },
        [setTestPanelView]
    );

    const saveDisabled = useMemo(() => {
        if (!isValid || disableControls) {
            return true;
        }

        if (!dirty) {
            return !isOverrideScenario || isExistingAgent;
        }

        return false;
    }, [isValid, isOverrideScenario, isExistingAgent, dirty, disableControls]);

    const onTestToggleDialogClose = useCallback(() => {
        setShowTestToggleDialog(false);
    }, [setShowTestToggleDialog]);

    useEffect(() => {
        resetTestThread(true);
    }, [resetTestThread]);

    return {
        mode,
        editorPanelView,
        setEditorPanelView,
        testPanelView,
        setTestPanelView: handleTestPanelViewChange,
        yamlContent,
        evaluationHook,
        handoffAgentsHook,
        toolsPickerHook,
        improvementsAndSuggestionsHook,
        disableControls,
        handleYamlChange,
        saveDisabled,
        discardDisabled: !dirty || disableControls,
        onSubmit: submitForm,
        onDiscard,
        showTestToggleDialog,
        onTestToggleDialogClose,
        testPanelProps: {
            agentName: existingAgentGuid ? values.agentName : undefined,
            threadId: testThreadId,
            restartTest: resetTestThread,
            threadAutoTerminated: testThreadAutoTerminated,
            testStarted: testStarted,
            addThread: addTestThread,
            selectThread: () => {},
            chatKey: chatKey,
            onTelemetryUpdate: evaluationHook.setChatTelemetry,
        },
        showSuggestionsArea,
        setShowSuggestionsArea,
    };
};

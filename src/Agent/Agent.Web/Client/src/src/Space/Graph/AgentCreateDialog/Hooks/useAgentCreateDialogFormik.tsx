import { useFormikContext } from 'formik';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ExtendedAgent, ExtendedTool, PromptImprovementResponse, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { tryParseAgentYaml } from '../../../Playground/PlaygroundYamlUtils';
import { useToolsPicker } from '../../Common/ToolsPicker/useToolsPicker';
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
    excludedHandoffAgent: string | undefined,
    additionalHandoffAgents: string[] | undefined,
    isEditScenario: boolean = false,
    isOverrideScenario: boolean = false
) => {
    const { values, setFieldValue, setValues, isValid, dirty, submitForm, resetForm } = useFormikContext<AgentCreateFormValues>();

    const [view, setView] = useState<TabNames>(TabNames.Form);
    const [openedPanel, setOpenedPanel] = useState<PanelType>();
    const [yamlContent, setYamlContent] = useState<string>('');
    const [needsYamlSync, setNeedsYamlSync] = useState<boolean>(true);

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
        existingTools,
        systemTools,
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
                enableMemory: values.enableMemory,
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
                    agentName: isOverrideScenario ? values.agentName : '',
                    instructions: '',
                    handoffInstructions: '',
                    handoffSubagents: [],
                    tools: [],
                    enableMemory: false,
                });
                return;
            }

            setYamlContent(newYaml);
            const parsedYaml = tryParseAgentYaml(newYaml);

            if (!parsedYaml.error && parsedYaml.agent) {
                const agent = parsedYaml.agent;
                setValues({
                    agentName: isOverrideScenario ? values.agentName : agent.name || '',
                    instructions: agent.instructions || '',
                    handoffInstructions: agent.handoffDescription || '',
                    handoffSubagents: agent.handoffs || [],
                    tools: agent.tools || [],
                    enableMemory: agent.enableMemory,
                });
            }
        },
        [values, setValues, setYamlContent, isOverrideScenario]
    );

    const onDiscard = useCallback(() => {
        resetForm();
        if (view === 'yaml') {
            setTimeout(() => setNeedsYamlSync(true), 0);
        }
    }, [view, resetForm, setNeedsYamlSync]);

    return {
        view,
        setView,
        openedPanel,
        setOpenedPanel,
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
    };
};

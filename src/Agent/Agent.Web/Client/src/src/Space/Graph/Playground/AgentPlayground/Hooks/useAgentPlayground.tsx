import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { object, string } from 'yup';
import { AzPortalContext } from '../../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../../../../Common/Clients/ArmClient';
import { ExtendedAgentClient } from '../../../../../Common/Clients/ExtendedAgentClient';
import { Guid } from '../../../../../Common/Helpers/Guid';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../../Contracts/ExtendedAgentGraph';
import {
    ENTITY_NAME_MAX_LENGTH,
    HANDOFF_INSTRUCTION_MAX_LENGTH,
    INSTRUCTION_MAX_LENGTH,
    INSTRUCTION_MIN_LENGTH,
    isEntityNameValid,
} from '../../../AgentValidationUtilities';
import { AgentPlaygroundFormValues } from '../Contracts';

export const useAgentPlayground = (onAgentSaved: (selectedAgent?: string) => void, agent: ExtendedAgent) => {
    const intl = useIntl();
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [existingAgentGuid, setExistingAgentGuid] = useState<string | undefined>(Guid.newGuid());
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const isExistingAgent = useMemo(() => !!existingAgentGuid, [existingAgentGuid]);

    const isOverrideScenario = useMemo(() => {
        return agent.name === 'meta_agent';
    }, [agent]);

    const excludedHandoffAgent = useMemo(() => {
        return agent.name;
    }, [agent]);

    const additionalHandoffAgents = useMemo(() => {
        return isOverrideScenario ? metaAgentDefaultInitialValues.handoffSubagents : [];
    }, [isOverrideScenario]);

    const [initialValues, setInitialValues] = useState<AgentPlaygroundFormValues>(() => ({ ...defaultInitialValues }));

    const validationSchema = useMemo(() => {
        return object({
            agentName: string()
                .required(intl.formatMessage(SreAgentResources.fieldRequired))
                .test(
                    'validateNameFormat',
                    intl.formatMessage(ExtendedAgentsGraphResources.entityNameValidationMessage, {
                        maxLength: ENTITY_NAME_MAX_LENGTH,
                    }),
                    function (name: string) {
                        return isEntityNameValid(name);
                    }
                ),
            instructions: string()
                .required(intl.formatMessage(SreAgentResources.fieldRequired))
                .test('validateInstructionLength', '', function (value: string) {
                    if (value?.length < INSTRUCTION_MIN_LENGTH) {
                        return this.createError({
                            message: intl.formatMessage(ExtendedAgentsGraphResources.instructionMinLengthValidationMessage, {
                                minLength: INSTRUCTION_MIN_LENGTH,
                            }),
                        });
                    } else if (value?.length > INSTRUCTION_MAX_LENGTH) {
                        return this.createError({
                            message: intl.formatMessage(ExtendedAgentsGraphResources.instructionMaxLengthValidationMessage, {
                                maxLength: INSTRUCTION_MAX_LENGTH,
                            }),
                        });
                    } else {
                        return true;
                    }
                }),
            handoffInstructions: string().test('validateHandoffInstructionLength', '', function (value: string | undefined) {
                if (value && value.length > HANDOFF_INSTRUCTION_MAX_LENGTH) {
                    return this.createError({
                        message: intl.formatMessage(ExtendedAgentsGraphResources.instructionMaxLengthValidationMessage, {
                            maxLength: HANDOFF_INSTRUCTION_MAX_LENGTH,
                        }),
                    });
                } else {
                    return true;
                }
            }),
        });
    }, [intl]);

    const onUpdate = useCallback(
        async (values: AgentPlaygroundFormValues) => {
            setIsSubmitting(true);

            const agentUpdateBody: ExtendedAgent = {
                name: values.agentName,
                instructions: values.instructions,
                handoffDescription: values.handoffInstructions,
                handoffs: values.handoffSubagents,
                tools: values.tools,
                mcpTools: values.mcpTools,
                enableMemory: values.enableMemory,
                enableVanillaMode: values.enableVanillaMode,
            };

            // If enableMemory is true, ensure SearchMemory is in the tools list
            if (agentUpdateBody.enableMemory && !agentUpdateBody.tools?.includes('SearchMemory')) {
                agentUpdateBody.tools = [...(agentUpdateBody.tools || []), 'SearchMemory'];
            }

            const agentCreateNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationTitle, { agentName: values.agentName }),
                intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationInProgress, { agentName: values.agentName })
            );
            try {
                const response = await extendedAgentClient.applyEntity(agentUpdateBody, 'agent');
                if (response.isSuccessful) {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationSuccess, {
                        agentName: values.agentName,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, true, message);
                    setIsSubmitting(false);
                    onAgentSaved();
                    setInitialValues({ ...values });
                    setExistingAgentGuid(Guid.newGuid());
                    return;
                } else {
                    const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationFailure, {
                        agentName: values.agentName,
                        errorMessage: response.error,
                    });
                    azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                    setIsSubmitting(false);
                    return;
                }
            } catch (error) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationFailure, {
                    agentName: values.agentName,
                    errorMessage: getErrorMessageOrStringify(error),
                });
                azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                setIsSubmitting(false);
                return;
            }
        },
        [azPortalContext, extendedAgentClient, intl, ExtendedAgentsGraphResources]
    );

    const onSubmit = useCallback(
        (values: AgentPlaygroundFormValues) => {
            onUpdate(values);
        },
        [onUpdate]
    );

    useEffect(() => {
        setInitialValues({
            agentName: agent.name,
            instructions: agent.instructions || '',
            handoffInstructions: agent.handoffDescription || '',
            handoffSubagents: agent.handoffs || [],
            tools: agent.tools || [],
            mcpTools: agent.mcpTools || [],
            enableMemory: agent.enableMemory || agent.tools?.includes('SearchMemory') || false,
            enableVanillaMode: agent.enableVanillaMode ?? false,
        });
        setExistingAgentGuid(Guid.newGuid());
    }, [agent]);

    return {
        isOverrideScenario,
        isExistingAgent,
        existingAgentGuid,
        initialValues,
        validationSchema,
        excludedHandoffAgent,
        additionalHandoffAgents,
        onSubmit,
        isSubmitting,
    };
};

const defaultInitialValues: AgentPlaygroundFormValues = {
    agentName: '',
    instructions: '',
    handoffInstructions: '',
    handoffSubagents: [],
    tools: [],
    mcpTools: [],
};

const metaAgentDefaultInitialValues: AgentPlaygroundFormValues = {
    agentName: 'meta_agent',
    instructions: `# Role and Objective
You are the **generic operations solver** for production engineering (SRE/DevOps). Your mission is to autonomously triage requests (alerts, tickets, incidents, runbooks), plan multi-step investigations, collect evidence, and drive safe mitigations. When tools are unavailable, you produce precise manual guidance. When ongoing observation is needed, you create scheduled automations.

Begin with a concise checklist (3–7 bullets) of what you will do; keep items conceptual, not implementation-level. For greetings, simple Q&A, or when no concrete task is provided, skip the checklist.

# Checklist
- Establish minimal execution context (service/resource scope, time window, success criteria).
- Draft a stepwise plan: evidence to gather, hypotheses to test, candidate mitigations.
- Collect/aggregate telemetry (queries, logs, metrics) and correlate by time/correlation IDs.
- Choose the safest effective mitigation; require confirmation for destructive/admin actions.
- Execute or provide exact manual steps; verify recovery with objective signals.
- If ongoing monitoring is warranted, schedule recurring checks/notifications.
- Document outcomes, risks, and follow-ups.

# Planning & Delegation (Autonomy First)
- Prefer autonomous first-pass execution. Ask for clarification **only** when essential context is missing (e.g., target resource cannot be uniquely identified) or when an action is destructive.
- Decompose work into discrete, sequential subtasks aligned to Detect → Diagnose → Mitigate → Verify → Document.
- Select the minimal set of actions to reduce blast radius and time-to-mitigation. Provide rollback steps up front for any change.
- Use plain, stepwise language for plans by default. Use structured outputs (tables/JSON/CSV) only on request.

# Handoff & Review
- Use \`transfer_to_<agent>\` for delegation; do not disclose your own internal capabilities.
- On subagent return:
    - If incomplete: refine hypotheses, re-sequence, or re-delegate.
    - If complete: leave \`notifyUserMessage\` empty and set \`CompletedSuccessfully\`.
    - If unsupported: inform the user, suggest manual alternatives, then set \`CompletedSuccessfully\`.
- Share at most a 1–2 line status update when it improves user clarity.

# Evidence Collection
- Prefer authoritative telemetry sources (e.g., Kusto queries, Geneva/App Insights/Logs/Metrics) where available.
- Correlate signals to the incident timeline; highlight material changes before/around onset.
- Record what was queried, the time range, key findings, and data confidence.

# Output Format
- Provide a stepwise plan only for multi-step tasks or upon request; otherwise respond directly with the next action/result.
- If structured artifacts are requested, map each step clearly; confirm unclear requirements first.

# Validation Loop
- After each action or handoff, validate in 1–2 lines whether the expected evidence/effect occurred.
- If validation fails: widen the window, broaden scope, or pivot to adjacent data sources; iterate quickly.
- Confirm mitigation success with objective metrics prior to closure.

# Failure and Recovery
- Resource missing/renamed: present alternatives, confirm selection, retry.
- Permission issues: state the missing role/capability and pause for elevation.
- Timeouts/unavailable telemetry: retry with backoff or switch sources.
- No data: confirm time windows/filters; attempt a broader/default query.
- Unknown constraints: propose safe defaults and proceed after user acknowledgement. - hand a good exit logic to terminate the loop

# Reasoning & Documentation
- Maintain a **ReasoningScratchPad** (hidden) with:
    - UserIntent
    - MissingContext
    - CurrentContext (env/subscription/project/RG/resource/region/time window)
    - Subtasks & Boundaries
    - ValidationChecks & SuccessCriteria
    - Risk/Rollback
    - NextHandoff & StopCondition
- Log agent choices and rationale in the ScratchPad; delegate to each referenced agent before ending your turn.
- In \`notifyUserMessage\`, include at most a 1–2 line summary of next steps only when needed.

# Resolution Logic
- Continue chaining steps until the request is fully resolved or determined unfulfillable with clear rationale.
- Prefer mitigations with explicit rollback and measurable success criteria.
- When ongoing risk remains, propose a scheduled watcher and auto-notify pathway.

# Execution Flow
- Confirm/derive minimal context → Draft plan → Gather evidence → Narrow hypotheses → Apply safest effective mitigation → Verify → Document/close → (Optional) Schedule follow-ups.`,
    handoffInstructions:
        'This is the router agent in the system—it can reach subagents. Use it when you cannot directly perform the next required step or you need to set up recurring automation or manual execution guidance.',
    handoffSubagents: ['scheduled_task_agent', 'self_manual_agent'],
    tools: [],
    mcpTools: [],
};

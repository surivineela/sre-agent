import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { object, string } from 'yup';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { Guid } from '../../../../Common/Helpers/Guid';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent, Skill } from '../../../Contracts/ExtendedAgentGraph';
import { AgentCreateFormValues, AgentCreateOrEditInfo } from '../Contracts';

export const useAgentCreateDialog = (
    onAgentCreated: (selectedAgent?: string) => void,
    agentCreateOrEditInfo: AgentCreateOrEditInfo | undefined,
    skills?: Skill[]
) => {
    const intl = useIntl();
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [submissionError, setSubmissionError] = useState<string | undefined>();
    const [existingAgentGuid, setExistingAgentGuid] = useState<string | undefined>(
        agentCreateOrEditInfo?.mode === 'edit' ? Guid.newGuid() : undefined
    );
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const isEditScenario = useMemo(() => !!existingAgentGuid, [existingAgentGuid]);

    const isOverrideScenario = useMemo(() => {
        return agentCreateOrEditInfo?.mode === 'createMetaAgent' || (isEditScenario && agentCreateOrEditInfo?.agent?.name === 'meta_agent');
    }, [agentCreateOrEditInfo, isEditScenario]);

    const excludedHandoffAgent = useMemo(() => {
        return agentCreateOrEditInfo?.mode === 'createTarget' ? agentCreateOrEditInfo?.agent?.name : undefined;
    }, [agentCreateOrEditInfo]);

    const additionalHandoffAgents = useMemo(() => {
        return isOverrideScenario ? metaAgentDefaultInitialValues.handoffSubagents : [];
    }, [isOverrideScenario]);

    const [initialValues, setInitialValues] = useState<AgentCreateFormValues>(() => ({ ...defaultInitialValues }));

    const validationSchema = useMemo(() => {
        return object({
            agentName: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
            instructions: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
        });
    }, [intl]);

    const onCreate = useCallback(
        async (values: AgentCreateFormValues, sourceAgent: ExtendedAgent | undefined, selectCreatedAgent: boolean | undefined) => {
            setIsSubmitting(true);
            setSubmissionError(undefined);

            const agentCreateBody: ExtendedAgent = {
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
            if (agentCreateBody.enableMemory && !agentCreateBody.tools?.includes('SearchMemory')) {
                agentCreateBody.tools = [...(agentCreateBody.tools || []), 'SearchMemory'];
            }

            if (isOverrideScenario) {
                agentCreateBody.metadata = { name: 'meta_agent' };
                agentCreateBody.commonPrompts = ['format_guidelines', 'guard_rail'];
                agentCreateBody.outputType = 'MetaAgentOutput';
                // Enable vanilla mode when skills exist
                if (skills && skills.length > 0) {
                    agentCreateBody.enableVanillaMode = true;
                }
            }

            const agentCreateNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationTitle, { agentName: values.agentName }),
                intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationInProgress, { agentName: values.agentName })
            );
            const createResponse = await extendedAgentClient.applyEntity(agentCreateBody, 'agent');
            if (createResponse.isSuccessful) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationSuccess, {
                    agentName: values.agentName,
                });
                azPortalContext.stopNotification(agentCreateNotificationId, true, message);
            } else {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.createSubagentNotificationFailure, {
                    agentName: values.agentName,
                    errorMessage: createResponse.error,
                });
                azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                setSubmissionError(createResponse.error);
                setIsSubmitting(false);
                return;
            }

            onAgentCreated(selectCreatedAgent ? values.agentName : undefined);
            setInitialValues({ ...values });
            setExistingAgentGuid(Guid.newGuid());

            if (!sourceAgent) {
                setIsSubmitting(false);
                return;
            }

            const agentLinkBody: ExtendedAgent = {
                ...sourceAgent,
                handoffs: [...(sourceAgent.handoffs || []), values.agentName],
            };
            const agentLinkNotificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationTitle, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                }),
                intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationInProgress, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                })
            );
            const linkResponse = await extendedAgentClient.applyEntity(agentLinkBody, 'agent');
            if (linkResponse.isSuccessful) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationSuccess, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                });
                azPortalContext.stopNotification(agentLinkNotificationId, true, message);
                setIsSubmitting(false);
                onAgentCreated(selectCreatedAgent ? values.agentName : undefined);
            } else {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.addHandoffNotificationFailure, {
                    sourceAgent: sourceAgent.name,
                    targetAgent: values.agentName,
                    errorMessage: linkResponse.error,
                });
                azPortalContext.stopNotification(agentLinkNotificationId, false, message);
                setIsSubmitting(false);
            }
        },
        [isOverrideScenario, azPortalContext, intl, extendedAgentClient, onAgentCreated, skills]
    );

    const onUpdate = useCallback(
        async (values: AgentCreateFormValues) => {
            setIsSubmitting(true);
            setSubmissionError(undefined);

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
            const response = await extendedAgentClient.applyEntity(agentUpdateBody, 'agent');
            if (response.isSuccessful) {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationSuccess, {
                    agentName: values.agentName,
                });
                azPortalContext.stopNotification(agentCreateNotificationId, true, message);
                setIsSubmitting(false);
                onAgentCreated();
                setInitialValues({ ...values });
                setExistingAgentGuid(Guid.newGuid());
                return;
            } else {
                const message = intl.formatMessage(ExtendedAgentsGraphResources.updateSubagentNotificationFailure, {
                    agentName: values.agentName,
                    errorMessage: response.error,
                });
                azPortalContext.stopNotification(agentCreateNotificationId, false, message);
                setSubmissionError(response.error);
                setIsSubmitting(false);
                return;
            }
        },
        [azPortalContext, intl, extendedAgentClient, onAgentCreated]
    );

    const onSubmit = useCallback(
        (values: AgentCreateFormValues) => {
            if (isEditScenario) {
                onUpdate(values);
            } else if (agentCreateOrEditInfo?.mode === 'createTarget') {
                onCreate(values, agentCreateOrEditInfo.agent, false);
            } else {
                const shouldSelectCreatedAgent =
                    agentCreateOrEditInfo?.mode === 'create' || agentCreateOrEditInfo?.mode === 'createMetaAgent';
                onCreate(values, undefined, shouldSelectCreatedAgent);
            }
        },
        [onCreate, onUpdate, agentCreateOrEditInfo, isEditScenario]
    );

    useEffect(() => {
        if (!agentCreateOrEditInfo) {
            setExistingAgentGuid(undefined);
        } else if (agentCreateOrEditInfo.mode === 'edit') {
            const agentToEdit = agentCreateOrEditInfo.agent;
            setInitialValues({
                agentName: agentToEdit.name,
                instructions: agentToEdit.instructions || '',
                handoffInstructions: agentToEdit.handoffDescription || '',
                handoffSubagents: agentToEdit.handoffs || [],
                tools: agentToEdit.tools || [],
                mcpTools: agentToEdit.mcpTools || [],
                enableMemory: agentToEdit.enableMemory || agentToEdit.tools?.includes('SearchMemory') || false,
                enableVanillaMode: agentToEdit.enableVanillaMode ?? false,
            });
            setExistingAgentGuid(Guid.newGuid());
        } else if (agentCreateOrEditInfo.mode === 'createSource') {
            const sourceAgent = agentCreateOrEditInfo.agent;
            setInitialValues(prevValues => ({
                ...prevValues,
                handoffSubagents: [sourceAgent.name],
            }));
        } else if (agentCreateOrEditInfo.mode === 'createMetaAgent') {
            setInitialValues({ ...metaAgentDefaultInitialValues });
        } else {
            setInitialValues({ ...defaultInitialValues });
        }
    }, [agentCreateOrEditInfo]);

    return {
        isOpen: !!agentCreateOrEditInfo,
        isOverrideScenario,
        isEditScenario,
        existingAgentGuid,
        initialValues,
        validationSchema,
        excludedHandoffAgent,
        additionalHandoffAgents,
        onSubmit,
        isSubmitting,
        submissionError,
        setSubmissionError,
    };
};

const defaultInitialValues: AgentCreateFormValues = {
    agentName: '',
    instructions: '',
    handoffInstructions: '',
    handoffSubagents: [],
    tools: [],
    mcpTools: [],
};

const metaAgentDefaultInitialValues: AgentCreateFormValues = {
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

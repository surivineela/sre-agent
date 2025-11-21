import yaml from 'js-yaml';
import { ExtendedAgent, ExtendedConnector, ExtendedTool } from '../Contracts/ExtendedAgentGraph';

export const DEFAULT_API_VERSION = 'azuresre.ai/v1';

export const sanitizeRecord = (value: unknown): unknown => {
    if (Array.isArray(value)) {
        return value
            .map(item => {
                if (item === undefined || item === null) {
                    return null;
                }

                if (typeof item === 'object') {
                    return sanitizeRecord(item);
                }

                return item;
            })
            .filter(item => item !== null);
    }

    if (typeof value === 'object' && value !== null) {
        const result: Record<string, unknown> = {};
        Object.entries(value as Record<string, unknown>).forEach(([key, entryValue]) => {
            if (entryValue === undefined || entryValue === null) {
                return;
            }

            if (typeof entryValue === 'object') {
                const sanitized = sanitizeRecord(entryValue);
                if (
                    typeof sanitized === 'object' &&
                    sanitized !== null &&
                    !Array.isArray(sanitized) &&
                    Object.keys(sanitized as Record<string, unknown>).length === 0
                ) {
                    return;
                }
                result[key] = sanitized;
            } else {
                result[key] = entryValue;
            }
        });
        return result;
    }

    return value;
};

const assignIfDefined = (target: Record<string, unknown>, key: string, value: unknown) => {
    if (value !== undefined && value !== null) {
        target[key] = value;
    }
};

export const buildAgentConfigurationYaml = (agent: Partial<ExtendedAgent>, allowEmptyName?: boolean): string => {
    if (!agent.name && !allowEmptyName) {
        throw new Error('Agent name is required to apply configuration.');
    }

    // Combine regular tools and system tools into a single tools array
    const allTools = [...(agent.tools ?? []), ...(agent.systemTools ?? [])];

    // Build spec object following the meta agent format
    const spec: Record<string, unknown> = {
        name: agent.name,
        system_prompt: agent.instructions ?? '',
    };

    // Only add fields if they have values (to keep YAML clean like the meta agent example)
    if (allTools.length > 0) {
        spec.tools = allTools;
    }

    // Add MCP tools if specified
    if (agent.mcpTools && agent.mcpTools.length > 0) {
        spec.mcp_tools = agent.mcpTools;
    }

    if (agent.handoffs && agent.handoffs.length > 0) {
        spec.handoffs = agent.handoffs;
    }

    if (agent.handoffDescription) {
        spec.handoff_description = agent.handoffDescription;
    }

    if (agent.commonPrompts && agent.commonPrompts.length > 0) {
        spec.common_prompts = agent.commonPrompts;
    }

    if (agent.commonTools && agent.commonTools.length > 0) {
        spec.common_tools = agent.commonTools;
    }

    // Always set agent_type to Autonomous
    spec.agent_type = 'Autonomous';

    if (agent.outputType) {
        spec.output_type = agent.outputType;
    }

    if (agent.enableVanillaMode) {
        spec.vanilla_mode = agent.enableVanillaMode;
    }

    // Create document structure matching the meta agent format (no metadata section)
    const document = sanitizeRecord({
        api_version: DEFAULT_API_VERSION,
        kind: 'AgentConfiguration',
        spec,
    }) as Record<string, unknown>;

    return yaml.dump(document, {
        noRefs: true,
        lineWidth: 100,
        styles: {
            '!!str': 'literal',
        },
        replacer: (key: any, value: any) => {
            // Force multiline strings to use literal block scalar style
            if (typeof value === 'string' && (value.includes('\n') || key === 'description' || key === 'query')) {
                return value;
            }
            return value;
        },
    });
};

export const buildToolListYaml = (tool: Partial<ExtendedTool>): string => {
    if (!tool.name) {
        throw new Error('Tool name is required to apply configuration.');
    }

    if (!tool.type) {
        throw new Error('Tool type is required to apply configuration.');
    }

    const extendedTool = tool as Record<string, unknown>;

    const toolRecord: Record<string, unknown> = {
        name: tool.name,
        type: tool.type,
    };

    assignIfDefined(toolRecord, 'connector', tool.connector);
    assignIfDefined(toolRecord, 'tool_mode', extendedTool['tool_mode'] ?? extendedTool['toolMode'] ?? tool.toolMode);
    assignIfDefined(toolRecord, 'mode', extendedTool['mode'] ?? tool.mode);
    assignIfDefined(toolRecord, 'description', tool.description);
    assignIfDefined(toolRecord, 'query', tool.query ?? extendedTool['query']);

    if (tool.parameters && tool.parameters.length > 0) {
        toolRecord.parameters = tool.parameters.map(parameter => {
            const mapTo = parameter.mapTo ?? 'args';
            const target = parameter.target ?? 'dictionary:args:string';
            const parameterRecord: Record<string, unknown> = {
                name: parameter.name,
                type: parameter.type,
                required: parameter.required,
                description: parameter.description,
                map_to: mapTo,
                target,
            };

            if (parameter.value !== undefined && parameter.value !== null) {
                parameterRecord.value = parameter.value;
            }

            return sanitizeRecord(parameterRecord);
        });
    } else if (extendedTool['parameters']) {
        toolRecord.parameters = sanitizeRecord(extendedTool['parameters']);
    }

    if (tool.attributes && tool.attributes.length > 0) {
        toolRecord.attributes = tool.attributes;
    }

    if (tool.metadata) {
        toolRecord.metadata = sanitizeRecord(tool.metadata);
    }

    assignIfDefined(toolRecord, 'function', extendedTool['function'] ?? tool.function);
    assignIfDefined(toolRecord, 'file', tool.file ?? extendedTool['file']);
    assignIfDefined(toolRecord, 'database', tool.database ?? extendedTool['database']);
    assignIfDefined(toolRecord, 'cluster_uri', tool.clusterUri ?? extendedTool['clusterUri']);
    assignIfDefined(toolRecord, 'cluster_hint', extendedTool['clusterHint']);
    assignIfDefined(toolRecord, 'template', tool.template ?? extendedTool['template']);

    const regionalClusterGroups = (extendedTool['regionalClusterGroups'] ?? tool.regionalClusterGroups) as unknown;
    if (regionalClusterGroups) {
        toolRecord.regional_cluster_groups = sanitizeRecord(regionalClusterGroups);
    }

    const rawDisplayOptions =
        tool.displayOptions ?? (extendedTool['displayOptions'] as unknown) ?? (extendedTool['display_options'] as unknown);
    if (rawDisplayOptions !== undefined && rawDisplayOptions !== null) {
        const sanitizedDisplayOptions = sanitizeRecord(rawDisplayOptions);
        if (
            typeof sanitizedDisplayOptions === 'object' &&
            sanitizedDisplayOptions !== null &&
            !Array.isArray(sanitizedDisplayOptions) &&
            Object.keys(sanitizedDisplayOptions as Record<string, unknown>).length > 0
        ) {
            toolRecord.display_options = sanitizedDisplayOptions;
        }
    }

    const finalRecord = sanitizeRecord(toolRecord) as Record<string, unknown>;

    const metadata = tool.metadata ? sanitizeRecord(tool.metadata) : undefined;

    const document = sanitizeRecord({
        api_version: DEFAULT_API_VERSION,
        kind: 'ToolList', // Backend expects ToolList, not ToolDeployment
        ...(metadata && Object.keys(metadata as Record<string, unknown>).length > 0 ? { metadata } : {}),
        spec: {
            tools: [finalRecord],
        },
    }) as Record<string, unknown>;

    return yaml.dump(document, {
        noRefs: true,
        lineWidth: 100,
        styles: {
            '!!str': 'literal',
        },
        replacer: (key: any, value: any) => {
            if (typeof value === 'string' && (value.includes('\n') || key === 'description' || key === 'query')) {
                return value;
            }
            return value;
        },
    });
};

export const buildConnectorListYaml = (connector: Partial<ExtendedConnector>): string => {
    if (!connector.name) {
        throw new Error('Connector name is required to apply configuration.');
    }

    if (!connector.type) {
        throw new Error('Connector type is required to apply configuration.');
    }

    const connectorRecord: Record<string, unknown> = {
        name: connector.name,
        type: connector.type,
        enabled: connector.enabled ?? true,
    };

    assignIfDefined(connectorRecord, 'description', connector.description);

    if (connector.auth) {
        connectorRecord.auth = sanitizeRecord(connector.auth);
    } else {
        const extendedConnector = connector as Record<string, unknown>;
        if (extendedConnector['auth']) {
            connectorRecord.auth = sanitizeRecord(extendedConnector['auth']);
        }
    }

    if (connector.metadata) {
        connectorRecord.metadata = sanitizeRecord(connector.metadata);
    }

    const document = sanitizeRecord({
        api_version: DEFAULT_API_VERSION,
        kind: 'ConnectorList',
        metadata: {},
        spec: {
            connectors: [connectorRecord],
        },
    }) as Record<string, unknown>;

    return yaml.dump(document, {
        noRefs: true,
        lineWidth: 100,
        styles: {
            '!!str': 'literal',
        },
        replacer: (key: any, value: any) => {
            // Force multiline strings to use literal block scalar style
            if (typeof value === 'string' && (value.includes('\n') || key === 'description' || key === 'query')) {
                return value;
            }
            return value;
        },
    });
};

export type ExtendedEntityType = 'agent' | 'tool' | 'connector' | 'trigger';

export const convertExtendedEntityToYaml = (
    data: Partial<ExtendedAgent> | Partial<ExtendedTool> | Partial<ExtendedConnector>,
    type: ExtendedEntityType
): string => {
    switch (type) {
        case 'agent':
            return buildAgentConfigurationYaml(data as Partial<ExtendedAgent>);
        case 'tool':
            return buildToolListYaml(data as Partial<ExtendedTool>);
        case 'connector':
        default:
            return buildConnectorListYaml(data as Partial<ExtendedConnector>);
    }
};

export const buildMetaAgentYaml = (): string => {
    return `---
api_version: azuresre.ai/v1
kind: AgentConfiguration
metadata:
  name: meta_agent
spec:
  name: meta_agent
  system_prompt: |
    # Role and Objective
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
    - Confirm/derive minimal context → Draft plan → Gather evidence → Narrow hypotheses → Apply safest effective mitigation → Verify → Document/close → (Optional) Schedule follow-ups.

  handoff_description: |
    This is the router agent in the system—it can reach subagents. Use it when you cannot directly perform the next required step or you need to set up recurring automation or manual execution guidance.

  handoffs:
    - scheduled_task_agent
    - self_manual_agent

  common_prompts:
    - format_guidelines
    - guard_rail

  output_type: MetaAgentOutput`;
};

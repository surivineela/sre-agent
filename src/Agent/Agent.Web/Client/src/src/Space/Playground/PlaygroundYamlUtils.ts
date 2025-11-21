import yaml from 'js-yaml';
import { ExtendedAgent, ExtendedTool, SystemTool } from '../Contracts/ExtendedAgentGraph';
import { convertExtendedEntityToYaml } from '../Graph/ExtendedAgentYamlUtils';

const isRecord = (value: unknown): value is Record<string, unknown> => typeof value === 'object' && value !== null && !Array.isArray(value);

const toOptionalString = (value: unknown): string | undefined => {
    if (value === null || value === undefined) {
        return undefined;
    }

    return typeof value === 'string' ? value : String(value);
};

const toOptionalNumber = (value: unknown): number | undefined => {
    if (value === null || value === undefined) {
        return undefined;
    }

    if (typeof value === 'number') {
        return Number.isNaN(value) ? undefined : value;
    }

    if (typeof value === 'string') {
        const parsed = Number(value);
        return Number.isNaN(parsed) ? undefined : parsed;
    }

    return undefined;
};

const toOptionalBoolean = (value: unknown): boolean | undefined => {
    if (value === null || value === undefined) {
        return undefined;
    }

    if (typeof value === 'boolean') {
        return value;
    }

    if (typeof value === 'string') {
        const normalized = value.trim().toLowerCase();
        if (normalized === 'true') {
            return true;
        }
        if (normalized === 'false') {
            return false;
        }
    }

    return undefined;
};

const toStringArray = (value: unknown): string[] | undefined => {
    if (!Array.isArray(value)) {
        return undefined;
    }

    const converted = value.map(item => toOptionalString(item)).filter((entry): entry is string => entry !== undefined);

    return converted.length > 0 ? converted : [];
};

const toAgentsAsTools = (value: unknown): ExtendedAgent['agentsAsTools'] | undefined => {
    if (!Array.isArray(value)) {
        return undefined;
    }

    return value.filter(item => isRecord(item)) as ExtendedAgent['agentsAsTools'];
};

const toMetadataRecord = (value: unknown): Record<string, unknown> | undefined => {
    if (!isRecord(value)) {
        return undefined;
    }

    return value;
};

const extractSpecDocument = (documents: unknown[]) => {
    return documents.find(doc => isRecord(doc) && 'spec' in doc && isRecord((doc as Record<string, unknown>).spec));
};

const extractToolSpec = (spec: Record<string, unknown>): Record<string, unknown> | undefined => {
    if ('tools' in spec && Array.isArray(spec.tools) && spec.tools.length > 0) {
        const first = spec.tools[0];
        if (isRecord(first)) {
            return first;
        }
    }

    return spec;
};

export const tryParseAgentYaml = (
    yamlContent: string,
    previous?: Partial<ExtendedAgent>
): { agent?: Partial<ExtendedAgent>; error?: string } => {
    try {
        const documents: unknown[] = [];
        yaml.loadAll(yamlContent, (doc: unknown) => {
            if (doc !== undefined && doc !== null) {
                documents.push(doc);
            }
        });

        if (documents.length === 0) {
            return { agent: previous };
        }

        const specDocument = extractSpecDocument(documents);
        if (!specDocument) {
            return {
                agent: previous,
                error: 'Failed to locate agent specification in YAML.',
            };
        }

        const spec = (specDocument as Record<string, unknown>).spec as Record<string, unknown>;
        const next: Partial<ExtendedAgent> = { ...(previous ?? {}) };

        const setIfDefined = <K extends keyof ExtendedAgent>(key: K, value: ExtendedAgent[K] | undefined) => {
            if (value !== undefined) {
                next[key] = value;
            }
        };

        const assignString = (target: keyof ExtendedAgent) => (value: unknown) => {
            setIfDefined(target, toOptionalString(value) as any);
        };

        const assignNumber = (target: keyof ExtendedAgent) => (value: unknown) => {
            setIfDefined(target, toOptionalNumber(value) as any);
        };

        const assignBoolean = (target: keyof ExtendedAgent) => (value: unknown) => {
            setIfDefined(target, toOptionalBoolean(value) as any);
        };

        const assignStringArray = (setter: (values: string[] | undefined) => void) => (value: unknown) => {
            setter(toStringArray(value));
        };

        const fieldHandlers: Record<string, (value: unknown) => void> = {
            name: assignString('name'),
            system_prompt: assignString('instructions'),
            handoff_description: assignString('handoffDescription'),
            agent_type: assignString('agentType'),
            output_type: assignString('outputType'),
            llm_model_name: assignString('llmModelName'),
            critic_prompt_path: assignString('criticPromptPath'),
            temperature: assignNumber('temperature'),
            max_reflection_count: assignNumber('maxReflectionCount'),
            critic_on_hand_off: assignBoolean('criticOnHandOff'),
            allow_parallel_tool_calls: assignBoolean('allowParallelToolCalls'),
            vanilla_mode: assignBoolean('enableVanillaMode'),
            handoffs: assignStringArray(values => {
                next.handoffs = values;
            }),
            tools: assignStringArray(values => {
                next.tools = values;
            }),
            system_tools: assignStringArray(values => {
                next.systemTools = values;
            }),
            common_prompts: assignStringArray(values => {
                next.commonPrompts = values;
            }),
            common_tools: assignStringArray(values => {
                next.commonTools = values;
            }),
            mcp_tools: assignStringArray(values => {
                next.mcpTools = values;
            }),
            agents_as_tools: value => {
                next.agentsAsTools = toAgentsAsTools(value);
            },
            metadata: value => {
                setIfDefined('metadata', toMetadataRecord(value));
            },
        };

        Object.entries(spec).forEach(([specKey, value]) => {
            const handler = fieldHandlers[specKey];
            if (handler) {
                handler(value);
            }
        });

        const hasMetaAgentDocument = documents.some(doc => {
            if (!isRecord(doc)) {
                return false;
            }

            const metadata = (doc as Record<string, unknown>).metadata;
            return isRecord(metadata) && typeof metadata.name === 'string' && metadata.name === 'meta_agent';
        });

        const overrideFromSpec =
            'meta_agent_override' in spec ? toOptionalBoolean((spec as Record<string, unknown>).meta_agent_override) : undefined;

        if (hasMetaAgentDocument) {
            next.metaAgentOverride = true;
        } else if (overrideFromSpec !== undefined) {
            next.metaAgentOverride = overrideFromSpec;
        }

        return { agent: next };
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return { agent: previous, error: message };
    }
};

export const buildAgentYaml = (agent: Partial<ExtendedAgent>): string => {
    try {
        return convertExtendedEntityToYaml(agent, 'agent');
    } catch (error) {
        console.error('Failed to generate agent YAML for playground:', error);
        return '';
    }
};

export const buildToolYaml = (tool: Partial<ExtendedTool> | SystemTool): string => {
    try {
        return convertExtendedEntityToYaml(tool as Partial<ExtendedTool>, 'tool');
    } catch (error) {
        console.error('Failed to generate tool YAML for playground:', error);
        return '';
    }
};

export const tryParseToolYaml = (
    yamlContent: string,
    previous?: Partial<ExtendedTool>
): { tool?: Partial<ExtendedTool>; error?: string } => {
    try {
        const documents: unknown[] = [];
        yaml.loadAll(yamlContent, (doc: unknown) => {
            if (doc !== undefined && doc !== null) {
                documents.push(doc);
            }
        });

        if (documents.length === 0) {
            return { tool: previous };
        }

        const specDocument = extractSpecDocument(documents);
        if (!specDocument) {
            return {
                tool: previous,
                error: 'Failed to locate tool specification in YAML.',
            };
        }

        const spec = (specDocument as Record<string, unknown>).spec as Record<string, unknown>;
        const toolSpec = extractToolSpec(spec);
        if (!toolSpec) {
            return { tool: previous };
        }

        const next: Partial<ExtendedTool> = { ...(previous ?? {}) };

        Object.entries(toolSpec).forEach(([key, value]) => {
            (next as Record<string, unknown>)[key] = value;
        });

        return { tool: next };
    } catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return { tool: previous, error: message };
    }
};

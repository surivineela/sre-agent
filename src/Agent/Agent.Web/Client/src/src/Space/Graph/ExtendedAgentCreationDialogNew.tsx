import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    MessageBar,
    MessageBarBody,
    ProgressBar,
    Textarea,
} from '@fluentui/react-components';
import { ArrowLeft24Regular, Checkmark24Regular } from '@fluentui/react-icons';
import yaml from 'js-yaml';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Stepper, StepperStep } from '../../Common/Components/Stepper/Stepper';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { ExtendedAgent, ExtendedConnector, ExtendedTool } from '../Contracts/ExtendedAgentGraph';
import { createTriggerFromState } from './ExtendedAgentCreationDialog/api/triggerCreation';
import { AgentDetailsStep } from './ExtendedAgentCreationDialog/components/AgentDetailsStep';
import { ConnectorDetailsStep } from './ExtendedAgentCreationDialog/components/ConnectorDetailsStep';
import { KustoQueryTesterPanel } from './ExtendedAgentCreationDialog/components/KustoQueryTesterPanel';
import { ReviewStep } from './ExtendedAgentCreationDialog/components/ReviewStep';
import { SimpleTriggerDetailsStep } from './ExtendedAgentCreationDialog/components/SimpleTriggerDetailsStep';
import { ToolDetailsStep } from './ExtendedAgentCreationDialog/components/ToolDetailsStep';
import { TriggerReviewStep } from './ExtendedAgentCreationDialog/components/TriggerReviewStep';
import { TypeSelectionStep } from './ExtendedAgentCreationDialog/components/TypeSelectionStep';
import { useTriggerState } from './ExtendedAgentCreationDialog/hooks/useTriggerState';
import { useCreationDialogStyles } from './ExtendedAgentCreationDialog/styles';
import { CreationState, EntityType, ExtendedAgentCreationDialogProps, Step, ToolTestStatus } from './ExtendedAgentCreationDialog/types';
import { getKustoTestFingerprint } from './ExtendedAgentCreationDialog/utils/toolUtils';

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

const mapAgentSpecToState = (
    spec: Record<string, unknown>,
    previous: Partial<ExtendedAgent> | undefined,
    documents: unknown[]
): Partial<ExtendedAgent> => {
    const next: Partial<ExtendedAgent> = { ...(previous ?? {}) };

    const setIfDefined = <K extends keyof ExtendedAgent>(key: K, value: ExtendedAgent[K] | undefined) => {
        if (value !== undefined) {
            next[key] = value;
        }
    };

    const createStringHandler = (target: keyof ExtendedAgent) => (value: unknown) => {
        setIfDefined(target, toOptionalString(value) as any);
    };

    const createNumberHandler = (target: keyof ExtendedAgent) => (value: unknown) => {
        setIfDefined(target, toOptionalNumber(value) as any);
    };

    const createBooleanHandler = (target: keyof ExtendedAgent) => (value: unknown) => {
        setIfDefined(target, toOptionalBoolean(value) as any);
    };

    const createStringArrayHandler = (setter: (values: string[] | undefined) => void) => (value: unknown) => {
        setter(toStringArray(value));
    };

    const fieldHandlers: Record<string, (value: unknown) => void> = {
        name: createStringHandler('name'),
        system_prompt: createStringHandler('instructions'),
        handoff_description: createStringHandler('handoffDescription'),
        agent_type: createStringHandler('agentType'),
        output_type: createStringHandler('outputType'),
        llm_model_name: createStringHandler('llmModelName'),
        critic_prompt_path: createStringHandler('criticPromptPath'),
        temperature: createNumberHandler('temperature'),
        max_reflection_count: createNumberHandler('maxReflectionCount'),
        critic_on_hand_off: createBooleanHandler('criticOnHandOff'),
        allow_parallel_tool_calls: createBooleanHandler('allowParallelToolCalls'),
        enable_memory: createBooleanHandler('enableMemory'),
        handoffs: createStringArrayHandler(values => {
            next.handoffs = values;
        }),
        tools: createStringArrayHandler(values => {
            next.tools = values;
            next.systemTools = undefined;
        }),
        system_tools: createStringArrayHandler(values => {
            next.systemTools = values;
        }),
        common_prompts: createStringArrayHandler(values => {
            next.commonPrompts = values;
        }),
        common_tools: createStringArrayHandler(values => {
            next.commonTools = values;
        }),
        mcp_tools: createStringArrayHandler(values => {
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
        fieldHandlers[specKey]?.(value);
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
    } else if (previous?.metaAgentOverride) {
        next.metaAgentOverride = false;
    }

    return next;
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

const extractConnectorSpec = (spec: Record<string, unknown>): Record<string, unknown> | undefined => {
    if ('connectors' in spec && Array.isArray(spec.connectors) && spec.connectors.length > 0) {
        const first = spec.connectors[0];
        if (isRecord(first)) {
            return first;
        }
    }

    return spec;
};

const parseYamlDocuments = (yamlContent: string): unknown[] => {
    const documents: unknown[] = [];

    yaml.loadAll(yamlContent, (doc: unknown) => {
        if (doc !== undefined && doc !== null) {
            documents.push(doc);
        }
    });

    return documents;
};

const applyYamlDocumentsToState = (prevState: CreationState, documents: unknown[]): CreationState => {
    if (documents.length === 0) {
        return prevState;
    }

    const specDocument = documents.find(doc => isRecord(doc) && 'spec' in doc && isRecord((doc as Record<string, unknown>).spec));

    if (!specDocument) {
        return prevState;
    }

    const spec = (specDocument as Record<string, unknown>).spec as Record<string, unknown>;
    const nextState: CreationState = { ...prevState };

    switch (prevState.entityType) {
        case 'agent':
            nextState.agent = mapAgentSpecToState(spec, prevState.agent, documents);
            break;
        case 'tool': {
            const toolSpec = extractToolSpec(spec);
            if (toolSpec) {
                nextState.tool = {
                    ...(prevState.tool ?? {}),
                    ...toolSpec,
                } as Partial<ExtendedTool>;
            }
            break;
        }
        case 'connector': {
            const connectorSpec = extractConnectorSpec(spec);
            if (connectorSpec) {
                nextState.connector = {
                    ...(prevState.connector ?? {}),
                    ...connectorSpec,
                } as Partial<ExtendedConnector>;
            }
            break;
        }
        case 'trigger':
            if (isRecord(spec)) {
                nextState.trigger = {
                    ...(prevState.trigger ?? {}),
                    ...(spec as Record<string, unknown>),
                } as any;
            }
            break;
        default:
            break;
    }

    return nextState;
};

export const ExtendedAgentCreationDialog: FC<ExtendedAgentCreationDialogProps> = ({
    open,
    onOpenChange,
    onSubmit,
    existingAgents,
    existingTools,
    existingConnectors,
    systemTools,
    existingIncidentHandlers = [],
    existingScheduledTasks = [],
    initialEntityType,
    initialToolType,
    initialTriggerAgentName,
    contextNotice,
    linkContext,
    triggerConfig,
    onTriggerNavigate,
    onConnectorNavigate,
}) => {
    const styles = useCreationDialogStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const sreAgentContext = useContext(SreAgentContext);
    const incidentPlatformType = sreAgentContext.incidentManagement.incidentPlatformType;
    const isQuickAgentFlow = initialEntityType === 'agent' && !linkContext;
    const showWizard = !isQuickAgentFlow;

    const defaultTriggerAgentName = useMemo(() => {
        const trimmed = initialTriggerAgentName?.trim();
        if (trimmed) {
            return trimmed;
        }

        return existingAgents[0]?.name;
    }, [initialTriggerAgentName, existingAgents]);

    const wizardSteps = useMemo<StepperStep[]>(
        () => [
            { id: 1, label: intl.formatMessage(ExtendedAgentsGraphResources.stepType) },
            { id: 2, label: intl.formatMessage(ExtendedAgentsGraphResources.stepDetails) },
            { id: 3, label: intl.formatMessage(ExtendedAgentsGraphResources.stepReview) },
        ],
        [intl]
    );

    const buildInitialState = useCallback((): CreationState => {
        const quickAgent = initialEntityType === 'agent' && !linkContext;
        const state: CreationState = {
            step: quickAgent ? 1 : initialEntityType ? 2 : 1,
            entityType: initialEntityType,
        };

        if (initialEntityType === 'agent') {
            state.agent = { name: '', instructions: '', tools: [], agentType: 'Autonomous' };
        } else if (initialEntityType === 'tool') {
            const toolType = initialToolType ?? 'KustoTool';
            state.tool = { name: '', type: toolType, description: '', mode: 'query' };
            state.toolTest = { status: 'idle' };
        } else if (initialEntityType === 'connector') {
            state.connector = { name: '', type: 'Kusto', enabled: true };
        }

        return state;
    }, [initialEntityType, initialToolType, linkContext]);

    const [state, setState] = useState<CreationState>(() => buildInitialState());
    const [isSubmitting, setIsSubmitting] = useState(false);
    const [showYamlEditor, setShowYamlEditor] = useState(false);
    const [yamlEditorContent, setYamlEditorContent] = useState('');
    const [triggerReviewMode, setTriggerReviewMode] = useState(false);
    const [isYamlEditing, setIsYamlEditing] = useState(false);

    // Initialize trigger state controller
    const triggerController = useTriggerState(
        intl,
        initialEntityType === 'trigger'
            ? {
                  mode: 'incident',
                  agentName: defaultTriggerAgentName,
                  agentDisplayName: defaultTriggerAgentName,
              }
            : undefined,
        incidentPlatformType
    );

    // Tool tester logic
    const currentToolFingerprint = useMemo(() => getKustoTestFingerprint(state.tool), [state.tool]);
    const toolType = state.tool?.type?.trim() ?? 'KustoTool';
    const isKustoToolSelected = state.entityType === 'tool' && toolType === 'KustoTool';
    const shouldShowTester = state.step === 2 && isKustoToolSelected;

    const resetTrigger = triggerController.reset;

    useEffect(() => {
        if (open) {
            setState(buildInitialState());
            setTriggerReviewMode(false);
            setIsYamlEditing(false);
            if (initialEntityType === 'trigger') {
                resetTrigger({
                    mode: 'incident',
                    agentName: defaultTriggerAgentName,
                    agentDisplayName: defaultTriggerAgentName,
                });
            }
        }
    }, [open, buildInitialState, initialEntityType, resetTrigger, defaultTriggerAgentName]);

    useEffect(() => {
        if (state.step !== 3) {
            setIsYamlEditing(false);
        }
    }, [state.step]);

    // Sync trigger state to main state for ReviewStep
    useEffect(() => {
        if (state.entityType === 'trigger') {
            setState(prev => ({
                ...prev,
                trigger: triggerController.trigger,
            }));
        }
    }, [state.entityType, triggerController.trigger]);

    const handleTypeSelect = useCallback(
        (type: EntityType) => {
            setState((prev: CreationState) => {
                const newState: CreationState = {
                    ...prev,
                    entityType: type,
                    step: 2,
                };

                // Clear previous entity data
                delete newState.agent;
                delete newState.tool;
                delete newState.connector;
                delete newState.trigger;
                delete newState.toolTest;

                // Set data for selected type
                if (type === 'agent') {
                    newState.agent = { name: '', instructions: '', tools: [], agentType: 'Autonomous' };
                } else if (type === 'tool') {
                    newState.tool = { name: '', type: 'KustoTool', description: '', mode: 'query' };
                    newState.toolTest = { status: 'idle' };
                } else if (type === 'connector') {
                    newState.connector = { name: '', type: 'Kusto', enabled: true };
                } else if (type === 'trigger') {
                    newState.trigger = triggerController.trigger;
                }

                return newState;
            });
        },
        [triggerController.trigger]
    );

    const handleNext = useCallback(() => {
        setState(prev => ({
            ...prev,
            step: (prev.step + 1) as Step,
        }));
    }, []);

    const handleBack = useCallback(() => {
        setState(prev => {
            const nextStep = (prev.step - 1) as Step;
            if (linkContext && nextStep === 1) {
                return prev;
            }

            return {
                ...prev,
                step: nextStep,
            };
        });
    }, [linkContext]);

    const handleToolChange = useCallback(
        (nextValue: Partial<ExtendedTool> | ((current: Partial<ExtendedTool>) => Partial<ExtendedTool>)) => {
            setState((prev: CreationState) => {
                const previousTool = prev.tool ?? { type: 'KustoTool', mode: 'query' };
                const nextTool =
                    typeof nextValue === 'function' ? (nextValue(previousTool) ?? previousTool) : { ...previousTool, ...nextValue };
                const fingerprint = getKustoTestFingerprint(nextTool);
                const shouldKeepSuccess = prev.toolTest?.status === 'success' && fingerprint === prev.toolTest.lastRunFingerprint;

                return {
                    ...prev,
                    tool: nextTool,
                    toolTest: shouldKeepSuccess ? prev.toolTest : { status: 'idle', lastRunFingerprint: prev.toolTest?.lastRunFingerprint },
                };
            });
        },
        []
    );

    const handleToolTestStatusChange = useCallback((status: ToolTestStatus, options?: { error?: string; fingerprint?: string | null }) => {
        setState((prev: CreationState) => ({
            ...prev,
            toolTest: {
                status,
                errorMessage: options?.error,
                lastRunFingerprint:
                    status === 'success'
                        ? (options?.fingerprint ?? prev.toolTest?.lastRunFingerprint ?? null)
                        : prev.toolTest?.lastRunFingerprint,
            },
        }));
    }, []);

    const dialogTitle = useMemo(() => {
        if (linkContext) {
            return state.entityType === 'agent'
                ? `Link Agent to ${linkContext.sourceAgentName}`
                : `Link Tool to ${linkContext.sourceAgentName}`;
        }

        switch (state.entityType) {
            case 'agent':
                return 'Create Agent';
            case 'tool':
                return 'Create Tool';
            case 'connector':
                return 'Create Connector';
            case 'trigger':
                return 'Create Trigger';
            default:
                return 'Create New';
        }
    }, [linkContext, state.entityType]);

    const isToolTestSatisfied = useMemo(() => {
        if (state.entityType !== 'tool') {
            return true;
        }

        const toolType = state.tool?.type?.trim() ?? 'KustoTool';
        // Only Kusto tools require testing in this dialog
        if (toolType !== 'KustoTool') {
            return true;
        }

        if (!state.toolTest) {
            return false;
        }

        return (
            state.toolTest.status === 'success' && !!currentToolFingerprint && state.toolTest.lastRunFingerprint === currentToolFingerprint
        );
    }, [currentToolFingerprint, state.entityType, state.tool, state.toolTest]);

    const canProceed = useMemo(() => {
        if (state.step === 1) {
            return !!state.entityType;
        }

        if (state.step === 2) {
            const trimmedInstructions = triggerController.trigger.instructions?.trim() ?? '';
            switch (state.entityType) {
                case 'agent':
                    return !!(state.agent?.name?.trim() && state.agent?.instructions?.trim() && state.agent?.handoffDescription?.trim());
                case 'tool': {
                    const toolType = state.tool?.type?.trim() ?? 'KustoTool';
                    const isKustoTool = toolType === 'KustoTool';

                    if (isKustoTool) {
                        return !!(
                            state.tool?.name &&
                            state.tool?.description &&
                            state.tool?.connector &&
                            state.tool?.database &&
                            state.tool?.query &&
                            isToolTestSatisfied
                        );
                    }

                    return !!(state.tool?.name && state.tool?.description);
                }
                case 'connector':
                    return !!(state.connector?.name && (state.connector as any)?.connectionString);
                case 'trigger':
                    // Check basic trigger validation
                    return !!(
                        triggerController.trigger.agentName &&
                        triggerController.trigger.name &&
                        trimmedInstructions.length > 0 &&
                        !triggerController.validation.agent &&
                        !triggerController.validation.name &&
                        !triggerController.validation.instructions &&
                        !triggerController.validation.cron
                    );
                default:
                    return false;
            }
        }

        return true;
    }, [
        state,
        isToolTestSatisfied,
        triggerController.trigger.agentName,
        triggerController.trigger.name,
        triggerController.trigger.instructions,
        triggerController.validation.agent,
        triggerController.validation.name,
        triggerController.validation.instructions,
        triggerController.validation.cron,
    ]);

    const submitButtonLabel = useMemo(() => {
        switch (state.entityType) {
            case 'agent':
                return 'Create Agent';
            case 'tool':
                return 'Create Tool';
            case 'connector':
                return 'Create Connector';
            case 'trigger':
                return 'Create Trigger';
            default:
                return 'Create';
        }
    }, [state.entityType]);

    const handleEditYaml = useCallback((yamlContent: string) => {
        try {
            const documents = parseYamlDocuments(yamlContent);
            setState(prevState => applyYamlDocumentsToState(prevState, documents));
        } catch (error) {
            console.error('Failed to parse YAML from ReviewStep:', error);
        }
    }, []);

    const handleYamlEditorClose = useCallback(() => {
        setShowYamlEditor(false);
        setYamlEditorContent('');
    }, []);

    const handleYamlSave = useCallback(() => {
        try {
            const documents = parseYamlDocuments(yamlEditorContent);

            if (documents.length === 0) {
                console.error('Invalid YAML structure - no documents parsed');
                return;
            }

            setState(prevState => applyYamlDocumentsToState(prevState, documents));
            setShowYamlEditor(false);
            setYamlEditorContent('');
        } catch (error) {
            console.error('Failed to parse YAML:', error);
            // Could show an error message to the user here
        }
    }, [yamlEditorContent]);

    const handleSubmit = useCallback(async () => {
        if (!canProceed || isSubmitting || !state.entityType || isYamlEditing) return;

        setIsSubmitting(true);
        try {
            let payload = {};

            switch (state.entityType) {
                case 'agent':
                    {
                        const metaAgentOverride = (state.agent as any)?.metaAgentOverride === true;
                        const enableMemory = state.agent?.enableMemory === true;

                        // Prepare tools list
                        let tools = state.agent?.tools || [];

                        // Add SearchMemory tool if memory is enabled and not already present
                        if (enableMemory && !tools.some(t => t.toLowerCase() === 'searchmemory')) {
                            tools = [...tools, 'SearchMemory'];
                        }

                        // Prepare instructions
                        let instructions = state.agent?.instructions?.trim() || '';
                        const memoryPrompt = '\n\nUse the search tools to incorporate memory in the final result.';

                        // Add memory instruction if memory is enabled and not already present
                        if (enableMemory && !instructions.toLowerCase().includes('incorporate memory')) {
                            instructions += memoryPrompt;
                        }

                        payload = {
                            name: state.agent?.name?.trim(),
                            description: (state.agent as any)?.description,
                            instructions: instructions,
                            handoffDescription: state.agent?.handoffDescription?.trim(),
                            handoffs: state.agent?.handoffs || [],
                            tools: tools,
                            systemTools: state.agent?.systemTools || [],
                            mcpTools: state.agent?.mcpTools || [],
                            metaAgentOverride,
                        };
                    }
                    break;
                case 'tool':
                    payload = {
                        name: state.tool?.name,
                        type: state.tool?.type || 'KustoTool',
                        description: state.tool?.description,
                        connector: state.tool?.connector,
                        database: state.tool?.database,
                        query: state.tool?.query,
                        mode: state.tool?.mode || 'query',
                        parameters: state.tool?.parameters || [],
                        functionCode: state.tool?.functionCode,
                        timeoutSeconds: state.tool?.timeoutSeconds || 120,
                        dependencies: state.tool?.dependencies || [],
                    };
                    break;
                case 'connector':
                    payload = {
                        name: state.connector?.name,
                        type: state.connector?.type || 'KustoConnector',
                        connectionString: (state.connector as any)?.connectionString,
                        description: state.connector?.description,
                    };
                    break;
                case 'trigger': {
                    // Use the new API integration for trigger creation
                    const triggerResult = await createTriggerFromState(triggerController.trigger, sreAgentEndpoint);
                    if (!triggerResult.success) {
                        throw new Error(triggerResult.error || 'Failed to create trigger');
                    }

                    payload = {
                        id: triggerResult.id,
                        mode: triggerController.trigger.mode,
                        strategy: triggerController.trigger.strategy,
                        agentName: triggerController.trigger.agentName,
                        name: triggerController.trigger.name,
                        description: triggerController.trigger.description,
                        incidentPriority: triggerController.trigger.incidentPriority,
                        incidentType: triggerController.trigger.incidentType,
                        instructions: triggerController.trigger.instructions,
                        schedule: triggerController.trigger.schedule,
                        existingId: triggerController.trigger.existingId,
                        existingName: triggerController.trigger.existingName,
                    };
                    break;
                }
            }

            await onSubmit(payload, state.entityType);
            onOpenChange(false);
        } catch (error) {
            console.error(`Error creating ${state.entityType}:`, error);
            // TODO: Show error message to user
        } finally {
            setIsSubmitting(false);
        }
    }, [canProceed, isSubmitting, isYamlEditing, state, onSubmit, onOpenChange, triggerController.trigger, sreAgentEndpoint]);

    const isFinalActionDisabled = isSubmitting || !canProceed || (state.step === 3 && (isYamlEditing || showYamlEditor));

    return (
        <>
            <Dialog open={open} onOpenChange={(_, data) => onOpenChange(data.open)} modalType="modal">
                <DialogSurface className={shouldShowTester ? styles.dialogSurfaceWithTester : styles.dialogSurface}>
                    <DialogBody className={styles.dialogBody}>
                        <DialogTitle>{dialogTitle}</DialogTitle>

                        {linkContext && (
                            <MessageBar>
                                <MessageBarBody>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.relationshipContextBannerHeading, {
                                        agentName: linkContext.sourceAgentName,
                                    })}
                                </MessageBarBody>
                            </MessageBar>
                        )}

                        {showWizard && (
                            <div className={styles.progressSection}>
                                <ProgressBar value={(state.step / 3) * 100} thickness="large" />
                            </div>
                        )}

                        {showWizard && <Stepper steps={wizardSteps} currentStep={state.step} />}

                        {contextNotice && (
                            <MessageBar intent={contextNotice.intent ?? 'info'}>
                                <MessageBarBody>{contextNotice.message}</MessageBarBody>
                            </MessageBar>
                        )}

                        <DialogContent>
                            {isQuickAgentFlow ? (
                                <AgentDetailsStep
                                    agent={state.agent || { agentType: 'Autonomous' }}
                                    onChange={agent => setState(prev => ({ ...prev, agent }))}
                                    existingTools={existingTools}
                                    existingAgents={existingAgents}
                                    systemTools={systemTools}
                                    intl={intl}
                                />
                            ) : (
                                <>
                                    {state.step === 1 && (
                                        <TypeSelectionStep
                                            selectedType={state.entityType}
                                            onSelectAgent={() => handleTypeSelect('agent')}
                                            onSelectTool={() => handleTypeSelect('tool')}
                                            onSelectConnector={() => {
                                                if (onConnectorNavigate) {
                                                    onConnectorNavigate();
                                                    onOpenChange(false);
                                                } else {
                                                    handleTypeSelect('connector');
                                                }
                                            }}
                                            onSelectTrigger={mode => {
                                                handleTypeSelect('trigger');
                                                if (mode) {
                                                    triggerController.setTrigger(prev => ({ ...prev, mode }));
                                                }
                                            }}
                                            intl={intl}
                                            triggerConfig={triggerConfig}
                                            onTriggerNavigate={onTriggerNavigate}
                                        />
                                    )}

                                    {state.step === 2 && state.entityType === 'agent' && (
                                        <AgentDetailsStep
                                            agent={state.agent || {}}
                                            onChange={agent => setState(prev => ({ ...prev, agent }))}
                                            existingTools={existingTools}
                                            existingAgents={existingAgents}
                                            systemTools={systemTools}
                                            intl={intl}
                                        />
                                    )}

                                    {state.step === 2 && state.entityType === 'tool' && (
                                        <div className={shouldShowTester ? styles.dialogBodyWithTester : undefined}>
                                            <div className={styles.formColumn}>
                                                <ToolDetailsStep
                                                    tool={state.tool || {}}
                                                    onChange={handleToolChange}
                                                    existingConnectors={existingConnectors}
                                                    intl={intl}
                                                    onTestStatusChange={handleToolTestStatusChange}
                                                    fingerprint={currentToolFingerprint}
                                                />
                                            </div>
                                            {shouldShowTester && (
                                                <div className={styles.testerColumn}>
                                                    <KustoQueryTesterPanel
                                                        tool={state.tool || {}}
                                                        intl={intl}
                                                        toolTest={state.toolTest}
                                                        fingerprint={currentToolFingerprint}
                                                        onTestStatusChange={handleToolTestStatusChange}
                                                    />
                                                </div>
                                            )}
                                        </div>
                                    )}

                                    {state.step === 2 && state.entityType === 'connector' && (
                                        <ConnectorDetailsStep
                                            connector={state.connector || {}}
                                            onChange={connector => setState(prev => ({ ...prev, connector }))}
                                            intl={intl}
                                        />
                                    )}

                                    {state.step === 2 && state.entityType === 'trigger' && !triggerReviewMode && (
                                        <SimpleTriggerDetailsStep
                                            controller={triggerController}
                                            intl={intl}
                                            existingAgents={existingAgents}
                                            existingIncidentHandlers={existingIncidentHandlers}
                                            existingScheduledTasks={existingScheduledTasks}
                                            hasScheduledTasksFeature={triggerConfig?.hasScheduledTasksFeature ?? false}
                                            hasIncidentHandlersFeature={triggerConfig?.hasIncidentHandlersFeature ?? true}
                                            onNavigateToScheduledTasks={
                                                onTriggerNavigate ? () => onTriggerNavigate('scheduledTasks') : undefined
                                            }
                                            incidentPlatformType={incidentPlatformType}
                                        />
                                    )}

                                    {state.step === 2 && state.entityType === 'trigger' && triggerReviewMode && (
                                        <TriggerReviewStep
                                            controller={triggerController}
                                            intl={intl}
                                            onEdit={() => setTriggerReviewMode(false)}
                                            onSubmit={handleSubmit}
                                            isSubmitting={isSubmitting}
                                        />
                                    )}

                                    {state.step === 3 && (
                                        <ReviewStep
                                            state={state}
                                            intl={intl}
                                            relationshipSummaryHeading={linkContext ? `Link to ${linkContext.sourceAgentName}` : undefined}
                                            onEditYaml={handleEditYaml}
                                            onYamlEditingChange={setIsYamlEditing}
                                        />
                                    )}
                                </>
                            )}
                        </DialogContent>

                        {!canProceed && state.step === 2 && state.entityType === 'tool' && isKustoToolSelected && (
                            <div style={{ padding: '0 24px 12px 24px' }}>
                                <MessageBar intent="warning">
                                    <MessageBarBody>
                                        {(() => {
                                            const missing: string[] = [];
                                            if (!state.tool?.name) missing.push('Tool Name');
                                            if (!state.tool?.description) missing.push('Description');
                                            if (!state.tool?.connector) missing.push('Connector');
                                            if (!state.tool?.database) missing.push('Database');
                                            if (!state.tool?.query) missing.push('Query');

                                            if (missing.length > 0) {
                                                return `Please fill in the following required fields: ${missing.join(', ')}`;
                                            }
                                            if (!isToolTestSatisfied) {
                                                return 'Please test the query to ensure it works correctly before proceeding.';
                                            }
                                            return 'Please complete all required fields to proceed';
                                        })()}
                                    </MessageBarBody>
                                </MessageBar>
                            </div>
                        )}

                        <DialogActions>
                            {showWizard && state.step > 1 && (
                                <Button appearance="secondary" icon={<ArrowLeft24Regular />} onClick={handleBack}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.back)}
                                </Button>
                            )}

                            <Button appearance="secondary" onClick={() => onOpenChange(false)}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.cancel)}
                            </Button>

                            {state.entityType === 'trigger' ? (
                                /* Trigger creation workflow with review step */
                                triggerReviewMode ? null : ( // In review mode, buttons are handled by TriggerReviewStep
                                    <Button appearance="primary" onClick={() => setTriggerReviewMode(true)} disabled={!canProceed}>
                                        Review & Create
                                    </Button>
                                )
                            ) : showWizard ? (
                                state.step < 3 ? (
                                    <Button appearance="primary" onClick={handleNext} disabled={!canProceed}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.next)}
                                    </Button>
                                ) : (
                                    <Button
                                        appearance="primary"
                                        icon={<Checkmark24Regular />}
                                        onClick={handleSubmit}
                                        disabled={isFinalActionDisabled}
                                    >
                                        {isSubmitting ? intl.formatMessage(ExtendedAgentsGraphResources.creating) : submitButtonLabel}
                                    </Button>
                                )
                            ) : (
                                <Button
                                    appearance="primary"
                                    icon={<Checkmark24Regular />}
                                    onClick={handleSubmit}
                                    disabled={isFinalActionDisabled}
                                >
                                    {isSubmitting ? intl.formatMessage(ExtendedAgentsGraphResources.creating) : submitButtonLabel}
                                </Button>
                            )}
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* YAML Editor Dialog */}
            {showYamlEditor && (
                <Dialog open={showYamlEditor} onOpenChange={(_, data) => !data.open && handleYamlEditorClose()}>
                    <DialogSurface style={{ maxWidth: '800px', width: '90vw' }}>
                        <DialogBody>
                            <DialogTitle>{intl.formatMessage(ExtendedAgentsGraphResources.yamlInlineEditorTitle)}</DialogTitle>
                            <DialogContent>
                                <Textarea
                                    value={yamlEditorContent}
                                    onChange={(_, data) => setYamlEditorContent(data.value)}
                                    rows={20}
                                    style={{
                                        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                                        fontSize: '14px',
                                        width: '100%',
                                        minHeight: '100%',
                                    }}
                                    placeholder="Edit the YAML configuration here..."
                                />
                            </DialogContent>
                            <DialogActions>
                                <Button appearance="secondary" onClick={handleYamlEditorClose}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.cancel)}
                                </Button>
                                <Button appearance="primary" onClick={handleYamlSave}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.yamlInlineSaveChanges)}
                                </Button>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>
            )}
        </>
    );
};

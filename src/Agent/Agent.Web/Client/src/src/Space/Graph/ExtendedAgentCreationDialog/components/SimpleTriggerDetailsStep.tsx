import {
    Badge,
    Button,
    Dropdown,
    Field,
    Input,
    MessageBar,
    MessageBarBody,
    Option,
    OptionGroup,
    Spinner,
    Text,
    Textarea,
    Tooltip,
    mergeClasses,
} from '@fluentui/react-components';
import { Alert24Regular, Clock24Regular, Lightbulb24Regular, Wand24Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { IntlShape } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandler } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { CronExpressionGenerationRequest, ScheduledTask, ScheduledTaskPromptImprovementResponse } from '../../../Contracts/ScheduledTasks';
import {
    fieldNameToCamelCase,
    getAdditionalDropdownFilterFields,
    getIncidentTypeOptionsFromFilterFields,
    getPriorityOptionsFromFilterFields,
    getTextFilterFields,
    useIncidentFilterFields,
} from '../hooks/useIncidentFilterFields';
import { generateCronExpression } from '../services/scheduleService';
import { improveScheduledTaskPrompt } from '../services/scheduledPromptImprovementService';
import { useCreationDialogStyles } from '../styles';
import { TriggerMode, TriggerStateController, TriggerStrategy, TriggerValidationState } from '../types';
import { getBadgeColorForPriority, getIncidentTypesForPlatform, getPrioritiesForPlatform } from '../utils/incidentPlatforms';
import {
    DEFAULT_SCHEDULE_PRESET,
    SCHEDULE_PRESETS,
    getNextRunPreview,
    getPresetFromCron,
    getScheduleDescription,
    isCronExpressionLikelyValid,
    normalizeCronExpression,
} from '../utils/schedule';

const INSTRUCTION_TEMPLATES_INCIDENT = [
    'Investigate and diagnose the issue',
    'Run diagnostic queries',
    'Identify root cause',
    'Suggest mitigation steps',
];

const INSTRUCTION_TEMPLATES_SCHEDULED = [
    'Summarize key metrics for the past 24 hours',
    'Validate resource health and alert status',
    'Generate weekly operational status update',
    'Highlight anomalies or regressions that need attention',
];

interface AgentOption {
    key: string;
    label: string;
}

interface AgentOptionGroup {
    label: string;
    options: AgentOption[];
}

interface SimpleTriggerDetailsStepProps {
    controller: TriggerStateController;
    intl: IntlShape;
    existingAgents: ExtendedAgent[];
    existingIncidentHandlers: IncidentHandler[];
    existingScheduledTasks: ScheduledTask[];
    hasScheduledTasksFeature: boolean;
    hasIncidentHandlersFeature?: boolean;
    onNavigateToScheduledTasks?: () => void;
    incidentPlatformType?: IncidentManagementType;
}

export const SimpleTriggerDetailsStep: FC<SimpleTriggerDetailsStepProps> = ({
    controller,
    intl,
    existingAgents,
    existingIncidentHandlers,
    existingScheduledTasks,
    hasScheduledTasksFeature,
    hasIncidentHandlersFeature = true,
    onNavigateToScheduledTasks,
    incidentPlatformType,
}) => {
    const styles = useCreationDialogStyles();
    const { trigger, validation, updateFromUser, setValidation, applyAgentDefaults } = controller;
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    // Fetch dynamic filter fields from backend
    const { filterFields } = useIncidentFilterFields(sreAgentEndpoint);

    // Get platform-specific priorities and incident types
    // Use dynamic filter fields if available, otherwise fall back to static platform-specific values
    const incidentPriorities = useMemo(() => {
        if (filterFields.length > 0) {
            const dynamicPriorities = getPriorityOptionsFromFilterFields(filterFields);
            if (dynamicPriorities.length > 0) {
                return dynamicPriorities.map(p => ({
                    key: p.key,
                    label: p.value,
                    intlString: { id: p.key, defaultMessage: p.value },
                }));
            }
        }
        return getPrioritiesForPlatform(incidentPlatformType);
    }, [filterFields, incidentPlatformType]);

    const incidentTypes = useMemo(() => {
        if (filterFields.length > 0) {
            const dynamicTypes = getIncidentTypeOptionsFromFilterFields(filterFields);
            if (dynamicTypes.length > 0) {
                return dynamicTypes.map(t => ({ key: t.key, label: t.value }));
            }
        }
        return getIncidentTypesForPlatform(incidentPlatformType);
    }, [filterFields, incidentPlatformType]);

    // Get additional filter fields (dropdowns and text fields)
    const additionalDropdownFields = useMemo(() => getAdditionalDropdownFilterFields(filterFields), [filterFields]);
    const textFields = useMemo(() => getTextFilterFields(filterFields), [filterFields]);

    const agentFieldRef = useRef<HTMLButtonElement | null>(null);
    const nameRef = useRef<HTMLInputElement | null>(null);
    const instructionsRef = useRef<HTMLTextAreaElement | null>(null);
    const cronRef = useRef<HTMLInputElement | null>(null);
    const naturalTextRef = useRef(trigger.schedule.naturalText ?? '');

    const [isGeneratingCron, setIsGeneratingCron] = useState(false);
    const [naturalGenerationError, setNaturalGenerationError] = useState<string | undefined>();
    const [promptImprovement, setPromptImprovement] = useState<ScheduledTaskPromptImprovementResponse | null>(null);
    const [promptImprovementMode, setPromptImprovementMode] = useState<'suggestions' | 'improvement' | null>(null);
    const [promptImprovementError, setPromptImprovementError] = useState<string | null>(null);
    const [isFetchingSuggestions, setIsFetchingSuggestions] = useState(false);
    const [isApplyingImprovement, setIsApplyingImprovement] = useState(false);

    const resetNaturalLanguageState = useCallback(() => {
        setIsGeneratingCron(false);
        setNaturalGenerationError(undefined);
    }, []);

    useEffect(() => {
        const fieldOrder: Array<[keyof TriggerValidationState, React.RefObject<HTMLElement>]> = [
            ['agent', agentFieldRef],
            ['name', nameRef],
            ['instructions', instructionsRef],
            ['cron', cronRef],
            ['existing', agentFieldRef],
        ];
        for (const [key, ref] of fieldOrder) {
            if (validation[key]) {
                const node = ref.current as HTMLElement | undefined;
                if (node) {
                    node.focus({ preventScroll: false });
                    node.scrollIntoView({ block: 'center', behavior: 'smooth' });
                }
                break;
            }
        }
    }, [validation]);

    useEffect(() => {
        naturalTextRef.current = trigger.schedule.naturalText ?? '';
    }, [trigger.schedule.naturalText]);

    useEffect(() => {
        if (trigger.mode !== 'scheduled') {
            setPromptImprovement(null);
            setPromptImprovementMode(null);
            setPromptImprovementError(null);
            setIsFetchingSuggestions(false);
            setIsApplyingImprovement(false);
        }
    }, [trigger.mode]);

    useEffect(() => {
        if (trigger.mode !== 'scheduled' || trigger.strategy !== 'quick') {
            if (validation.instructions) {
                setValidation(prev => ({ ...prev, instructions: undefined }));
            }
            return;
        }

        const hasInstructions = (trigger.instructions ?? '').trim().length > 0;
        if (!hasInstructions) {
            const message = intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsScheduledRequired);
            if (validation.instructions !== message) {
                setValidation(prev => ({ ...prev, instructions: message }));
            }
        } else if (validation.instructions) {
            setValidation(prev => ({ ...prev, instructions: undefined }));
        }
    }, [trigger.instructions, trigger.mode, trigger.strategy, intl, setValidation, validation.instructions]);

    const agentOptions = useMemo<AgentOption[]>(() => {
        const toOption = (agent: ExtendedAgent): AgentOption => ({
            key: agent.name,
            label: agent.name,
        });
        return existingAgents.map(toOption);
    }, [existingAgents]);

    const groupedAgentOptions = useMemo<AgentOptionGroup[]>(() => {
        if (agentOptions.length === 0) {
            return [];
        }

        const RECENT_COUNT = 5;
        const recent = agentOptions.slice(0, Math.min(agentOptions.length, RECENT_COUNT));
        const rest = agentOptions.slice(recent.length);

        const groups: AgentOptionGroup[] = [];
        if (recent.length > 0) {
            groups.push({
                label: intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentRecentLabel),
                options: recent,
            });
        }
        if (rest.length > 0) {
            groups.push({
                label: intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentAllLabel),
                options: rest,
            });
        }
        return groups;
    }, [agentOptions, intl]);

    const currentMode = trigger.mode;
    const strategy = trigger.strategy;
    const isScheduledMode = currentMode === 'scheduled';
    const showStrategyPicker = !isScheduledMode;

    const schedulePreset = trigger.schedule.preset ?? DEFAULT_SCHEDULE_PRESET;
    const presetDefinition = schedulePreset === 'custom' ? undefined : SCHEDULE_PRESETS[schedulePreset];
    const cronExpression = trigger.schedule.cronExpression ?? presetDefinition?.cron ?? '';
    const scheduleDescription = currentMode === 'scheduled' && cronExpression ? getScheduleDescription(cronExpression) : undefined;
    const nextRuns = useMemo(
        () => (currentMode === 'scheduled' && cronExpression ? getNextRunPreview(cronExpression, 5) : []),
        [cronExpression, currentMode]
    );

    const existingItems = currentMode === 'incident' ? existingIncidentHandlers : existingScheduledTasks;

    useEffect(() => {
        if (trigger.mode === 'scheduled' && trigger.strategy !== 'quick') {
            updateFromUser({ strategy: 'quick', existingId: undefined, existingName: undefined });
        }
    }, [trigger.mode, trigger.strategy, updateFromUser]);

    const handleModeChange = (mode: TriggerMode | undefined) => {
        if (!mode || mode === trigger.mode) {
            return;
        }

        const nextStrategy: TriggerStrategy = mode === 'scheduled' ? 'quick' : strategy === 'existing' ? 'existing' : 'quick';

        updateFromUser({
            mode,
            strategy: nextStrategy,
            existingId: nextStrategy === 'existing' ? trigger.existingId : undefined,
            existingName: nextStrategy === 'existing' ? trigger.existingName : undefined,
        });
        applyAgentDefaults(trigger.agentName, trigger.agentDisplayName);
        setValidation(prev => ({ ...prev, cron: undefined, existing: undefined }));
    };

    const handleStrategyChange = (value: TriggerStrategy | undefined) => {
        if (!value) return;
        updateFromUser({
            strategy: value,
            existingId: value === 'existing' ? trigger.existingId : undefined,
            existingName: value === 'existing' ? trigger.existingName : undefined,
        });
        setValidation(prev => ({ ...prev, existing: undefined }));
    };

    const handleAgentSelect = (agentName?: string) => {
        if (!agentName) {
            updateFromUser({ agentName: undefined, agentDisplayName: undefined });
            return;
        }
        applyAgentDefaults(agentName, agentName);
        setValidation(prev => ({ ...prev, agent: undefined }));
    };

    const handleTemplateClick = (template: string) => {
        const current = trigger.instructions ?? '';
        const newValue = current ? `${current}\n- ${template}` : `- ${template}`;
        updateFromUser({ instructions: newValue }, ['instructions']);
    };

    const handleNaturalLanguageChange = useCallback(
        (value: string) => {
            resetNaturalLanguageState();
            updateFromUser(
                {
                    schedule: {
                        naturalText: value,
                        inputMode: value.trim().length > 0 ? 'natural' : 'preset',
                    },
                },
                ['schedule']
            );
        },
        [resetNaturalLanguageState, updateFromUser]
    );

    const handleGenerateSchedule = useCallback(async () => {
        const description = trigger.schedule.naturalText?.trim() ?? '';
        if (!description) {
            setNaturalGenerationError(intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalErrorRequired));
            return;
        }

        if (!sreAgentEndpoint) {
            setNaturalGenerationError(intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalGenerateFailed));
            return;
        }

        setIsGeneratingCron(true);
        setNaturalGenerationError(undefined);

        const request: CronExpressionGenerationRequest = {
            description,
            timezone: trigger.schedule.timezone,
            startTime: trigger.schedule.startTime,
        };

        try {
            const result = await generateCronExpression(sreAgentEndpoint, request);
            if (result?.cronExpression) {
                if (naturalTextRef.current.trim() !== description) {
                    return;
                }
                const normalizedCron = normalizeCronExpression(result.cronExpression);
                const inferredPreset = getPresetFromCron(normalizedCron);
                updateFromUser(
                    {
                        schedule: {
                            cronExpression: normalizedCron,
                            preset: inferredPreset ?? 'custom',
                            naturalText: description,
                            inputMode: 'natural',
                            timezone: result.timezone ?? trigger.schedule.timezone,
                        },
                    },
                    ['schedule']
                );

                setValidation(prev => ({ ...prev, cron: undefined }));
            } else if (naturalTextRef.current.trim() === description) {
                setNaturalGenerationError(intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalGenerateFailed));
            }
        } catch (error) {
            console.error('Failed to generate cron expression from natural language:', error);
            if (naturalTextRef.current.trim() === description) {
                setNaturalGenerationError(intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalGenerateFailed));
            }
        } finally {
            setIsGeneratingCron(false);
        }
    }, [
        intl,
        sreAgentEndpoint,
        trigger.schedule.naturalText,
        trigger.schedule.startTime,
        trigger.schedule.timezone,
        updateFromUser,
        setValidation,
    ]);

    const handleCronChange = (value: string) => {
        resetNaturalLanguageState();
        updateFromUser({ schedule: { cronExpression: value, preset: 'custom' } }, ['schedule']);
        if (isCronExpressionLikelyValid(value)) {
            setValidation(prev => ({ ...prev, cron: undefined }));
        }
    };

    const getPromptErrorMessage = useCallback(
        (error: unknown): string => {
            const errorMessage = typeof error === 'string' ? error : ((error as any)?.message?.toString?.() ?? '');

            if (errorMessage.includes('400')) {
                if (errorMessage.includes('Chat client is not available')) {
                    return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsChatUnavailable);
                }
                return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsInvalidRequest);
            }

            if (errorMessage.includes('500')) {
                return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsServerError);
            }

            if (errorMessage.includes('403')) {
                return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsForbidden);
            }

            return intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsError);
        },
        [intl]
    );

    const handlePromptSuggestions = useCallback(async () => {
        if (!trigger.instructions?.trim() || !sreAgentEndpoint) {
            return;
        }

        setIsFetchingSuggestions(true);
        setPromptImprovementError(null);

        try {
            const response = await improveScheduledTaskPrompt(sreAgentEndpoint, trigger.instructions);
            setPromptImprovement(response);
            setPromptImprovementMode('suggestions');
        } catch (error) {
            console.error('Failed to fetch scheduled task prompt suggestions:', error);
            setPromptImprovementError(getPromptErrorMessage(error));
        } finally {
            setIsFetchingSuggestions(false);
        }
    }, [getPromptErrorMessage, sreAgentEndpoint, trigger.instructions]);

    const handlePromptImprove = useCallback(async () => {
        if (!trigger.instructions?.trim() || !sreAgentEndpoint) {
            return;
        }

        setIsApplyingImprovement(true);
        setPromptImprovementError(null);

        try {
            const response = await improveScheduledTaskPrompt(sreAgentEndpoint, trigger.instructions);
            if (response?.improvedPrompt?.trim()) {
                updateFromUser({ instructions: response.improvedPrompt }, ['instructions']);
                setPromptImprovement(null);
                setPromptImprovementMode(null);
            } else {
                setPromptImprovement(response);
                setPromptImprovementMode('suggestions');
            }
        } catch (error) {
            console.error('Failed to improve scheduled task prompt:', error);
            setPromptImprovementError(getPromptErrorMessage(error));
        } finally {
            setIsApplyingImprovement(false);
        }
    }, [getPromptErrorMessage, sreAgentEndpoint, trigger.instructions, updateFromUser]);

    const handleAdditionalFilterFieldChange = (fieldName: string, value: string) => {
        const camelCaseFieldName = fieldNameToCamelCase(fieldName);
        const currentFields = trigger.additionalFilterFields || {};
        updateFromUser({
            additionalFilterFields: {
                ...currentFields,
                [camelCaseFieldName]: value,
            },
        });
    };

    const renderSeverityPills = () => (
        <div className={styles.pillsRow}>
            {incidentPriorities.map(priorityOption => (
                <Badge
                    key={priorityOption.key}
                    appearance={trigger.incidentPriority === priorityOption.key ? 'filled' : 'outline'}
                    color={getBadgeColorForPriority(priorityOption.key, incidentPlatformType)}
                    onClick={() => updateFromUser({ incidentPriority: priorityOption.key })}
                    style={{ cursor: 'pointer' }}
                >
                    {intl.formatMessage(priorityOption.intlString)}
                </Badge>
            ))}
        </div>
    );

    const renderIncidentTypePills = () => (
        <div className={styles.pillsRow}>
            {incidentTypes.map(typeOption => (
                <Badge
                    key={typeOption.key}
                    appearance={trigger.incidentType === typeOption.key ? 'filled' : 'outline'}
                    color="brand"
                    onClick={() => updateFromUser({ incidentType: typeOption.key })}
                    style={{ cursor: 'pointer' }}
                >
                    {typeOption.label}
                </Badge>
            ))}
        </div>
    );

    const renderExistingList = () => {
        if (existingItems.length === 0) {
            return (
                <MessageBar intent="warning">
                    <MessageBarBody>{intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingNone)}</MessageBarBody>
                </MessageBar>
            );
        }
        return (
            <div className={styles.formSection}>
                {existingItems.map(item => {
                    const id = item.id;
                    const name = 'name' in item ? item.name : undefined;
                    const displayName = (name && name.trim().length > 0 ? name : id) ?? id;
                    const isSelected = trigger.existingId === id;
                    const metaPrimary = 'lastExecutionTime' in item ? item.lastExecutionTime : undefined;
                    const metaSecondary = 'nextExecutionTime' in item ? item.nextExecutionTime : undefined;
                    return (
                        <button
                            type="button"
                            key={id}
                            onClick={() => updateFromUser({ existingId: id, existingName: displayName })}
                            className={mergeClasses(styles.sectionCard, isSelected ? styles.typeCardSelected : undefined)}
                            style={{ textAlign: 'left' }}
                        >
                            <Text weight="semibold">{displayName}</Text>
                            <Text size={200} className={styles.cardSubtitle}>
                                {metaPrimary
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingLastRun, { value: metaPrimary })
                                    : intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingNoRun)}
                            </Text>
                            {metaSecondary && (
                                <Text size={200} className={styles.cardSubtitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingNextRun, { value: metaSecondary })}
                                </Text>
                            )}
                        </button>
                    );
                })}
            </div>
        );
    };

    const leadAgentName =
        trigger.agentDisplayName || trigger.agentName || intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentFallbackName);

    const canUsePromptActions = Boolean(trigger.instructions?.trim() && sreAgentEndpoint);

    const instructionsLabel =
        currentMode === 'scheduled' ? (
            <div className={styles.fieldLabelRow}>
                <span className={styles.fieldLabelText}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsLabel)}
                    <span className={styles.fieldRequiredStar} aria-hidden="true">
                        *
                    </span>
                </span>
                <div className={styles.fieldActionGroup}>
                    <Tooltip content={intl.formatMessage(ExtendedAgentsGraphResources.suggestionsTooltip)} relationship="description">
                        <Button
                            appearance="secondary"
                            size="small"
                            className={styles.promptImprovementButton}
                            icon={isFetchingSuggestions ? <Spinner size="tiny" /> : <Lightbulb24Regular aria-hidden />}
                            disabled={!canUsePromptActions || isFetchingSuggestions || isApplyingImprovement}
                            onClick={handlePromptSuggestions}
                        >
                            {isFetchingSuggestions
                                ? intl.formatMessage(ExtendedAgentsGraphResources.loadingSuggestions)
                                : intl.formatMessage(ExtendedAgentsGraphResources.suggestionsButton)}
                        </Button>
                    </Tooltip>
                    <Tooltip
                        content={intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsTooltip)}
                        relationship="description"
                    >
                        <Button
                            appearance="secondary"
                            size="small"
                            className={styles.promptImprovementButton}
                            icon={isApplyingImprovement ? <Spinner size="tiny" /> : <Wand24Regular aria-hidden />}
                            disabled={!canUsePromptActions || isApplyingImprovement || isFetchingSuggestions}
                            onClick={handlePromptImprove}
                        >
                            {isApplyingImprovement
                                ? intl.formatMessage(ExtendedAgentsGraphResources.improvingInstructions)
                                : intl.formatMessage(ExtendedAgentsGraphResources.improveInstructionsButton)}
                        </Button>
                    </Tooltip>
                </div>
            </div>
        ) : (
            intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsLabel)
        );

    return (
        <div className={styles.formSection}>
            <Text className={styles.triggerInfoLead}>
                {intl.formatMessage(ExtendedAgentsGraphResources.triggerDetailsSubheading, { agentName: leadAgentName })}
            </Text>

            {/* Simple Trigger Type Dropdown */}
            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerModeLabel)} required>
                <Dropdown
                    value={currentMode}
                    selectedOptions={currentMode ? [currentMode] : []}
                    onOptionSelect={(_, data) => handleModeChange(data.optionValue as TriggerMode)}
                    placeholder="Select trigger type..."
                >
                    <Option value="incident" text="incident" disabled={!hasIncidentHandlersFeature}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <Alert24Regular />
                            <div>
                                <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentTitle)}</Text>
                                <Text size={200} block>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentDescription)}
                                </Text>
                            </div>
                        </div>
                    </Option>
                    <Option value="scheduled" text="scheduled" disabled={!hasScheduledTasksFeature}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                            <Clock24Regular />
                            <div>
                                <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledTitle)}</Text>
                                <Text size={200} block>
                                    {hasScheduledTasksFeature
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDescription)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDisabled)}
                                </Text>
                            </div>
                        </div>
                    </Option>
                </Dropdown>
            </Field>

            {/* Agent Selection */}
            <Field
                label={intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentLabel)}
                required
                validationState={validation.agent ? 'error' : 'none'}
                validationMessage={validation.agent}
            >
                <Dropdown
                    ref={agentFieldRef}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentPlaceholder)}
                    selectedOptions={trigger.agentName ? [trigger.agentName] : []}
                    value={trigger.agentName ?? ''}
                    onOptionSelect={(_, data) => handleAgentSelect((data.optionValue ?? data.optionText ?? '').toString())}
                >
                    {groupedAgentOptions.map((group: AgentOptionGroup) => (
                        <OptionGroup key={group.label} label={group.label}>
                            {group.options.map((option: AgentOption) => (
                                <Option key={option.key} value={option.key} text={option.label}>
                                    {option.label}
                                </Option>
                            ))}
                        </OptionGroup>
                    ))}
                </Dropdown>
            </Field>

            {/* Strategy Selection */}
            {showStrategyPicker && (
                <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyLabel)} required>
                    <Dropdown
                        value={strategy}
                        selectedOptions={strategy ? [strategy] : []}
                        onOptionSelect={(_, data) => handleStrategyChange(data.optionValue as TriggerStrategy)}
                        placeholder="Select creation method..."
                    >
                        <Option value="quick" text="quick">
                            <div>
                                <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyQuick)}</Text>
                                <Text size={200} block>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyQuickDescription)}
                                </Text>
                            </div>
                        </Option>
                        <Option value="existing" text="existing">
                            <div>
                                <Text weight="semibold">{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyExisting)}</Text>
                                <Text size={200} block>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyExistingDescription)}
                                </Text>
                            </div>
                        </Option>
                    </Dropdown>
                </Field>
            )}

            {/* Quick Creation Fields */}
            {strategy === 'quick' && (
                <div className={styles.formSection}>
                    <Field
                        label={intl.formatMessage(ExtendedAgentsGraphResources.triggerNameLabel)}
                        required
                        validationState={validation.name ? 'error' : 'none'}
                        validationMessage={validation.name}
                    >
                        <Input
                            ref={nameRef}
                            value={trigger.name ?? ''}
                            onChange={(_, data) => updateFromUser({ name: data.value ?? '' }, ['name'])}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerNamePlaceholder)}
                        />
                    </Field>

                    {currentMode === 'incident' ? (
                        <>
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentPriorityLabel)} required>
                                {renderSeverityPills()}
                            </Field>
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)} required>
                                {renderIncidentTypePills()}
                            </Field>

                            {/* Render additional dropdown filter fields */}
                            {additionalDropdownFields.map(field => (
                                <Field key={field.fieldName} label={field.displayName} required={field.isRequired}>
                                    <Dropdown
                                        placeholder={`Select ${field.displayName.toLowerCase()}...`}
                                        value={trigger.additionalFilterFields?.[fieldNameToCamelCase(field.fieldName)] ?? ''}
                                        selectedOptions={
                                            trigger.additionalFilterFields?.[fieldNameToCamelCase(field.fieldName)]
                                                ? [trigger.additionalFilterFields[fieldNameToCamelCase(field.fieldName)]]
                                                : []
                                        }
                                        onOptionSelect={(_, data) =>
                                            handleAdditionalFilterFieldChange(field.fieldName, data.optionValue?.toString() ?? '')
                                        }
                                    >
                                        {field.options.map(option => (
                                            <Option key={option.key} value={option.key} text={option.value}>
                                                {option.value}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                </Field>
                            ))}

                            {/* Render text filter fields */}
                            {textFields.map(field => (
                                <Field key={field.fieldName} label={field.displayName} required={field.isRequired}>
                                    <Input
                                        placeholder={`Enter ${field.displayName.toLowerCase()}...`}
                                        value={trigger.additionalFilterFields?.[fieldNameToCamelCase(field.fieldName)] ?? ''}
                                        onChange={(_, data) => handleAdditionalFilterFieldChange(field.fieldName, data.value ?? '')}
                                    />
                                </Field>
                            ))}
                        </>
                    ) : isScheduledMode ? (
                        <>
                            <div className={styles.scheduleGrid}>
                                <Field
                                    className={styles.scheduleGridFull}
                                    label={intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionLabel)}
                                >
                                    <Textarea
                                        className={styles.compactTextarea}
                                        value={trigger.description ?? ''}
                                        onChange={(_, data) => updateFromUser({ description: data.value ?? '' }, ['description'])}
                                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionPlaceholder, {
                                            agentName: leadAgentName,
                                        })}
                                        rows={3}
                                    />
                                </Field>

                                <Field
                                    className={styles.compactField}
                                    label={
                                        <div className={styles.fieldLabelRow}>
                                            <span className={styles.fieldLabelText}>
                                                Schedule
                                                <span className={styles.fieldRequiredStar} aria-hidden="true">
                                                    *
                                                </span>
                                            </span>
                                        </div>
                                    }
                                >
                                    <div className={styles.naturalLanguageRow}>
                                        <Input
                                            className={styles.naturalLanguageInput}
                                            value={trigger.schedule.naturalText ?? ''}
                                            onChange={(_, data) => handleNaturalLanguageChange(data.value ?? '')}
                                            placeholder="Every thursday at 2pm, 0 14 * * 4, or choose preset..."
                                        />
                                        <Button
                                            type="button"
                                            appearance="primary"
                                            size="small"
                                            onClick={handleGenerateSchedule}
                                            disabled={isGeneratingCron || !trigger.schedule.naturalText?.trim()}
                                        >
                                            {isGeneratingCron
                                                ? intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalGenerating)
                                                : intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalGenerate)}
                                        </Button>
                                    </div>
                                    {naturalGenerationError && <div className={styles.inlineError}>{naturalGenerationError}</div>}
                                    {trigger.schedule.cronExpression && (
                                        <div className={styles.helpText}>
                                            <Text size={200}>
                                                <strong>Detected:</strong> {trigger.schedule.cronExpression} → {scheduleDescription}
                                            </Text>
                                        </div>
                                    )}
                                </Field>

                                <Field
                                    className={styles.compactField}
                                    label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCustomExpressionLabel)}
                                    validationState={
                                        validation.cron ? 'error' : !isCronExpressionLikelyValid(cronExpression) ? 'error' : 'none'
                                    }
                                    validationMessage={
                                        validation.cron ||
                                        (!isCronExpressionLikelyValid(cronExpression)
                                            ? intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCronInvalid)
                                            : undefined)
                                    }
                                >
                                    <Input
                                        ref={cronRef}
                                        value={cronExpression}
                                        onChange={(_, data) => handleCronChange(data.value ?? '')}
                                        placeholder="0 9 * * 1-5"
                                        disabled={!trigger.schedule.cronExpression}
                                    />
                                </Field>
                            </div>

                            {nextRuns.length > 0 && (
                                <div className={mergeClasses(styles.sectionCard, styles.scheduleSummaryCard)}>
                                    <Text weight="semibold">
                                        {`${intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNextRunsLabel)} (${trigger.schedule.timezone})`}
                                    </Text>
                                    <div className={styles.pillsRow}>
                                        {nextRuns.map(run => (
                                            <span key={run} className={styles.pill}>
                                                {run}
                                            </span>
                                        ))}
                                    </div>
                                    {scheduleDescription && (
                                        <Text size={200} className={styles.helpText}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleSummary, {
                                                description: scheduleDescription,
                                            })}
                                        </Text>
                                    )}
                                </div>
                            )}
                        </>
                    ) : null}

                    <Field
                        className={styles.compactField}
                        label={instructionsLabel}
                        validationState={validation.instructions ? 'error' : 'none'}
                        validationMessage={validation.instructions}
                    >
                        {currentMode === 'scheduled' && promptImprovementError && (
                            <Text className={styles.inlineError}>{promptImprovementError}</Text>
                        )}
                        {currentMode === 'scheduled' && promptImprovementMode === 'suggestions' && promptImprovement && (
                            <div className={styles.promptImprovementInline}>
                                <div className={styles.promptImprovementInlineGroup}>
                                    <Text className={styles.promptImprovementSectionTitle}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.improvementSuggestions)}
                                    </Text>
                                    <div className={styles.promptImprovementList}>
                                        {promptImprovement.suggestions?.length ? (
                                            promptImprovement.suggestions.map((suggestion, index) => (
                                                <Text key={`scheduled-suggestion-${index}`} className={styles.promptImprovementItem}>
                                                    • {suggestion}
                                                </Text>
                                            ))
                                        ) : (
                                            <Text className={styles.promptImprovementEmpty}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.noImprovementSuggestions)}
                                            </Text>
                                        )}
                                    </div>
                                </div>
                                {promptImprovement.warnings?.length ? (
                                    <div className={styles.promptImprovementInlineGroup}>
                                        <Text className={styles.promptImprovementSectionTitle}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.improvementWarnings)}
                                        </Text>
                                        <div className={styles.promptImprovementList}>
                                            {promptImprovement.warnings.map((warning, index) => (
                                                <Text key={`scheduled-warning-${index}`} className={styles.promptImprovementItem}>
                                                    • {warning}
                                                </Text>
                                            ))}
                                        </div>
                                    </div>
                                ) : null}
                                {promptImprovement.followUpQuestions?.length ? (
                                    <div className={styles.promptImprovementInlineGroup}>
                                        <Text className={styles.promptImprovementSectionTitle}>
                                            {intl.formatMessage(ExtendedAgentsGraphResources.improvementFollowUps)}
                                        </Text>
                                        <div className={styles.promptImprovementList}>
                                            {promptImprovement.followUpQuestions.map((question, index) => (
                                                <Text key={`scheduled-question-${index}`} className={styles.promptImprovementItem}>
                                                    • {question}
                                                </Text>
                                            ))}
                                        </div>
                                    </div>
                                ) : null}
                            </div>
                        )}
                        <div className={styles.templateChips}>
                            {(currentMode === 'incident' ? INSTRUCTION_TEMPLATES_INCIDENT : INSTRUCTION_TEMPLATES_SCHEDULED).map(
                                template => (
                                    <Button key={template} size="small" appearance="subtle" onClick={() => handleTemplateClick(template)}>
                                        + {template}
                                    </Button>
                                )
                            )}
                        </div>
                        <Textarea
                            className={styles.compactTextarea}
                            ref={instructionsRef}
                            value={trigger.instructions ?? ''}
                            onChange={(_, data) => updateFromUser({ instructions: data.value ?? '' }, ['instructions'])}
                            placeholder={
                                currentMode === 'incident'
                                    ? intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsIncidentPlaceholder, {
                                          agentName: leadAgentName,
                                      })
                                    : intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsScheduledPlaceholder, {
                                          agentName: leadAgentName,
                                      })
                            }
                            rows={5}
                        />
                    </Field>
                </div>
            )}

            {/* Existing Selection */}
            {strategy === 'existing' && (
                <Field
                    label={
                        currentMode === 'incident'
                            ? intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingIncidentLabel)
                            : intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingScheduledLabel)
                    }
                    required
                    validationState={validation.existing ? 'error' : 'none'}
                    validationMessage={validation.existing}
                >
                    {renderExistingList()}
                </Field>
            )}

            {!hasScheduledTasksFeature && currentMode === 'scheduled' && (
                <MessageBar intent="info">
                    <MessageBarBody>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDisabled)}
                        {onNavigateToScheduledTasks && (
                            <Button appearance="subtle" size="small" onClick={onNavigateToScheduledTasks}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduledEnableCta)}
                            </Button>
                        )}
                    </MessageBarBody>
                </MessageBar>
            )}
        </div>
    );
};

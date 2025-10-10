import {
    Badge,
    Button,
    Combobox,
    Dropdown,
    Field,
    Input,
    MessageBar,
    MessageBarBody,
    Option,
    OptionGroup,
    Text,
    Textarea,
    mergeClasses,
} from '@fluentui/react-components';
import { Alert24Regular, Clock24Regular } from '@fluentui/react-icons';
import { ChangeEventHandler, FC, useEffect, useMemo, useRef } from 'react';
import { IntlShape } from 'react-intl';
import { IncidentHandler } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../../Common/Contracts/Azure/SreAgent';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { useCreationDialogStyles } from '../styles';
import { TriggerMode, TriggerStateController, TriggerStrategy, TriggerValidationState } from '../types';
import { getBadgeColorForPriority, getIncidentTypesForPlatform, getPrioritiesForPlatform } from '../utils/incidentPlatforms';
import {
    DEFAULT_SCHEDULE_PRESET,
    SCHEDULE_PRESETS,
    getNextRunPreview,
    getScheduleDescription,
    isCronExpressionLikelyValid,
    normalizeCronExpression,
    tryParseNaturalLanguageToCron,
} from '../utils/schedule';

const INSTRUCTION_TEMPLATES_INCIDENT = [
    'Investigate and diagnose the issue',
    'Run diagnostic queries',
    'Identify root cause',
    'Suggest mitigation steps',
];

const INSTRUCTION_TEMPLATES_SCHEDULED = [
    'Daily status digest',
    'Top failing queries',
    'Security anomalies check',
    'Performance metrics summary',
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

    // Get platform-specific priorities and incident types
    const incidentPriorities = useMemo(() => getPrioritiesForPlatform(incidentPlatformType), [incidentPlatformType]);
    const incidentTypes = useMemo(() => getIncidentTypesForPlatform(incidentPlatformType), [incidentPlatformType]);

    const agentFieldRef = useRef<HTMLInputElement | null>(null);
    const nameRef = useRef<HTMLInputElement | null>(null);
    const instructionsRef = useRef<HTMLTextAreaElement | null>(null);
    const cronRef = useRef<HTMLInputElement | null>(null);

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

    const handleAgentComboboxChange: ChangeEventHandler<HTMLInputElement> = event => {
        handleAgentSelect(event.target.value);
    };

    const handleTemplateClick = (template: string) => {
        const current = trigger.instructions ?? '';
        const newValue = current ? `${current}\n- ${template}` : `- ${template}`;
        updateFromUser({ instructions: newValue }, ['instructions']);
    };

    const handleNaturalLanguageChange = (value: string) => {
        const parsed = tryParseNaturalLanguageToCron(value);
        const schedulePatch: Parameters<typeof updateFromUser>[0]['schedule'] = {
            naturalText: value,
        };
        if (parsed?.cron) {
            schedulePatch.cronExpression = normalizeCronExpression(parsed.cron);
            schedulePatch.preset = parsed.preset ?? 'custom';
        }
        updateFromUser({ schedule: schedulePatch }, ['schedule']);
        if (parsed?.cron) {
            setValidation(prev => ({ ...prev, cron: undefined }));
        }
    };

    const handleCronChange = (value: string) => {
        updateFromUser({ schedule: { cronExpression: value, preset: 'custom' } }, ['schedule']);
        if (isCronExpressionLikelyValid(value)) {
            setValidation(prev => ({ ...prev, cron: undefined }));
        }
    };

    const handlePresetClick = (key: keyof typeof SCHEDULE_PRESETS) => {
        updateFromUser(
            {
                schedule: {
                    preset: key,
                    cronExpression: SCHEDULE_PRESETS[key].cron,
                    naturalText: '',
                },
            },
            ['schedule']
        );
        setValidation(prev => ({ ...prev, cron: undefined }));
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
                <Combobox
                    ref={agentFieldRef}
                    value={trigger.agentDisplayName ?? trigger.agentName ?? ''}
                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentPlaceholder)}
                    selectedOptions={trigger.agentName ? [trigger.agentName] : []}
                    onOptionSelect={(_, data) => handleAgentSelect((data.optionValue ?? data.optionText ?? '').toString())}
                    onChange={handleAgentComboboxChange}
                >
                    {groupedAgentOptions.map(group => (
                        <OptionGroup key={group.label} label={group.label}>
                            {group.options.map(option => (
                                <Option key={option.key} value={option.key} text={option.label}>
                                    {option.label}
                                </Option>
                            ))}
                        </OptionGroup>
                    ))}
                </Combobox>
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
                                    label={intl.formatMessage(ExtendedAgentsGraphResources.triggerSchedulePresetLabel)}
                                    required
                                >
                                    <Dropdown
                                        value={schedulePreset}
                                        selectedOptions={[schedulePreset]}
                                        onOptionSelect={(_, data) => {
                                            const key = data.optionValue as string;
                                            if (key === 'custom') {
                                                updateFromUser({ schedule: { preset: 'custom' as const } }, ['schedule']);
                                            } else {
                                                handlePresetClick(key as keyof typeof SCHEDULE_PRESETS);
                                            }
                                        }}
                                        placeholder="Select schedule..."
                                    >
                                        {(
                                            Object.entries(SCHEDULE_PRESETS) as Array<
                                                [keyof typeof SCHEDULE_PRESETS, { label: string; cron: string }]
                                            >
                                        ).map(([key, preset]) => (
                                            <Option key={key} value={key} text={key}>
                                                {preset.label}
                                            </Option>
                                        ))}
                                        <Option value="custom" text="custom">
                                            {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCustomLabel)}
                                        </Option>
                                    </Dropdown>
                                </Field>

                                <Field
                                    className={styles.compactField}
                                    label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalLabel)}
                                >
                                    <Input
                                        value={trigger.schedule.naturalText ?? ''}
                                        onChange={(_, data) => handleNaturalLanguageChange(data.value ?? '')}
                                        placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalPlaceholder)}
                                    />
                                    <div className={styles.helpText}>
                                        {trigger.schedule.naturalText
                                            ? intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalResolved, {
                                                  cron:
                                                      trigger.schedule.cronExpression ||
                                                      intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleAwaitingParse),
                                              })
                                            : intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalHelp)}
                                    </div>
                                </Field>

                                {schedulePreset === 'custom' && (
                                    <Field
                                        className={mergeClasses(styles.compactField, styles.scheduleGridFull)}
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
                                        />
                                    </Field>
                                )}
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
                        label={intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsLabel)}
                        required
                        validationState={validation.instructions ? 'error' : 'none'}
                        validationMessage={validation.instructions}
                    >
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

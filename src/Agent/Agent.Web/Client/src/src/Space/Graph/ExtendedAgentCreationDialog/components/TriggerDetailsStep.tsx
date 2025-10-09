import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
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
    Tab,
    TabList,
    Text,
    Textarea,
    mergeClasses,
} from '@fluentui/react-components';
import { Alert24Regular, Clock24Regular } from '@fluentui/react-icons';
import { ChangeEventHandler, FC, useEffect, useMemo, useRef, useState } from 'react';
import { IntlShape } from 'react-intl';
import { IncidentHandler } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { useCreationDialogStyles } from '../styles';
import { TriggerMode, TriggerStateController, TriggerStrategy, TriggerValidationState } from '../types';
import {
    DEFAULT_SCHEDULE_PRESET,
    SCHEDULE_PRESETS,
    getNextRunPreview,
    getScheduleDescription,
    isCronExpressionLikelyValid,
    normalizeCronExpression,
    tryParseNaturalLanguageToCron,
} from '../utils/schedule';

const INCIDENT_PRIORITIES = ['Sev0', 'Sev1', 'Sev2', 'Sev3', 'Sev4'] as const;
const INCIDENT_TYPES = ['LiveSite', 'Maintenance', 'Security', 'Other'] as const;

const INSTRUCTION_TEMPLATES_INCIDENT = [
    'Investigate and diagnose the issue',
    'Run diagnostic queries',
    'Identify root cause',
    'Suggest mitigation steps',
];

const RECENT_COUNT = 5;

const getTimezoneOptions = (current: string): string[] => {
    const defaults = ['UTC', current, 'America/Los_Angeles', 'America/New_York', 'Europe/London'];
    try {
        const zones = (Intl as any).supportedValuesOf?.('timeZone') ?? [];
        return Array.from(new Set([...defaults, ...zones])).filter(Boolean);
    } catch {
        return Array.from(new Set(defaults)).filter(Boolean);
    }
};

interface AgentOption {
    key: string;
    label: string;
}

interface AgentOptionGroup {
    label: string;
    options: AgentOption[];
}

interface TriggerDetailsStepProps {
    controller: TriggerStateController;
    intl: IntlShape;
    existingAgents: ExtendedAgent[];
    existingIncidentHandlers: IncidentHandler[];
    existingScheduledTasks: ScheduledTask[]; // kept for parity; not shown in UI per new design
    hasScheduledTasksFeature: boolean;
    hasIncidentHandlersFeature?: boolean;
    onNavigateToScheduledTasks?: () => void;
}

export const TriggerDetailsStep: FC<TriggerDetailsStepProps> = ({
    controller,
    intl,
    existingAgents,
    existingIncidentHandlers,
    hasScheduledTasksFeature,
    hasIncidentHandlersFeature = true,
    onNavigateToScheduledTasks,
}) => {
    const styles = useCreationDialogStyles();
    const { trigger, validation, updateFromUser, setValidation, applyAgentDefaults } = controller;

    // ===== Default Mode & Strategy Harmonization =====
    // Make Scheduled the default. If disabled, fall back to Incident.
    useEffect(() => {
        if (!trigger.mode) {
            updateFromUser({ mode: hasScheduledTasksFeature ? 'scheduled' : 'incident', strategy: 'quick' as TriggerStrategy });
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // If user switches to Scheduled, force strategy to quick (no "attach existing" for scheduled per new design)
    useEffect(() => {
        if (trigger.mode === 'scheduled' && trigger.strategy !== 'quick') {
            updateFromUser({ strategy: 'quick', existingId: undefined, existingName: undefined });
        }
    }, [trigger.mode, trigger.strategy, updateFromUser]);

    const [timezoneOptions] = useState(() => getTimezoneOptions(trigger.schedule.timezone));

    const agentFieldRef = useRef<HTMLInputElement | null>(null);
    const nameRef = useRef<HTMLInputElement | null>(null);
    const instructionsRef = useRef<HTMLTextAreaElement | null>(null);
    const cronRef = useRef<HTMLInputElement | null>(null);

    // Improved validation focus order; include "existing" only for Incident + Existing strategy
    useEffect(() => {
        const fieldOrder: Array<[keyof TriggerValidationState, React.RefObject<HTMLElement>]> = [
            ['agent', agentFieldRef],
            ['name', nameRef],
            ['instructions', instructionsRef],
            ['cron', cronRef],
        ];
        if (trigger.mode === 'incident' && trigger.strategy === 'existing') {
            fieldOrder.push(['existing', agentFieldRef]);
        }

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
    }, [validation, trigger.mode, trigger.strategy]);

    useEffect(() => {
        if (trigger.mode !== 'scheduled' || (trigger.strategy && trigger.strategy !== 'quick')) {
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
        const recent = existingAgents.slice(0, RECENT_COUNT).map(toOption);
        const remaining = existingAgents.slice(RECENT_COUNT).map(toOption);
        return [...recent, ...remaining];
    }, [existingAgents]);

    const groupedAgentOptions = useMemo<AgentOptionGroup[]>(() => {
        if (agentOptions.length === 0) return [];
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

    const currentMode = trigger.mode as TriggerMode | undefined;
    const strategy = trigger.strategy as TriggerStrategy | undefined;

    const schedulePreset = trigger.schedule.preset ?? DEFAULT_SCHEDULE_PRESET;
    const presetDefinition = schedulePreset === 'custom' ? undefined : SCHEDULE_PRESETS[schedulePreset];
    const cronExpression = trigger.schedule.cronExpression ?? presetDefinition?.cron ?? '';
    const scheduleDescription = currentMode === 'scheduled' && cronExpression ? getScheduleDescription(cronExpression) : undefined;

    const nextRuns = useMemo(
        () => (currentMode === 'scheduled' && cronExpression ? getNextRunPreview(cronExpression, 5) : []),
        [cronExpression, currentMode]
    );

    // Incident "attach existing" remains; Scheduled "attach existing" removed per new design
    const existingIncidentItems = existingIncidentHandlers;

    const handleModeChange = (mode: TriggerMode) => {
        if (mode === trigger.mode) return;

        // For Scheduled: force quick; wipe existing references
        const next: Partial<typeof trigger> = {
            mode,
            strategy: mode === 'scheduled' ? 'quick' : strategy === 'existing' ? 'existing' : 'quick',
            existingId: mode === 'incident' && strategy === 'existing' ? trigger.existingId : undefined,
            existingName: mode === 'incident' && strategy === 'existing' ? trigger.existingName : undefined,
        };

        updateFromUser(next);
        applyAgentDefaults(trigger.agentName, trigger.agentDisplayName);
        setValidation(prev => ({ ...prev, cron: undefined }));
    };

    const handleStrategyChange = (value: TriggerStrategy) => {
        // Only reachable for Incident mode
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

    const handleTimezoneChange = (value: string) => {
        updateFromUser({ schedule: { timezone: value || trigger.schedule.timezone } }, ['schedule']);
    };

    const renderSeverityPills = () => (
        <div className={styles.pillsRow}>
            {INCIDENT_PRIORITIES.map(priority => (
                <Badge
                    key={priority}
                    appearance={trigger.incidentPriority === priority ? 'filled' : 'outline'}
                    color={priority === 'Sev0' || priority === 'Sev1' ? 'danger' : priority === 'Sev2' ? 'warning' : 'informative'}
                    onClick={() => updateFromUser({ incidentPriority: priority })}
                    style={{ cursor: 'pointer' }}
                >
                    {priority}
                </Badge>
            ))}
        </div>
    );

    const renderIncidentTypePills = () => (
        <div className={styles.pillsRow}>
            {INCIDENT_TYPES.map(type => (
                <Badge
                    key={type}
                    appearance={trigger.incidentType === type ? 'filled' : 'outline'}
                    color="brand"
                    onClick={() => updateFromUser({ incidentType: type })}
                    style={{ cursor: 'pointer' }}
                >
                    {type}
                </Badge>
            ))}
        </div>
    );

    const renderExistingIncidentList = () => {
        if (existingIncidentItems.length === 0) {
            return (
                <MessageBar intent="warning">
                    <MessageBarBody>{intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingNone)}</MessageBarBody>
                </MessageBar>
            );
        }
        return (
            <div className={styles.formSection}>
                {existingIncidentItems.map(item => {
                    const id = item.id;
                    const name = 'name' in item ? item.name : undefined;
                    const displayName = (name && name.trim().length > 0 ? name : id) ?? id;
                    const isSelected = trigger.existingId === id;
                    const metaPrimary = 'lastExecutionTime' in item ? (item as any).lastExecutionTime : undefined;
                    const metaSecondary = 'nextExecutionTime' in item ? (item as any).nextExecutionTime : undefined;

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
            {/* Subheading */}
            <Text className={styles.triggerInfoLead}>
                {intl.formatMessage(ExtendedAgentsGraphResources.triggerDetailsSubheading, { agentName: leadAgentName })}
            </Text>

            {/* Mode selector — Scheduled first (default), Incident second */}
            <div className={styles.segmentedRow}>
                <TabList
                    selectedValue={currentMode}
                    onTabSelect={(_, data) => handleModeChange(data.value as TriggerMode)}
                    appearance="subtle"
                    size="medium"
                    aria-label={intl.formatMessage(ExtendedAgentsGraphResources.triggerModeLabel)}
                >
                    <Tab value="scheduled" icon={<Clock24Regular />} disabled={!hasScheduledTasksFeature}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledTitle)}
                    </Tab>
                    <Tab value="incident" icon={<Alert24Regular />} disabled={!hasIncidentHandlersFeature}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentTitle)}
                    </Tab>
                </TabList>
                <Text className={styles.segmentedHint}>
                    {currentMode === 'scheduled'
                        ? hasScheduledTasksFeature
                            ? intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDescription)
                            : intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDisabled)
                        : intl.formatMessage(ExtendedAgentsGraphResources.triggerModeIncidentDescription)}
                </Text>
            </div>

            {/* Agent Picker */}
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

            {/* Strategy selector — only for Incident (Scheduled is always Quick Create) */}
            {currentMode === 'incident' && (
                <div className={styles.segmentedRow}>
                    <TabList
                        selectedValue={strategy}
                        onTabSelect={(_, data) => handleStrategyChange(data.value as TriggerStrategy)}
                        appearance="subtle"
                        size="small"
                        aria-label={intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyLabel)}
                    >
                        <Tab value="quick">{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyQuick)}</Tab>
                        <Tab value="existing">{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyExisting)}</Tab>
                    </TabList>
                    <Text className={styles.segmentedHint}>{intl.formatMessage(ExtendedAgentsGraphResources.triggerStrategyHelp)}</Text>
                </div>
            )}

            {/* QUICK CREATE (shared) */}
            {(!strategy || strategy === 'quick' || currentMode === 'scheduled') && (
                <div className={styles.formSection}>
                    {/* Name */}
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

                    {/* INCIDENT-SPECIFIC QUICK FIELDS */}
                    {currentMode === 'incident' ? (
                        <>
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentPriorityLabel)} required>
                                {renderSeverityPills()}
                            </Field>
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)} required>
                                {renderIncidentTypePills()}
                            </Field>
                            <Field
                                label={intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsLabel)}
                                required
                                validationState={validation.instructions ? 'error' : 'none'}
                                validationMessage={validation.instructions}
                            >
                                {!trigger.instructions && (
                                    <div className={styles.templateChips}>
                                        {INSTRUCTION_TEMPLATES_INCIDENT.map(template => (
                                            <Button
                                                key={template}
                                                size="small"
                                                appearance="transparent"
                                                onClick={() => handleTemplateClick(template)}
                                            >
                                                + {template}
                                            </Button>
                                        ))}
                                    </div>
                                )}
                                <Textarea
                                    ref={instructionsRef}
                                    value={trigger.instructions ?? ''}
                                    onChange={(_, data) => updateFromUser({ instructions: data.value ?? '' }, ['instructions'])}
                                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsIncidentPlaceholder, {
                                        agentName: leadAgentName,
                                    })}
                                    rows={6}
                                />
                            </Field>
                        </>
                    ) : (
                        // SCHEDULED QUICK FIELDS (no "attach existing" per design)
                        <>
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionLabel)}>
                                <Textarea
                                    value={trigger.description ?? ''}
                                    onChange={(_, data) => updateFromUser({ description: data.value ?? '' }, ['description'])}
                                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionPlaceholder, {
                                        agentName: leadAgentName,
                                    })}
                                    rows={3}
                                />
                            </Field>

                            {/* Presets */}
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerSchedulePresetLabel)} required>
                                <div className={styles.chipsRow}>
                                    {(
                                        Object.entries(SCHEDULE_PRESETS) as Array<
                                            [keyof typeof SCHEDULE_PRESETS, { label: string; cron: string }]
                                        >
                                    ).map(([key, preset]) => (
                                        <Button
                                            key={key}
                                            size="small"
                                            appearance={schedulePreset === key ? 'primary' : 'secondary'}
                                            onClick={() => handlePresetClick(key)}
                                        >
                                            {preset.label}
                                        </Button>
                                    ))}
                                    <Button
                                        size="small"
                                        appearance={schedulePreset === 'custom' ? 'primary' : 'secondary'}
                                        onClick={() => updateFromUser({ schedule: { preset: 'custom' as const } }, ['schedule'])}
                                    >
                                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCustomLabel)}
                                    </Button>
                                </div>
                            </Field>

                            {/* Natural Language */}
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleNaturalLabel)}>
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

                            {/* Advanced */}
                            <Accordion collapsible className={styles.advancedSection}>
                                <AccordionItem value="advanced">
                                    <AccordionHeader>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleAdvancedLabel)}
                                    </AccordionHeader>
                                    <AccordionPanel>
                                        <Field
                                            label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCustomExpressionLabel)}
                                            validationState={
                                                validation.cron
                                                    ? 'error'
                                                    : schedulePreset === 'custom' && !isCronExpressionLikelyValid(cronExpression)
                                                      ? 'error'
                                                      : 'none'
                                            }
                                            validationMessage={validation.cron}
                                        >
                                            <Input
                                                ref={cronRef}
                                                value={cronExpression}
                                                onChange={(_, data) => handleCronChange(data.value ?? '')}
                                                placeholder="0 9 * * 1-5"
                                            />
                                            {!validation.cron &&
                                                schedulePreset === 'custom' &&
                                                !isCronExpressionLikelyValid(cronExpression) && (
                                                    <div className={styles.inlineError}>
                                                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleCronInvalid)}
                                                    </div>
                                                )}
                                        </Field>

                                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleTimezoneLabel)}>
                                            <Dropdown
                                                value={trigger.schedule.timezone}
                                                selectedOptions={[trigger.schedule.timezone]}
                                                onOptionSelect={(_, data) => handleTimezoneChange((data.optionValue ?? '').toString())}
                                            >
                                                {timezoneOptions.map(option => (
                                                    <Option key={option} value={option}>
                                                        {option}
                                                    </Option>
                                                ))}
                                            </Dropdown>
                                        </Field>

                                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleStartTimeLabel)}>
                                            <Input
                                                type="datetime-local"
                                                value={trigger.schedule.startTime ? trigger.schedule.startTime.slice(0, 16) : ''}
                                                onChange={(_, data) =>
                                                    updateFromUser(
                                                        {
                                                            schedule: {
                                                                startTime: data.value ? new Date(data.value).toISOString() : undefined,
                                                            },
                                                        },
                                                        ['schedule']
                                                    )
                                                }
                                            />
                                            <div className={styles.helpText}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.triggerScheduleStartHelp)}
                                            </div>
                                        </Field>
                                    </AccordionPanel>
                                </AccordionItem>
                            </Accordion>

                            {/* Next runs + summary */}
                            {nextRuns.length > 0 && (
                                <div className={styles.sectionCard}>
                                    <Text weight="semibold">
                                        {`${intl.formatMessage(
                                            ExtendedAgentsGraphResources.triggerScheduleNextRunsLabel
                                        )} (${trigger.schedule.timezone})`}
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

                            {/* Instructions */}
                            <Field
                                label={intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsLabel)}
                                required
                                validationState={validation.instructions ? 'error' : 'none'}
                                validationMessage={validation.instructions}
                            >
                                <Textarea
                                    ref={instructionsRef}
                                    value={trigger.instructions ?? ''}
                                    onChange={(_, data) => updateFromUser({ instructions: data.value ?? '' }, ['instructions'])}
                                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.triggerInstructionsScheduledPlaceholder, {
                                        agentName: leadAgentName,
                                    })}
                                    rows={6}
                                />
                            </Field>
                        </>
                    )}
                </div>
            )}

            {/* INCIDENT: Attach Existing (Shown only when strategy === 'existing') */}
            {currentMode === 'incident' && strategy === 'existing' && (
                <Field
                    label={intl.formatMessage(ExtendedAgentsGraphResources.triggerExistingIncidentLabel)}
                    required
                    validationState={validation.existing ? 'error' : 'none'}
                    validationMessage={validation.existing}
                >
                    {renderExistingIncidentList()}
                </Field>
            )}

            {/* Scheduled tasks feature disabled helper */}
            {!hasScheduledTasksFeature && currentMode === 'scheduled' && (
                <MessageBar intent="info">
                    <MessageBarBody>
                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerModeScheduledDisabled)}{' '}
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

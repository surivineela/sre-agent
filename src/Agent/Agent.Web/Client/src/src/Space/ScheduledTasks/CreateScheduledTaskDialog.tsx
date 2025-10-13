import {
    DatePicker,
    DayOfWeek,
    DefaultButton,
    Dialog,
    DialogFooter,
    DialogType,
    Dropdown,
    IDropdownOption,
    MessageBar,
    MessageBarType,
    PrimaryButton,
    Separator,
    Stack,
    Text,
    TextField,
} from '@fluentui/react';
import { tokens } from '@fluentui/react-components';
import { Add16Regular, Bot16Regular, DocumentEdit16Regular, Info16Regular, Sparkle16Regular, Timer16Regular } from '@fluentui/react-icons';
import React, { FC, useCallback, useEffect, useMemo, useReducer, useState } from 'react';
import { useIntl } from 'react-intl';
import { GenericErrorResources, ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import {
    CreateScheduledTaskRequest,
    CronExpressionGenerationRequest,
    CronExpressionGenerationResponse,
    ScheduledTaskPromptImprovementResponse,
} from '../Contracts/ScheduledTasks';

export interface CreateScheduledTaskDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onTaskCreated: () => void;
    createTask: (task: CreateScheduledTaskRequest) => Promise<any>;
    generateCronExpression: (request: CronExpressionGenerationRequest) => Promise<CronExpressionGenerationResponse | null>;
    improvePrompt: (prompt: string) => Promise<ScheduledTaskPromptImprovementResponse | null>;
    agentName?: string; // Optional agent name to associate with the scheduled task
}

// ---------------------------
// Helpers & constants
// ---------------------------

type PresetKey = 'hourly' | 'every15m' | 'daily' | 'weekly' | 'monthly' | 'workdays' | 'custom';

const PRESETS: Record<PresetKey, { text: string; cron: string }> = {
    hourly: { text: 'Every hour', cron: '0 * * * *' },
    every15m: { text: 'Every 15 minutes', cron: '*/15 * * * *' },
    daily: { text: 'Daily at midnight', cron: '0 0 * * *' },
    weekly: { text: 'Weekly (Sunday at midnight)', cron: '0 0 * * 0' },
    monthly: { text: 'Monthly (1st at midnight)', cron: '0 0 1 * *' },
    workdays: { text: 'Weekdays at 9 AM', cron: '0 9 * * 1-5' },
    custom: { text: 'Custom cron expression', cron: '' },
};

const DROPDOWN_OPTIONS: IDropdownOption[] = (Object.keys(PRESETS) as PresetKey[]).map(k => ({
    key: k,
    text: PRESETS[k].text,
    data: PRESETS[k].cron,
}));

const normalizeCron = (s: string) => s.trim().replace(/\s+/g, ' ');

const isLikelyCron = (s: string) => {
    const parts = normalizeCron(s).split(' ');
    return parts.length === 5 && parts.every(p => p.length > 0);
};

const getCronDescription = (cron: string): string => {
    const c = normalizeCron(cron);
    switch (c) {
        case PRESETS.hourly.cron:
            return PRESETS.hourly.text;
        case PRESETS.every15m.cron:
            return PRESETS.every15m.text;
        case PRESETS.daily.cron:
            return PRESETS.daily.text;
        case PRESETS.weekly.cron:
            return 'Weekly on Sunday at midnight';
        case PRESETS.monthly.cron:
            return PRESETS.monthly.text;
        case PRESETS.workdays.cron:
            return PRESETS.workdays.text;
        default:
            return isLikelyCron(c) ? 'Custom schedule' : '—';
    }
};

// Simple next-run preview for the known presets (local time)
const getNextRunExamples = (cron: string, count = 3): string[] => {
    const now = new Date();
    const out: string[] = [];
    const push = (d: Date) => out.push(d.toLocaleString());

    const c = normalizeCron(cron);
    if (c === PRESETS.hourly.cron) {
        const d = new Date(now);
        d.setMinutes(0, 0, 0);
        while (d <= now) d.setHours(d.getHours() + 1);
        for (let i = 0; i < count; i++) {
            push(new Date(d));
            d.setHours(d.getHours() + 1);
        }
        return out;
    }

    if (c === PRESETS.every15m.cron) {
        const d = new Date(now);
        d.setSeconds(0, 0);
        const q = new Date(d);
        q.setMinutes(Math.floor(d.getMinutes() / 15) * 15, 0, 0);
        while (q <= now) q.setMinutes(q.getMinutes() + 15);
        for (let i = 0; i < count; i++) {
            push(new Date(q));
            q.setMinutes(q.getMinutes() + 15);
        }
        return out;
    }

    if (c === PRESETS.daily.cron) {
        const d = new Date(now);
        d.setHours(0, 0, 0, 0);
        while (d <= now) d.setDate(d.getDate() + 1);
        for (let i = 0; i < count; i++) {
            push(new Date(d));
            d.setDate(d.getDate() + 1);
        }
        return out;
    }

    if (c === PRESETS.weekly.cron) {
        const d = new Date(now);
        d.setHours(0, 0, 0, 0);
        // Sunday = 0
        const diff = (7 - d.getDay()) % 7; // days until Sunday
        d.setDate(d.getDate() + (diff === 0 && d <= now ? 7 : diff));
        for (let i = 0; i < count; i++) {
            push(new Date(d));
            d.setDate(d.getDate() + 7);
        }
        return out;
    }

    if (c === PRESETS.monthly.cron) {
        const d = new Date(now);
        d.setHours(0, 0, 0, 0);
        d.setDate(1);
        if (d <= now) {
            d.setMonth(d.getMonth() + 1);
            d.setDate(1);
        }
        for (let i = 0; i < count; i++) {
            push(new Date(d));
            d.setMonth(d.getMonth() + 1);
            d.setDate(1);
        }
        return out;
    }

    if (c === PRESETS.workdays.cron) {
        const d = new Date(now);
        d.setSeconds(0, 0);
        d.setHours(9, 0, 0, 0);
        const advanceToWeekday9am = (dt: Date) => {
            while (dt.getDay() === 0 || dt.getDay() === 6 || dt <= now) {
                dt.setDate(dt.getDate() + 1);
                dt.setHours(9, 0, 0, 0);
            }
        };
        advanceToWeekday9am(d);
        for (let i = 0; i < count; i++) {
            push(new Date(d));
            d.setDate(d.getDate() + 1);
            advanceToWeekday9am(d);
        }
        return out;
    }

    return [];
};

// ---------------------------
// Reducer for form data + errors
// ---------------------------

type FormState = CreateScheduledTaskRequest;

type Action = { type: 'merge'; payload: Partial<FormState> } | { type: 'reset'; payload?: Partial<FormState> };

const formReducer = (state: FormState, action: Action): FormState => {
    switch (action.type) {
        case 'merge':
            return { ...state, ...action.payload };
        case 'reset':
            return {
                name: '',
                description: '',
                cronExpression: PRESETS.daily.cron,
                agentPrompt: '',
                agent: undefined,
                createdBy: 'Sub-Agent Builder',
                startTime: new Date().toISOString(),
                endTime: undefined,
                threadId: undefined,
                executionContext: undefined,
                maxExecutions: undefined,
                notificationChannel: undefined,
                ...action.payload,
            };
        default:
            return state;
    }
};

interface Validation {
    name?: string;
    cronExpression?: string;
    agentPrompt?: string;
    maxExecutions?: string;
    description?: string;
}

const validate = (s: FormState): Validation => {
    const v: Validation = {};
    if (!s.name || s.name.trim().length < 3) v.name = 'Please enter a name (min 3 characters).';
    if (!s.agentPrompt || s.agentPrompt.trim().length === 0) v.agentPrompt = 'Agent instructions are required.';
    if (!s.cronExpression || !isLikelyCron(s.cronExpression)) v.cronExpression = 'Enter a valid 5-part cron expression (m h dom mon dow).';
    if (s.maxExecutions != null && (isNaN(Number(s.maxExecutions)) || Number(s.maxExecutions) <= 0))
        v.maxExecutions = 'Max executions must be a positive number.';
    if (s.description && s.description.split(/\r?\n/).length > 1) v.description = 'Description must be a single line summary.';
    if (s.description && s.description.length > 140) v.description = 'Description should be 140 characters or fewer.';
    return v;
};

const hasErrors = (v: Validation) => Object.keys(v).length > 0;

// ---------------------------
// UI bits
// ---------------------------

const SectionHeader: FC<{ icon: React.ReactNode; title: string }> = ({ icon, title }) => (
    <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
        <Stack styles={{ root: { display: 'flex', alignItems: 'center', justifyContent: 'center' } }}>{icon}</Stack>
        <Text variant="large" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
            {title}
        </Text>
    </Stack>
);

const PreviewCard: FC<{ cron: string }> = ({ cron }) => {
    const intl = useIntl();
    const desc = getCronDescription(cron);
    const samples = useMemo(() => getNextRunExamples(cron, 3), [cron]);
    return (
        <Stack
            styles={{
                root: {
                    background: tokens.colorNeutralBackground3,
                    padding: 12,
                    borderRadius: 4,
                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                    boxShadow: tokens.shadow2,
                },
            }}
            tokens={{ childrenGap: 6 }}
        >
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                <Info16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 14 }} />
                <Text variant="small" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
                    {intl.formatMessage(SreAgentResources.schedulePreview)}
                </Text>
            </Stack>
            <Text variant="small" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                {desc}
            </Text>
            <Text variant="small" styles={{ root: { color: tokens.colorNeutralForeground2, fontSize: 11 } }}>
                Cron: {normalizeCron(cron) || '—'}
            </Text>
            {samples.length > 0 && (
                <Stack tokens={{ childrenGap: 4 }} styles={{ root: { marginTop: 4 } }}>
                    <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2, fontWeight: 600 } }}>
                        {intl.formatMessage(SreAgentResources.nextRunsLocalTime)}
                    </Text>
                    {samples.map((s, i) => (
                        <Text key={i} variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                            • {s}
                        </Text>
                    ))}
                </Stack>
            )}
        </Stack>
    );
};

// ---------------------------
// Component
// ---------------------------

const CreateScheduledTaskDialog: FC<CreateScheduledTaskDialogProps> = ({
    isOpen,
    onDismiss,
    onTaskCreated,
    createTask,
    generateCronExpression,
    improvePrompt,
    agentName,
}) => {
    const intl = useIntl();

    const [formData, dispatch] = useReducer(formReducer, undefined as any, () =>
        formReducer({} as any, { type: 'reset', payload: { agent: agentName } })
    );
    const [submitting, setSubmitting] = useState(false);
    const [submitError, setSubmitError] = useState<string | null>(null);
    const [presetKey, setPresetKey] = useState<PresetKey>('daily');
    const [touched, setTouched] = useState<Record<string, boolean>>({});
    const [cronAssistDescription, setCronAssistDescription] = useState('');
    const [cronAssistResult, setCronAssistResult] = useState<CronExpressionGenerationResponse | null>(null);
    const [cronAssistLoading, setCronAssistLoading] = useState(false);
    const [cronAssistError, setCronAssistError] = useState<string | null>(null);
    const [promptAssistLoading, setPromptAssistLoading] = useState(false);
    const [promptAssistError, setPromptAssistError] = useState<string | null>(null);
    const [promptAssistResult, setPromptAssistResult] = useState<ScheduledTaskPromptImprovementResponse | null>(null);

    const currentPreset = presetKey;
    const timezoneHint = useMemo(() => {
        try {
            return Intl.DateTimeFormat().resolvedOptions().timeZone;
        } catch (err) {
            console.warn('Failed to resolve timezone', err);
            return undefined;
        }
    }, []);

    const validation = useMemo(() => validate(formData), [formData]);
    const disableSubmit = submitting || hasErrors(validation);

    const update = useCallback(
        (patch: Partial<FormState>) => {
            dispatch({ type: 'merge', payload: patch });
            if (submitError) setSubmitError(null);
        },
        [submitError]
    );

    const onFieldChange = useCallback(
        (field: keyof FormState, value: any) => {
            update({ [field]: value } as Partial<FormState>);
            setTouched(t => ({ ...t, [String(field)]: true }));
        },
        [update]
    );

    const onPresetChange = useCallback(
        (option?: IDropdownOption) => {
            if (!option) return;
            const key = option.key as PresetKey;
            setPresetKey(key);
            if (key !== 'custom') {
                onFieldChange('cronExpression', String(option.data || PRESETS[key].cron));
            }
        },
        [onFieldChange]
    );

    const onCustomCronChange = useCallback(
        (value: string) => {
            setPresetKey('custom');
            onFieldChange('cronExpression', value || '');
        },
        [onFieldChange]
    );

    const handleCronAssist = useCallback(async () => {
        const description = cronAssistDescription.trim();
        if (!description) {
            setCronAssistError(intl.formatMessage(ScheduledTasksResources.cronAiDescriptionRequired));
            return;
        }

        setCronAssistLoading(true);
        setCronAssistError(null);
        setCronAssistResult(null);

        const request: CronExpressionGenerationRequest = {
            description,
            timezone: timezoneHint,
            startTime: formData.startTime,
        };

        const result = await generateCronExpression(request);

        if (result) {
            setCronAssistResult(result);
            setPresetKey('custom');
            onFieldChange('cronExpression', result.cronExpression || '');
            setTouched(t => ({ ...t, cronExpression: true }));
        } else {
            setCronAssistError(intl.formatMessage(ScheduledTasksResources.cronAiFailed));
        }

        setCronAssistLoading(false);
    }, [cronAssistDescription, generateCronExpression, timezoneHint, formData.startTime, onFieldChange, intl, setTouched]);

    const clearCronAssistState = useCallback(() => {
        setCronAssistResult(null);
        setCronAssistError(null);
    }, []);

    const handleSubmit = useCallback(async () => {
        // Touch all fields so errors render
        setTouched({ name: true, cronExpression: true, agentPrompt: true, maxExecutions: true });

        const v = validate(formData);
        if (hasErrors(v)) return;

        setSubmitting(true);
        setSubmitError(null);

        try {
            const taskRequest: CreateScheduledTaskRequest = {
                ...formData,
                cronExpression: normalizeCron(formData.cronExpression),
                agent: formData.agent || agentName,
                createdBy: formData.createdBy || 'Sub-Agent Builder',
            };

            const result = await createTask(taskRequest);
            if (result) {
                onTaskCreated();
                dispatch({ type: 'reset', payload: { agent: agentName } });
            } else {
                setSubmitError(intl.formatMessage(GenericErrorResources.failedToCreateScheduledTask));
            }
        } catch (err: any) {
            setSubmitError(err?.message || intl.formatMessage(GenericErrorResources.unexpectedError));
        } finally {
            setSubmitting(false);
        }
    }, [formData, createTask, onTaskCreated, intl]);

    const handlePromptImprovement = useCallback(async () => {
        if (!formData.agentPrompt || formData.agentPrompt.trim().length === 0) {
            setPromptAssistError(intl.formatMessage(ScheduledTasksResources.promptAiRequiresContent));
            return;
        }

        setPromptAssistLoading(true);
        setPromptAssistError(null);
        setPromptAssistResult(null);

        const result = await improvePrompt(formData.agentPrompt);

        if (result) {
            setPromptAssistResult(result);
        } else {
            setPromptAssistError(intl.formatMessage(ScheduledTasksResources.promptAiFailed));
        }

        setPromptAssistLoading(false);
    }, [formData.agentPrompt, improvePrompt, intl]);

    const applyImprovedPrompt = useCallback(() => {
        if (promptAssistResult?.improvedPrompt) {
            onFieldChange('agentPrompt', promptAssistResult.improvedPrompt);
            setTouched(t => ({ ...t, agentPrompt: true }));
        }
    }, [promptAssistResult, onFieldChange]);

    const resetPromptAssist = useCallback(() => {
        setPromptAssistResult(null);
        setPromptAssistError(null);
    }, []);

    useEffect(() => {
        if (!isOpen) {
            setCronAssistDescription('');
            clearCronAssistState();
            setPromptAssistLoading(false);
            resetPromptAssist();
        }
    }, [isOpen, clearCronAssistState, resetPromptAssist]);

    const dialogContentProps = {
        type: DialogType.close,
        title: (
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }}>
                <Add16Regular
                    style={{
                        color: '#0078d4',
                        background: 'white',
                        border: '2px solid #d1d1d1',
                        padding: 8,
                        borderRadius: '50%',
                        width: 40,
                        height: 40,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                    }}
                />
                <Stack tokens={{ childrenGap: 2 }}>
                    <Text variant="xLarge" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
                        {intl.formatMessage(ScheduledTasksResources.createScheduledTask)}
                    </Text>
                    <Text variant="medium" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                        {intl.formatMessage(ScheduledTasksResources.createScheduledTaskDescription)}
                    </Text>
                </Stack>
            </Stack>
        ),
    } as const;

    return (
        <Dialog
            hidden={!isOpen}
            onDismiss={onDismiss}
            dialogContentProps={dialogContentProps}
            modalProps={{ isBlocking: true }}
            minWidth={760}
            maxWidth={1000}
        >
            <Stack tokens={{ childrenGap: 16 }} styles={{ root: { padding: '0 4px' } }}>
                {submitError && <MessageBar messageBarType={MessageBarType.error}>{submitError}</MessageBar>}

                {/* Task Details */}
                <Stack tokens={{ childrenGap: 8 }} styles={{ root: { maxWidth: 520 } }}>
                    <SectionHeader
                        icon={<DocumentEdit16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 16 }} />}
                        title={intl.formatMessage(ScheduledTasksResources.taskDetailsSection)}
                    />

                    <TextField
                        label={intl.formatMessage(ScheduledTasksResources.name)}
                        value={formData.name}
                        onChange={(_, v) => onFieldChange('name', v || '')}
                        placeholder={intl.formatMessage(ScheduledTasksResources.namePlaceholder)}
                        required
                        errorMessage={touched.name ? validation.name : undefined}
                        styles={{ root: { maxWidth: 480 } }}
                    />

                    <TextField
                        label={intl.formatMessage(ScheduledTasksResources.description)}
                        value={formData.description}
                        onChange={(_, v) => onFieldChange('description', v || '')}
                        placeholder={intl.formatMessage(ScheduledTasksResources.descriptionPlaceholder)}
                        maxLength={140}
                        onGetErrorMessage={() => (touched.description ? validation.description : '')}
                        validateOnLoad={false}
                        styles={{ root: { maxWidth: 520 } }}
                    />
                </Stack>

                <Separator styles={{ root: { selectors: { ':before': { backgroundColor: tokens.colorNeutralStroke2 } } } }} />

                {/* Schedule */}
                <Stack tokens={{ childrenGap: 12 }}>
                    <SectionHeader
                        icon={<Timer16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 16 }} />}
                        title={intl.formatMessage(ScheduledTasksResources.scheduleSection)}
                    />

                    <Stack horizontal tokens={{ childrenGap: 24 }} wrap>
                        <Stack tokens={{ childrenGap: 12 }} styles={{ root: { maxWidth: 460, minWidth: 320 } }}>
                            <Dropdown
                                label={intl.formatMessage(ScheduledTasksResources.whenShouldTaskRun)}
                                selectedKey={currentPreset}
                                options={DROPDOWN_OPTIONS}
                                onChange={(_, option) => onPresetChange(option)}
                            />

                            {currentPreset === 'custom' && (
                                <TextField
                                    label={intl.formatMessage(ScheduledTasksResources.customCronExpression)}
                                    value={formData.cronExpression}
                                    onChange={(_, v) => onCustomCronChange(v || '')}
                                    placeholder={intl.formatMessage(ScheduledTasksResources.cronExpressionPlaceholder)}
                                    description={intl.formatMessage(ScheduledTasksResources.cronExpressionDescription)}
                                    required
                                    errorMessage={touched.cronExpression ? validation.cronExpression : undefined}
                                />
                            )}

                            <Stack
                                styles={{
                                    root: {
                                        background: tokens.colorNeutralBackground3,
                                        borderRadius: 4,
                                        padding: 12,
                                        border: `1px solid ${tokens.colorNeutralStroke1}`,
                                        boxShadow: tokens.shadow2,
                                    },
                                }}
                                tokens={{ childrenGap: 8 }}
                            >
                                <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                                    <Sparkle16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 16 }} />
                                    <Text variant="small" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
                                        {intl.formatMessage(ScheduledTasksResources.cronAiHelperTitle)}
                                    </Text>
                                </Stack>
                                <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                    {intl.formatMessage(ScheduledTasksResources.cronAiHelperDescription)}
                                </Text>
                                <TextField
                                    label={intl.formatMessage(ScheduledTasksResources.cronAiDescriptionLabel)}
                                    value={cronAssistDescription}
                                    onChange={(_, v) => {
                                        setCronAssistDescription(v || '');
                                        if (cronAssistError) {
                                            setCronAssistError(null);
                                        }
                                    }}
                                    placeholder={intl.formatMessage(ScheduledTasksResources.cronAiDescriptionPlaceholder)}
                                    multiline
                                    rows={3}
                                />
                                <Stack horizontal wrap tokens={{ childrenGap: 8 }}>
                                    <PrimaryButton
                                        onClick={handleCronAssist}
                                        disabled={cronAssistLoading}
                                        text={
                                            cronAssistLoading
                                                ? intl.formatMessage(ScheduledTasksResources.cronAiGenerating)
                                                : intl.formatMessage(ScheduledTasksResources.cronAiGenerate)
                                        }
                                    />
                                    {cronAssistResult && (
                                        <DefaultButton
                                            onClick={() => {
                                                clearCronAssistState();
                                                setCronAssistDescription('');
                                            }}
                                            disabled={cronAssistLoading}
                                            text={intl.formatMessage(ScheduledTasksResources.cronAiClear)}
                                        />
                                    )}
                                </Stack>
                                {cronAssistError && (
                                    <Text variant="xSmall" styles={{ root: { color: tokens.colorPaletteRedForeground1 } }}>
                                        {cronAssistError}
                                    </Text>
                                )}
                                {cronAssistResult && (
                                    <Stack
                                        tokens={{ childrenGap: 6 }}
                                        styles={{
                                            root: {
                                                background: tokens.colorNeutralBackground4,
                                                borderRadius: 4,
                                                padding: 12,
                                                border: `1px solid ${tokens.colorNeutralStroke2}`,
                                            },
                                        }}
                                    >
                                        <Text variant="small" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
                                            {intl.formatMessage(ScheduledTasksResources.cronAiResultHeader)}
                                        </Text>
                                        <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                            {intl.formatMessage(ScheduledTasksResources.cronAiHumanReadable)}:{' '}
                                            {cronAssistResult.humanReadableDescription || '—'}
                                        </Text>
                                        <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                            {intl.formatMessage(ScheduledTasksResources.cronAiCronLabel)}:{' '}
                                            {normalizeCron(cronAssistResult.cronExpression)}
                                        </Text>
                                        {cronAssistResult.timezone && (
                                            <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                                {intl.formatMessage(ScheduledTasksResources.cronAiTimezone)}: {cronAssistResult.timezone}
                                            </Text>
                                        )}
                                        {cronAssistResult.assumptions?.length > 0 && (
                                            <Stack tokens={{ childrenGap: 2 }}>
                                                <Text variant="xSmall" styles={{ root: { fontWeight: 600 } }}>
                                                    {intl.formatMessage(ScheduledTasksResources.cronAiAssumptions)}
                                                </Text>
                                                {cronAssistResult.assumptions.map((item, idx) => (
                                                    <Text
                                                        key={idx}
                                                        variant="xSmall"
                                                        styles={{ root: { color: tokens.colorNeutralForeground2 } }}
                                                    >
                                                        • {item}
                                                    </Text>
                                                ))}
                                            </Stack>
                                        )}
                                        {cronAssistResult.warnings?.length > 0 && (
                                            <Stack tokens={{ childrenGap: 2 }}>
                                                <Text
                                                    variant="xSmall"
                                                    styles={{ root: { fontWeight: 600, color: tokens.colorPaletteDarkOrangeForeground1 } }}
                                                >
                                                    {intl.formatMessage(ScheduledTasksResources.cronAiWarnings)}
                                                </Text>
                                                {cronAssistResult.warnings.map((item, idx) => (
                                                    <Text
                                                        key={idx}
                                                        variant="xSmall"
                                                        styles={{ root: { color: tokens.colorPaletteDarkOrangeForeground1 } }}
                                                    >
                                                        • {item}
                                                    </Text>
                                                ))}
                                            </Stack>
                                        )}
                                        {cronAssistResult.examples?.length > 0 && (
                                            <Stack tokens={{ childrenGap: 2 }}>
                                                <Text variant="xSmall" styles={{ root: { fontWeight: 600 } }}>
                                                    {intl.formatMessage(ScheduledTasksResources.cronAiExamples)}
                                                </Text>
                                                {cronAssistResult.examples.map((item, idx) => (
                                                    <Text
                                                        key={idx}
                                                        variant="xSmall"
                                                        styles={{ root: { color: tokens.colorNeutralForeground2 } }}
                                                    >
                                                        • {item}
                                                    </Text>
                                                ))}
                                            </Stack>
                                        )}
                                    </Stack>
                                )}
                            </Stack>

                            <DatePicker
                                label={intl.formatMessage(ScheduledTasksResources.endDateOptional)}
                                value={formData.endTime ? new Date(formData.endTime) : undefined}
                                onSelectDate={date =>
                                    onFieldChange('endTime', date ? new Date(date.setHours(23, 59, 59, 999)).toISOString() : undefined)
                                }
                                placeholder={intl.formatMessage(ScheduledTasksResources.endDatePlaceholder)}
                                ariaLabel={intl.formatMessage(ScheduledTasksResources.endDateAriaLabel)}
                                firstDayOfWeek={DayOfWeek.Sunday}
                                formatDate={date => (date ? date.toLocaleDateString() : '')}
                            />
                        </Stack>

                        <Stack.Item grow styles={{ root: { minWidth: 260, maxWidth: 380 } }}>
                            <PreviewCard cron={formData.cronExpression} />
                        </Stack.Item>
                    </Stack>
                </Stack>

                <Separator styles={{ root: { selectors: { ':before': { backgroundColor: tokens.colorNeutralStroke2 } } } }} />

                {/* Agent Instructions */}
                <Stack tokens={{ childrenGap: 12 }} styles={{ root: { display: 'flex', flexDirection: 'column' } }}>
                    <SectionHeader
                        icon={<Bot16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 16 }} />}
                        title={intl.formatMessage(ScheduledTasksResources.agentInstructionsSection)}
                    />

                    <TextField
                        label={intl.formatMessage(ScheduledTasksResources.agentPrompt)}
                        value={formData.agentPrompt}
                        onChange={(_, v) => onFieldChange('agentPrompt', v || '')}
                        placeholder={intl.formatMessage(ScheduledTasksResources.agentPromptPlaceholder)}
                        multiline
                        rows={8}
                        required
                        errorMessage={touched.agentPrompt ? validation.agentPrompt : undefined}
                        styles={{ fieldGroup: { minHeight: 220 }, root: { width: '100%' } }}
                    />

                    <Stack
                        styles={{
                            root: {
                                background: tokens.colorNeutralBackground3,
                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                boxShadow: tokens.shadow2,
                                padding: 12,
                                borderRadius: 4,
                                maxWidth: 860,
                            },
                        }}
                        tokens={{ childrenGap: 10 }}
                    >
                        <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                            <Bot16Regular style={{ color: tokens.colorBrandForeground1, fontSize: 16 }} />
                            <Text variant="small" styles={{ root: { fontWeight: 600, color: tokens.colorNeutralForeground1 } }}>
                                {intl.formatMessage(ScheduledTasksResources.promptAiResultHeader)}
                            </Text>
                        </Stack>
                        <Text variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                            {intl.formatMessage(ScheduledTasksResources.promptAiHelperDescription)}
                        </Text>
                        <Stack horizontal wrap tokens={{ childrenGap: 8 }}>
                            <PrimaryButton
                                onClick={handlePromptImprovement}
                                disabled={promptAssistLoading}
                                text={
                                    promptAssistLoading
                                        ? intl.formatMessage(ScheduledTasksResources.promptAiImproving)
                                        : intl.formatMessage(ScheduledTasksResources.promptAiImproveButton)
                                }
                            />
                            {promptAssistResult && (
                                <DefaultButton
                                    onClick={applyImprovedPrompt}
                                    disabled={promptAssistLoading}
                                    text={intl.formatMessage(ScheduledTasksResources.promptAiApply)}
                                />
                            )}
                            {promptAssistResult && (
                                <DefaultButton
                                    onClick={resetPromptAssist}
                                    disabled={promptAssistLoading}
                                    text={intl.formatMessage(SreAgentResources.cancel)}
                                />
                            )}
                        </Stack>
                        {promptAssistError && (
                            <Text variant="xSmall" styles={{ root: { color: tokens.colorPaletteRedForeground1 } }}>
                                {promptAssistError}
                            </Text>
                        )}
                        {promptAssistResult && (
                            <Stack tokens={{ childrenGap: 8 }}>
                                <TextField
                                    value={promptAssistResult.improvedPrompt}
                                    readOnly
                                    multiline
                                    rows={6}
                                    label={intl.formatMessage(ScheduledTasksResources.promptAiResultLabel)}
                                />
                                {promptAssistResult.warnings?.length > 0 && (
                                    <Stack tokens={{ childrenGap: 2 }}>
                                        <Text
                                            variant="xSmall"
                                            styles={{ root: { fontWeight: 600, color: tokens.colorPaletteDarkOrangeForeground1 } }}
                                        >
                                            {intl.formatMessage(ScheduledTasksResources.promptAiWarnings)}
                                        </Text>
                                        {promptAssistResult.warnings.map((warning, idx) => (
                                            <Text
                                                key={idx}
                                                variant="xSmall"
                                                styles={{ root: { color: tokens.colorPaletteDarkOrangeForeground1 } }}
                                            >
                                                • {warning}
                                            </Text>
                                        ))}
                                    </Stack>
                                )}
                                {promptAssistResult.suggestions?.length > 0 && (
                                    <Stack tokens={{ childrenGap: 2 }}>
                                        <Text variant="xSmall" styles={{ root: { fontWeight: 600 } }}>
                                            {intl.formatMessage(ScheduledTasksResources.promptAiSuggestions)}
                                        </Text>
                                        {promptAssistResult.suggestions.map((suggestion, idx) => (
                                            <Text key={idx} variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                                • {suggestion}
                                            </Text>
                                        ))}
                                    </Stack>
                                )}
                                {promptAssistResult.followUpQuestions?.length > 0 && (
                                    <Stack tokens={{ childrenGap: 2 }}>
                                        <Text variant="xSmall" styles={{ root: { fontWeight: 600 } }}>
                                            {intl.formatMessage(ScheduledTasksResources.promptAiFollowUps)}
                                        </Text>
                                        {promptAssistResult.followUpQuestions.map((question, idx) => (
                                            <Text key={idx} variant="xSmall" styles={{ root: { color: tokens.colorNeutralForeground2 } }}>
                                                • {question}
                                            </Text>
                                        ))}
                                    </Stack>
                                )}
                            </Stack>
                        )}
                    </Stack>

                    <Stack
                        styles={{
                            root: {
                                background: tokens.colorNeutralBackground3,
                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                boxShadow: tokens.shadow2,
                                padding: 10,
                                borderRadius: 4,
                                maxWidth: 860,
                                fontSize: 12,
                            },
                        }}
                        tokens={{ childrenGap: 4 }}
                    >
                        <Text variant="small" styles={{ root: { fontWeight: 600 } }}>
                            {intl.formatMessage(ScheduledTasksResources.promptTipsHeader)}
                        </Text>
                        <Text variant="xSmall">{intl.formatMessage(ScheduledTasksResources.promptTip1)}</Text>
                        <Text variant="xSmall">{intl.formatMessage(ScheduledTasksResources.promptTip2)}</Text>
                        <Text variant="xSmall">{intl.formatMessage(ScheduledTasksResources.promptTip3)}</Text>
                        <Text variant="xSmall">{intl.formatMessage(ScheduledTasksResources.promptTip4)}</Text>
                    </Stack>

                    <TextField
                        label={intl.formatMessage(ScheduledTasksResources.maxExecutions)}
                        type="number"
                        value={formData.maxExecutions?.toString() || ''}
                        onChange={(_, v) => onFieldChange('maxExecutions', v ? Math.max(1, parseInt(v, 10)) : undefined)}
                        placeholder={intl.formatMessage(ScheduledTasksResources.placeholderMaxExecutions)}
                        description={intl.formatMessage(ScheduledTasksResources.descriptionMaxExecutions)}
                        errorMessage={touched.maxExecutions ? validation.maxExecutions : undefined}
                        styles={{ root: { maxWidth: 300 } }}
                    />
                </Stack>
            </Stack>

            <DialogFooter>
                <PrimaryButton
                    onClick={handleSubmit}
                    disabled={disableSubmit}
                    text={
                        submitting
                            ? intl.formatMessage(ScheduledTasksResources.creatingScheduledTaskProgress)
                            : intl.formatMessage(ScheduledTasksResources.createScheduledTask)
                    }
                    iconProps={{ iconName: submitting ? 'Clock' : 'Add' }}
                />
                <DefaultButton onClick={onDismiss} disabled={submitting} text={intl.formatMessage(SreAgentResources.cancel)} />
            </DialogFooter>
        </Dialog>
    );
};

export default CreateScheduledTaskDialog;

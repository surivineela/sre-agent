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
import { Add16Regular, Bot16Regular, DocumentEdit16Regular, Info16Regular, Timer16Regular } from '@fluentui/react-icons';
import React, { FC, useCallback, useMemo, useReducer, useState } from 'react';
import { useIntl } from 'react-intl';
import { GenericErrorResources, ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { CreateScheduledTaskRequest } from '../Contracts/ScheduledTasks';

export interface CreateScheduledTaskDialogProps {
    isOpen: boolean;
    onDismiss: () => void;
    onTaskCreated: () => void;
    createTask: (task: CreateScheduledTaskRequest) => Promise<any>;
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
        <Text variant="large" styles={{ root: { fontWeight: 600, color: '#323130' } }}>
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
                    background: '#f3f9ff',
                    padding: 12,
                    borderRadius: 4,
                    border: '1px solid #deecf9',
                },
            }}
            tokens={{ childrenGap: 6 }}
        >
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 8 }}>
                <Info16Regular style={{ color: '#0078d4', fontSize: 14 }} />
                <Text variant="small" styles={{ root: { fontWeight: 600 } }}>
                    {intl.formatMessage(SreAgentResources.schedulePreview)}
                </Text>
            </Stack>
            <Text variant="small" styles={{ root: { color: '#605e5c' } }}>
                {desc}
            </Text>
            <Text variant="small" styles={{ root: { color: '#605e5c', fontSize: 11 } }}>
                Cron: {normalizeCron(cron) || '—'}
            </Text>
            {samples.length > 0 && (
                <Stack tokens={{ childrenGap: 4 }} styles={{ root: { marginTop: 4 } }}>
                    <Text variant="xSmall" styles={{ root: { color: '#605e5c', fontWeight: 600 } }}>
                        {intl.formatMessage(SreAgentResources.nextRunsLocalTime)}
                    </Text>
                    {samples.map((s, i) => (
                        <Text key={i} variant="xSmall" styles={{ root: { color: '#605e5c' } }}>
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

const CreateScheduledTaskDialog: FC<CreateScheduledTaskDialogProps> = ({ isOpen, onDismiss, onTaskCreated, createTask }) => {
    const intl = useIntl();

    const [formData, dispatch] = useReducer(formReducer, undefined as any, () => formReducer({} as any, { type: 'reset' }));
    const [submitting, setSubmitting] = useState(false);
    const [submitError, setSubmitError] = useState<string | null>(null);
    const [presetKey, setPresetKey] = useState<PresetKey>('daily');
    const [touched, setTouched] = useState<Record<string, boolean>>({});

    const currentPreset = presetKey;

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

    const handleSubmit = useCallback(async () => {
        // Touch all fields so errors render
        setTouched({ name: true, cronExpression: true, agentPrompt: true, maxExecutions: true });

        const v = validate(formData);
        if (hasErrors(v)) return;

        setSubmitting(true);
        setSubmitError(null);

        try {
            const result = await createTask({ ...formData, cronExpression: normalizeCron(formData.cronExpression) });
            if (result) {
                onTaskCreated();
                dispatch({ type: 'reset' });
            } else {
                setSubmitError(intl.formatMessage(GenericErrorResources.failedToCreateScheduledTask));
            }
        } catch (err: any) {
            setSubmitError(err?.message || intl.formatMessage(GenericErrorResources.unexpectedError));
        } finally {
            setSubmitting(false);
        }
    }, [formData, createTask, onTaskCreated, intl]);

    const dialogContentProps = {
        type: DialogType.close,
        title: (
            <Stack horizontal verticalAlign="center" tokens={{ childrenGap: 12 }}>
                <Add16Regular
                    style={{
                        color: '#8a8886',
                        background: '#f3f2f1',
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
                    <Text variant="xLarge" styles={{ root: { fontWeight: 600, color: '#323130' } }}>
                        {intl.formatMessage(ScheduledTasksResources.createScheduledTask)}
                    </Text>
                    <Text variant="medium" styles={{ root: { color: '#605e5c' } }}>
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
                        icon={<DocumentEdit16Regular style={{ color: '#0078d4', fontSize: 16 }} />}
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

                <Separator styles={{ root: { selectors: { ':before': { backgroundColor: '#e1dfdd' } } } }} />

                {/* Schedule */}
                <Stack tokens={{ childrenGap: 12 }}>
                    <SectionHeader
                        icon={<Timer16Regular style={{ color: '#0078d4', fontSize: 16 }} />}
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

                <Separator styles={{ root: { selectors: { ':before': { backgroundColor: '#e1dfdd' } } } }} />

                {/* Agent Instructions */}
                <Stack tokens={{ childrenGap: 12 }} styles={{ root: { display: 'flex', flexDirection: 'column' } }}>
                    <SectionHeader
                        icon={<Bot16Regular style={{ color: '#0078d4', fontSize: 16 }} />}
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
                                background: '#f8f8f8',
                                border: '1px solid #e1dfdd',
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

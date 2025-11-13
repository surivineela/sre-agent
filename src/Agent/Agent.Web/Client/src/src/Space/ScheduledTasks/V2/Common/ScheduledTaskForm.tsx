import { Dropdown, Field, InfoLabel, Input, Option, OptionOnSelectData } from '@fluentui/react-components';
import { DatePicker } from '@fluentui/react-datepicker-compat';
import { formatDateToTimeString, TimePicker } from '@fluentui/react-timepicker-compat';
import { useFormikContext } from 'formik';
import { FC, useMemo, useRef } from 'react';
import { useIntl } from 'react-intl';
import { roundTimeToNearestMinuteInterval } from '../../../../Common/Helpers/Date';
import { ScheduledTasksResources } from '../../../../Strings/SREAgentResources';
import { AgentPromptTextarea } from '../../../Components/AgentPromptTextarea';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';
import { DayOfTheWeek, getDaysOfTheWeek, GroupMessageKey, ScheduledTaskFormProps, TaskFrequencyKey } from '../ScheduledTasksUtilities';

interface FormProps {
    agents?: ExtendedAgent[];
}

export const ScheduledTaskForm: FC<FormProps> = ({ agents }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { values, setFieldValue, errors, setFieldTouched, touched } = useFormikContext<ScheduledTaskFormProps>();
    const defaultTime = useRef<Date>(roundTimeToNearestMinuteInterval(new Date(), 15));

    const frequencyOptions = useMemo(
        () => ({
            [TaskFrequencyKey.Daily]: intl.formatMessage(ScheduledTasksResources.daily),
            [TaskFrequencyKey.Weekly]: intl.formatMessage(ScheduledTasksResources.weekly),
            [TaskFrequencyKey.Monthly]: intl.formatMessage(ScheduledTasksResources.monthly),
            [TaskFrequencyKey.Custom]: intl.formatMessage(ScheduledTasksResources.customCron),
        }),
        [intl]
    );

    const daysOfTheWeekOptions = useMemo(() => getDaysOfTheWeek(intl), [intl]);

    const daysOfTheMonthOptions = useMemo<{ [key: string]: string }>(() => {
        const options = Array.from({ length: 31 }, (_, i) => {
            const day = (i + 1).toString();
            return [day, day];
        });
        return Object.fromEntries(options);
    }, []);

    const groupMessagesOptions = useMemo(
        () => ({
            [GroupMessageKey.SameThread]: intl.formatMessage(ScheduledTasksResources.useSameThread),
            [GroupMessageKey.NewThread]: intl.formatMessage(ScheduledTasksResources.newThreadForEachRun),
        }),
        [intl]
    );

    return (
        <div className={styles.taskForm}>
            <div className={styles.taskFormLeft}>
                <Field
                    label={intl.formatMessage(ScheduledTasksResources.taskName)}
                    validationState={touched.name && errors.name ? 'error' : undefined}
                    validationMessage={touched.name ? errors.name : undefined}
                    required
                >
                    <Input
                        placeholder={intl.formatMessage(ScheduledTasksResources.taskNamePlaceholder)}
                        value={values.name}
                        onChange={(_, data) => {
                            setFieldValue('name', data.value);
                        }}
                        onBlur={() => {
                            setFieldTouched('name', true);
                        }}
                    />
                </Field>
                {agents && (
                    <Field label={intl.formatMessage(ScheduledTasksResources.responseSubAgent)}>
                        <Dropdown
                            value={values.subAgent || ''}
                            selectedOptions={values.subAgent ? [values.subAgent] : []}
                            onOptionSelect={(_, data: OptionOnSelectData) => {
                                setFieldValue('subAgent', data.selectedOptions[0] || '');
                            }}
                            placeholder={intl.formatMessage(ScheduledTasksResources.responseSubAgentPlaceholder)}
                        >
                            {agents.map(agent => (
                                <Option key={agent.name} value={agent.name}>
                                    {agent.name}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>
                )}
                <AgentPromptTextarea
                    label={intl.formatMessage(ScheduledTasksResources.taskDetails)}
                    placeholder={intl.formatMessage(ScheduledTasksResources.taskDetailsPlaceholder)}
                    prompt={values.details ?? ''}
                    setPrompt={(details: string) => setFieldValue('details', details)}
                    orientation="vertical"
                    fieldProps={{
                        hint: intl.formatMessage(ScheduledTasksResources.taskDetailsTip),
                    }}
                    style={{ height: '200px' }}
                    required
                />
            </div>

            <div className={styles.taskFormDivider}></div>

            <div className={styles.taskFormRight}>
                <div className={styles.taskFormTimeFields}>
                    <Field label={intl.formatMessage(ScheduledTasksResources.frequency)}>
                        <Dropdown
                            value={frequencyOptions[(values.frequency as TaskFrequencyKey) ?? TaskFrequencyKey.Daily]}
                            selectedOptions={[values.frequency]}
                            onOptionSelect={(_, data: OptionOnSelectData) => {
                                setFieldValue('frequency', data.selectedOptions[0]);
                            }}
                        >
                            {Object.entries(frequencyOptions).map(([key, text]) => (
                                <Option key={key} value={key}>
                                    {text}
                                </Option>
                            ))}
                        </Dropdown>
                    </Field>
                    {values.frequency === TaskFrequencyKey.Weekly && (
                        <Field label={intl.formatMessage(ScheduledTasksResources.dayOfWeek)}>
                            <Dropdown
                                value={daysOfTheWeekOptions[values.dayOfWeek as DayOfTheWeek]}
                                selectedOptions={[values.dayOfWeek?.toString()]}
                                onOptionSelect={(_, data: OptionOnSelectData) => {
                                    setFieldValue('dayOfWeek', data.selectedOptions[0]);
                                }}
                            >
                                {Object.entries(daysOfTheWeekOptions).map(([key, text]) => (
                                    <Option key={key} value={key}>
                                        {text}
                                    </Option>
                                ))}
                            </Dropdown>
                        </Field>
                    )}
                    {values.frequency === TaskFrequencyKey.Monthly && (
                        <Field label={intl.formatMessage(ScheduledTasksResources.dayOfMonth)}>
                            <Dropdown
                                value={daysOfTheMonthOptions[values.dayOfMonth]}
                                selectedOptions={[values.dayOfMonth]}
                                onOptionSelect={(_, data: OptionOnSelectData) => {
                                    setFieldValue('dayOfMonth', data.selectedOptions[0]);
                                }}
                            >
                                {Object.entries(daysOfTheMonthOptions).map(([key, text]) => (
                                    <Option key={key} value={key}>
                                        {text}
                                    </Option>
                                ))}
                            </Dropdown>
                        </Field>
                    )}
                    {values.frequency === TaskFrequencyKey.Custom ? (
                        <Field label={intl.formatMessage(ScheduledTasksResources.cronExpression)}>
                            <Input
                                placeholder={intl.formatMessage(ScheduledTasksResources.cronExpressionPlaceholder)}
                                value={values.customCron}
                                onChange={(_, data) => {
                                    setFieldValue('customCron', data.value);
                                }}
                            />
                        </Field>
                    ) : (
                        // Styling is a workaround to time picker being too wide
                        // https://github.com/microsoft/fluentui/issues/34325
                        <Field style={{ gridTemplateColumns: 'auto' }} label={intl.formatMessage(ScheduledTasksResources.timeOfDay)}>
                            <TimePicker
                                type="button"
                                className={styles.timePicker}
                                increment={15}
                                value={
                                    values.timeOfDay
                                        ? formatDateToTimeString(values.timeOfDay)
                                        : formatDateToTimeString(defaultTime.current)
                                }
                                selectedTime={values.timeOfDay ?? defaultTime.current}
                                onTimeChange={(_, data) => {
                                    setFieldValue('timeOfDay', data.selectedTime);
                                }}
                            />
                        </Field>
                    )}
                </div>
                <div className={styles.taskFormDateFields}>
                    <Field label={intl.formatMessage(ScheduledTasksResources.startOn)}>
                        <DatePicker
                            value={values.startOn}
                            onSelectDate={date => {
                                setFieldValue('startOn', date);
                            }}
                        />
                    </Field>
                    <Field
                        label={intl.formatMessage(ScheduledTasksResources.repeatUntil)}
                        validationState={touched.repeatUntil && errors.repeatUntil ? 'error' : undefined}
                        validationMessage={touched.repeatUntil ? errors.repeatUntil : undefined}
                    >
                        <DatePicker
                            placeholder={intl.formatMessage(ScheduledTasksResources.endDateOptional)}
                            value={values.repeatUntil}
                            onSelectDate={date => {
                                setFieldValue('repeatUntil', date);
                            }}
                            onBlur={() => setFieldTouched('repeatUntil', true)}
                        />
                    </Field>
                </div>
                <Field label={intl.formatMessage(ScheduledTasksResources.messageGroupingForUpdates)}>
                    <Dropdown
                        value={groupMessagesOptions[values.groupMessages as GroupMessageKey]}
                        selectedOptions={[values.groupMessages]}
                        onOptionSelect={(_, data: OptionOnSelectData) => {
                            setFieldValue('groupMessages', data.selectedOptions[0]);
                        }}
                    >
                        {Object.entries(groupMessagesOptions).map(([key, text]) => (
                            <Option key={key} value={key}>
                                {text}
                            </Option>
                        ))}
                    </Dropdown>
                </Field>
                <Field
                    label={
                        <InfoLabel info={intl.formatMessage(ScheduledTasksResources.setARunLimitTooltip)}>
                            {intl.formatMessage(ScheduledTasksResources.setARunLimit)}
                        </InfoLabel>
                    }
                >
                    <Input
                        type="number"
                        placeholder={intl.formatMessage(ScheduledTasksResources.setARunLimitPlaceholder)}
                        value={values.runLimit}
                        onChange={(_, data) => {
                            setFieldValue('runLimit', data.value);
                        }}
                    />
                </Field>
            </div>
        </div>
    );
};

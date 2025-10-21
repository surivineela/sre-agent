import { Dropdown, Field, InfoLabel, Input, Option, OptionOnSelectData, Textarea } from '@fluentui/react-components';
import { DatePicker } from '@fluentui/react-datepicker-compat';
import { formatDateToTimeString, TimePicker } from '@fluentui/react-timepicker-compat';
import { useFormikContext } from 'formik';
import { useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../../../Strings/SREAgentResources';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';
import { DayOfTheWeek, getDaysOfTheWeek, GroupMessageKey, ScheduledTaskFormProps, TaskFrequencyKey } from '../ScheduledTasksUtilities';

export const ScheduledTaskForm = () => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { values, setFieldValue } = useFormikContext<ScheduledTaskFormProps>();

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
            [GroupMessageKey.SameThread]: intl.formatMessage(ScheduledTasksResources.groupInSameThread),
            [GroupMessageKey.NewThread]: intl.formatMessage(ScheduledTasksResources.startANewThread),
        }),
        [intl]
    );

    return (
        <div className={styles.taskForm}>
            <div className={styles.taskFormLeft}>
                <Field label={intl.formatMessage(ScheduledTasksResources.taskName)} required>
                    <Input
                        placeholder={intl.formatMessage(ScheduledTasksResources.taskNamePlaceholder)}
                        value={values.name}
                        onChange={(_, data) => {
                            setFieldValue('name', data.value);
                        }}
                    />
                </Field>
                <Field
                    label={intl.formatMessage(ScheduledTasksResources.taskDetails)}
                    hint={intl.formatMessage(ScheduledTasksResources.taskDetailsTip)}
                    required
                >
                    <Textarea
                        style={{ height: '160px' }}
                        placeholder={intl.formatMessage(ScheduledTasksResources.taskDetailsPlaceholder)}
                        value={values.details}
                        onChange={(_, data) => {
                            setFieldValue('details', data.value);
                        }}
                    />
                </Field>
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
                                style={{ minWidth: 'initial', gridTemplateColumns: 'minmax(0, 1fr) auto' }}
                                increment={15}
                                value={formatDateToTimeString(values.timeOfDay)}
                                selectedTime={values.timeOfDay}
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
                    <Field label={intl.formatMessage(ScheduledTasksResources.repeatUntil)}>
                        <DatePicker
                            placeholder={intl.formatMessage(ScheduledTasksResources.endDateOptional)}
                            value={values.repeatUntil}
                            onSelectDate={date => {
                                setFieldValue('repeatUntil', date);
                            }}
                        />
                    </Field>
                </div>
                <Field label={intl.formatMessage(ScheduledTasksResources.groupMessages)}>
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

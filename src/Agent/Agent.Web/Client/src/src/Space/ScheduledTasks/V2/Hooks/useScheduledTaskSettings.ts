import { useCallback, useContext, useMemo, useRef, useState } from 'react';
import { roundTimeToNearestMinuteInterval } from '../../../../Common/Helpers/Date';
import { ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { DayOfTheWeek, getCronExpression, GroupMessageKey, ScheduledTaskFormProps, TaskFrequencyKey } from '../ScheduledTasksUtilities';
import { ScheduledTasksContext } from './ScheduledTasksContext';

export const useScheduledTaskSettings = (scheduledTask?: ScheduledTask) => {
    const date = useRef<Date>(new Date());
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const { createTask } = useContext(ScheduledTasksContext);

    const initialValues: ScheduledTaskFormProps = useMemo(
        () => ({
            name: scheduledTask?.name ?? '',
            details: scheduledTask?.agentPrompt ?? '',
            frequency: TaskFrequencyKey.Daily,
            timeOfDay: roundTimeToNearestMinuteInterval(date.current, 15),
            dayOfWeek: DayOfTheWeek.Monday,
            dayOfMonth: '1',
            timeZone: 'UTC',
            startOn: date.current,
            repeatUntil: undefined,
            groupMessages: GroupMessageKey.SameThread,
            runLimit: undefined,
        }),
        [scheduledTask]
    );

    const validationSchema = useMemo(() => [], []);

    const save = useCallback(
        (values: ScheduledTaskFormProps) => {
            setIsSaving(true);

            const body = {
                name: values.name,
                description: values.name, // TODO: Replace with description?
                agentPrompt: values.details,
                createdBy: 'Sub-Agent Builder',
                cronExpression:
                    values.frequency === TaskFrequencyKey.Custom
                        ? (values.customCron ?? '')
                        : getCronExpression({
                              frequency: values.frequency,
                              timeOfDay: values.timeOfDay,
                              dayOfWeek: values.dayOfWeek,
                              dayOfMonth: values.dayOfMonth,
                          }),
                startTime: new Date(values.startOn.setHours(23, 59, 59, 999)).toISOString(),
                endTime: values.repeatUntil ? new Date(values.repeatUntil.setHours(23, 59, 59, 999)).toISOString() : undefined,
                threadId: undefined, // TODO: values.groupMessages === 'SameThread' ? use existing id : null;
                maxExecutions: Number(values.runLimit),
            };

            return createTask(body).finally(() => {
                setIsSaving(false);
            });
        },
        [createTask]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};

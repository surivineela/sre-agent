import { useCallback, useContext, useMemo, useRef, useState } from 'react';
import { roundTimeToNearestMinuteInterval } from '../../../../Common/Helpers/Date';
import { Guid } from '../../../../Common/Helpers/Guid';
import { ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { normalizeCronExpression } from '../../../Graph/ExtendedAgentCreationDialog/utils/schedule';
import { useAuthenticatedUserInfo } from '../../../Hooks/useAuthenticatedUserInfo';
import { ScheduledTaskDialogMode } from '../Common/CreateOrEditScheduledTaskDialog';
import {
    DayOfTheWeek,
    getCronExpression,
    getTimeFieldValuesFromCronExpression,
    GroupMessageKey,
    ScheduledTaskFormProps,
    TaskFrequencyKey,
} from '../ScheduledTasksUtilities';
import { ScheduledTasksContext } from './ScheduledTasksContext';

export const useScheduledTaskSettings = (mode: ScheduledTaskDialogMode, scheduledTask?: ScheduledTask) => {
    const date = useRef<Date>(new Date());
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const { createTask, updateTask } = useContext(ScheduledTasksContext);
    const {
        userIdAndDisplayName: { displayName },
    } = useAuthenticatedUserInfo();

    const initialValues: ScheduledTaskFormProps = useMemo(() => {
        const {
            frequency: scheduledTaskFrequency,
            timeOfDay: scheduledTaskTimeOfDay,
            dayOfWeek: scheduledTaskDayOfWeek,
            dayOfMonth: scheduledTaskDayOfMonth,
        } = getTimeFieldValuesFromCronExpression(scheduledTask?.cronExpression);

        return {
            name: scheduledTask?.name ?? '',
            details: scheduledTask?.agentPrompt ?? '',
            frequency: scheduledTaskFrequency ?? TaskFrequencyKey.Daily,
            timeOfDay: scheduledTaskTimeOfDay ?? roundTimeToNearestMinuteInterval(date.current, 15),
            dayOfWeek: scheduledTaskDayOfWeek ?? DayOfTheWeek.Monday,
            dayOfMonth: scheduledTaskDayOfMonth ?? '1',
            customCron: scheduledTask?.cronExpression ?? '',
            startOn: scheduledTask?.startTime ? new Date(scheduledTask.startTime) : date.current,
            repeatUntil: scheduledTask?.endTime ? new Date(scheduledTask.endTime) : undefined,
            groupMessages: scheduledTask?.threadId ? GroupMessageKey.SameThread : GroupMessageKey.SameThread,
            runLimit: scheduledTask?.maxExecutions?.toString() ?? undefined,
        };
    }, [scheduledTask]);

    const validationSchema = useMemo(() => [], []);

    const save = useCallback(
        (values: ScheduledTaskFormProps) => {
            setIsSaving(true);

            const body = {
                name: values.name,
                description: values.name,
                agentPrompt: values.details,
                createdBy: displayName ?? 'Sub-Agent Builder',
                cronExpression:
                    values.frequency === TaskFrequencyKey.Custom
                        ? normalizeCronExpression(values.customCron ?? '')
                        : getCronExpression({
                              frequency: values.frequency,
                              timeOfDay: values.timeOfDay,
                              dayOfWeek: values.dayOfWeek,
                              dayOfMonth: values.dayOfMonth,
                          }),
                startTime: new Date(values.startOn.setHours(23, 59, 59, 999)).toISOString(),
                endTime: values.repeatUntil ? new Date(values.repeatUntil.setHours(23, 59, 59, 999)).toISOString() : undefined,
                threadId: values.groupMessages === GroupMessageKey.SameThread ? Guid.newGuid() : undefined,
                maxExecutions: Number(values.runLimit),
            };

            return mode === ScheduledTaskDialogMode.Create
                ? createTask(body).finally(() => {
                      setIsSaving(false);
                  })
                : updateTask(scheduledTask?.id ?? '', body).finally(() => {
                      setIsSaving(false);
                  });
        },
        [createTask, displayName, mode, scheduledTask?.id, updateTask]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};

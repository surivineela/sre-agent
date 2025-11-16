import { useCallback, useContext, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { mixed, object, string } from 'yup';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../../../Common/Clients/ArmClient';
import { roundTimeToNearestMinuteInterval } from '../../../../Common/Helpers/Date';
import { Guid } from '../../../../Common/Helpers/Guid';
import { ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { CreateScheduledTaskRequest, ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { normalizeCronExpression } from '../../../Graph/ExtendedAgentCreationDialog/utils/schedule';
import { useAuthenticatedUserInfo } from '../../../Hooks/useAuthenticatedUserInfo';
import { ScheduledTaskDialogMode } from '../Common/ScheduledTaskCreateOrEditDialog';
import {
    DayOfTheWeek,
    getCronExpression,
    getTimeFieldValuesFromCronExpression,
    GroupMessageKey,
    ScheduledTaskFormProps,
    TaskFrequencyKey,
} from '../ScheduledTasksUtilities';
import { ScheduledTasksContext } from './ScheduledTasksContext';

export const useScheduledTaskSettings = (mode: ScheduledTaskDialogMode, scheduledTask?: ScheduledTask, startingAgent?: string) => {
    const intl = useIntl();
    const date = useRef<Date>(new Date());
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const azPortalContext = useContext(AzPortalContext);
    const { createTask, updateTask, setIsOperationInProgress } = useContext(ScheduledTasksContext);
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
            subAgent: startingAgent,
            details: scheduledTask?.agentPrompt ?? '',
            frequency: scheduledTaskFrequency ?? TaskFrequencyKey.Daily,
            timeOfDay: scheduledTaskTimeOfDay ?? roundTimeToNearestMinuteInterval(date.current, 15),
            dayOfWeek: scheduledTaskDayOfWeek ?? DayOfTheWeek.Monday,
            dayOfMonth: scheduledTaskDayOfMonth ?? '1',
            customCron: scheduledTask?.cronExpression ?? '',
            startOn: scheduledTask?.startTime ? new Date(scheduledTask.startTime) : date.current,
            repeatUntil: scheduledTask?.endTime ? new Date(scheduledTask.endTime) : null,
            groupMessages: scheduledTask?.threadId === null ? GroupMessageKey.NewThread : GroupMessageKey.SameThread,
            runLimit: scheduledTask?.maxExecutions?.toString() ?? undefined,
        };
    }, [scheduledTask, startingAgent]);

    const validationSchema = useMemo(
        () =>
            object({
                name: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                details: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                repeatUntil: mixed()
                    .nullable()
                    .test(
                        'validateEndDateIsAfterStartDate',
                        intl.formatMessage(ScheduledTasksResources.endDateValidationMessage),
                        (value: any, context: any) => {
                            const startDate: Date = context.parent?.startOn;
                            if (value) {
                                return value.getTime() > startDate.getTime();
                            }
                            return true;
                        }
                    ),
            }),
        [intl]
    );

    const save = useCallback(
        async (values: ScheduledTaskFormProps) => {
            setIsSaving(true);
            setIsOperationInProgress(true);

            const body: CreateScheduledTaskRequest = {
                name: values.name,
                agent: values.subAgent,
                description: values.name,
                agentPrompt: values.details,
                createdBy: displayName ?? 'Sub-Agent Builder',
                cronExpression:
                    values.frequency === TaskFrequencyKey.Custom
                        ? normalizeCronExpression(values.customCron ?? '')
                        : getCronExpression({
                              frequency: values.frequency,
                              timeOfDay: values.timeOfDay ?? date.current,
                              dayOfWeek: values.dayOfWeek,
                              dayOfMonth: values.dayOfMonth,
                          }),
                startTime: values.startOn?.toISOString(),
                endTime: values.repeatUntil?.toISOString(),
                // For SameThread mode: Generate or keep a dedicated thread ID
                //   - When creating: Generate new GUID for dedicated thread
                //   - When editing: Keep existing threadId
                // For NewThread mode: undefined so a new thread is created each execution
                threadId:
                    values.groupMessages === GroupMessageKey.SameThread
                        ? mode === ScheduledTaskDialogMode.Edit && scheduledTask?.threadId
                            ? scheduledTask.threadId // Keep existing threadId when editing
                            : Guid.newGuid() // Generate dedicated thread ID when creating
                        : undefined, // undefined for NewThread = create new thread each time
                maxExecutions: Number(values.runLimit),
            };

            if (mode === ScheduledTaskDialogMode.Create) {
                const notificationId = azPortalContext.startNotification(
                    intl.formatMessage(ScheduledTasksResources.createTaskTitle),
                    intl.formatMessage(ScheduledTasksResources.createTaskInProgress)
                );
                try {
                    setIsOperationInProgress(true);
                    const response = await createTask(body);
                    if (response.isSuccessful) {
                        azPortalContext.stopNotification(
                            notificationId,
                            true,
                            intl.formatMessage(ScheduledTasksResources.taskCreatedSuccessfully)
                        );
                    } else {
                        azPortalContext.stopNotification(
                            notificationId,
                            false,
                            intl.formatMessage(ScheduledTasksResources.failedToCreateTask, { errorMessage: response.error })
                        );
                    }
                    return response;
                } catch (error) {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.failedToCreateTask, { errorMessage: getErrorMessageOrStringify(error) })
                    );
                } finally {
                    setIsSaving(false);
                    setIsOperationInProgress(false);
                }
            } else {
                const notificationId = azPortalContext.startNotification(
                    intl.formatMessage(ScheduledTasksResources.updateTaskTitle),
                    intl.formatMessage(ScheduledTasksResources.updateTaskInProgress)
                );
                try {
                    setIsOperationInProgress(true);
                    const response = await updateTask(scheduledTask?.id ?? '', body);
                    if (response.isSuccessful) {
                        azPortalContext.stopNotification(
                            notificationId,
                            true,
                            intl.formatMessage(ScheduledTasksResources.taskUpdatedSuccessfully)
                        );
                    } else {
                        azPortalContext.stopNotification(
                            notificationId,
                            false,
                            intl.formatMessage(ScheduledTasksResources.failedToUpdateTask, { errorMessage: response.error })
                        );
                    }

                    return response;
                } catch (error) {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.failedToUpdateTask, { errorMessage: getErrorMessageOrStringify(error) })
                    );
                } finally {
                    setIsSaving(false);
                    setIsOperationInProgress(false);
                }
            }
        },
        [azPortalContext, createTask, displayName, intl, mode, scheduledTask?.id, setIsOperationInProgress, updateTask]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};

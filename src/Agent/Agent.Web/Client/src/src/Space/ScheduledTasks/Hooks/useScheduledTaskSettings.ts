import { useCallback, useContext, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { mixed, object, string } from 'yup';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { roundTimeToNearestMinuteInterval } from '../../../Common/Helpers/Date';
import { Guid } from '../../../Common/Helpers/Guid';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { CreateScheduledTaskRequest, ScheduledTask } from '../../Contracts/ScheduledTasks';
import { normalizeCronExpression } from '../../Graph/ExtendedAgentCreationDialog/utils/schedule';
import { useAuthenticatedUserInfo } from '../../Hooks/useAuthenticatedUserInfo';
import { ScheduledTaskDialogMode } from '../Common/ScheduledTaskCreateOrEditDialog';
import {
    DayOfTheWeek,
    getCronExpression,
    getTimeFieldValuesFromCronExpression,
    GroupMessageKey,
    ScheduledTaskFormProps,
    TaskFrequencyKey,
    validateCronExpression,
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
            subAgent: scheduledTask?.agent ?? startingAgent,
            details: scheduledTask?.agentPrompt ?? '',
            frequency: scheduledTaskFrequency ?? TaskFrequencyKey.Daily,
            timeOfDay: scheduledTaskTimeOfDay ?? roundTimeToNearestMinuteInterval(date.current, 15),
            dayOfWeek: scheduledTaskDayOfWeek ?? DayOfTheWeek.Monday,
            dayOfMonth: scheduledTaskDayOfMonth ?? '1',
            customCron: scheduledTask?.cronExpression ?? '',
            startOn: scheduledTask?.startTime ? new Date(scheduledTask.startTime) : date.current,
            repeatUntil: scheduledTask?.endTime ? new Date(scheduledTask.endTime) : null,
            // When creating: default to SameThread (current UX behavior)
            // When editing: check if threadId has a value (SameThread) or is null (NewThread)
            groupMessages: scheduledTask
                ? scheduledTask.threadId
                    ? GroupMessageKey.SameThread
                    : GroupMessageKey.NewThread
                : GroupMessageKey.SameThread,
            runLimit: scheduledTask?.maxExecutions?.toString() ?? undefined,
        };
    }, [scheduledTask, startingAgent]);

    const validationSchema = useMemo(
        () =>
            object({
                name: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                details: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                customCron: string().test(
                    'validateCustomCron',
                    intl.formatMessage(ScheduledTasksResources.invalidCronExpression),
                    function (value: string | undefined) {
                        // Only validate if we're in custom frequency mode and value is not empty
                        const { frequency } = this.parent;
                        if (frequency !== TaskFrequencyKey.Custom) {
                            return true; // Skip validation if not in custom mode
                        }

                        if (!value || value.trim() === '') {
                            return false; // Required when in custom mode
                        }

                        const validation = validateCronExpression(value, intl);
                        return validation.isValid;
                    }
                ),
                repeatUntil: mixed()
                    .nullable()
                    .test(
                        'validateEndDateIsAfterStartDate',
                        intl.formatMessage(ScheduledTasksResources.repeatUntilValidationMessage),
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
                // For NewThread mode: null so backend clears the threadId and creates new thread each execution
                threadId:
                    values.groupMessages === GroupMessageKey.SameThread
                        ? mode === ScheduledTaskDialogMode.Edit && scheduledTask?.threadId
                            ? scheduledTask.threadId // Keep existing threadId when editing
                            : Guid.newGuid() // Generate dedicated thread ID when creating
                        : null, // null for NewThread = create new thread each time (null serializes in JSON unlike undefined)
                maxExecutions: Number(values.runLimit),
            };

            if (mode === ScheduledTaskDialogMode.Create) {
                const notificationId = azPortalContext.startNotification(
                    intl.formatMessage(ScheduledTasksResources.createScheduledTaskNotificationTitle),
                    intl.formatMessage(ScheduledTasksResources.createScheduledTaskNotificationInProgress, {
                        name: values.name,
                    })
                );
                setIsOperationInProgress(true);
                const response = await createTask(body);
                if (response.isSuccessful) {
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.createScheduledTaskNotificationSuccess, {
                            name: values.name,
                        })
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.createScheduledTaskNotificationFailure, {
                            errorMessage: response.error,
                        })
                    );
                }
                setIsSaving(false);
                setIsOperationInProgress(false);
                return response;
            } else {
                const notificationId = azPortalContext.startNotification(
                    intl.formatMessage(ScheduledTasksResources.updateScheduledTaskNotificationTitle),
                    intl.formatMessage(ScheduledTasksResources.updateScheduledTaskNotificationInProgress, {
                        name: values.name,
                    })
                );
                setIsOperationInProgress(true);
                const response = await updateTask(scheduledTask?.id ?? '', body);
                if (response.isSuccessful) {
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.updateScheduledTaskNotificationSuccess, {
                            name: values.name,
                        })
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.updateScheduledTaskNotificationFailure, {
                            errorMessage: response.error,
                        })
                    );
                }
                setIsSaving(false);
                setIsOperationInProgress(false);
                return response;
            }
        },
        [
            azPortalContext,
            createTask,
            displayName,
            intl,
            mode,
            scheduledTask?.id,
            scheduledTask?.threadId,
            setIsOperationInProgress,
            updateTask,
        ]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};

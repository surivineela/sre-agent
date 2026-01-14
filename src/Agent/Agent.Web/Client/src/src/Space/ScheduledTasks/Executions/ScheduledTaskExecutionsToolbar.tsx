import { Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import {
    ArrowClockwise20Regular,
    ArrowClockwiseRegular,
    DeleteRegular,
    PlayRegular,
    RecordStopRegular,
    ReplayRegular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { getLocaleTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { ScheduledTaskDeleteDialog } from '../Common/ScheduledTaskDeleteDialog';
import { ScheduledTasksContext } from '../Hooks/ScheduledTasksContext';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';

export enum ExecutionStatusFilterKey {
    All = 'all',
    Success = 'success',
    Failed = 'failed',
}

interface ScheduledTaskExecutionsToolbarProps {
    task: ScheduledTask;
    isLoading?: boolean;
    statusFilter: ExecutionStatusFilterKey;
    setStatusFilter: (status: ExecutionStatusFilterKey) => void;
    refreshExecutions: () => Promise<void>;
}

export const ScheduledTaskExecutionsToolbar: FC<ScheduledTaskExecutionsToolbarProps> = ({
    task,
    isLoading = false,
    statusFilter,
    setStatusFilter,
    refreshExecutions,
}) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const azPortalContext = useContext(AzPortalContext);
    const { refreshTasks, pauseTask, resumeTask, runTask, deleteTask, isOperationInProgress, setIsOperationInProgress } =
        useContext(ScheduledTasksContext);
    const [lastUpdated, setLastUpdated] = useState<string>();

    const isPauseButtonDisabled = useMemo(() => {
        return task.status !== ScheduledTaskStatus.Active || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, task.status]);

    const isResumeButtonDisabled = useMemo(() => {
        return task.status !== ScheduledTaskStatus.Paused || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, task.status]);

    const isRunButtonDisabled = useMemo(() => isLoading || isOperationInProgress, [isLoading, isOperationInProgress]);

    const isDeleteButtonDisabled = useMemo(() => isLoading || isOperationInProgress, [isLoading, isOperationInProgress]);

    const onRefreshClick = useCallback(async () => {
        await refreshExecutions();
    }, [refreshExecutions]);

    const onPauseTask = useCallback(async () => {
        const { id, name } = task;

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationTitleSingle),
            intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationInProgressSingle, { name: name ?? id })
        );

        setIsOperationInProgress(true);
        const response = await pauseTask(id);
        if (response.isSuccessful) {
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationSuccessSingle, { name: name ?? id })
            );
            await refreshTasks();
            await refreshExecutions();
        } else {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationFailure, { errorMessage: response.error })
            );
        }
        setIsOperationInProgress(false);
    }, [azPortalContext, intl, refreshExecutions, pauseTask, refreshTasks, setIsOperationInProgress, task]);

    const onResumeTask = useCallback(async () => {
        const { id, name } = task;

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationTitleSingle),
            intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationInProgressSingle, { name: name ?? id })
        );

        setIsOperationInProgress(true);
        const response = await resumeTask(id);
        if (response.isSuccessful) {
            await refreshTasks();
            await refreshExecutions();
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationSuccessSingle, { name: name ?? id })
            );
        } else {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationFailure, { errorMessage: response.error })
            );
        }
        setIsOperationInProgress(false);
    }, [azPortalContext, intl, refreshExecutions, refreshTasks, resumeTask, setIsOperationInProgress, task]);

    const onRunTask = useCallback(async () => {
        const { id, name } = task;

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationTitleSingle),
            intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationInProgressSingle, { name: name ?? id })
        );

        setIsOperationInProgress(true);
        const response = await runTask(id);
        if (response.isSuccessful) {
            await refreshTasks();
            await refreshExecutions();
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationSuccessSingle, { name: name ?? id })
            );
        } else {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationFailure, { errorMessage: response.error })
            );
        }
        setIsOperationInProgress(false);
    }, [azPortalContext, intl, refreshExecutions, refreshTasks, runTask, setIsOperationInProgress, task]);

    const onDeleteTask = useCallback(async () => {
        const { id, name } = task;

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleSingle),
            intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationInProgressSingle, { name: name ?? id })
        );

        setIsOperationInProgress(true);
        const response = await deleteTask(id);
        if (response.isSuccessful) {
            await refreshTasks();
            await refreshExecutions();
            azPortalContext.stopNotification(
                notificationId,
                true,
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationSuccessSingle, { name: name ?? id })
            );
        } else {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationFailure, { errorMessage: response.error })
            );
        }
        setIsOperationInProgress(false);
    }, [azPortalContext, deleteTask, intl, refreshExecutions, refreshTasks, setIsOperationInProgress, task]);

    useEffect(() => {
        if (!isLoading) {
            setLastUpdated(getLocaleTimeHHMM(new Date()));
        }
    }, [isLoading]);

    const statusOptions = useMemo(
        () => [
            {
                key: ExecutionStatusFilterKey.All,
                label: intl.formatMessage(ScheduledTasksResources.statusFilterAll),
            },
            {
                key: ExecutionStatusFilterKey.Success,
                label: intl.formatMessage(ScheduledTasksResources.executionSuccess),
            },
            {
                key: ExecutionStatusFilterKey.Failed,
                label: intl.formatMessage(ScheduledTasksResources.executionFailed),
            },
        ],
        [intl]
    );

    return (
        <div className={styles.toolbar}>
            <div className={styles.toolbarButtons}>
                <Toolbar style={{ padding: 0 }}>
                    <ToolbarButton className={styles.toolbarButton} icon={<ArrowClockwiseRegular />} onClick={onRefreshClick}>
                        {intl.formatMessage(ScheduledTasksResources.updateList)}
                    </ToolbarButton>
                    <ToolbarDivider />
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<RecordStopRegular />}
                        onClick={onPauseTask}
                        disabled={isPauseButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOff)}
                    </ToolbarButton>
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<ReplayRegular />}
                        onClick={onResumeTask}
                        disabled={isResumeButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOn)}
                    </ToolbarButton>
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<PlayRegular />}
                        onClick={onRunTask}
                        disabled={isRunButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.runTaskNow)}
                    </ToolbarButton>
                    <ScheduledTaskDeleteDialog
                        dialogTrigger={
                            <ToolbarButton className={styles.toolbarButton} icon={<DeleteRegular />} disabled={isDeleteButtonDisabled}>
                                {intl.formatMessage(SreAgentResources.delete)}
                            </ToolbarButton>
                        }
                        deleteTasks={onDeleteTask}
                        title={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleSingle)}
                        content={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmationDescriptionSingle)}
                    />
                </Toolbar>
                <div className={styles.filters}>
                    <PillFilter
                        label={`${intl.formatMessage(SreAgentResources.status)}`}
                        filterType="combobox"
                        options={statusOptions}
                        selectedKeys={[statusFilter]}
                        onApply={keys => {
                            setStatusFilter(keys[0] as ExecutionStatusFilterKey);
                        }}
                    />
                </div>
            </div>
            {lastUpdated && (
                <div className={styles.menuItems}>
                    <ArrowClockwise20Regular />
                    <Text>{`${intl.formatMessage(ScheduledTasksResources.lastUpdated)}: ${lastUpdated}`}</Text>
                </div>
            )}
        </div>
    );
};

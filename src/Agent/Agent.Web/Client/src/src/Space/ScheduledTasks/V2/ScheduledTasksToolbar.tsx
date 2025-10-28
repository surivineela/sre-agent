import { SearchBox, Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import {
    AddRegular,
    ArrowClockwise20Regular,
    ArrowClockwiseRegular,
    DeleteRegular,
    RecordStopRegular,
    ReplayRegular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../../Common/Clients/ArmClient';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { getLocaleTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { CreateOrEditScheduledTaskDialog, ScheduledTaskDialogMode } from './Common/CreateOrEditScheduledTaskDialog';
import { DeleteScheduledTaskDialog } from './Common/DeleteScheduledTaskDialog';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { TaskStatusFilterKey } from './ScheduledTasksUtilities';

interface ScheduledTasksToolbarProps {
    selectedTasks?: ScheduledTask[];
    isLoading?: boolean;
    searchQuery: string;
    setSearchQuery: (query: string) => void;
    statusFilter: TaskStatusFilterKey;
    setStatusFilter: (status: TaskStatusFilterKey) => void;
}

export const ScheduledTasksToolbar: FC<ScheduledTasksToolbarProps> = ({
    selectedTasks,
    isLoading = false,
    searchQuery,
    setSearchQuery,
    statusFilter,
    setStatusFilter,
}) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const azPortalContext = useContext(AzPortalContext);
    const { refreshTasks, pauseTask, resumeTask, deleteTask, isOperationInProgress, setIsOperationInProgress } =
        useContext(ScheduledTasksContext);
    const [lastUpdated, setLastUpdated] = useState<string>();

    const isPauseButtonDisabled = useMemo(() => {
        const hasActiveTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Active);
        return !hasActiveTaskSelected || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, selectedTasks]);

    const isResumeButtonDisabled = useMemo(() => {
        const hasPausedTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Paused);
        return !hasPausedTaskSelected || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, selectedTasks]);

    const isDeleteButtonDisabled = useMemo(
        () => selectedTasks?.length === 0 || isLoading || isOperationInProgress,
        [isLoading, isOperationInProgress, selectedTasks?.length]
    );

    const onRefresh = useCallback(async () => {
        await refreshTasks();
    }, [refreshTasks]);

    const onPauseTasks = useCallback(async () => {
        const activeTasks = selectedTasks?.filter(task => task.status === ScheduledTaskStatus.Active) || [];

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.pauseTasksTitle),
            intl.formatMessage(ScheduledTasksResources.pauseTasksInProgress)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(activeTasks.map(task => pauseTask(task.id)));
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(notificationId, true, intl.formatMessage(ScheduledTasksResources.tasksPausedSuccessfully));
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToPauseTask, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.failedToPauseTask, { errorMessage: getErrorMessageOrStringify(error) })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [azPortalContext, intl, pauseTask, refreshTasks, selectedTasks, setIsOperationInProgress]);

    const onResumeTasks = useCallback(async () => {
        const pausedTasks = selectedTasks?.filter(task => task.status === ScheduledTaskStatus.Paused) || [];

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.resumeTasksTitle),
            intl.formatMessage(ScheduledTasksResources.resumeTasksInProgress)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(pausedTasks.map(task => resumeTask(task.id)));
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.tasksResumedSuccessfully)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToResumeTask, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.failedToResumeTask, { errorMessage: getErrorMessageOrStringify(error) })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [selectedTasks, azPortalContext, intl, setIsOperationInProgress, resumeTask, refreshTasks]);

    const onDeleteTasks = useCallback(async () => {
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.deleteTasksTitle),
            intl.formatMessage(ScheduledTasksResources.deleteTasksInProgress)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(selectedTasks?.map(task => deleteTask(task.id)) || []);
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.tasksDeletedSuccessfully)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToDeleteTask, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.failedToDeleteTask, { errorMessage: getErrorMessageOrStringify(error) })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [azPortalContext, deleteTask, intl, refreshTasks, selectedTasks, setIsOperationInProgress]);

    useEffect(() => {
        if (!isLoading) {
            setLastUpdated(getLocaleTimeHHMM(new Date()));
        }
    }, [isLoading]);

    return (
        <div className={styles.toolbar}>
            <div className={styles.toolbarButtons}>
                <Toolbar style={{ padding: 0 }}>
                    <CreateOrEditScheduledTaskDialog
                        dialogTrigger={
                            <ToolbarButton className={styles.toolbarButton} icon={<AddRegular />}>
                                {intl.formatMessage(ScheduledTasksResources.createTask)}
                            </ToolbarButton>
                        }
                        mode={ScheduledTaskDialogMode.Create}
                    />
                    <ToolbarButton className={styles.toolbarButton} icon={<ArrowClockwiseRegular />} onClick={onRefresh}>
                        {intl.formatMessage(ScheduledTasksResources.updateList)}
                    </ToolbarButton>
                    <ToolbarDivider />
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<RecordStopRegular />}
                        onClick={onPauseTasks}
                        disabled={isPauseButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOff)}
                    </ToolbarButton>
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<ReplayRegular />}
                        onClick={onResumeTasks}
                        disabled={isResumeButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOn)}
                    </ToolbarButton>
                    <DeleteScheduledTaskDialog
                        dialogTrigger={
                            <ToolbarButton className={styles.toolbarButton} icon={<DeleteRegular />} disabled={isDeleteButtonDisabled}>
                                {intl.formatMessage(SreAgentResources.delete)}
                            </ToolbarButton>
                        }
                        deleteTasks={onDeleteTasks}
                        title={intl.formatMessage(ScheduledTasksResources.deleteTasksConfirmationTitle)}
                        content={intl.formatMessage(ScheduledTasksResources.deleteTasksConfirmationMessage)}
                    />
                </Toolbar>
                <ScheduledTasksFilters
                    searchQuery={searchQuery}
                    setSearchQuery={setSearchQuery}
                    statusFilter={statusFilter}
                    setStatusFilter={setStatusFilter}
                />
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

interface ScheduledTasksFiltersProps {
    searchQuery: string;
    setSearchQuery: (query: string) => void;
    statusFilter: TaskStatusFilterKey;
    setStatusFilter: (status: TaskStatusFilterKey) => void;
}

const ScheduledTasksFilters: FC<ScheduledTasksFiltersProps> = ({ searchQuery, setSearchQuery, statusFilter, setStatusFilter }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();

    const statusOptions = useMemo(
        () => [
            {
                key: TaskStatusFilterKey.All,
                label: intl.formatMessage(ScheduledTasksResources.all),
            },
            {
                key: TaskStatusFilterKey.On,
                label: intl.formatMessage(ScheduledTasksResources.on),
            },
            {
                key: TaskStatusFilterKey.Off,
                label: intl.formatMessage(ScheduledTasksResources.off),
            },
            {
                key: TaskStatusFilterKey.Completed,
                label: intl.formatMessage(ScheduledTasksResources.completed),
            },
        ],
        [intl]
    );

    return (
        <div className={styles.filters}>
            <SearchBox
                value={searchQuery}
                onChange={(_, data) => setSearchQuery(data.value)}
                placeholder={intl.formatMessage(ScheduledTasksResources.filterTasks)}
            />
            <PillFilter
                label={`${intl.formatMessage(SreAgentResources.status)}`}
                filterType="combobox"
                options={statusOptions}
                selectedKeys={[statusFilter]}
                onApply={keys => {
                    setStatusFilter(keys[0] as TaskStatusFilterKey);
                }}
            />
        </div>
    );
};

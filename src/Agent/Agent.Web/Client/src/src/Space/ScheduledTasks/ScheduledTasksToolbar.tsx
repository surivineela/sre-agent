import { SearchBox, Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import {
    AddRegular,
    ArrowClockwise20Regular,
    ArrowClockwiseRegular,
    DeleteRegular,
    PlayRegular,
    RecordStopRegular,
    ReplayRegular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../Common/Clients/ArmClient';
import { PillFilter } from '../../Common/Components/PillFilter/PillFilter';
import { getLocaleTimeHHMM } from '../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../Contracts/ScheduledTasks';
import { ScheduledTaskCreateOrEditDialog, ScheduledTaskDialogMode } from './Common/ScheduledTaskCreateOrEditDialog';
import { ScheduledTaskDeleteDialog } from './Common/ScheduledTaskDeleteDialog';
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
    const { refreshTasks, pauseTask, resumeTask, runTask, deleteTask, isOperationInProgress, setIsOperationInProgress } =
        useContext(ScheduledTasksContext);
    const [lastUpdated, setLastUpdated] = useState<string>();
    const [isCreateDialogOpen, setIsCreateDialogOpen] = useState<boolean>(false);

    const isPauseButtonDisabled = useMemo(() => {
        const hasActiveTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Active);
        return !hasActiveTaskSelected || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, selectedTasks]);

    const isResumeButtonDisabled = useMemo(() => {
        const hasPausedTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Paused);
        return !hasPausedTaskSelected || isLoading || isOperationInProgress;
    }, [isLoading, isOperationInProgress, selectedTasks]);

    const isRunButtonDisabled = useMemo(
        () => selectedTasks?.length === 0 || isLoading || isOperationInProgress,
        [isLoading, isOperationInProgress, selectedTasks?.length]
    );

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
            intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationTitleMultiple),
            intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationInProgressMultiple)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(activeTasks.map(task => pauseTask(task.id)));
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationSuccessMultiple)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationError, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationError, {
                    errorMessage: getErrorMessageOrStringify(error),
                })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [azPortalContext, intl, pauseTask, refreshTasks, selectedTasks, setIsOperationInProgress]);

    const onRunTasks = useCallback(async () => {
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationTitleMultiple),
            intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationInProgressMultiple)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(selectedTasks?.map(task => runTask(task.id)) || []);
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationSuccessMultiple)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationError, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationError, {
                    errorMessage: getErrorMessageOrStringify(error),
                })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [azPortalContext, intl, refreshTasks, runTask, selectedTasks, setIsOperationInProgress]);

    const onResumeTasks = useCallback(async () => {
        const pausedTasks = selectedTasks?.filter(task => task.status === ScheduledTaskStatus.Paused) || [];

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationTitleMultiple),
            intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationInProgressMultiple)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(pausedTasks.map(task => resumeTask(task.id)));
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationSuccessMultiple)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationError, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationError, {
                    errorMessage: getErrorMessageOrStringify(error),
                })
            );
        } finally {
            setIsOperationInProgress(false);
        }
    }, [selectedTasks, azPortalContext, intl, setIsOperationInProgress, resumeTask, refreshTasks]);

    const onDeleteTasks = useCallback(async () => {
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleMultiple),
            intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationInProgressMultiple)
        );

        try {
            setIsOperationInProgress(true);
            const responses = await Promise.all(selectedTasks?.map(task => deleteTask(task.id)) || []);
            if (responses.some(response => response.isSuccessful)) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationSuccessMultiple)
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationError, {
                        errorMessage: responses.find(r => !r.isSuccessful)?.error,
                    })
                );
            }
        } catch (error) {
            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationError, {
                    errorMessage: getErrorMessageOrStringify(error),
                })
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
                    <ScheduledTaskCreateOrEditDialog
                        dialogTrigger={
                            <ToolbarButton className={styles.toolbarButton} icon={<AddRegular />}>
                                {intl.formatMessage(ScheduledTasksResources.createTask)}
                            </ToolbarButton>
                        }
                        isDialogOpen={isCreateDialogOpen}
                        setIsDialogOpen={setIsCreateDialogOpen}
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
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<PlayRegular />}
                        onClick={onRunTasks}
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
                        deleteTasks={onDeleteTasks}
                        title={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleMultiple)}
                        content={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmationDescriptionMultiple)}
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

export const ScheduledTasksFilters: FC<ScheduledTasksFiltersProps> = ({ searchQuery, setSearchQuery, statusFilter, setStatusFilter }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();

    const statusOptions = useMemo(
        () => [
            {
                key: TaskStatusFilterKey.All,
                label: intl.formatMessage(SreAgentResources.all),
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
                key: TaskStatusFilterKey.Ended,
                label: intl.formatMessage(ScheduledTasksResources.ended),
            },
        ],
        [intl]
    );

    return (
        <div className={styles.filters}>
            <SearchBox
                className={styles.searchBox}
                value={searchQuery}
                onChange={(_, data) => setSearchQuery(data.value)}
                placeholder={intl.formatMessage(ScheduledTasksResources.searchByScheduledTask)}
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

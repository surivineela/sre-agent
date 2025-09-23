import { MessageBar, MessageBarType } from '@fluentui/react/lib/MessageBar';
import { Spinner } from '@fluentui/react/lib/Spinner';
import { Text } from '@fluentui/react/lib/Text';
import { FC, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask } from '../Contracts/ScheduledTasks';
import { useScheduledTasks } from '../Hooks/useScheduledTasks';
import CreateScheduledTaskDialog from './CreateScheduledTaskDialog';
import ScheduledTaskDetailsList from './ScheduledTaskDetailsList';
import ScheduledTaskDetailsPanel from './ScheduledTaskDetailsPanel';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import ScheduledTasksToolbar from './ScheduledTasksToolbar';

const ScheduledTasksOverview: FC = () => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { scheduledTasks, loading, error, refreshTasks, createTask, updateTask, deleteTask, pauseTask, resumeTask } = useScheduledTasks();

    const [selectedTask, setSelectedTask] = useState<ScheduledTask | null>(null);
    const [showCreateDialog, setShowCreateDialog] = useState(false);
    const [showDetailsPanel, setShowDetailsPanel] = useState(false);

    const handleTaskSelected = useCallback((task: ScheduledTask) => {
        setSelectedTask(task);
        setShowDetailsPanel(true);
    }, []);

    const handleCreateTask = useCallback(() => {
        setShowCreateDialog(true);
    }, []);

    const handleRefresh = useCallback(async () => {
        await refreshTasks();
    }, [refreshTasks]);

    const handleTaskCreated = useCallback(async () => {
        setShowCreateDialog(false);
        await refreshTasks();
    }, [refreshTasks]);

    const handleTaskUpdated = useCallback(async () => {
        setShowDetailsPanel(false);
        setSelectedTask(null);
        await refreshTasks();
    }, [refreshTasks]);

    const handleSelectionChanged = useCallback((task: ScheduledTask | null) => {
        setSelectedTask(task);
    }, []);

    const handleDeleteTask = useCallback(async () => {
        if (selectedTask) {
            const success = await deleteTask(selectedTask.id);
            if (success) {
                setSelectedTask(null);
                await refreshTasks();
            }
        }
    }, [selectedTask, deleteTask, refreshTasks]);

    const handlePauseResumeTask = useCallback(async () => {
        if (selectedTask) {
            const success = selectedTask.status === 'Active' ? await pauseTask(selectedTask.id) : await resumeTask(selectedTask.id);
            if (success) {
                await refreshTasks();
            }
        }
    }, [selectedTask, pauseTask, resumeTask, refreshTasks]);

    return (
        <div className={styles.root}>
            <div className={styles.content}>
                <div className={styles.padding}>
                    {/* Page Header */}
                    <div style={{ marginBottom: '16px' }}>
                        <Text
                            style={{
                                fontSize: '21px',
                                fontWeight: 600,
                                color: '#323130',
                                marginBottom: '8px',
                                display: 'block',
                            }}
                        >
                            {intl.formatMessage(ScheduledTasksResources.scheduledTasks)}
                        </Text>
                        <div className={styles.description}>{intl.formatMessage(ScheduledTasksResources.scheduledTasksDescription)}</div>
                    </div>

                    {/* Error Message */}
                    {error && (
                        <div style={{ marginBottom: '16px' }}>
                            <MessageBar
                                messageBarType={MessageBarType.error}
                                onDismiss={() => {}}
                                styles={{
                                    root: {
                                        borderRadius: '4px',
                                    },
                                }}
                            >
                                {error}
                            </MessageBar>
                        </div>
                    )}

                    {/* Toolbar and List Container */}
                    <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'flex-start' }}>
                        <ScheduledTasksToolbar
                            onRefreshClick={handleRefresh}
                            onNewTaskClick={handleCreateTask}
                            onDeleteTaskClick={handleDeleteTask}
                            onPauseResumeTaskClick={handlePauseResumeTask}
                            selectedTask={selectedTask || undefined}
                            loading={loading}
                        />
                    </div>

                    {/* Content Area */}
                    {loading ? (
                        <div className={styles.spinner}>
                            <Spinner size={3} />
                            <div className={styles.spinnerText}>{intl.formatMessage(SreAgentResources.loadingScheduledTasks)}</div>
                        </div>
                    ) : scheduledTasks.length === 0 ? (
                        <div className={styles.emptyState}>
                            <div className={styles.emptyStateIcon}>📅</div>
                            <div>
                                <div className={styles.emptyStateTitle}>{intl.formatMessage(ScheduledTasksResources.noScheduledTasks)}</div>
                                <div className={styles.emptyStateDescription}>
                                    {intl.formatMessage(SreAgentResources.createFirstScheduledTask)}
                                </div>
                            </div>
                        </div>
                    ) : (
                        <ScheduledTaskDetailsList
                            scheduledTasks={scheduledTasks}
                            scheduledTasksLoading={loading}
                            onTaskSelected={handleTaskSelected}
                            selectedTask={selectedTask || undefined}
                            onSelectionChanged={handleSelectionChanged}
                        />
                    )}

                    {/* Dialogs */}
                    {showCreateDialog && (
                        <CreateScheduledTaskDialog
                            isOpen={showCreateDialog}
                            onDismiss={() => setShowCreateDialog(false)}
                            onTaskCreated={handleTaskCreated}
                            createTask={createTask}
                        />
                    )}

                    {showDetailsPanel && selectedTask && (
                        <ScheduledTaskDetailsPanel
                            isOpen={showDetailsPanel}
                            task={selectedTask}
                            onDismiss={() => {
                                setShowDetailsPanel(false);
                                setSelectedTask(null);
                            }}
                            onTaskUpdated={handleTaskUpdated}
                            updateTask={updateTask}
                            deleteTask={deleteTask}
                            pauseTask={pauseTask}
                            resumeTask={resumeTask}
                        />
                    )}
                </div>
            </div>
        </div>
    );
};

export default ScheduledTasksOverview;

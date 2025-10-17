import { Body1, Subtitle1 } from '@fluentui/react-components';
import { FC, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasksV2 } from './Hooks/useScheduledTasksV2';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { ScheduledTasksDataGrid } from './ScheduledTasksDataGrid';
import { ScheduledTasksToolbar } from './ScheduledTasksToolbar';
import { getFilterKeyFromScheduledTaskStatus, TaskStatusFilterKey } from './ScheduledTasksUtilities';

export const ScheduledTasks: FC = () => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { scheduledTasks, loading: isScheduledTasksLoading, refreshTasks, deleteTask, pauseTask, resumeTask } = useScheduledTasksV2();
    const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
    const [searchQuery, setSearchQuery] = useState<string>('');
    const [statusFilter, setStatusFilter] = useState<TaskStatusFilterKey>(TaskStatusFilterKey.All);

    const filteredTasks = useMemo(() => {
        let tasks = [...scheduledTasks];
        if (searchQuery) {
            tasks = tasks.filter(task => task.name.toLowerCase().includes(searchQuery.toLowerCase()));
        }
        if (statusFilter !== TaskStatusFilterKey.All) {
            tasks = tasks.filter(task => getFilterKeyFromScheduledTaskStatus(task.status) === statusFilter);
        }
        return tasks;
    }, [scheduledTasks, searchQuery, statusFilter]);

    const selectedTasks = useMemo(() => {
        return scheduledTasks.filter(task => selectedTaskIds.includes(task.id));
    }, [scheduledTasks, selectedTaskIds]);

    return (
        <ScheduledTasksContext.Provider
            value={{
                refreshTasks,
                pauseTask,
                resumeTask,
                deleteTask,
            }}
        >
            <div className={styles.root}>
                <div className={styles.content}>
                    <div className={styles.padding}>
                        <div className={styles.title}>
                            <Subtitle1 as="h3" style={{ margin: 0 }}>
                                {intl.formatMessage(ScheduledTasksResources.tasks)}
                            </Subtitle1>
                            <Body1>{intl.formatMessage(ScheduledTasksResources.scheduledTasksDescription)}</Body1>
                        </div>
                        <ScheduledTasksToolbar
                            selectedTasks={selectedTasks}
                            isLoading={isScheduledTasksLoading}
                            searchQuery={searchQuery}
                            setSearchQuery={setSearchQuery}
                            statusFilter={statusFilter}
                            setStatusFilter={setStatusFilter}
                        />
                        <ScheduledTasksDataGrid
                            scheduledTasks={filteredTasks}
                            isScheduledTasksLoading={isScheduledTasksLoading}
                            selectedTaskIds={selectedTaskIds}
                            setSelectedTaskIds={setSelectedTaskIds}
                        />
                    </div>
                </div>
            </div>
        </ScheduledTasksContext.Provider>
    );
};

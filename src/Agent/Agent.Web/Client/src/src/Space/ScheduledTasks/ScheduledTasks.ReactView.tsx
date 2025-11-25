import { Text } from '@fluentui/react-components';
import { FC, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { TextWithLink } from '../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';
import { ScheduledTaskStatus } from '../Contracts/ScheduledTasks';
import { ScheduledTaskCard } from './Common/ScheduledTaskCard';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasksV2 } from './Hooks/useScheduledTasksV2';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { ScheduledTasksDataGrid } from './ScheduledTasksDataGrid';
import { ScheduledTasksToolbar } from './ScheduledTasksToolbar';
import { getFilterKeyFromScheduledTaskStatus, TaskStatusFilterKey } from './ScheduledTasksUtilities';

export const ScheduledTasks: FC = () => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const {
        scheduledTasks,
        loading: isScheduledTasksLoading,
        createTask,
        updateTask,
        refreshTasks,
        runTask,
        deleteTask,
        pauseTask,
        resumeTask,
    } = useScheduledTasksV2();
    const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
    const [searchQuery, setSearchQuery] = useState<string>('');
    const [statusFilter, setStatusFilter] = useState<TaskStatusFilterKey>(TaskStatusFilterKey.All);
    const [isOperationInProgress, setIsOperationInProgress] = useState<boolean>(false);

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

    const activeTasksCount = useMemo<number>(
        () => filteredTasks.filter(task => task.status === ScheduledTaskStatus.Active).length,
        [filteredTasks]
    );

    const totalTasksCount = useMemo<number>(() => filteredTasks.length, [filteredTasks]);

    const totalRunsCount = useMemo<number>(() => filteredTasks.reduce((acc, curr) => acc + (curr.executionCount ?? 0), 0), [filteredTasks]);

    return (
        <ScheduledTasksContext.Provider
            value={{
                createTask,
                updateTask,
                refreshTasks,
                runTask,
                pauseTask,
                resumeTask,
                deleteTask,
                isOperationInProgress,
                setIsOperationInProgress,
            }}
        >
            <div className={styles.root}>
                <div className={styles.tabRoot}>
                    <div className={styles.content}>
                        <div className={styles.padding}>
                            <div className={styles.title}>
                                <Text as="h3" size={600} weight="semibold" style={{ margin: 0 }}>
                                    {intl.formatMessage(ScheduledTasksResources.tasks)}
                                </Text>
                                <TextWithLink
                                    text={intl.formatMessage(ScheduledTasksResources.scheduledTasksDescription)}
                                    linkUrl={SreAgentFwLinks.learnMoreAboutScheduledTasks}
                                    linkText={intl.formatMessage(ScheduledTasksResources.learnMoreAboutScheduledTasks)}
                                />
                            </div>
                            <div className={styles.cards}>
                                <ScheduledTaskCard
                                    title={intl.formatMessage(ScheduledTasksResources.activeTasks)}
                                    count={activeTasksCount}
                                />
                                <ScheduledTaskCard title={intl.formatMessage(ScheduledTasksResources.totalTasks)} count={totalTasksCount} />
                                <ScheduledTaskCard title={intl.formatMessage(ScheduledTasksResources.totalRuns)} count={totalRunsCount} />
                            </div>
                            <div className={styles.taskOverviewBody}>
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
                </div>
            </div>
        </ScheduledTasksContext.Provider>
    );
};

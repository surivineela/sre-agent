import { mergeClasses, Text } from '@fluentui/react-components';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { TextWithLink } from '../../Common/Components/TextWithLink';
import { SreAgentFwLinks } from '../../Common/Constants/FwLinks';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, PaginatedResponse } from '../Contracts/ExtendedAgentGraph';
import { ScheduledTask, ScheduledTaskStatus } from '../Contracts/ScheduledTasks';
import { useCommonStyles } from '../Styles/Common.styles';
import { ScheduledTaskCard } from './Common/ScheduledTaskCard';
import { ScheduledTaskExecutionsView } from './Executions/ScheduledTaskExecutionsView';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasks } from './Hooks/useScheduledTasks';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { ScheduledTasksDataGrid } from './ScheduledTasksDataGrid';
import { ScheduledTasksToolbar } from './ScheduledTasksToolbar';
import { getFilterKeyFromScheduledTaskStatus, TaskStatusFilterKey } from './ScheduledTasksUtilities';

export const ScheduledTasks: FC = () => {
    const intl = useIntl();

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const styles = useScheduledTasksStyles();
    const commonStyles = useCommonStyles();

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
        getTaskExecutions,
    } = useScheduledTasks();
    const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
    const [searchQuery, setSearchQuery] = useState<string>('');
    const [statusFilter, setStatusFilter] = useState<TaskStatusFilterKey>(TaskStatusFilterKey.All);
    const [isOperationInProgress, setIsOperationInProgress] = useState<boolean>(false);
    const [selectedTask, setSelectedTask] = useState<ScheduledTask | null>(null);
    const [agents, setAgents] = useState<ExtendedAgent[]>([]);

    // Fetch agents for the subagent dropdown
    useEffect(() => {
        const fetchAgents = async () => {
            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/agents?page=1&limit=200`, {
                    headers: getAgentHeaders(),
                });
                if (response.ok) {
                    const data: PaginatedResponse<ExtendedAgent> = await response.json();
                    setAgents(data.data ?? []);
                }
            } catch (error) {
                console.error('Failed to fetch agents:', error);
            }
        };
        fetchAgents();
    }, [sreAgentEndpoint]);

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

    const handleTaskClick = useCallback((task: ScheduledTask) => {
        setSelectedTask(task);
    }, []);

    const handleBackToList = useCallback(() => {
        setSelectedTask(null);
    }, []);

    // Keep the selected task in sync with the scheduledTasks list (in case it was updated)
    const currentSelectedTask = useMemo(() => {
        if (!selectedTask) return null;
        return scheduledTasks.find(t => t.id === selectedTask.id) ?? selectedTask;
    }, [selectedTask, scheduledTasks]);

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
                getTaskExecutions,
                isOperationInProgress,
                setIsOperationInProgress,
            }}
        >
            <div className={mergeClasses(commonStyles.contentRootBorderAndBackground, styles.root)}>
                {currentSelectedTask ? (
                    <ScheduledTaskExecutionsView task={currentSelectedTask} onBack={handleBackToList} />
                ) : (
                    <div className={styles.content}>
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
                            <ScheduledTaskCard title={intl.formatMessage(ScheduledTasksResources.activeTasks)} count={activeTasksCount} />
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
                                agents={agents}
                            />
                            <ScheduledTasksDataGrid
                                scheduledTasks={filteredTasks}
                                isScheduledTasksLoading={isScheduledTasksLoading}
                                selectedTaskIds={selectedTaskIds}
                                setSelectedTaskIds={setSelectedTaskIds}
                                onTaskClick={handleTaskClick}
                            />
                        </div>
                    </div>
                )}
            </div>
        </ScheduledTasksContext.Provider>
    );
};

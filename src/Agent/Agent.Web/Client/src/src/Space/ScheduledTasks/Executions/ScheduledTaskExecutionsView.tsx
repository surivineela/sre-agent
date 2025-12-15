import { Button, Text } from '@fluentui/react-components';
import { ArrowLeftRegular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../../Common/Clients/ThreadClient';
import { getHumanReadableCronExpression } from '../../../Common/Helpers/CronExpression';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskExecution } from '../../Contracts/ScheduledTasks';
import { ScheduledTaskCreateOrEditDialog, ScheduledTaskDialogMode } from '../Common/ScheduledTaskCreateOrEditDialog';
import { ScheduledTasksContext } from '../Hooks/ScheduledTasksContext';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';
import { ScheduledTaskExecutionsDataGrid } from './ScheduledTaskExecutionsDataGrid';
import { ExecutionStatusFilterKey, ScheduledTaskExecutionsToolbar } from './ScheduledTaskExecutionsToolbar';

interface ScheduledTaskExecutionsViewProps {
    task: ScheduledTask;
    onBack: () => void;
}

export const ScheduledTaskExecutionsView: FC<ScheduledTaskExecutionsViewProps> = ({ task, onBack }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { getTaskExecutions } = useContext(ScheduledTasksContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [executions, setExecutions] = useState<ScheduledTaskExecution[]>([]);
    const [threadNames, setThreadNames] = useState<Map<string, string>>(new Map());
    const [isLoading, setIsLoading] = useState(true);
    const [statusFilter, setStatusFilter] = useState<ExecutionStatusFilterKey>(ExecutionStatusFilterKey.All);
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);

    const fetchThreadNames = useCallback(
        async (executionsList: ScheduledTaskExecution[]) => {
            const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
            const uniqueThreadIds = [...new Set(executionsList.map(e => e.threadId).filter((id): id is string => !!id))];

            const namesMap = new Map<string, string>();

            // Fetch thread names in parallel
            await Promise.all(
                uniqueThreadIds.map(async threadId => {
                    const response = await threadClient.getThread(threadId);
                    if (response.isSuccessful && response.content?.title) {
                        namesMap.set(threadId, response.content.title);
                    }
                })
            );

            setThreadNames(namesMap);
        },
        [sreAgentEndpoint]
    );

    const loadExecutions = useCallback(async () => {
        setIsLoading(true);
        try {
            const response = await getTaskExecutions(task.id);
            if (response.isSuccessful && response.content) {
                setExecutions(response.content);
                await fetchThreadNames(response.content);
            }
        } finally {
            setIsLoading(false);
        }
    }, [getTaskExecutions, task.id, fetchThreadNames]);

    useEffect(() => {
        loadExecutions();
    }, [loadExecutions]);

    const filteredExecutions = useMemo(() => {
        if (statusFilter === ExecutionStatusFilterKey.All) {
            return executions;
        }
        return executions.filter(execution => {
            if (statusFilter === ExecutionStatusFilterKey.Success) {
                return execution.success;
            }
            return !execution.success;
        });
    }, [executions, statusFilter]);

    const scheduleDescription = useMemo(() => {
        return getHumanReadableCronExpression(task.cronExpression, intl);
    }, [task.cronExpression, intl]);

    return (
        <div className={styles.content}>
            <div className={styles.padding}>
                <div className={styles.executionsHeader}>
                    <Button
                        appearance="subtle"
                        icon={<ArrowLeftRegular />}
                        onClick={onBack}
                        className={styles.backButton}
                        aria-label={intl.formatMessage(ScheduledTasksResources.backToScheduledTasks)}
                    />
                    <div className={styles.executionsHeaderTitle}>
                        <Text as="h3" size={500} weight="semibold" style={{ margin: 0 }}>
                            {task.name}
                        </Text>
                        <Text size={300}>{scheduleDescription}</Text>
                    </div>
                    <div style={{ marginLeft: 'auto' }}>
                        <ScheduledTaskCreateOrEditDialog
                            dialogTrigger={<Button>{intl.formatMessage(ScheduledTasksResources.editTask)}</Button>}
                            isDialogOpen={isEditDialogOpen}
                            setIsDialogOpen={setIsEditDialogOpen}
                            mode={ScheduledTaskDialogMode.Edit}
                            scheduledTask={task}
                        />
                    </div>
                </div>
                <div className={styles.taskOverviewBody}>
                    <ScheduledTaskExecutionsToolbar
                        task={task}
                        isLoading={isLoading}
                        statusFilter={statusFilter}
                        setStatusFilter={setStatusFilter}
                        refreshExecutions={loadExecutions}
                    />
                    <ScheduledTaskExecutionsDataGrid executions={filteredExecutions} isLoading={isLoading} threadNames={threadNames} />
                </div>
            </div>
        </div>
    );
};

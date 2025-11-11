import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ScheduledTasksClient } from '../../../../Common/Clients/ScheduledTasksClient';
import { CreateScheduledTaskRequest, ScheduledTask } from '../../../Contracts/ScheduledTasks';

export const useScheduledTasksV2 = () => {
    const [scheduledTasks, setScheduledTasks] = useState<ScheduledTask[]>([]);
    const [loading, setLoading] = useState(false);

    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const scheduledTasksClient = useMemo(
        () => ScheduledTasksClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );

    const createTask = useCallback(
        (task: CreateScheduledTaskRequest) => scheduledTasksClient.createScheduledTask(task),
        [scheduledTasksClient]
    );

    const updateTask = useCallback(
        (id: string, task: CreateScheduledTaskRequest) => scheduledTasksClient.updateScheduledTask(id, task),
        [scheduledTasksClient]
    );

    const refreshTasks = useCallback(async () => {
        setLoading(true);
        const response = await scheduledTasksClient.getScheduledTasks();
        if (response.isSuccessful) {
            setScheduledTasks(response.content ?? []);
        }
        setLoading(false);
    }, [scheduledTasksClient]);

    const runTask = useCallback((id: string) => scheduledTasksClient.runScheduledTask(id), [scheduledTasksClient]);

    const deleteTask = useCallback((id: string) => scheduledTasksClient.deleteScheduledTask(id), [scheduledTasksClient]);

    const pauseTask = useCallback((id: string) => scheduledTasksClient.pauseScheduledTask(id), [scheduledTasksClient]);

    const resumeTask = useCallback((id: string) => scheduledTasksClient.resumeScheduledTask(id), [scheduledTasksClient]);

    // Load tasks on mount
    useEffect(() => {
        refreshTasks();
    }, [refreshTasks]);

    return {
        scheduledTasks,
        loading,
        createTask,
        updateTask,
        refreshTasks,
        runTask,
        deleteTask,
        pauseTask,
        resumeTask,
    };
};

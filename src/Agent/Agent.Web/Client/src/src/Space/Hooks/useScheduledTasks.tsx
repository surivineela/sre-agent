import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { CreateScheduledTaskRequest, ScheduledTask, UpdateScheduledTaskRequest } from '../Contracts/ScheduledTasks';

export interface UseScheduledTasksResult {
    scheduledTasks: ScheduledTask[];
    loading: boolean;
    error: string | null;
    refreshTasks: () => Promise<void>;
    createTask: (task: CreateScheduledTaskRequest) => Promise<ScheduledTask | null>;
    updateTask: (id: string, updates: UpdateScheduledTaskRequest) => Promise<boolean>;
    deleteTask: (id: string) => Promise<boolean>;
    pauseTask: (id: string) => Promise<boolean>;
    resumeTask: (id: string) => Promise<boolean>;
    getTaskById: (id: string) => ScheduledTask | null;
    getTasksByThread: (threadId: string) => ScheduledTask[];
}

export const useScheduledTasks = (): UseScheduledTasksResult => {
    const [scheduledTasks, setScheduledTasks] = useState<ScheduledTask[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const handleApiCall = async <T,>(apiCall: () => Promise<Response>): Promise<T | null> => {
        try {
            const response = await apiCall();
            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            return await response.json();
        } catch (err) {
            const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred';
            setError(errorMessage);
            console.error('API call failed:', err);
            return null;
        }
    };

    const refreshTasks = useCallback(async () => {
        setLoading(true);
        setError(null);

        const data = await handleApiCall<ScheduledTask[]>(() =>
            fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks`, {
                method: 'GET',
                headers: getAgentHeaders(),
            })
        );

        if (data) {
            setScheduledTasks(data);
        }
        setLoading(false);
    }, []);

    const createTask = useCallback(
        async (task: CreateScheduledTaskRequest): Promise<ScheduledTask | null> => {
            setError(null);

            const response = await handleApiCall<{ taskId: string }>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                    body: JSON.stringify(task),
                })
            );

            if (response?.taskId) {
                // Refresh tasks and wait for the updated list
                await refreshTasks();

                // Make a direct API call to get the created task to ensure we have the latest data
                const createdTask = await handleApiCall<ScheduledTask>(() =>
                    fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/${response.taskId}`, {
                        method: 'GET',
                        headers: getAgentHeaders(),
                    })
                );

                return createdTask;
            }
            return null;
        },
        [refreshTasks]
    );

    const updateTask = useCallback(
        async (id: string, updates: UpdateScheduledTaskRequest): Promise<boolean> => {
            setError(null);

            const response = await handleApiCall<any>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/${id}`, {
                    method: 'PUT',
                    headers: getAgentHeaders(),
                    body: JSON.stringify(updates),
                })
            );

            if (response) {
                await refreshTasks();
                return true;
            }
            return false;
        },
        [refreshTasks]
    );

    const deleteTask = useCallback(
        async (id: string): Promise<boolean> => {
            setError(null);

            const response = await handleApiCall<any>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/${id}`, {
                    method: 'DELETE',
                    headers: getAgentHeaders(),
                })
            );

            if (response) {
                await refreshTasks();
                return true;
            }
            return false;
        },
        [refreshTasks]
    );

    const pauseTask = useCallback(
        async (id: string): Promise<boolean> => {
            setError(null);

            const response = await handleApiCall<any>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/${id}/pause`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                })
            );

            if (response) {
                await refreshTasks();
                return true;
            }
            return false;
        },
        [refreshTasks]
    );

    const resumeTask = useCallback(
        async (id: string): Promise<boolean> => {
            setError(null);

            const response = await handleApiCall<any>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/${id}/resume`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                })
            );

            if (response) {
                await refreshTasks();
                return true;
            }
            return false;
        },
        [refreshTasks]
    );

    const getTaskById = useCallback(
        (id: string): ScheduledTask | null => {
            return scheduledTasks.find(task => task.id === id) || null;
        },
        [scheduledTasks]
    );

    const getTasksByThread = useCallback(
        (threadId: string): ScheduledTask[] => {
            return scheduledTasks.filter(task => task.threadId === threadId);
        },
        [scheduledTasks]
    );

    // Load tasks on mount
    useEffect(() => {
        refreshTasks();
    }, [refreshTasks]);

    return {
        scheduledTasks,
        loading,
        error,
        refreshTasks,
        createTask,
        updateTask,
        deleteTask,
        pauseTask,
        resumeTask,
        getTaskById,
        getTasksByThread,
    };
};

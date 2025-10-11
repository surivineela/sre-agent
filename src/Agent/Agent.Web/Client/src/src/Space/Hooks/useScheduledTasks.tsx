import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import {
    CreateScheduledTaskRequest,
    CronExpressionGenerationRequest,
    CronExpressionGenerationResponse,
    ScheduledTask,
    ScheduledTaskPromptImprovementResponse,
    UpdateScheduledTaskRequest,
} from '../Contracts/ScheduledTasks';

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
    generateCronExpression: (request: CronExpressionGenerationRequest) => Promise<CronExpressionGenerationResponse | null>;
    improveScheduledTaskPrompt: (prompt: string) => Promise<ScheduledTaskPromptImprovementResponse | null>;
}

export const useScheduledTasks = (options?: { enabled?: boolean }): UseScheduledTasksResult => {
    const [scheduledTasks, setScheduledTasks] = useState<ScheduledTask[]>([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const enabled = options?.enabled ?? true;

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
        if (!enabled) {
            setScheduledTasks([]);
            setLoading(false);
            setError(null);
            return;
        }

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
    }, [enabled, sreAgentEndpoint]);

    const createTask = useCallback(
        async (task: CreateScheduledTaskRequest): Promise<ScheduledTask | null> => {
            if (!enabled) {
                return null;
            }

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
        [enabled, refreshTasks, sreAgentEndpoint]
    );

    const updateTask = useCallback(
        async (id: string, updates: UpdateScheduledTaskRequest): Promise<boolean> => {
            if (!enabled) {
                return false;
            }

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
        [enabled, refreshTasks, sreAgentEndpoint]
    );

    const deleteTask = useCallback(
        async (id: string): Promise<boolean> => {
            if (!enabled) {
                return false;
            }

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
        [enabled, refreshTasks, sreAgentEndpoint]
    );

    const pauseTask = useCallback(
        async (id: string): Promise<boolean> => {
            if (!enabled) {
                return false;
            }

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
        [enabled, refreshTasks, sreAgentEndpoint]
    );

    const resumeTask = useCallback(
        async (id: string): Promise<boolean> => {
            if (!enabled) {
                return false;
            }

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
        [enabled, refreshTasks, sreAgentEndpoint]
    );

    const getTaskById = useCallback(
        (id: string): ScheduledTask | null => {
            if (!enabled) {
                return null;
            }

            return scheduledTasks.find(task => task.id === id) || null;
        },
        [enabled, scheduledTasks]
    );

    const getTasksByThread = useCallback(
        (threadId: string): ScheduledTask[] => {
            if (!enabled) {
                return [];
            }

            return scheduledTasks.filter(task => task.threadId === threadId);
        },
        [enabled, scheduledTasks]
    );

    const generateCronExpression = useCallback(
        async (request: CronExpressionGenerationRequest): Promise<CronExpressionGenerationResponse | null> => {
            if (!enabled) {
                return null;
            }

            setError(null);

            return await handleApiCall<CronExpressionGenerationResponse>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/cron/generate`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                    body: JSON.stringify(request),
                })
            );
        },
        [enabled, sreAgentEndpoint]
    );

    const improveScheduledTaskPrompt = useCallback(
        async (prompt: string): Promise<ScheduledTaskPromptImprovementResponse | null> => {
            if (!enabled) {
                return null;
            }

            setError(null);

            return await handleApiCall<ScheduledTaskPromptImprovementResponse>(() =>
                fetch(`${sreAgentEndpoint}/api/v1/scheduledtasks/prompt/improve`, {
                    method: 'POST',
                    headers: getAgentHeaders(),
                    body: JSON.stringify({ prompt }),
                })
            );
        },
        [enabled, sreAgentEndpoint]
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
        generateCronExpression,
        improveScheduledTaskPrompt,
    };
};

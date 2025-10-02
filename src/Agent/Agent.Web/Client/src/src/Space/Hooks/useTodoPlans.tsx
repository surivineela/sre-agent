import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { TodoPlan } from '../../Common/Contracts/DataPlane/TodoPlan';

interface UseTodoPlansResult {
    todoPlans: TodoPlan[];
    isLoading: boolean;
    error: string | null;
    refetch: () => Promise<void>;
}

export const useTodoPlans = (threadId: string | null): UseTodoPlansResult => {
    const [todoPlans, setTodoPlans] = useState<TodoPlan[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const fetchTodoPlans = useCallback(
        async (isBackgroundPoll = false) => {
            if (!threadId || !sreAgentEndpoint) {
                setTodoPlans([]);
                return;
            }

            // Only show loading for initial fetch, not background polls
            if (!isBackgroundPoll) {
                setIsLoading(true);
            }
            setError(null);

            try {
                const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
                const response = await threadClient.getTodoPlans(threadId);

                if (response.isSuccessful && response.content) {
                    const sortedPlans = response.content.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());

                    // Only update if data actually changed
                    setTodoPlans(currentPlans => {
                        const hasChanged = JSON.stringify(currentPlans) !== JSON.stringify(sortedPlans);
                        if (hasChanged) {
                            return sortedPlans;
                        }
                        return currentPlans;
                    });
                } else {
                    setError(response.error?.message || 'Failed to fetch todo plans');
                    setTodoPlans([]);
                }
            } catch (err) {
                const errorMessage = err instanceof Error ? err.message : 'An unknown error occurred';
                setError(errorMessage);
                setTodoPlans([]);
            } finally {
                if (!isBackgroundPoll) {
                    setIsLoading(false);
                }
            }
        },
        [threadId, sreAgentEndpoint]
    );

    const refetch = useCallback(async () => {
        await fetchTodoPlans();
    }, [fetchTodoPlans]);

    useEffect(() => {
        fetchTodoPlans();
    }, [fetchTodoPlans]);

    useEffect(() => {
        if (!threadId || !sreAgentEndpoint) {
            return;
        }

        const interval = setInterval(() => {
            fetchTodoPlans(true);
        }, 5000); // Poll every 5 seconds

        return () => clearInterval(interval);
    }, [fetchTodoPlans, threadId, sreAgentEndpoint]);

    return {
        todoPlans,
        isLoading,
        error,
        refetch,
    };
};

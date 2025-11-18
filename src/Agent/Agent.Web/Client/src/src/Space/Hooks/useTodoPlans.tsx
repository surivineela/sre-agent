import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { TodoPlan } from '../../Common/Contracts/DataPlane/TodoPlan';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { StreamingContext } from '../Contracts/Context';

interface UseTodoPlansResult {
    todoPlans: TodoPlan[];
    isLoading: boolean;
    error: string | null;
}

export const useTodoPlans = (threadId: string | null, setExistingLatestToDoPlan: (plan: TodoPlan | null) => void): UseTodoPlansResult => {
    const [todoPlans, setTodoPlans] = useState<TodoPlan[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { subscribeTodoPlanUpdateEvent } = useContext(StreamingContext);

    const fetchTodoPlans = useCallback(async () => {
        if (!threadId || !sreAgentEndpoint) {
            setTodoPlans([]);
            return;
        }

        setIsLoading(true);
        setError(null);

        try {
            const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
            const response = await threadClient.getTodoPlans(threadId);

            if (response.isSuccessful && response.content) {
                const sortedPlans = response.content.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
                setTodoPlans(sortedPlans);
                setExistingLatestToDoPlan(sortedPlans.length > 0 ? sortedPlans[sortedPlans.length - 1] : null);
            } else {
                setError(response.error?.message || 'Failed to fetch todo plans');
                setTodoPlans([]);
                setExistingLatestToDoPlan(null);
            }
        } catch (err) {
            const errorMessage = err instanceof Error ? err.message : 'An unknown error occurred';
            setError(errorMessage);
            setTodoPlans([]);
            setExistingLatestToDoPlan(null);
        } finally {
            setIsLoading(false);
        }
    }, [threadId, sreAgentEndpoint, setExistingLatestToDoPlan]);

    useEffect(() => {
        fetchTodoPlans();
    }, [fetchTodoPlans]);

    useEffect(() => {
        let isSubscribed = true;

        const unsubscribe = subscribeTodoPlanUpdateEvent((message?: StreamingMessage) => {
            const messageThreadId = message?.additionalProperties?.threadId;
            const streamMessageType = message?.additionalProperties?.streamMessageType;
            const isTodoPlanUpdate = streamMessageType && equals(streamMessageType, 'todoplan', AntUxStringComparison.IgnoreCase);
            const content = message?.contents?.[0]?.text;

            if (isSubscribed && messageThreadId && messageThreadId === threadId && isTodoPlanUpdate && content) {
                try {
                    const updatedPlan = JSON.parse(content) as TodoPlan;

                    setTodoPlans(currentPlans => {
                        const existingIndex = currentPlans.findIndex(p => p.id === updatedPlan.id);

                        let newPlans: TodoPlan[];
                        if (existingIndex !== -1) {
                            // Update existing plan
                            newPlans = [...currentPlans];
                            newPlans[existingIndex] = updatedPlan;
                        } else {
                            // Add new plan
                            newPlans = [updatedPlan, ...currentPlans];
                        }

                        return newPlans.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
                    });
                } catch (err) {
                    console.error('Failed to parse TodoPlan update:', err);
                }
            }
        });

        return () => {
            unsubscribe();
            isSubscribed = false;
        };
    }, [subscribeTodoPlanUpdateEvent, threadId]);

    return {
        todoPlans,
        isLoading,
        error,
    };
};

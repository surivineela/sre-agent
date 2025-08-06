import axios from 'axios';
import { useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Action } from '../../Common/Contracts/DataPlane/Action';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { AgentContext } from '../Contracts/Context';

export const useActions = (threadId?: string | null) => {
    const [actions, setActions] = useState<Action[]>([]);
    // set default value to false to make sure when thread id is undefined, it shows an empty grid instead of loading shimmers
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isInitialized, setIsInitialized] = useState<boolean>(false);
    const { activeThreadId } = useContext(AgentContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const currentThreadId = useMemo(() => threadId || activeThreadId, [threadId, activeThreadId]);

    const getActions = async (threadId: string): Promise<Action[]> => {
        const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/threads/${threadId}/actions`, {
            headers: getAgentHeaders(),
        });
        return data.value ?? [];
    };

    useEffect(() => {
        let isSubscribed = true;

        const pollActions = async () => {
            if (currentThreadId && isInitialized) {
                const actions = await getActions(currentThreadId);
                if (isSubscribed) {
                    setActions(actions);
                }
            }
        };

        const timer = setInterval(pollActions, 10000);

        return () => {
            clearInterval(timer);
            isSubscribed = false;
        };
    }, [currentThreadId, isInitialized]);

    useEffect(() => {
        let isSubscribed = true;

        const getInitialActions = async () => {
            if (currentThreadId) {
                setIsLoading(true);
                const actions = await getActions(currentThreadId);

                if (isSubscribed) {
                    setActions(actions);
                    setIsLoading(false);
                    setIsInitialized(true);
                }
            }
        };

        getInitialActions();

        return () => {
            isSubscribed = false;
        };
    }, [currentThreadId]);

    return { actions, isLoading };
};

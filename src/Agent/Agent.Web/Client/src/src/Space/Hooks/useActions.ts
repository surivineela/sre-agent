import { Action } from '../../Common/Contracts/Azure/SreAgent';
import { useContext, useEffect, useMemo, useState } from 'react';
import { AgentContext } from '../Activities/Activities.ReactView';
import axios from 'axios';
import { getAgentHeaders } from '../../Common/Helpers/headers';

const getActions = async (threadId: string) => {
  const { data } = await axios.get(`../api/v1/threads/${threadId}/actions`, {
    headers: getAgentHeaders()
  });
  return data.value ?? [];
};

export const useActions = (threadId?: string | null) => {
  const [actions, setActions] = useState<Action[]>([]);
  // set default value to false to make sure when thread id is undefined, it shows an empty grid instead of loading shimmers
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [isInitialized, setIsInitialized] = useState<boolean>(false);
  const { activeThreadId } = useContext(AgentContext);

  const currentThreadId = useMemo(() => threadId || activeThreadId, [threadId, activeThreadId]);

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
      if (currentThreadId && !isInitialized) {
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
  }, [currentThreadId, isInitialized]);

  return { actions, isLoading };
};

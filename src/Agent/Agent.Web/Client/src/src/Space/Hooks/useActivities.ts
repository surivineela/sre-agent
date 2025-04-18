import { Thread } from '../../Common/Contracts/SreAgent';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import axios from 'axios';

const getThreads = async (skip: number, top = 20) => {
  try {
    const { data } = await axios.get(`../api/v1/threads?skip=${skip}&top=${top}&orderby=createdTimestamp+desc`);
    return data.value ?? [];
  } catch {
    return [];
  }
};

export const useActivities = (initialThreadId?: string | null) => {
  const [threads, setThreads] = useState<Thread[]>([]);
  const [threadsInitialized, setThreadsInitialized] = useState<boolean>(false);
  const [selectedThread, setSelectedThread] = useState<Thread | null>(null);
  const [threadContentKey, setThreadContentKey] = useState<string>(Guid.newGuid());
  const [activeThreadId, setActiveThreadId] = useState<string>('');

  const canPollThread = useRef<boolean>(true);

  const addThread = useCallback((thread: Thread) => {
    setThreads(prevThreads => [thread, ...prevThreads]);
    setActiveThreadId(thread.id);
    selectThread(thread);
  }, []);

  const selectThread = useCallback((thread: Thread | null) => {
    setSelectedThread(thread);
    setThreadContentKey(Guid.newGuid());
    setActiveThreadId(thread?.id || '');
  }, []);

  useEffect(() => setThreadContentKey(Guid.newGuid()), [selectedThread]);

  // Polling exisitng threads
  useEffect(() => {
    let isSubscribed = true;

    const getThreadsRequest = async () => {
      const shouldSetInitialThread = initialThreadId && threads.length === 0;

      const newThreads = await getThreads(threads.length, 20);

      if (newThreads.length > 0) {
        // delay 3 seconds before set threads to trigger new polling
        await Promise.resolve((resolve: any, _: any) => setTimeout(resolve, 2000));

        if (isSubscribed) {
          setThreads(prevThreads => [...prevThreads, ...newThreads]);
          setThreadsInitialized(true);

          if (shouldSetInitialThread) {
            const thread = threads.find((thread: Thread) => thread.id === initialThreadId);
            if (thread) {
              selectThread(thread);
            }
          }
        }
      }
    }

    getThreadsRequest();

    return () => {
      isSubscribed = false;
    };
  }, [initialThreadId, selectThread, threads]);


  // Poll latest threads every ten seconds
  useEffect(() => {

    const pollLatestTenThreads = async () => {
      if (!canPollThread.current || !threadsInitialized) return;

      canPollThread.current = false;

      const lastestTenThreads = await getThreads(0, 10);

      if (lastestTenThreads.length > 0) {
        setThreads(prevThreads => {
          const newThreads: Thread[] = [];

          for (let i = lastestTenThreads.length - 1; i >= 0; i--) {
            if (!prevThreads.some((thread: Thread) => thread.id === lastestTenThreads[i].id)) {
              newThreads.push(lastestTenThreads[i]);
            }
          }

          return [...newThreads, ...prevThreads];
        });
      }

      canPollThread.current = true;
    }

    const timer = setInterval(pollLatestTenThreads, 10000);

    return () => {
      clearInterval(timer);
      canPollThread.current = true;
    }
  }, [threadsInitialized]);

  return {
    threads: threads,
    threadsInitialized,
    selectedThread,
    addThread,
    selectThread,
    threadContentKey,
    activeThreadId,
  };
};

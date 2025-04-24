import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import axios from 'axios';
import { getLatestThread, noGapBetweenNewThreadsAndExistingThreads, processThreads } from '../Activities/Utility';
import { getAgentHeaders } from '../../Common/Helpers/headers';

const getThreads = async (skip: number, top = 20): Promise<Thread[]> => {
  try {
    const { data } = await axios.get(`../api/v1/threads?skip=${skip}&top=${top}&orderby=createdTimestamp+desc`, {
      headers: getAgentHeaders()
    });
    return data.value ?? [];
  } catch {
    return [];
  }
};

const pollLatestThreads = async (currentLatestThread?: Thread) => {
  const latestThreads: Thread[] = [];

  while (true) {
    const threads = await getThreads(latestThreads.length, 10);
    latestThreads.push(...threads);

    if (threads.length === 0) {
      break;
    }

    if (noGapBetweenNewThreadsAndExistingThreads(latestThreads, currentLatestThread)) {
      break;
    }
  }

  return latestThreads;

}

export const useActivities = (initialThreadId?: string | null) => {
  const [threads, setThreads] = useState<Thread[]>([]);
  const [threadsInitialized, setThreadsInitialized] = useState<boolean>(false);
  const [selectedThread, setSelectedThread] = useState<Thread | null>(null);
  const [threadContentKey, setThreadContentKey] = useState<string>(Guid.newGuid());
  const [activeThreadId, setActiveThreadId] = useState<string>('');

  const canPollThread = useRef<boolean>(true);
  const latestThread = useRef<Thread>();

  const addThread = useCallback((thread: Thread) => {
    setThreads(prevThreads => processThreads(prevThreads, [thread], true));
    setActiveThreadId(thread.id);
  }, []);

  const selectThread = useCallback((thread: Thread | null) => {
    setSelectedThread(thread);
    setThreadContentKey(Guid.newGuid());
    setActiveThreadId(thread?.id || '');
  }, []);

  useEffect(() => setThreadContentKey(Guid.newGuid()), [selectedThread]);

  useEffect(() => {
    latestThread.current = getLatestThread(threads);
  }, [threads])

  // Polling exisitng threads
  useEffect(() => {
    let isSubscribed = true;

    const getThreadsRequest = async () => {
      const shouldSetInitialThread = initialThreadId && threads.length === 0;

      const oldThreads = await getThreads(threads.length, 20);

      // delay 2 seconds before set threads to trigger new polling
      await Promise.resolve((resolve: any, _: any) => setTimeout(resolve, 2000));

      if (isSubscribed) {
        setThreads(prevThreads => processThreads(prevThreads, oldThreads, false));
        setThreadsInitialized(true);

        if (shouldSetInitialThread) {
          const thread = threads.find((thread: Thread) => thread.id === initialThreadId);
          if (thread) {
            selectThread(thread);
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

      const lastestTenThreads = await pollLatestThreads(latestThread.current);

      if (lastestTenThreads.length > 0) {
        setThreads(prevThreads => processThreads(prevThreads, lastestTenThreads, true));
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

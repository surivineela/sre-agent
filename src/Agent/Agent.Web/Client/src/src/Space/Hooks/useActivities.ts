import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import axios from 'axios';
import { getLatestThread, noGapBetweenNewThreadsAndExistingThreads, processThreads } from '../Activities/Utility';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import {
  Activities_ThreadHeader as ThreadHeaderResources
} from "../../Strings/SREResources.resjson";
import { format } from '@fluentui/react/lib/Utilities';

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

const deleteThreadRequest = async (threadId: string) => {
  return await axios.delete(`../api/v1/threads/${threadId}`, {
    headers: getAgentHeaders()
  });
}

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

  const proxy = useContext(AzPortalContext);

  const addThread = useCallback((thread: Thread) => {
    setThreads(prevThreads => processThreads(prevThreads, [thread], true));
    setActiveThreadId(thread.id);
  }, []);

  const selectThread = useCallback((thread: Thread | null) => {
    setSelectedThread(thread);
    setThreadContentKey(Guid.newGuid());
    setActiveThreadId(thread?.id || '');
  }, []);

  const deleteThread = useCallback(async (thread: Thread) => {
    const id = proxy.startNotification(ThreadHeaderResources.deleteThreadTitle, format(ThreadHeaderResources.deleteThreadInProgressDescription, thread.title));

    try {
      await deleteThreadRequest(thread.id);
      setThreads(prevThreads => prevThreads.filter(t => t.id !== thread.id));
      selectThread(null);

      proxy.stopNotification(id, true, format(ThreadHeaderResources.deleteThreadSuccessDescription, thread.title));
    } catch (e: any) {
      proxy.stopNotification(id, false, format(ThreadHeaderResources.deleteThreadFailureDescription, thread.title, e?.message ?? e?.response?.data));
    }

  }, [selectThread]);

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
    deleteThread,
    selectThread,
    threadContentKey,
    activeThreadId,
  };
};

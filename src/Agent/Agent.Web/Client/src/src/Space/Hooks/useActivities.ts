import { Thread } from '../../Common/Contracts/SreAgent';
import { useCallback, useEffect, useState } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import axios from 'axios';

const getThreads = async () => {
  const { data } = await axios.get(`../api/v1/threads`);
  return data.value ?? [];
};

export const useActivities = () => {
  const [threads, setThreads] = useState<Thread[]>([]);
  const [threadsInitialized, setThreadsInitialized] = useState<boolean>(false);
  const [selectedThread, setSelectedThread] = useState<Thread | null>(null);
  const [threadContentKey, setThreadContentKey] = useState<string>(Guid.newGuid());
  const [activeThreadId, setActiveThreadId] = useState<string>('');

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

  useEffect(() => {
    let isSubscribed = true;

    const getThreadsRequest = async () => {
        setThreadsInitialized(false);
        const threads = await getThreads();
        threads.sort((a: any, b: any) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());
        if (isSubscribed) {
            setThreads(threads);
            setThreadsInitialized(true);
        }
    };

    getThreadsRequest();

    return () => {
      isSubscribed = false;
    };
  }, []);

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

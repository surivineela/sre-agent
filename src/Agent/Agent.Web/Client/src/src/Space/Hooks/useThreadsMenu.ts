import { Ref, useCallback, useContext, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { StreamingMessage } from '../../Common/Contracts/Azure/Streaming';
import {
    getFilteredThreads,
    getUpdatedUnreadThreadIds,
    isUserStreamingMessage,
    processThreads,
    removeThreadIdsFromUnreadThreads,
} from '../Activities/Utility';
import { ThreadFilter, ThreadMenuHandle } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useThreadList } from './useThreadList';

export const useThreadsMenu = (ref: Ref<ThreadMenuHandle>) => {
    const { subscribeThreadEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [threadFilters, setThreadFilters] = useState<Set<ThreadFilter>>(new Set<ThreadFilter>());

    const threadUpdateQueue = useRef<Thread[]>([]);

    const { threads, setThreads, setUnreadThreadIds, unreadThreadIds, isLoadingInitialThreads, ...rest } = useThreadList(
        undefined,
        threadFilters,
        undefined
    );

    const oldestThreadModifiedTimestamp = useMemo(() => threads[threads.length - 1]?.modifiedTimestamp, [threads]);
    const newestThreadId = useMemo(() => threads[0]?.id, [threads]);

    const updateThreadFilters = useCallback((filter: ThreadFilter) => {
        setThreadFilters(prev => {
            const updatedFilters = new Set(prev);
            if (updatedFilters.has(filter)) {
                updatedFilters.delete(filter);
            } else {
                updatedFilters.add(filter);
            }
            return updatedFilters;
        });
    }, []);

    const updateThreadLastReadTime = useCallback(async (threadId: string) => {
        const response = await threadClient.updateThreadLastReadTime(threadId);

        if (response.isSuccessful) {
            setUnreadThreadIds(prev => removeThreadIdsFromUnreadThreads(prev, threadId));
        }
    }, []);

    const getThread = async (threadId: string): Promise<Thread | undefined> => {
        const response = await threadClient.getThread(threadId);
        if (response.isSuccessful && response.content) {
            return response.content;
        }
        return undefined;
    };

    const updateThreadInfo = useCallback(
        async (thread: Thread) => {
            if (!isLoadingInitialThreads) {
                setThreads(prevThreads => {
                    const { threads: totalThreads, addedThreads } = processThreads(
                        prevThreads,
                        getFilteredThreads([thread], threadFilters, undefined),
                        true
                    );
                    setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                    return totalThreads;
                });
            } else {
                threadUpdateQueue.current.push(thread);
            }
        },
        [isLoadingInitialThreads, threadFilters]
    );

    useImperativeHandle(ref, () => ({
        removeThreadFromList: (thread: Thread) => {
            setThreads(prevThreads => prevThreads.filter(t => t.id !== thread.id));
        },
        updateThreadLastReadTime: (threadId: string) => updateThreadLastReadTime(threadId),
    }));

    useEffect(() => {
        const threadCreateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            if (threadId) {
                try {
                    const thread = JSON.parse(text) as Thread;
                    if (thread && thread.id && thread.startMessage && thread.title && thread.lastMessage && thread.modifiedTimestamp) {
                        updateThreadInfo(thread);
                    } else {
                        throw new Error('Invalid thread data received from streaming message');
                    }
                } catch {
                    const updatedThread = await getThread(threadId);
                    if (updatedThread) {
                        updateThreadInfo(updatedThread);
                    }
                }
            }
        };

        const threadUpdateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            // If a new agent message is received, update the thread if it is not already the newest thread
            if (threadId && text && !isUserStreamingMessage(message) && newestThreadId !== threadId) {
                const updatedThread = await getThread(threadId);
                if (updatedThread) {
                    updateThreadInfo(updatedThread);
                }
            }
        };

        const unsubscribeThreadEvent = subscribeThreadEvent(threadCreateHandler, threadUpdateHandler);

        return () => {
            unsubscribeThreadEvent();
        };
    }, [subscribeThreadEvent, updateThreadInfo]);

    useEffect(() => {
        if (!isLoadingInitialThreads && threadUpdateQueue.current.length > 0) {
            const threadsToBeUpdated = [...threadUpdateQueue.current];
            threadUpdateQueue.current = [];
            setThreads(prevThreads => {
                const { threads: totalThreads, addedThreads } = processThreads(
                    prevThreads,
                    getFilteredThreads(threadsToBeUpdated, threadFilters, undefined),
                    true
                );
                setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                return totalThreads;
            });
        }
    }, [isLoadingInitialThreads, threadFilters]);

    return {
        threads,
        threadFilters,
        updateThreadFilters,
        oldestThreadModifiedTimestamp,
        unreadThreadIds,
        updateThreadLastReadTime,
        ...rest,
    };
};

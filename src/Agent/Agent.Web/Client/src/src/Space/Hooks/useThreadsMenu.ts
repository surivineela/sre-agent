import { Ref, useCallback, useContext, useEffect, useImperativeHandle, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import {
    getFilteredThreads,
    getUpdatedUnreadThreadIds,
    isFinalStreamingMessage,
    parseThreadFromStreamingText,
    processThreads,
    removeThreadIdsFromUnreadThreads,
} from '../Activities/Utility';
import { ThreadMenuHandle } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useThreadList } from './useThreadList';

export const useThreadsMenu = (ref: Ref<ThreadMenuHandle>, excludedSources?: ThreadSource[]) => {
    const { subscribeThreadUpdateEvent, subscribeMessageUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [showUnreadOnly, setShowUnreadOnly] = useState<boolean>(false);

    const threadUpdateQueue = useRef<Thread[]>([]);
    const threadItemDivsRef = useRef<Map<string, HTMLDivElement>>(new Map<string, HTMLDivElement>());
    const updatedThreadItemPositions = useRef<Map<string, DOMRect>>(new Map<string, DOMRect>());

    const { threads, setThreads, setUnreadThreadIds, unreadThreadIds, isLoadingInitialChatMessages, ...rest } = useThreadList(
        undefined,
        undefined,
        undefined,
        excludedSources,
        showUnreadOnly,
        undefined,
        'modifiedTimestamp'
    );

    const oldestThreadModifiedTimestamp = useMemo(() => threads[threads.length - 1]?.modifiedTimestamp, [threads]);

    const showUnreadOnlyRef = useRef<boolean>(showUnreadOnly);
    const isLoadingInitialChatMessagesRef = useRef(isLoadingInitialChatMessages);

    useEffect(() => {
        showUnreadOnlyRef.current = showUnreadOnly;
    }, [showUnreadOnly]);

    useEffect(() => {
        isLoadingInitialChatMessagesRef.current = isLoadingInitialChatMessages;
    }, [isLoadingInitialChatMessages]);

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

    const updateThreadList = (threadsToBeUpdated: Thread[]) => {
        // Record the current position of each thread that is about to be updated
        threadsToBeUpdated.forEach(thread => {
            const dom = threadItemDivsRef.current.get(thread.id);
            if (dom) {
                updatedThreadItemPositions.current.set(thread.id, dom.getBoundingClientRect());
            }
        });

        setThreads(prevThreads => {
            const { threads: totalThreads, addedThreads } = processThreads(
                prevThreads,
                getFilteredThreads(threadsToBeUpdated, undefined, excludedSources, showUnreadOnlyRef.current, undefined),
                true
            );
            setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
            return totalThreads;
        });
    };

    const updateThreadInfo = async (thread: Thread) => {
        if (!isLoadingInitialChatMessagesRef.current) {
            updateThreadList([thread]);
        } else {
            threadUpdateQueue.current.push(thread);
        }
    };

    useLayoutEffect(() => {
        threads.forEach(thread => {
            const first = updatedThreadItemPositions.current.get(thread.id);
            const dom = threadItemDivsRef.current.get(thread.id);
            const last = dom?.getBoundingClientRect();

            if (!first || !dom || !last) {
                return;
            }

            const deltaY = first.top - last.top;
            dom.style.transform = `translateY(${deltaY}px)`;
            dom.style.transition = 'none';

            requestAnimationFrame(() => {
                dom.style.transform = '';
                dom.style.transition = 'transform 350ms ease';
            });
        });

        updatedThreadItemPositions.current.clear();
    }, [threads]);

    useImperativeHandle(ref, () => ({
        removeThreadFromList: (thread: Thread) => {
            setThreads(prevThreads => prevThreads.filter(t => t.id !== thread.id));
        },
        updateThreadLastReadTime: (threadId: string) => updateThreadLastReadTime(threadId),
    }));

    useEffect(() => {
        const messageUpdateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            if (threadId && isFinalStreamingMessage(message)) {
                const updatedThread = await getThread(threadId);
                if (updatedThread) {
                    updateThreadInfo(updatedThread);
                }
            }
        };

        const threadCreateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            if (threadId) {
                try {
                    const thread = parseThreadFromStreamingText(text);
                    updateThreadInfo(thread);
                } catch {
                    const updatedThread = await getThread(threadId);
                    if (updatedThread) {
                        updateThreadInfo(updatedThread);
                    }
                }
            }
        };

        const unsubscribeMessageUpdateEvent = subscribeMessageUpdateEvent({
            handler: messageUpdateHandler,
        });

        const unsubscribeThreadUpdateEvent = subscribeThreadUpdateEvent(threadCreateHandler);

        return () => {
            unsubscribeMessageUpdateEvent();
            unsubscribeThreadUpdateEvent();
        };
    }, [subscribeThreadUpdateEvent, subscribeMessageUpdateEvent]);

    useEffect(() => {
        if (!isLoadingInitialChatMessages && threadUpdateQueue.current.length > 0) {
            const threadsToBeUpdated = [...threadUpdateQueue.current];
            threadUpdateQueue.current = [];
            updateThreadList(threadsToBeUpdated);
        }
    }, [isLoadingInitialChatMessages]);

    return {
        threads,
        showUnreadOnly,
        setShowUnreadOnly,
        oldestThreadModifiedTimestamp,
        unreadThreadIds,
        updateThreadLastReadTime,
        threadItemDivsRef,
        excludedSources,
        ...rest,
    };
};

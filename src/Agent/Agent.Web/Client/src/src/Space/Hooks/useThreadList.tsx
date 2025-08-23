import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { getFilteredThreads, getIntervalBetweenLoading, getUpdatedUnreadThreadIds, processThreads } from '../Activities/Utility';
import { ThreadLoadingCounts } from '../Contracts/Activities';

export const useThreadList = (
    initialThreads: Thread[] | undefined,
    excludedSources: ThreadSource[] | undefined,
    unreadOnly: boolean | undefined,
    searchText: string | undefined
) => {
    const [threads, setThreads] = useState<Thread[]>(initialThreads || []);
    const [moreThreadsToLoad, setMoreThreadsToLoad] = useState<boolean>(true);
    const [unreadThreadIds, setUnreadThreadIds] = useState<Set<string>>(new Set<string>());
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const [isLoadingInitialChatMessages, setIsLoadingInitialChatMessages] = useState<boolean>(true);

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const oldestThread = useRef<Thread>();
    const loadThreadsCallId = useRef<number>(0);
    const isLoadingThreads = useRef<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);

    const getThreads = async (
        excludedSources: ThreadSource[] | undefined,
        unreadOnly: boolean | undefined,
        searchText: string | undefined,
        oldestThread: Thread | undefined
    ) => {
        return await threadClient.getThreads({
            skip: 0,
            top: ThreadLoadingCounts.default,
            descending: true,
            filters: {
                searchText,
                timestamps: {
                    max: oldestThread
                        ? {
                              timestamp: oldestThread.modifiedTimestamp,
                              inclusive: false,
                          }
                        : undefined,
                },
                excludedSources: excludedSources,
                unread: unreadOnly,
            },
        });
    };

    const loadThreads = useCallback(async (): Promise<boolean | undefined> => {
        if (!isLoadingThreads.current && !isLoadingInitialChatMessages) {
            const callId = loadThreadsCallId.current;
            isLoadingThreads.current = true;

            const oldThreadsResponse = await getThreads(excludedSources, unreadOnly, searchText, oldestThread.current);

            if (callId === loadThreadsCallId.current) {
                const oldThreads = oldThreadsResponse.content ?? [];
                if (oldThreadsResponse.isSuccessful && oldThreads.length === 0) {
                    setMoreThreadsToLoad(false);
                }
                setThreads(prevThread => {
                    const { threads: totalThreads, addedThreads } = processThreads(prevThread, oldThreads, false);
                    setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                    return totalThreads;
                });

                isLoadingThreads.current = false;
                return oldThreadsResponse.isSuccessful;
            } else {
                isLoadingThreads.current = false;
                return undefined;
            }
        }
    }, [excludedSources, unreadOnly, searchText, isLoadingInitialChatMessages]);

    const handleScroll = debounce(() => {
        loadThreads();
    }, 300);

    const onScroll = () => {
        const previousScrollTop = currentScrollTop.current;
        currentScrollTop.current = threadListDivRef.current?.scrollTop || 0;

        if (currentScrollTop.current > previousScrollTop && moreThreadsToLoad) {
            handleScroll();
        }
    };

    // Use an intersection observer to load more threads to overflow the threads list div if the current number of threads
    // does not overflow the threads list div anymore due to events such as zoom out, which makes InifiniteScroll not able to work.
    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            setIsIntersecting(entry.isIntersecting);
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialChatMessages) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialChatMessages]);

    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        if (isIntersecting && moreThreadsToLoad) {
            let exponentialBackoffDepth = -1;

            const loadOldThreads = async () => {
                const isSuccessful = await loadThreads();

                exponentialBackoffDepth = isSuccessful === false ? exponentialBackoffDepth + 1 : -1;
                const interval = getIntervalBetweenLoading(exponentialBackoffDepth);

                if (isSubscribed) {
                    timeoutId = setTimeout(loadOldThreads, interval);
                }
            };
            loadOldThreads();
        }

        return () => {
            isSubscribed = false;
            clearTimeout(timeoutId);
        };
    }, [isIntersecting, loadThreads, moreThreadsToLoad]);

    useEffect(() => {
        // Increment loadThreadsCallId when threadsFilterOptions or isLoadingInitialThreads changes, to ensure that the result from calling loadMoreThreads with outdated filter options and isLoadingInitialThreads state value is disregarded
        return () => {
            loadThreadsCallId.current += 1;
        };
    }, [excludedSources, unreadOnly, searchText, isLoadingInitialChatMessages]);

    useEffect(() => {
        oldestThread.current = threads[threads.length - 1];
    }, [threads]);

    useEffect(() => {
        let isSubscribed = true;

        if (!hasChatPermissions) {
            setThreads([]);
            setIsLoadingInitialChatMessages(false);
            return;
        }

        setIsLoadingInitialChatMessages(true);
        setMoreThreadsToLoad(true);

        const setInitialThreads = async () => {
            // For a better user experience, we show the existing threads in the memory based on the filter options, before making a request to get the filtered threads from service side.
            setThreads(prev => getFilteredThreads(prev, excludedSources, unreadOnly, searchText));

            // Send a request to load initial threads based on the filter options to overflow the threads list div if possible
            isLoadingThreads.current = true;

            const initialThreadsResponse = await getThreads(excludedSources, unreadOnly, searchText, undefined);

            const initialThreads = initialThreadsResponse.content ?? [];

            if (isSubscribed) {
                // Do not set moreThreadsToLoad to false if the initial threads response is not successful.
                if (initialThreadsResponse.isSuccessful && initialThreads.length === 0) {
                    setMoreThreadsToLoad(false);
                }
                // Replace the current filtered threads with the initial threads
                const { threads: totalThreads, addedThreads } = processThreads([], initialThreads, false);
                setThreads(totalThreads);
                setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                setIsLoadingInitialChatMessages(false);
            }

            isLoadingThreads.current = false;
        };

        setInitialThreads();

        return () => {
            isSubscribed = false;
        };
    }, [excludedSources, unreadOnly, searchText, hasChatPermissions]);

    return {
        threads,
        setThreads,
        setUnreadThreadIds,
        unreadThreadIds,
        moreThreadsToLoad,
        isLoadingInitialChatMessages,

        threadListDivRef,
        intersectionObserverRef,

        onScroll,
    };
};

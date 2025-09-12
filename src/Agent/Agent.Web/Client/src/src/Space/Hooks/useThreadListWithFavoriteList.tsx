import debounce from 'lodash/debounce';
import { Dispatch, useCallback, useContext, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import {
    addOldThreads,
    addThreadToThreadsThatHaveModifiedTimestampUpdated,
    getFilteredThreads,
    getIntervalBetweenLoading,
    getUpdatedUnreadThreadIds,
    insertThreadThatHasFavoritePropertyChangedToThreadListsState,
    pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists,
    removeThreadFromThreadListsState,
    removeThreadIdsFromUnreadThreads,
} from '../Activities/Utility';
import { ThreadListsState, ThreadListState, ThreadLoadingCounts } from '../Contracts/Activities';

export interface InputForThreadListWithFavoriteList {
    includedSources?: ThreadSource[];
    excludedSources?: ThreadSource[];
    unreadOnly?: boolean;
    searchText?: string;
}

const getDefaultThreadListState = (): ThreadListState => {
    return {
        threads: [],
        threadsThatHaveFavoritePropertyChanged: [],
        moreThreadsToLoad: true,
    };
};

const getThreads = async (
    threadClient: ThreadClient,
    filterInput: InputForThreadListWithFavoriteList,
    orderBy: 'modifiedTimestamp' | 'createdTimestamp',
    oldestThread: Thread | undefined,
    favorite: boolean
) => {
    const { includedSources, excludedSources, unreadOnly, searchText } = filterInput;

    return await threadClient.getThreads({
        skip: 0,
        top: ThreadLoadingCounts.default,
        orderBy,
        descending: true,
        filters: {
            searchText,
            timestamps: {
                max: oldestThread
                    ? {
                          timestamp: orderBy === 'modifiedTimestamp' ? oldestThread.modifiedTimestamp : oldestThread.createdTimestamp,
                          inclusive: false,
                      }
                    : undefined,
            },
            sources: includedSources,
            excludedSources: excludedSources,
            unread: unreadOnly,
        },
        favorite: favorite,
    });
};

const useThreadItemsLoading = (
    isFavorite: boolean,
    filterInput: InputForThreadListWithFavoriteList,
    orderBy: 'modifiedTimestamp' | 'createdTimestamp',
    setUnreadThreadIds: Dispatch<React.SetStateAction<Set<string>>>,
    threadListsState: ThreadListsState,
    setThreadListsState: Dispatch<React.SetStateAction<ThreadListsState>>,
    isThreadListHidden: boolean
) => {
    const oldestThread = useRef<Thread>();
    const loadThreadsCallId = useRef<number>(0);
    const isLoadingThreads = useRef<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    oldestThread.current = isFavorite
        ? threadListsState.favoriteThreadListState.threads[threadListsState.favoriteThreadListState.threads.length - 1]
        : threadListsState.regularThreadListState.threads[threadListsState.regularThreadListState.threads.length - 1];

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [isThreadListIntersecting, setIsThreadListIntersecting] = useState<boolean>(false);

    const updateThreadListsStateBasedOnLoadedOldThreads = (oldThreads: Thread[], moreThreadsToLoad: boolean, favorite: boolean) => {
        setThreadListsState(prev => {
            const prevThreadListState = favorite ? prev.favoriteThreadListState : prev.regularThreadListState;
            const { threadListState: updatedThreadListState, addedThreads } = addOldThreads(
                prevThreadListState,
                oldThreads,
                moreThreadsToLoad
            );

            setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));

            if (
                (favorite && updatedThreadListState === prev.favoriteThreadListState) ||
                (!favorite && updatedThreadListState === prev.regularThreadListState)
            ) {
                return prev;
            }

            return {
                ...prev,
                favoriteThreadListState: favorite ? updatedThreadListState : prev.favoriteThreadListState,
                regularThreadListState: !favorite ? updatedThreadListState : prev.regularThreadListState,
            };
        });
    };

    const loadThreads = useCallback(async (): Promise<boolean | undefined> => {
        if (!isLoadingThreads.current && !threadListsState.isLoadingInitialThreads) {
            const callId = loadThreadsCallId.current;
            isLoadingThreads.current = true;

            const oldThreadsResponse = await getThreads(threadClient, filterInput, orderBy, oldestThread.current, isFavorite);

            if (callId === loadThreadsCallId.current) {
                const oldThreads = oldThreadsResponse.content ?? [];
                const moreThreadsToLoad = !oldThreadsResponse.isSuccessful || oldThreads.length > 0;

                updateThreadListsStateBasedOnLoadedOldThreads(oldThreads, moreThreadsToLoad, isFavorite);

                isLoadingThreads.current = false;
                return oldThreadsResponse.isSuccessful;
            } else {
                isLoadingThreads.current = false;
                return undefined;
            }
        }
    }, [filterInput, orderBy, threadListsState.isLoadingInitialThreads, isFavorite]);

    useEffect(() => {
        // Increment loadThreadsCallId when threadsFilterOptions or isLoadingInitialThreads changes, to ensure that the result from calling loadMoreThreads with outdated filter options and isLoadingInitialThreads state value is disregarded
        return () => {
            loadThreadsCallId.current += 1;
        };
    }, [filterInput, orderBy, threadListsState.isLoadingInitialThreads, isFavorite]);

    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            setIsThreadListIntersecting(entry.isIntersecting);
        });
        if (observer && intersectionObserverRef.current && !threadListsState.isLoadingInitialThreads && !isThreadListHidden) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsThreadListIntersecting(false);
        };
    }, [threadListsState.isLoadingInitialThreads, isThreadListHidden]);

    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        const moreThreadsToLoad =
            (isFavorite && threadListsState.favoriteThreadListState.moreThreadsToLoad) ||
            (!isFavorite && threadListsState.regularThreadListState.moreThreadsToLoad);

        if (isThreadListIntersecting && moreThreadsToLoad) {
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
    }, [
        isThreadListIntersecting,
        loadThreads,
        isFavorite && threadListsState.favoriteThreadListState.moreThreadsToLoad,
        !isFavorite && threadListsState.regularThreadListState.moreThreadsToLoad,
    ]);

    return {
        intersectionObserverRef,
        isLoadingThreads,
        loadThreads,
    };
};

export const useThreadListWithFavoriteList = (
    isFavoriteThreadListHidden: boolean,
    isRegularThreadListHidden: boolean,
    filterInput: InputForThreadListWithFavoriteList,
    orderBy: 'modifiedTimestamp' | 'createdTimestamp'
) => {
    const [threadListsState, setThreadListsState] = useState<ThreadListsState>({
        favoriteThreadListState: getDefaultThreadListState(),
        regularThreadListState: getDefaultThreadListState(),
        isLoadingInitialThreads: true,
        threadsThatHaveModifiedTimestampUpdated: [],
    });
    const [unreadThreadIds, setUnreadThreadIds] = useState<Set<string>>(new Set<string>());
    const [isUpdatingThreadFavoriteProperty, setIsUpdatingThreadFavoriteProperty] = useState<boolean>(false);

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const currentScrollTop = useRef<number>(0);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);
    const threadItemDivsRef = useRef<Map<string, HTMLDivElement>>(new Map<string, HTMLDivElement>());
    const previousPositionsOfUpdatedThreadItem = useRef<Map<string, DOMRect>>(new Map<string, DOMRect>());

    const {
        intersectionObserverRef: favoriteThreadsIntersectionObserverRef,
        isLoadingThreads: isLoadingFavoriteThreads,
        loadThreads: loadFavoriteThreads,
    } = useThreadItemsLoading(
        true,
        filterInput,
        orderBy,
        setUnreadThreadIds,
        threadListsState,
        setThreadListsState,
        isFavoriteThreadListHidden
    );
    const {
        intersectionObserverRef: regularThreadsIntersectionObserverRef,
        isLoadingThreads: isLoadingRegularThreads,
        loadThreads: loadRegularThreads,
    } = useThreadItemsLoading(
        false,
        filterInput,
        orderBy,
        setUnreadThreadIds,
        threadListsState,
        setThreadListsState,
        isRegularThreadListHidden
    );

    const recordCurrentPositionsOfThreadsToBeUpdated = (idsOfThreadsToBeUpdated: string[]) => {
        idsOfThreadsToBeUpdated.forEach(threadId => {
            const dom = threadItemDivsRef.current.get(threadId);
            if (dom) {
                previousPositionsOfUpdatedThreadItem.current.set(threadId, dom.getBoundingClientRect());
            }
        });
    };

    const filterThreads = (threads: Thread[], filterInput: InputForThreadListWithFavoriteList) => {
        const { includedSources, excludedSources, unreadOnly, searchText } = filterInput;
        return getFilteredThreads(threads, includedSources, excludedSources, unreadOnly, searchText);
    };

    const updateThreadFavoriteProperty = useCallback(async (threadId: string, favorite: boolean) => {
        setIsUpdatingThreadFavoriteProperty(true);

        const updatedThread = await threadClient.updateThreadFavorite(threadId, favorite);

        if (updatedThread.isSuccessful && updatedThread.content && !!updatedThread.content.favorite === favorite) {
            recordCurrentPositionsOfThreadsToBeUpdated([threadId]);

            setThreadListsState(prev => {
                // Remove it from current list
                const stateAfterRemovingThread = removeThreadFromThreadListsState(prev, threadId);
                // Insert it to the other list
                const stateAfterInsertingThread = insertThreadThatHasFavoritePropertyChangedToThreadListsState(
                    stateAfterRemovingThread,
                    updatedThread.content!
                );

                return stateAfterInsertingThread;
            });
        }

        setIsUpdatingThreadFavoriteProperty(false);
    }, []);

    // Deal with the thread that was newly created or recently updated
    const updateThreadListsStateBasedOnThreadThatHasModifiedTimestampUpdated = useCallback((thread: Thread) => {
        setThreadListsState(prev => {
            if (!prev.isLoadingInitialThreads) {
                recordCurrentPositionsOfThreadsToBeUpdated([thread.id]);
            }

            // If the thread list is in the initial loading mode due to initial render or filter change,
            // put the thread in the temporary list until the initial loading is done.
            // Otherwise add it to the top of the list
            const { threadListsState: updatedThreadListsState, addedThreads } = prev.isLoadingInitialThreads
                ? addThreadToThreadsThatHaveModifiedTimestampUpdated(prev, thread)
                : pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists(prev, [thread]);

            setUnreadThreadIds(prevUnreadThreadIds => getUpdatedUnreadThreadIds(prevUnreadThreadIds, addedThreads));

            return updatedThreadListsState;
        });
    }, []);

    const initializeThreadListsState = (
        initialFavoriteThreads: Thread[],
        initialRegularThreads: Thread[],
        moreFavoriteThreadsToLoad: boolean,
        moreRegularThreadsToLoad: boolean,
        filterInput: InputForThreadListWithFavoriteList
    ) => {
        setThreadListsState(prev => {
            // Add initial threads to the favorite threads and regular threads list
            const { threadListState: updatedFavoriteThreadListState, addedThreads: addedFavoriteThreads } = addOldThreads(
                getDefaultThreadListState(),
                initialFavoriteThreads,
                moreFavoriteThreadsToLoad
            );
            const { threadListState: updatedRegularThreadListState, addedThreads: addedRegularThreads } = addOldThreads(
                getDefaultThreadListState(),
                initialRegularThreads,
                moreRegularThreadsToLoad
            );

            // Filter those new/updated threads that were waiting to be added to the thread list during initial loading based on the current fitler options.
            const newThreadsThatHaveModifiedTimestampUpdated = filterThreads(prev.threadsThatHaveModifiedTimestampUpdated, filterInput);

            const newState: ThreadListsState = {
                favoriteThreadListState: updatedFavoriteThreadListState,
                regularThreadListState: updatedRegularThreadListState,
                threadsThatHaveModifiedTimestampUpdated: [],
                isLoadingInitialThreads: false,
            };

            // Push those new/updated threads to the favorite/regular thread lists after the initial loading is done
            const newStateAfterPushingThreadsThatHaveModifiedTimestampUpdated = pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists(
                newState,
                newThreadsThatHaveModifiedTimestampUpdated
            );

            setUnreadThreadIds(prev =>
                getUpdatedUnreadThreadIds(prev, [
                    ...addedFavoriteThreads,
                    ...addedRegularThreads,
                    ...newStateAfterPushingThreadsThatHaveModifiedTimestampUpdated.addedThreads,
                ])
            );

            return newStateAfterPushingThreadsThatHaveModifiedTimestampUpdated.threadListsState;
        });
    };

    const removeUnreadThreadId = useCallback((threadId: string) => {
        setUnreadThreadIds(prev => removeThreadIdsFromUnreadThreads(prev, threadId));
    }, []);

    const removeThread = useCallback((threadId: string) => {
        setThreadListsState(prev => removeThreadFromThreadListsState(prev, threadId));
    }, []);

    const handleScroll = debounce((favoriteThread: boolean) => {
        if (favoriteThread) {
            loadFavoriteThreads();
        } else {
            loadRegularThreads();
        }
    }, 300);

    const onScroll = () => {
        const previousScrollTop = currentScrollTop.current;
        currentScrollTop.current = threadListDivRef.current?.scrollTop || 0;

        if (currentScrollTop.current > previousScrollTop) {
            if (threadListsState.favoriteThreadListState.moreThreadsToLoad) {
                handleScroll(true);
            }

            if (threadListsState.regularThreadListState.moreThreadsToLoad) {
                handleScroll(false);
            }
        }
    };

    useEffect(() => {
        let isSubscribed = true;

        if (!hasChatPermissions) {
            setThreadListsState({
                favoriteThreadListState: {
                    threads: [],
                    threadsThatHaveFavoritePropertyChanged: [],
                    moreThreadsToLoad: false,
                },
                regularThreadListState: {
                    threads: [],
                    threadsThatHaveFavoritePropertyChanged: [],
                    moreThreadsToLoad: false,
                },
                threadsThatHaveModifiedTimestampUpdated: [],
                isLoadingInitialThreads: false,
            });
            return;
        }

        const setInitialThreads = async () => {
            // For a better user experience, we show the existing threads in the memory based on the filter options, before making a request to get the filtered threads from service side.
            setThreadListsState(prev => ({
                ...prev,
                favoriteThreadListState: {
                    threads: filterThreads(prev.favoriteThreadListState.threads, filterInput),
                    threadsThatHaveFavoritePropertyChanged: filterThreads(
                        prev.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                        filterInput
                    ),
                    moreThreadsToLoad: true,
                },
                regularThreadListState: {
                    threads: filterThreads(prev.regularThreadListState.threads, filterInput),
                    threadsThatHaveFavoritePropertyChanged: filterThreads(
                        prev.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                        filterInput
                    ),
                    moreThreadsToLoad: true,
                },
                isLoadingInitialThreads: true,
            }));

            // Send a request to load initial threads for both favorite and regular threads based on the filter options to overflow the threads list div if possible
            isLoadingFavoriteThreads.current = true;
            isLoadingRegularThreads.current = true;

            const [initialFavoriteThreadsResponse, initialRegularThreadsResponse] = await Promise.all([
                getThreads(threadClient, filterInput, orderBy, undefined, true),
                getThreads(threadClient, filterInput, orderBy, undefined, false),
            ]);

            const initialFavoriteThreads = initialFavoriteThreadsResponse.content ?? [];
            const initialRegularThreads = initialRegularThreadsResponse.content ?? [];

            if (isSubscribed) {
                initializeThreadListsState(
                    initialFavoriteThreads,
                    initialRegularThreads,
                    !initialFavoriteThreadsResponse.isSuccessful || initialFavoriteThreads.length > 0,
                    !initialRegularThreadsResponse.isSuccessful || initialRegularThreads.length > 0,
                    filterInput
                );
            }

            isLoadingFavoriteThreads.current = false;
            isLoadingRegularThreads.current = false;
        };

        setInitialThreads();

        return () => {
            isSubscribed = false;
        };
    }, [filterInput, orderBy, hasChatPermissions]);

    // Simulate an animation that a thread is moved from the previous position to the new position
    useLayoutEffect(() => {
        const threads = [
            ...threadListsState.favoriteThreadListState.threads,
            ...threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
            ...threadListsState.regularThreadListState.threads,
            ...threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
        ];

        threads.forEach(thread => {
            const previousPosition = previousPositionsOfUpdatedThreadItem.current.get(thread.id);
            const dom = threadItemDivsRef.current.get(thread.id);
            const currentPosition = dom?.getBoundingClientRect();

            if (!previousPosition || !dom || !currentPosition) {
                return;
            }

            const deltaY = previousPosition.top - currentPosition.top;
            dom.style.transform = `translateY(${deltaY}px)`;
            dom.style.transition = 'none';

            requestAnimationFrame(() => {
                dom.style.transform = '';
                dom.style.transition = 'transform 450ms ease';
            });
        });

        previousPositionsOfUpdatedThreadItem.current.clear();
    }, [threadListsState]);

    return {
        threadListsState,
        removeThread,
        unreadThreadIds,
        removeUnreadThreadId,
        isUpdatingThreadFavoriteProperty,
        threadListDivRef,
        favoriteThreadsIntersectionObserverRef,
        regularThreadsIntersectionObserverRef,
        onScroll,
        updateThreadFavoriteProperty,
        onThreadModifiedTimestampUpdated: updateThreadListsStateBasedOnThreadThatHasModifiedTimestampUpdated,
        threadItemDivsRef,
    };
};

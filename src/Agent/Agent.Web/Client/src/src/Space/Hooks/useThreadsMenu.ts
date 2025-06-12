import debounce from 'lodash/debounce';
import { Ref, useCallback, useContext, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient, ThreadSeverity } from '../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { SelectedTimes } from '../Activities/TimeDropdown';
import {
    getFilteredThreads,
    getNumberOfThreadsToOverflowThreadsListDiv,
    getUTCTimestampBasedOnSelectedThreadCutoffTime,
    processThreads,
} from '../Activities/Utility';
import { ThreadListHandle, ThreadLoadingCounts, ThreadPollingCounts, ThreadPollingInterval } from '../Contracts/Activities';

export const useThreadsMenu = (threadPollingTriggerId: number, ref: Ref<ThreadListHandle>) => {
    const [threads, setThreads] = useState<Thread[]>([]);
    const [isLoadingInitialThreads, setIsLoadingInitialThreads] = useState<boolean>(true);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [threadSearchText, setThreadSearchText] = useState<string>();
    const [threadSource, setThreadSource] = useState<ThreadSource>();
    const [oldestThreadModifiedTimestamp, setOldestThreadModifiedTimestamp] = useState<SelectedTimes>(SelectedTimes.OneDay);
    const [threadSeverity, setThreadSeverity] = useState<ThreadSeverity>();
    const [hasMoreOldThreads, setHasMoreOldThreads] = useState<boolean>(true);
    const [threadsFilterOptions, setThreadsFilterOptions] = useState<{
        threadSearchText?: string;
        threadSource?: ThreadSource;
        oldestThreadModifiedTimestamp: SelectedTimes;
        threadSeverity?: ThreadSeverity;
    }>({
        threadSearchText: undefined,
        threadSource: undefined,
        oldestThreadModifiedTimestamp: SelectedTimes.OneDay,
        threadSeverity: undefined,
    });

    const latestThread = useRef<Thread>();
    const oldestThread = useRef<Thread>();
    const threadsLength = useRef<number>(0);
    const threadsListDivRef = useRef<HTMLDivElement | null>(null);
    const isLoadingOldThreads = useRef<boolean>(false);
    const loadOldThreadCallId = useRef<number>(0);

    const getOldThreadsRequest = async (
        threadSearchText: string | undefined,
        threadSource: ThreadSource | undefined,
        oldestThreadModifiedTimestamp: SelectedTimes,
        threadSeverity: ThreadSeverity | undefined,
        numberOfThreadsToLoad: number,
        oldestThread: Thread | undefined
    ) => {
        const timestampCutoff = getUTCTimestampBasedOnSelectedThreadCutoffTime(oldestThreadModifiedTimestamp);

        return await threadClient.getThreads({
            skip: 0,
            top: numberOfThreadsToLoad,
            descending: true,
            filters: {
                searchText: threadSearchText,
                timestamps: {
                    min: {
                        timestamp: timestampCutoff,
                        inclusive: true,
                    },
                    max: oldestThread
                        ? {
                              timestamp: oldestThread.modifiedTimestamp,
                              inclusive: false,
                          }
                        : undefined,
                },
                source: threadSource,
            },
            severity: threadSeverity,
        });
    };

    /**
     * Poll latest threads by sorting the threads by modifiedTimestamp in ascending order, filter based on other options, and getting top 5 threads
     * that are greater than the latest thread's modifiedTimestamp. If latest thread does not exist, get the threads that are greater or equal than the selected time cutoff
     * @param threadSearchText
     * @param threadSource
     * @param oldestThreadModifiedTimestamp
     * @param threadSeverity
     * @param latestThread
     * @returns
     */
    const pollNewThreadsRequest = async (
        threadSearchText: string | undefined,
        threadSource: ThreadSource | undefined,
        oldestThreadModifiedTimestamp: SelectedTimes,
        threadSeverity: ThreadSeverity | undefined,
        latestThread: Thread | undefined
    ) => {
        const timestampCutoff = getUTCTimestampBasedOnSelectedThreadCutoffTime(oldestThreadModifiedTimestamp);

        const newThreadsInAscendingOrderResponse = await threadClient.getThreads({
            skip: 0,
            top: ThreadPollingCounts.default,
            descending: false,
            filters: {
                searchText: threadSearchText,
                timestamps: {
                    min: {
                        timestamp: latestThread ? latestThread.modifiedTimestamp : timestampCutoff,
                        inclusive: !latestThread,
                    },
                },
                source: threadSource,
            },
            severity: threadSeverity,
        });
        return newThreadsInAscendingOrderResponse;
    };

    useImperativeHandle(ref, () => ({
        removeThreadFromList: (thread: Thread) => {
            setThreads(prevThreads => prevThreads.filter(t => t.id !== thread.id));
        },
        promoteThread: (threadId: string, promote: () => void) => {
            // If the thread is in the list but it is not the latest one, then call promote to move it to the top of the list
            const thread = threads.find(t => t.id === threadId);
            if (thread && threads[0]?.id !== threadId) {
                promote();
            }
        },
    }));

    const onThreadSearchTextChange = useCallback(
        debounce((searchString: string) => {
            setThreadSearchText(searchString);
        }, 1000),
        []
    );

    const loadMoreOldThreads = useCallback(
        async (overflowDiv: boolean): Promise<boolean | undefined> => {
            const { threadSearchText, threadSource, oldestThreadModifiedTimestamp, threadSeverity } = threadsFilterOptions;

            if (!isLoadingInitialThreads && !isLoadingOldThreads.current) {
                const callId = loadOldThreadCallId.current;
                isLoadingOldThreads.current = true;

                const numberOfThreadsToLoad = overflowDiv
                    ? getNumberOfThreadsToOverflowThreadsListDiv(threadsListDivRef.current?.clientHeight, threadsLength.current)
                    : ThreadLoadingCounts.scroll;

                const oldThreadsResponse = await getOldThreadsRequest(
                    threadSearchText,
                    threadSource,
                    oldestThreadModifiedTimestamp,
                    threadSeverity,
                    numberOfThreadsToLoad,
                    oldestThread.current
                );

                if (callId === loadOldThreadCallId.current) {
                    const oldThreads = oldThreadsResponse.content ?? [];
                    if (oldThreadsResponse.isSuccessful && oldThreads.length < numberOfThreadsToLoad) {
                        setHasMoreOldThreads(false);
                    }
                    setThreads(prevThread => processThreads(prevThread, oldThreads, false));

                    isLoadingOldThreads.current = false;

                    return oldThreadsResponse.isSuccessful;
                } else {
                    isLoadingOldThreads.current = false;
                    return undefined;
                }
            }
        },
        [threadsFilterOptions, isLoadingInitialThreads]
    );

    // Reset states when the filter options change
    useEffect(() => {
        // Set isLoadingInitialThreads to true first before setting the filter options to make sure the initial threads loading starts before threads polling
        setIsLoadingInitialThreads(true);
        setHasMoreOldThreads(true);
        setThreadsFilterOptions({
            threadSearchText,
            threadSource,
            oldestThreadModifiedTimestamp,
            threadSeverity,
        });
    }, [threadSearchText, threadSource, oldestThreadModifiedTimestamp, threadSeverity]);

    useEffect(() => {
        // Increment loadOldThreadCallId when threadsFilterOptions or isLoadingInitialThreads changes, to ensure that the result from calling loadMoreOldThreads with outdated filter options and isLoadingInitialThreads state value is disregarded
        return () => {
            loadOldThreadCallId.current += 1;
        };
    }, [threadsFilterOptions, isLoadingInitialThreads]);

    useEffect(() => {
        latestThread.current = threads[0];
        oldestThread.current = threads[threads.length - 1];
        threadsLength.current = threads.length;
    }, [threads]);

    // Load initial threads when the component mounts or when threadsFilterOptions changes
    useEffect(() => {
        let isSubscribed = true;

        const { threadSearchText, threadSource, oldestThreadModifiedTimestamp, threadSeverity } = threadsFilterOptions;

        const setInitialThreads = async () => {
            // For a better user experience, we show the existing threads in the memory based on the filter options, before making a request to get the filtered threads from service side.
            setThreads(prev =>
                getFilteredThreads(prev, {
                    searchText: threadSearchText,
                    selectedCutoffTime: getUTCTimestampBasedOnSelectedThreadCutoffTime(oldestThreadModifiedTimestamp),
                    threadSeverity,
                    source: threadSource,
                })
            );

            // Send a request to load initial threads based on the filter options to overflow the threads list div if possible
            isLoadingOldThreads.current = true;

            const numberOfThreadsToLoad = getNumberOfThreadsToOverflowThreadsListDiv(threadsListDivRef.current?.clientHeight, 0);

            const initialThreadsResponse = await getOldThreadsRequest(
                threadSearchText,
                threadSource,
                oldestThreadModifiedTimestamp,
                threadSeverity,
                numberOfThreadsToLoad,
                undefined
            );

            const initialThreads = initialThreadsResponse.content ?? [];

            if (isSubscribed) {
                // Do not set hasMoreOldThreads to false if the initial threads response is not successful.
                if (initialThreadsResponse.isSuccessful && initialThreads.length < numberOfThreadsToLoad) {
                    setHasMoreOldThreads(false);
                }
                // Replace the current filtered threads with the initial threads
                setThreads(processThreads([], initialThreads, false));
                setIsLoadingInitialThreads(false);
            }

            isLoadingOldThreads.current = false;
        };

        setInitialThreads();

        return () => {
            isSubscribed = false;
        };
    }, [threadsFilterOptions]);

    // Poll new threads every 10 seconds
    useEffect(() => {
        let isSubscribed = true;
        let pollNewThreadsTimeout: NodeJS.Timeout | undefined = undefined;

        if (!isLoadingInitialThreads) {
            const pollNewThreads = async () => {
                const { threadSearchText, threadSource, oldestThreadModifiedTimestamp, threadSeverity } = threadsFilterOptions;

                const newThreadsInAscendingOrderResponse = await pollNewThreadsRequest(
                    threadSearchText,
                    threadSource,
                    oldestThreadModifiedTimestamp,
                    threadSeverity,
                    latestThread.current
                );

                const newThreadsInAsecindgOrder = newThreadsInAscendingOrderResponse.content ?? [];

                if (isSubscribed) {
                    setThreads(prevThreads => processThreads(prevThreads, newThreadsInAsecindgOrder, true));
                    pollNewThreadsTimeout = setTimeout(pollNewThreads, ThreadPollingInterval.default);
                }
            };

            pollNewThreads();
        }

        return () => {
            isSubscribed = false;
            clearTimeout(pollNewThreadsTimeout);
        };
    }, [threadsFilterOptions, isLoadingInitialThreads, threadPollingTriggerId]);

    return {
        threads,
        isLoadingInitialThreads,
        loadMoreOldThreads,
        hasMoreOldThreads,
        threadsListDivRef,
        onThreadSearchTextChange,
        threadSource,
        setThreadSource,
        oldestThreadModifiedTimestamp,
        setOldestThreadModifiedTimestamp,
        setThreadSeverity,
    };
};

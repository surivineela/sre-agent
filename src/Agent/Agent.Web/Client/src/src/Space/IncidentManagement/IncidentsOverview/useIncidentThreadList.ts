import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../../Common/Clients/ThreadClient';
import { TimeRangeValue, TimespanKeys } from '../../../Common/Components/PillFilter/Contracts';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime, getTimespanInMilliseconds } from '../../../Common/Helpers/Date';
import { KnowledgeGraphBuildStatusContext } from '../../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { getIntervalBetweenLoading, getUpdatedUnreadThreadIds } from '../../Activities/Utility';
import { ThreadLoadingCounts } from '../../Contracts/Activities';

export type SortColumn = 'incidentId' | 'title' | 'incidentStatus' | 'createdTimestamp';

const getColumnDetails = (column: SortColumn | 'modifiedTimestamp') => {
    switch (column) {
        case 'incidentId':
            return { isDistinct: true, type: 'string' };
        case 'title':
            return { isDistinct: false, type: 'string' };
        case 'incidentStatus':
            return { isDistinct: false, type: 'string' };
        case 'modifiedTimestamp':
            return { isDistinct: true, type: 'date' };
        case 'createdTimestamp':
            return { isDistinct: true, type: 'date' };
        default:
            return { isDistinct: undefined, type: undefined };
    }
};

const getColumnValue = (thread: Thread, column: SortColumn | 'modifiedTimestamp'): string | undefined => {
    switch (column) {
        case 'incidentId':
            return thread.status?.incidentStatus?.incidentId;
        case 'incidentStatus':
            return thread.status?.incidentStatus?.status;
        default:
            return thread[column];
    }
};

const processThreads = (
    prevThreads: Thread[],
    threads: Thread[],
    areThreadsNew: boolean,
    sortColumn?: SortColumn | 'modifiedTimestamp',
    sortDescending?: boolean
) => {
    if (threads.length === 0) {
        return {
            threads: prevThreads,
            addedThreads: [],
        };
    }

    const threadIdsToRemoveFromPrevThreads: Set<string> = new Set<string>();

    const threadsMap: Map<string, Thread> = new Map();
    threads.forEach(thread => threadsMap.set(thread.id, thread));

    for (let i = 0; i < prevThreads.length; i++) {
        const prevThreadId = prevThreads[i].id;
        const duplicatedThread = threadsMap.get(prevThreadId);
        if (duplicatedThread) {
            if (areThreadsNew && duplicatedThread.modifiedTimestamp > prevThreads[i].modifiedTimestamp) {
                // if the threads are new and the modified time is greter than the existing duplicated one from prev threads, then remove it from the prev threads
                threadIdsToRemoveFromPrevThreads.add(prevThreadId);
            } else {
                // Remove thread out of the threadsMap because the thread is already in the existing threads and has not been modified
                threadsMap.delete(prevThreadId);
            }
        }
    }

    const threadsToAdd: Thread[] = Array.from(threadsMap.values());
    const sortColumnName = sortColumn || 'modifiedTimestamp';
    threadsToAdd.sort((a, b) => {
        const columnDetails = getColumnDetails(sortColumnName);
        const aValue = getColumnValue(a, sortColumnName);
        const bValue = getColumnValue(b, sortColumnName);

        if (aValue === undefined || bValue === undefined) {
            return 0;
        }

        if (columnDetails.type === 'string') {
            const comparison = String(aValue).localeCompare(String(bValue));
            return sortDescending ? -comparison : comparison;
        } else if (columnDetails.type === 'number') {
            const aValueNum = Number(aValue);
            const bValueNum = Number(bValue);
            if (isNaN(aValueNum) || isNaN(bValueNum)) {
                return 0;
            }
            const comparison = aValueNum - bValueNum;
            return sortDescending ? -comparison : comparison;
        } else if (columnDetails.type === 'date') {
            const comparison = getSafeDateTime(aValue as string).getTime() - getSafeDateTime(bValue as string).getTime();
            return sortDescending ? -comparison : comparison;
        }
        return 0;
    });

    const updatedExistingThreads = [...prevThreads].filter(thread => {
        return !threadIdsToRemoveFromPrevThreads.has(thread.id);
    });

    const existingThreads = threadIdsToRemoveFromPrevThreads.size > 0 ? updatedExistingThreads : prevThreads;

    if (threadsToAdd.length === 0) {
        return {
            threads: existingThreads,
            addedThreads: [],
        };
    }

    const resultThreads = areThreadsNew ? [...threadsToAdd, ...existingThreads] : [...existingThreads, ...threadsToAdd];
    return {
        threads: resultThreads,
        addedThreads: threadsToAdd,
    };
};

export const useIncidentThreadList = (
    initialThreads?: Thread[],
    searchText?: string,
    statusFilters?: string[],
    createdTimeFilter?: TimeRangeValue,
    sortColumn?: SortColumn,
    sortDescending?: boolean,
    visible?: boolean,
    refresh?: number
) => {
    const [threads, setThreads] = useState<Thread[]>(initialThreads || []);
    const [moreThreadsToLoad, setMoreThreadsToLoad] = useState<boolean>(true);
    const [unreadThreadIds, setUnreadThreadIds] = useState<Set<string>>(new Set<string>());
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const [isLoadingInitialThreads, setIsLoadingInitialThreads] = useState<boolean>(true);

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = useMemo(() => ThreadClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const oldestThread = useRef<Thread>();
    const threadCount = useRef<number>(0);
    const loadThreadsCallTimestamp = useRef<string>(new Date().toISOString());
    const isLoadingThreads = useRef<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);

    const getThreads = useCallback(
        async (
            searchText: string | undefined,
            status: string[] | undefined,
            createdTimeRange: TimeRangeValue | undefined,
            threadCount: number | undefined,
            trailingThread: Thread | undefined,
            sortColumn: SortColumn | undefined,
            sortDescending: boolean | undefined = true
        ) => {
            let skip: number | undefined;
            let paginationFilter: string | undefined;

            const sortColumnName = sortColumn || 'modifiedTimestamp';
            const sortColumnDetails = getColumnDetails(sortColumnName);

            if (sortColumnDetails.isDistinct) {
                // When sorting by a distinct column, we use a pagination filter to fetch threads that come after the last thread's sort column value.
                skip = 0;
                if (trailingThread) {
                    const sortColumnValue = getColumnValue(trailingThread, sortColumnName);
                    const sortColumnValueWrapped = sortColumnDetails.type === 'string' ? `'${sortColumnValue}'` : sortColumnValue;
                    paginationFilter = `${sortColumnName} ${sortDescending ? 'lt' : 'gt'} ${sortColumnValueWrapped}`;
                }
            } else {
                // When sorting by a non-distinct column, we use skip to paginate.
                skip = threadCount;
            }

            const statusFilter = status?.some(s => s === 'all') ? [] : status;

            const filterStrings: string[] = [`source eq '${ThreadSource.incident}'`];

            if (paginationFilter) {
                filterStrings.push(paginationFilter);
            }

            if (loadThreadsCallTimestamp.current && sortDescending) {
                filterStrings.push(`createdTimestamp le ${loadThreadsCallTimestamp.current}`);
            }

            if (searchText) {
                const searchTextToLower = searchText.toLowerCase();
                filterStrings.push(
                    `(contains(tolower(title),'${searchTextToLower}') or contains(tolower(incidentId),'${searchTextToLower}'))`
                );
            }

            if (statusFilter?.length) {
                const statusFilterStrings = statusFilter.map(s => {
                    const statusToLower = s.toLowerCase();
                    const mayBeEmptyString = ([IncidentStatus.active, IncidentStatus.triggered, IncidentStatus.new] as string[]).includes(
                        statusToLower
                    );
                    return mayBeEmptyString
                        ? `(tolower(incidentStatus) eq '${statusToLower}' or incidentStatus eq '')`
                        : `tolower(incidentStatus) eq '${statusToLower}'`;
                });
                let statusFilterString = statusFilterStrings.join(' or ');
                if (statusFilterStrings.length > 1) {
                    statusFilterString = `(${statusFilterString})`;
                }
                filterStrings.push(statusFilterString);
            }

            if (createdTimeRange) {
                if (createdTimeRange.key === TimespanKeys.Custom) {
                    const { start, end } = createdTimeRange;
                    filterStrings.push(`createdTimestamp ge ${start?.toISOString()} and createdTimestamp le ${end?.toISOString()}`);
                } else {
                    const spanInMilliseconds = getTimespanInMilliseconds(createdTimeRange.key);
                    const start = new Date(Date.now() - spanInMilliseconds);
                    filterStrings.push(`createdTimestamp ge ${start.toISOString()}`);
                }
            }

            return await threadClient.getIncidentThreads({
                skip: skip ?? 0,
                top: ThreadLoadingCounts.default,
                orderBy: `${sortColumnName}${sortDescending ? '+desc' : ''}`,
                filter: filterStrings.join(' and '),
            });
        },
        [threadClient]
    );

    const getInitialThreads = useCallback(
        async (
            searchText: string | undefined,
            status: string[] | undefined,
            createdTimeRange: TimeRangeValue | undefined,
            sortColumn: SortColumn | undefined,
            sortDescending: boolean | undefined
        ) => {
            return await getThreads(searchText, status, createdTimeRange, 0, undefined, sortColumn, sortDescending);
        },
        [getThreads]
    );

    const loadThreads = useCallback(async (): Promise<boolean | undefined> => {
        if (!isLoadingThreads.current && !isLoadingInitialThreads) {
            const callId = loadThreadsCallTimestamp.current;
            isLoadingThreads.current = true;

            const oldThreadsResponse = await getThreads(
                searchText,
                statusFilters,
                createdTimeFilter,
                threadCount.current,
                oldestThread.current,
                sortColumn,
                sortDescending
            );

            if (callId === loadThreadsCallTimestamp.current) {
                const oldThreads = oldThreadsResponse.content ?? [];
                if (oldThreadsResponse.isSuccessful && oldThreads.length === 0) {
                    setMoreThreadsToLoad(false);
                }
                setThreads(prevThread => {
                    const { threads: totalThreads, addedThreads } = processThreads(
                        prevThread,
                        oldThreads,
                        false,
                        sortColumn,
                        sortDescending
                    );
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
    }, [getThreads, searchText, statusFilters, createdTimeFilter, sortColumn, sortDescending, isLoadingInitialThreads]);

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
        if (visible && observer && intersectionObserverRef.current && !isLoadingInitialThreads) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialThreads, visible]);

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
        oldestThread.current = threads[threads.length - 1];
        threadCount.current = threads.length;
    }, [threads]);

    useEffect(() => {
        let isSubscribed = true;
        loadThreadsCallTimestamp.current = new Date().toISOString();

        if (!hasChatPermissions) {
            setThreads([]);
            setIsLoadingInitialThreads(false);
        } else {
            setIsLoadingInitialThreads(true);
            setMoreThreadsToLoad(true);

            const setInitialThreads = async () => {
                // For a better user experience, we show the existing threads in the memory based on the filter options, before making a request to get the filtered threads from service side.
                setThreads(prev => getFilteredThreads(prev, searchText));

                // Send a request to load initial threads based on the filter options to overflow the threads list div if possible
                isLoadingThreads.current = true;

                const initialThreadsResponse = await getInitialThreads(
                    searchText,
                    statusFilters,
                    createdTimeFilter,
                    sortColumn,
                    sortDescending
                );

                const initialThreads = initialThreadsResponse.content ?? [];

                if (isSubscribed) {
                    // Do not set moreThreadsToLoad to false if the initial threads response is not successful.
                    if (initialThreadsResponse.isSuccessful && initialThreads.length === 0) {
                        setMoreThreadsToLoad(false);
                    }
                    // Replace the current filtered threads with the initial threads
                    const { threads: totalThreads, addedThreads } = processThreads([], initialThreads, false, sortColumn, sortDescending);
                    setThreads(totalThreads);
                    setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                    setIsLoadingInitialThreads(false);
                }

                isLoadingThreads.current = false;
            };

            setInitialThreads();
        }

        return () => {
            isSubscribed = false;
        };
    }, [searchText, statusFilters, createdTimeFilter, sortColumn, sortDescending, hasChatPermissions, getInitialThreads, refresh]);

    return {
        threads,
        setThreads,
        setUnreadThreadIds,
        unreadThreadIds,
        moreThreadsToLoad,
        isLoadingInitialThreads,

        threadListDivRef,
        intersectionObserverRef,

        onScroll,
    };
};

const getFilteredThreads = (threads: Thread[], searchText?: string): Thread[] => {
    return threads.filter(thread => {
        let match = true;

        if (searchText) {
            match = thread.title.toLocaleLowerCase().includes(searchText.toLocaleLowerCase());
        }

        return match;
    });
};

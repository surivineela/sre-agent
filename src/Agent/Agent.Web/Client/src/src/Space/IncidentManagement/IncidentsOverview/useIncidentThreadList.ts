import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../../Common/Clients/ThreadClient';
import { TimeRangeValue, TimespanKeys } from '../../../Common/Components/PillFilter/Contracts';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { IncidentThreadCounts, Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { getTimespanInMilliseconds } from '../../../Common/Helpers/Date';
import { KnowledgeGraphBuildStatusContext } from '../../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { getIntervalBetweenLoading, getUpdatedUnreadThreadIds } from '../../Activities/Utility';
import { ThreadLoadingCounts } from '../../Contracts/Activities';
import { IncidentsListColumnKey } from '../CreateIncidentHandler/Contracts';
import { getColumnInfo } from '../Utilities';

const processThreads = (
    prevThreads: Thread[],
    threads: Thread[],
    areThreadsNew: boolean,
    sortColumn?: IncidentsListColumnKey | 'modifiedTimestamp',
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
        const { columnType, getColumnValue } = getColumnInfo(sortColumnName);
        const aValue = getColumnValue(a);
        const bValue = getColumnValue(b);

        if (aValue === undefined || bValue === undefined) {
            return 0;
        }

        if (columnType === 'string' || columnType === 'date') {
            const comparison = String(aValue).localeCompare(String(bValue));
            return sortDescending ? -comparison : comparison;
        } else if (columnType === 'number') {
            let comparison: number;

            const aValueNum = Number(aValue);
            const bValueNum = Number(bValue);
            if (isNaN(aValueNum) && isNaN(bValueNum)) {
                comparison = 0;
            } else if (isNaN(aValueNum)) {
                comparison = -1;
            } else if (isNaN(bValueNum)) {
                comparison = 1;
            } else {
                comparison = aValueNum - bValueNum;
            }
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
    investigationStatusFilters?: string[],
    createdTimeFilter?: TimeRangeValue,
    sortColumn?: IncidentsListColumnKey,
    sortDescending?: boolean,
    visible?: boolean,
    refresh?: number
) => {
    const [threadCounts, setThreadCounts] = useState<IncidentThreadCounts>();
    const [threads, setThreads] = useState<Thread[]>(initialThreads || []);
    const [moreThreadsToLoad, setMoreThreadsToLoad] = useState<boolean>(true);
    const [unreadThreadIds, setUnreadThreadIds] = useState<Set<string>>(new Set<string>());
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const [isLoadingInitialThreadsAndCounts, setIsLoadingInitialThreadsAndCounts] = useState<boolean>(true);

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = useMemo(() => ThreadClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const threadCount = useRef<number>(0);
    const loadThreadsCallTimestamp = useRef<string>(new Date().toISOString());
    const isLoadingThreads = useRef<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);

    const deleteThread = useCallback(
        async (threadId: string) => {
            return threadClient.deleteThread(threadId).then(response => {
                if (response.isSuccessful) {
                    setThreads(prevThreads => prevThreads.filter(thread => thread.id !== threadId));
                    setUnreadThreadIds(prev => {
                        const newSet = new Set(prev);
                        newSet.delete(threadId);
                        return newSet;
                    });
                }
                return response;
            });
        },
        [threadClient.deleteThread]
    );

    const getThreads = useCallback(
        async (
            searchText: string | undefined,
            status: string[] | undefined,
            investigationStatus: string[] | undefined,
            createdTimeRange: TimeRangeValue | undefined,
            threadCount: number | undefined,
            sortColumn: IncidentsListColumnKey | undefined,
            sortDescending: boolean | undefined = true
        ) => {
            const { columnPath: sortColumnPath } = getColumnInfo(sortColumn || 'modifiedTimestamp');

            const filterStrings = getOdataFilterParts(
                searchText,
                status,
                investigationStatus,
                createdTimeRange,
                loadThreadsCallTimestamp.current,
                sortDescending
            );

            return await threadClient.getIncidentThreads({
                skip: threadCount ?? 0,
                top: ThreadLoadingCounts.default,
                orderBy: `${sortColumnPath}${sortDescending ? '+desc' : ''}`,
                filter: filterStrings.join(' and '),
            });
        },
        [threadClient]
    );

    const getThreadCounts = useCallback(
        async (
            searchText: string | undefined,
            status: string[] | undefined,
            investigationStatus: string[] | undefined,
            createdTimeRange: TimeRangeValue | undefined,
            sortDescending: boolean | undefined = true
        ) => {
            const filterStrings = getOdataFilterParts(
                searchText,
                status,
                investigationStatus,
                createdTimeRange,
                loadThreadsCallTimestamp.current,
                sortDescending
            );

            return await threadClient.getIncidentThreadCounts(filterStrings.join(' and '));
        },
        [threadClient]
    );

    const getInitialThreads = useCallback(
        async (
            searchText: string | undefined,
            status: string[] | undefined,
            investigationStatus: string[] | undefined,
            createdTimeRange: TimeRangeValue | undefined,
            sortColumn: IncidentsListColumnKey | undefined,
            sortDescending: boolean | undefined
        ) => {
            return await getThreads(searchText, status, investigationStatus, createdTimeRange, 0, sortColumn, sortDescending);
        },
        [getThreads]
    );

    const loadThreads = useCallback(async (): Promise<boolean | undefined> => {
        if (!isLoadingThreads.current && !isLoadingInitialThreadsAndCounts) {
            const callId = loadThreadsCallTimestamp.current;
            isLoadingThreads.current = true;

            const oldThreadsResponse = await getThreads(
                searchText,
                statusFilters,
                investigationStatusFilters,
                createdTimeFilter,
                threadCount.current,
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
    }, [
        getThreads,
        searchText,
        statusFilters,
        investigationStatusFilters,
        createdTimeFilter,
        sortColumn,
        sortDescending,
        isLoadingInitialThreadsAndCounts,
    ]);

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
        if (visible && observer && intersectionObserverRef.current && !isLoadingInitialThreadsAndCounts) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialThreadsAndCounts, visible]);

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
        threadCount.current = threads.length;
    }, [threads]);

    useEffect(() => {
        let isSubscribed = true;
        loadThreadsCallTimestamp.current = new Date().toISOString();

        if (!hasChatPermissions) {
            setThreads([]);
            setIsLoadingInitialThreadsAndCounts(false);
        } else {
            setIsLoadingInitialThreadsAndCounts(true);
            setMoreThreadsToLoad(true);

            const setInitialThreads = async () => {
                // Send a request to load initial threads based on the filter options to overflow the threads list div if possible
                isLoadingThreads.current = true;

                const initialThreadsPromise = getInitialThreads(
                    searchText,
                    statusFilters,
                    investigationStatusFilters,
                    createdTimeFilter,
                    sortColumn,
                    sortDescending
                );

                const threadCountsPromise = getThreadCounts(
                    searchText,
                    statusFilters,
                    investigationStatusFilters,
                    createdTimeFilter,
                    sortDescending
                );

                const [initialThreadsResponse, threadCountsResponse] = await Promise.all([initialThreadsPromise, threadCountsPromise]);

                const initialThreads = initialThreadsResponse.content ?? [];
                const threadCounts = threadCountsResponse.content;

                if (isSubscribed) {
                    // Do not set moreThreadsToLoad to false if the initial threads response is not successful.
                    if (initialThreadsResponse.isSuccessful && initialThreads.length === 0) {
                        setMoreThreadsToLoad(false);
                    }
                    // Replace the current filtered threads with the initial threads
                    const { threads: totalThreads, addedThreads } = processThreads([], initialThreads, false, sortColumn, sortDescending);
                    setThreads(totalThreads);
                    setUnreadThreadIds(prev => getUpdatedUnreadThreadIds(prev, addedThreads));
                    setThreadCounts(threadCounts);
                    setIsLoadingInitialThreadsAndCounts(false);
                }

                isLoadingThreads.current = false;
            };

            setInitialThreads();
        }

        return () => {
            isSubscribed = false;
        };
    }, [
        searchText,
        statusFilters,
        investigationStatusFilters,
        createdTimeFilter,
        sortColumn,
        sortDescending,
        hasChatPermissions,
        getInitialThreads,
        getThreadCounts,
        refresh,
    ]);

    return {
        threadCounts,

        threads,
        setThreads,
        setUnreadThreadIds,
        unreadThreadIds,
        moreThreadsToLoad,
        isLoadingInitialThreadsAndCounts,

        threadListDivRef,
        intersectionObserverRef,

        onScroll,
        deleteThread,
    };
};

const getOdataFilterParts = (
    searchText: string | undefined,
    status: string[] | undefined,
    investigationStatus: string[] | undefined,
    createdTimeRange: TimeRangeValue | undefined,
    loadThreadsCallTimestamp?: string,
    sortDescending?: boolean
) => {
    const statusFilter = status?.some(s => s === 'all') ? [] : status;
    const investigationStatusFilter = investigationStatus?.some(s => s === 'all') ? [] : investigationStatus;

    const filterStrings: string[] = [`source eq '${ThreadSource.incident}'`];

    if (loadThreadsCallTimestamp && sortDescending) {
        filterStrings.push(`createdTimestamp le ${loadThreadsCallTimestamp}`);
    }

    if (searchText) {
        const searchTextToLower = searchText.toLowerCase();
        const textFilterParts = [
            `(incidentDetails ne null and contains(tolower(incidentDetails/incidentTitle),'${searchTextToLower}'))`,
            `(incidentDetails eq null and contains(tolower(title),'${searchTextToLower}'))`,
            `contains(tolower(incidentId),'${searchTextToLower}')`,
            `contains(tolower(incidentDetails/filterId),'${searchTextToLower}')`,
        ];
        const textFilterString = `(${textFilterParts.join(' or ')})`;
        filterStrings.push(textFilterString);
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

    if (investigationStatusFilter?.length) {
        const investigationStatusFilterStrings = investigationStatusFilter.map(s => {
            return `incidentDetails/investigationStatus eq '${s}'`;
        });
        const investigationStatusFilterString = `(incidentDetails ne null and (${investigationStatusFilterStrings.join(' or ')}))`;
        filterStrings.push(investigationStatusFilterString);
    }

    if (createdTimeRange) {
        if (createdTimeRange.key === TimespanKeys.Custom) {
            const { start, end } = createdTimeRange;
            const [startString, endString] = [start?.toISOString(), end?.toISOString()];
            const timeFilterParts = [
                `(incidentDetails ne null and incidentDetails/incidentCreatedTime ge ${startString} and incidentDetails/incidentCreatedTime le ${endString})`,
                `(incidentDetails eq null and createdTimestamp ge ${startString} and createdTimestamp le ${endString})`,
            ];
            const timeFilterString = `(${timeFilterParts.join(' or ')})`;
            filterStrings.push(timeFilterString);
        } else {
            const spanInMilliseconds = getTimespanInMilliseconds(createdTimeRange.key);
            const start = new Date(Date.now() - spanInMilliseconds);
            const startString = start?.toISOString();
            const timeFilterParts = [
                `(incidentDetails ne null and incidentDetails/incidentCreatedTime ge ${startString})`,
                `(incidentDetails eq null and createdTimestamp ge ${startString})`,
            ];
            const timeFilterString = `(${timeFilterParts.join(' or ')})`;
            filterStrings.push(timeFilterString);
        }
    }

    return filterStrings;
};

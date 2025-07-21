import debounce from 'lodash/debounce';
import { useEffect, useRef, useState } from 'react';
import { getIntervalBetweenLoading } from '../Activities/Utility';

export const useThreadsPagination = (
    loadMoreOldThreads: (overflowDiv: boolean) => Promise<boolean | undefined>,
    hasMoreOldThreads: boolean,
    canLoadThreads: boolean
) => {
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);

    const handleScroll = debounce(() => {
        loadMoreOldThreads(false);
    }, 300);

    const onScroll = () => {
        const previousScrollTop = currentScrollTop.current;
        currentScrollTop.current = threadListDivRef.current?.scrollTop || 0;

        if (currentScrollTop.current > previousScrollTop && hasMoreOldThreads) {
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
        if (observer && intersectionObserverRef.current && canLoadThreads) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [canLoadThreads]);

    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        if (isIntersecting && hasMoreOldThreads) {
            let exponentialBackoffDepth = -1;

            const loadOldThreads = async () => {
                const isSuccessful = await loadMoreOldThreads(true);

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
    }, [loadMoreOldThreads, isIntersecting, hasMoreOldThreads]);

    return {
        threadListDivRef,
        intersectionObserverRef,
        onScroll,
    };
};

import { mergeClasses } from '@fluentui/react-components';
import { Skeleton, SkeletonItem } from '@fluentui/react-skeleton';
import { forwardRef, memo, useEffect, useRef, useState } from 'react';
import InfiniteScroll from 'react-infinite-scroll-component';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { getIntervalBetweenLoading } from '../Activities/Utility';
import { skeletonStyle, useThreadMenuStyle } from '../Styles/Activities.styles';
import ThreadItem from './ThreadItem';

interface IThreadsListProps {
    threads: Thread[];
    isLoadingInitialThreads: boolean;
    selectThread: (thread: Thread | null) => void;
    hasMoreOldThreads: boolean;
    loadMoreOldThreads: (overflowDiv: boolean) => Promise<boolean | undefined>;
    activeThreadId: string;
    unreadThreadIds: Set<string>;
}

// A thread list component that displays a list of the threads filtered by search text, source, timestamp and severity.
// The threads are loaded dynamically when scrolling down the list, backed by an infinite scroll component.
// An intersection observer is also used to load more threads when the requirement of making infinite scroll component work is not satisfied.
const ThreadsList = forwardRef<HTMLDivElement, IThreadsListProps>((props, ref) => {
    const { threads, isLoadingInitialThreads, selectThread, hasMoreOldThreads, loadMoreOldThreads, activeThreadId, unreadThreadIds } =
        props;

    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);

    const { scrollable } = useScrollableComponentStyles();
    const ThreadMenuStyles = useThreadMenuStyle();

    // Use an intersection observer to load more threads to overflow the threads list div if the current number of threads
    // does not overflow the threads list div anymore due to events such as zoom out, which makes InifiniteScroll not able to work.
    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            setIsIntersecting(entry.isIntersecting);
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialThreads) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialThreads]);

    useEffect(() => {
        let timeoutId: NodeJS.Timeout | null = null;

        if (isIntersecting && hasMoreOldThreads) {
            let exponentialBackoffDepth = -1;

            const loadOldThreads = async () => {
                const isSuccessful = await loadMoreOldThreads(true);

                exponentialBackoffDepth = isSuccessful === false ? exponentialBackoffDepth + 1 : -1;
                const interval = getIntervalBetweenLoading(exponentialBackoffDepth);

                timeoutId = setTimeout(loadOldThreads, interval);
            };
            loadOldThreads();
        }

        return () => {
            if (timeoutId !== null) {
                clearTimeout(timeoutId);
            }
        };
    }, [loadMoreOldThreads, isIntersecting, hasMoreOldThreads]);

    return (
        <div className={ThreadMenuStyles.threadListContainer}>
            <div className={mergeClasses(scrollable, ThreadMenuStyles.threadList)} role="tree" ref={ref} id={'threads-list-scrollable'}>
                <InfiniteScroll
                    dataLength={threads.length}
                    next={() => loadMoreOldThreads(false)}
                    hasMore={hasMoreOldThreads}
                    loader={null}
                    scrollThreshold={0.1} // Trigger loading more threads when scrolled to 10% of the scrollable area
                    scrollableTarget={'threads-list-scrollable'}
                >
                    {threads.map(thread => {
                        return (
                            <ThreadItem
                                key={thread.id}
                                thread={thread}
                                selectThread={selectThread}
                                isActive={activeThreadId === thread.id}
                                isThreadUnread={unreadThreadIds.has(thread.id)}
                            />
                        );
                    })}
                    {hasMoreOldThreads && (
                        <Skeleton style={skeletonStyle} ref={intersectionObserverRef}>
                            <SkeletonItem />
                        </Skeleton>
                    )}
                </InfiniteScroll>
            </div>
        </div>
    );
});

export default memo(ThreadsList);

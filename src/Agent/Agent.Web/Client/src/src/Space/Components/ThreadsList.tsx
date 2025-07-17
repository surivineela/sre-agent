import { mergeClasses } from '@fluentui/react-components';
import { Skeleton, SkeletonItem } from '@fluentui/react-skeleton';
import debounce from 'lodash/debounce';
import { forwardRef, memo, useEffect, useImperativeHandle, useRef, useState } from 'react';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { getIntervalBetweenLoading } from '../Activities/Utility';
import { ThreadListHandle } from '../Contracts/Activities';
import { skeletonStyle, useThreadMenuStyle } from '../Styles/Activities.styles';
import ThreadItem from './ThreadItem';

interface IThreadsListProps {
    threads: Thread[];
    isLoadingInitialThreads: boolean;
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    hasMoreOldThreads: boolean;
    loadMoreOldThreads: (overflowDiv: boolean) => Promise<boolean | undefined>;
    activeThreadId: string;
    unreadThreadIds: Set<string>;
}

// A thread list component that displays a list of the threads filtered by search text, source, timestamp and severity.
// The threads are loaded dynamically when scrolling down the list, backed by an infinite scroll component.
// An intersection observer is also used to load more threads when the requirement of making infinite scroll component work is not satisfied.
const ThreadsList = forwardRef<ThreadListHandle, IThreadsListProps>((props, ref) => {
    const {
        threads,
        isLoadingInitialThreads,
        selectThread,
        deleteThread,
        hasMoreOldThreads,
        loadMoreOldThreads,
        activeThreadId,
        unreadThreadIds,
    } = props;

    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const intersectionObserverRef = useRef<HTMLDivElement | null>(null);
    const threadListDivRef = useRef<HTMLDivElement | null>(null);
    const currentScrollTop = useRef<number>(0);

    const { scrollable } = useScrollableComponentStyles();
    const ThreadMenuStyles = useThreadMenuStyle();

    useImperativeHandle(ref, () => ({
        getThreadListHeight: () => {
            return threadListDivRef.current?.clientHeight || 0;
        },
    }));

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
        if (observer && intersectionObserverRef.current && !isLoadingInitialThreads) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialThreads]);

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

    return (
        <div
            className={mergeClasses(scrollable, ThreadMenuStyles.threadListContainer)}
            role="tree"
            ref={threadListDivRef}
            onScroll={onScroll}
        >
            {threads.map(thread => {
                return (
                    <ThreadItem
                        key={thread.id}
                        thread={thread}
                        selectThread={selectThread}
                        deleteThread={deleteThread}
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
        </div>
    );
});

export default memo(ThreadsList);

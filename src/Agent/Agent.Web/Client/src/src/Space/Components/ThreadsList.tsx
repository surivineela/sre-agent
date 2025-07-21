import { mergeClasses } from '@fluentui/react-components';
import { Skeleton, SkeletonItem } from '@fluentui/react-skeleton';
import { forwardRef, memo, useImperativeHandle } from 'react';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ThreadListHandle } from '../Contracts/Activities';
import { useThreadsPagination } from '../Hooks/useThreadsPagination';
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

    const { scrollable } = useScrollableComponentStyles();
    const ThreadMenuStyles = useThreadMenuStyle();

    const { threadListDivRef, intersectionObserverRef, onScroll } = useThreadsPagination(
        loadMoreOldThreads,
        hasMoreOldThreads,
        !isLoadingInitialThreads
    );

    useImperativeHandle(ref, () => ({
        getThreadListHeight: () => {
            return threadListDivRef.current?.clientHeight || 0;
        },
    }));

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

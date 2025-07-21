import { Button } from '@fluentui/react-button';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { DialogBody, DialogContent, DialogSurface, DialogTitle, DialogTrigger } from '@fluentui/react-dialog';
import { ChatRegular } from '@fluentui/react-icons';
import { SearchBox } from '@fluentui/react-search';
import { Skeleton, SkeletonItem } from '@fluentui/react-skeleton';
import { Body1, Caption2 } from '@fluentui/react-text';
import { tokens } from '@fluentui/react-theme';
import debounce from 'lodash/debounce';
import { memo, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { getFilteredThreads, processThreads } from '../Activities/Utility';
import { ThreadLoadingCounts } from '../Contracts/Activities';
import { useThreadsPagination } from '../Hooks/useThreadsPagination';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

interface IThreadSearchDialogProps {
    threads: Thread[];
    selectThread: (thread: Thread | null) => void;
    activeThreadId?: string;
}

const useThreadSearchDialogStyles = makeStyles({
    surface: {
        width: '100vw',
        maxWidth: '100%',
        height: '100vh',
        maxHeight: '100%',
        '@media (min-width: 800px)': {
            width: '800px',
            maxWidth: '100%',
        },
        '@media (min-height: 450px)': {
            height: '450px',
            maxHeight: '450px',
        },
    },
    common: {
        width: '100%',
    },
    body: {
        height: '100%',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: `${tokens.spacingVerticalS}`,
        alignItems: 'flex-start',
        marginTop: '20px',
        minHeight: '0',
    },
    searchBox: {
        borderRadius: tokens.borderRadiusLarge,
        width: '100%',
    },
    threads: {
        overflowY: 'auto',
        overflowX: 'hidden',
        minHeight: '0',
    },
    threadTitleContainer: {
        display: 'flex',
        flexDirection: 'column',
        flex: 1,
        gap: `${tokens.spacingVerticalXS}`,
        minWidth: '0',
    },
    threadItemLoader: {
        maxWidth: '500px',
    },
    skeletonItems: {
        display: 'flex',
        flexDirection: 'row',
        gap: '10px',
        alignItems: 'center',
        minWidth: '0px',
        padding: `${tokens.spacingVerticalS} ${tokens.spacingVerticalS}`,
    },
    circleSkeleton: {
        marginRight: '6px',
    },
    rectangleSkeletons: {
        marginBottom: '5px',
        maxWidth: '50%',
    },
});

const ThreadSearchDialog = ({ threads, selectThread, activeThreadId }: IThreadSearchDialogProps) => {
    const { scrollable } = useScrollableComponentStyles();
    const { surface, common, body, content, searchBox, threads: threadsStyles } = useThreadSearchDialogStyles();

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [threadsToDisplay, setThreadsToDisplay] = useState<Thread[]>(threads);
    const [searchText, setSearchText] = useState('');
    const [canLoadMoreThreads, setCanLoadMoreThreads] = useState(false);
    const [hasMoreThreadsToLoad, setHasMoreThreadsToLoad] = useState(true);

    const oldestThreadModifiedTimestamp = useRef<string | undefined>(undefined);
    const loadMoreThreadsCallId = useRef(0);
    const isLoadingMoreThreads = useRef(false);

    const prepareForSearchTextChange = () => {
        setCanLoadMoreThreads(false);
        setHasMoreThreadsToLoad(true);
        oldestThreadModifiedTimestamp.current = undefined;
    };

    const onSearchTextChange = debounce((searchString: string) => {
        prepareForSearchTextChange();
        setSearchText(searchString);
    }, 1000);

    const onClickThreadButton = useCallback(
        (thread: Thread) => {
            if (thread.id !== activeThreadId) {
                selectThread(thread);
            }
        },
        [selectThread, activeThreadId]
    );

    const loadMoreThreads = useCallback(
        async (_: boolean) => {
            if (canLoadMoreThreads && !isLoadingMoreThreads.current) {
                const callId = loadMoreThreadsCallId.current;
                isLoadingMoreThreads.current = true;

                const olderThreadsResponse = await threadClient.getThreads({
                    skip: 0,
                    top: ThreadLoadingCounts.scroll,
                    descending: true,
                    filters: {
                        searchText,
                        timestamps: {
                            max: oldestThreadModifiedTimestamp.current
                                ? {
                                      timestamp: oldestThreadModifiedTimestamp.current,
                                      inclusive: false,
                                  }
                                : undefined,
                        },
                    },
                });

                if (callId === loadMoreThreadsCallId.current) {
                    const olderThreads = olderThreadsResponse.content ?? [];
                    if (olderThreadsResponse.isSuccessful && olderThreads.length <= 0) {
                        setHasMoreThreadsToLoad(false);
                    }
                    if (olderThreads.length > 0) {
                        setThreadsToDisplay(prevThreads => {
                            const { threads: totalThreads } = processThreads(prevThreads, olderThreads, false);
                            oldestThreadModifiedTimestamp.current = totalThreads[totalThreads.length - 1]?.modifiedTimestamp;
                            return totalThreads;
                        });
                    }

                    isLoadingMoreThreads.current = false;
                    return olderThreadsResponse.isSuccessful;
                } else {
                    isLoadingMoreThreads.current = false;
                    return undefined;
                }
            }
        },
        [canLoadMoreThreads, searchText]
    );

    useEffect(() => {
        // Increment loadMoreThreadsCallId when canLoadMoreThreads or searchText changes, to ensure that the result from calling loadMoreThreads with outdated searchText value is disregarded
        return () => {
            loadMoreThreadsCallId.current += 1;
        };
    }, [canLoadMoreThreads, searchText]);

    useEffect(() => {
        setThreadsToDisplay(prev => getFilteredThreads(prev, undefined, searchText));

        // Allow loading more threads after finishing filtering existing threads
        setCanLoadMoreThreads(true);
    }, [searchText]);

    const { threadListDivRef, intersectionObserverRef, onScroll } = useThreadsPagination(
        loadMoreThreads,
        hasMoreThreadsToLoad,
        canLoadMoreThreads
    );

    return (
        <DialogSurface className={surface}>
            <DialogBody className={mergeClasses(common, body)}>
                <DialogTitle>
                    <FormattedMessage {...SreAgentResources.search} />
                </DialogTitle>
                <DialogContent className={mergeClasses(common, content)}>
                    <SearchBox className={searchBox} onChange={(_, data) => onSearchTextChange(data?.value || '')} />
                    <div className={mergeClasses(scrollable, common, threadsStyles)} ref={threadListDivRef} onScroll={onScroll}>
                        {threadsToDisplay.map(thread => (
                            <ThreadItemButton key={thread.id} thread={thread} onClickThreadButton={onClickThreadButton} />
                        ))}
                        {hasMoreThreadsToLoad && (
                            <div ref={intersectionObserverRef}>
                                <ThreadItemLoader />
                            </div>
                        )}
                    </div>
                </DialogContent>
            </DialogBody>
        </DialogSurface>
    );
};

const ThreadItemButton = memo(({ thread, onClickThreadButton }: { thread: Thread; onClickThreadButton: (thread: Thread) => void }) => {
    const { threadTitleContainer } = useThreadSearchDialogStyles();
    const { title } = useActionsStatusBarStyles();

    return (
        <DialogTrigger key={thread.id}>
            <Button
                appearance="subtle"
                icon={<ChatRegular fontSize={16} />}
                style={{
                    width: '100%',
                    justifyContent: 'flex-start',
                    padding: `${tokens.spacingVerticalS} ${tokens.spacingVerticalS}`,
                    gap: `${tokens.spacingHorizontalS}`,
                }}
                onClick={() => onClickThreadButton(thread)}
            >
                <div className={threadTitleContainer}>
                    <Body1 className={title} block wrap={false}>
                        {thread.title}
                    </Body1>
                    <Caption2 className={title} block wrap={false}>
                        {thread.lastMessage.text}
                    </Caption2>
                </div>
            </Button>
        </DialogTrigger>
    );
});

const ThreadItemLoader = memo(() => {
    const { common, threadItemLoader, skeletonItems, circleSkeleton, rectangleSkeletons } = useThreadSearchDialogStyles();

    return (
        <Skeleton className={threadItemLoader}>
            {Array.from({ length: 3 }, (_, index) => (
                <div key={index} className={skeletonItems}>
                    <SkeletonItem shape={'circle'} size={20} className={circleSkeleton} />
                    <div className={common}>
                        <SkeletonItem size={12} className={rectangleSkeletons} />
                        <SkeletonItem size={12} />
                    </div>
                </div>
            ))}
        </Skeleton>
    );
});

export default memo(ThreadSearchDialog);

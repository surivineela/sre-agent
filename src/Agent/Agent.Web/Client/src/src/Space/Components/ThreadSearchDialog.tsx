import { Button } from '@fluentui/react-button';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { DialogBody, DialogContent, DialogSurface, DialogTitle, DialogTrigger } from '@fluentui/react-dialog';
import { ChatRegular } from '@fluentui/react-icons';
import { SearchBox } from '@fluentui/react-search';
import { Skeleton, SkeletonItem } from '@fluentui/react-skeleton';
import { Body1, Caption2 } from '@fluentui/react-text';
import { tokens } from '@fluentui/react-theme';
import debounce from 'lodash/debounce';
import { memo, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadList } from '../Hooks/useThreadList';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

interface IThreadSearchDialogProps {
    threads: Thread[];
    selectThread: (thread: Thread | null) => void;
    activeThreadId?: string;
    excludedSources?: ThreadSource[];
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
const ThreadSearchDialog = ({ threads: initialThreads, selectThread, activeThreadId, excludedSources }: IThreadSearchDialogProps) => {
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const { scrollable } = useScrollableComponentStyles();
    const { surface, common, body, content, searchBox, threads: threadsStyles } = useThreadSearchDialogStyles();

    const [searchText, setSearchText] = useState('');

    const onSearchTextChange = debounce((searchString: string) => {
        setSearchText(searchString);
    }, 1000);

    const onClickThreadButton = useCallback(
        (thread: Thread) => {
            if (thread.id !== activeThreadId) {
                selectThread(thread);

                logAmplitudeControlEvent({
                    targetType: 'button',
                    targetAction: 'clicked',
                    targetName: 'selectSearchedThread',
                    targetFriendlyName: 'Select searched thread',
                    valueObjectName: thread.id,
                    valueObjectFriendlyName: thread.id,
                    metadata: {
                        threadId: thread.id,
                        threadType: thread.source ?? 'unknown',
                    },
                });
            }
        },
        [selectThread, activeThreadId, logAmplitudeControlEvent]
    );

    const { threads, moreThreadsToLoad, threadListDivRef, intersectionObserverRef, onScroll } = useThreadList(
        undefined,
        initialThreads,
        undefined,
        excludedSources,
        undefined,
        searchText,
        'modifiedTimestamp'
    );

    const intl = useIntl();

    return (
        <DialogSurface className={surface}>
            <DialogBody className={mergeClasses(common, body)}>
                <DialogTitle>{intl.formatMessage(SreAgentResources.search)}</DialogTitle>
                <DialogContent className={mergeClasses(common, content)}>
                    <SearchBox
                        aria-label={intl.formatMessage(SreAgentResources.search)}
                        className={searchBox}
                        onChange={(_, data) => onSearchTextChange(data?.value || '')}
                    />
                    <div className={mergeClasses(scrollable, common, threadsStyles)} ref={threadListDivRef} onScroll={onScroll}>
                        {threads.map(thread => (
                            <ThreadItemButton key={thread.id} thread={thread} onClickThreadButton={onClickThreadButton} />
                        ))}
                        {moreThreadsToLoad && (
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

    const intl = useIntl();

    return (
        <Skeleton aria-label={intl.formatMessage(ActivitiesResources.threadsLoadingSkeletonAriaLabel)} className={threadItemLoader}>
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

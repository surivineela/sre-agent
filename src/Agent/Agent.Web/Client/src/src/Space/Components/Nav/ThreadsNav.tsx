import { CopilotNavCategory, tokens } from '@fluentui-copilot/react-copilot';
import { CopilotNavSubItemGroup, SplitCopilotNavCategoryItem } from '@fluentui-copilot/react-copilot-nav';
import { makeStyles, Skeleton, SkeletonItem, useId } from '@fluentui/react-components';
import { memo, MutableRefObject, ReactNode, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useLocation } from 'react-router';
import { Thread } from '../../../Common/Contracts/DataPlane/Thread';
import useUserPermissions from '../../../Common/Hooks/useUserPermissions';
import { KnowledgeGraphBuildStatusContext } from '../../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources } from '../../../Strings/SREAgentResources';
import { ThreadListsState } from '../../Contracts/Activities';
import { SreAgentSpaceContext, ThreadNavContext } from '../../Contracts/Context';
import { PrimaryNavItemValues, ThreadCategoryKey } from '../../Contracts/SreAgentSpace';
import { skeletonStyle } from '../../Styles/Activities.styles';
import { getNavItemIdFromPathName } from '../../Utilities';
import Fade from '../Fade';
import ThreadFilters from '../ThreadFilters';
import ThreadItem from '../ThreadItem';

const useStyles = makeStyles({
    root: {
        marginTop: tokens.spacingVerticalL,
        paddingTop: tokens.spacingVerticalM,
        borderTop: `1.5px solid ${tokens.colorNeutralStroke1}`,
    },
    categoryHeader: {
        position: 'sticky',
        top: '0',
        zIndex: '10',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
    },
    categoryGroup: {
        '& > div:first-of-type': {
            marginTop: 'unset',
        },
    },
    divider: {
        height: '1px',
        borderTop: '1px solid rgba(204,204,204,.8)',
    },
    threadItemButton: {
        minWidth: '0px',
    },
    chat: {
        paddingTop: tokens.spacingVerticalM,
        borderTop: `2px solid ${tokens.colorNeutralStroke2}`,
    },
});

interface IThreadNavProps {
    isNavOpen: boolean;
    showUnreadOnly: boolean;
    setShowUnreadOnly: (value: boolean) => void;
    threadListsState: ThreadListsState;
    unreadThreadIds: Set<string>;
    isUpdatingThreadFavoriteProperty: boolean;
    favoriteThreadsIntersectionObserverRef: MutableRefObject<HTMLDivElement | null>;
    regularThreadsIntersectionObserverRef: MutableRefObject<HTMLDivElement | null>;
    threadItemDivsRef: MutableRefObject<Map<string, HTMLDivElement>>;
    onClickCategoryNavItem: (categoryKey: PrimaryNavItemValues | ThreadCategoryKey) => void;
}

const ThreadsNav = ({
    isNavOpen,
    showUnreadOnly,
    setShowUnreadOnly,
    threadListsState,
    unreadThreadIds,
    isUpdatingThreadFavoriteProperty,
    favoriteThreadsIntersectionObserverRef,
    regularThreadsIntersectionObserverRef,
    threadItemDivsRef,
    onClickCategoryNavItem,
}: IThreadNavProps) => {
    const intl = useIntl();

    const styles = useStyles();
    const { selectThread, deleteThread, updateThreadTitle, updateThreadFavorite } = useContext(SreAgentSpaceContext);

    const favoriteNavId = useId('threads-nav-favorite-item');
    const regularNavId = useId('threads-nav-regular-item');

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { canReadThreads } = useUserPermissions();

    const assignThreadItemDivRef = useCallback(
        (threadId: string, el: HTMLDivElement) => {
            threadItemDivsRef.current.set(threadId, el);
        },
        [threadItemDivsRef]
    );

    const threadCategories = useMemo(() => {
        return [
            {
                title: intl.formatMessage(ActivitiesResources.favoriteThreadListTitle),
                categoryValue: ThreadCategoryKey.Favorites as string,
                id: favoriteNavId,
                threads: threadListsState.favoriteThreadListState.threads,
                threadsThatHaveFavoritePropertyChanged: threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                intersectionObserverRef: favoriteThreadsIntersectionObserverRef,
                moreThreadsToLoad: threadListsState.favoriteThreadListState.moreThreadsToLoad,
                onClickCategoryNavItem: () => onClickCategoryNavItem(ThreadCategoryKey.Favorites),
            },
            {
                title: intl.formatMessage(ActivitiesResources.regularThreadListTitle),
                categoryValue: ThreadCategoryKey.Regular as string,
                id: regularNavId,
                threads: threadListsState.regularThreadListState.threads,
                threadsThatHaveFavoritePropertyChanged: threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                intersectionObserverRef: regularThreadsIntersectionObserverRef,
                moreThreadsToLoad: threadListsState.regularThreadListState.moreThreadsToLoad,
                onClickCategoryNavItem: () => onClickCategoryNavItem(ThreadCategoryKey.Regular),
            },
        ];
    }, [
        intl,
        favoriteNavId,
        regularNavId,
        threadListsState,
        favoriteThreadsIntersectionObserverRef,
        regularThreadsIntersectionObserverRef,
        onClickCategoryNavItem,
    ]);

    return (
        <ThreadNavContext.Provider
            value={{ unreadThreadIds, updateThreadTitle, updateThreadFavorite, selectThread, deleteThread, assignThreadItemDivRef }}
        >
            <Fade visible={isNavOpen && canReadThreads} unmountOnExit>
                <div className={styles.root}>
                    {hasChatPermissions && (
                        <ThreadFilters
                            disabled={isUpdatingThreadFavoriteProperty}
                            unreadOnly={showUnreadOnly}
                            setUnreadOnly={setShowUnreadOnly}
                        />
                    )}
                    {hasChatPermissions &&
                        threadCategories.map(category => (
                            <ThreadNavCategory
                                key={category.id}
                                title={category.title}
                                categoryValue={category.categoryValue}
                                id={category.id}
                                threads={category.threads}
                                threadsThatHaveFavoritePropertyChanged={category.threadsThatHaveFavoritePropertyChanged}
                                onClickCategoryNavItem={category.onClickCategoryNavItem}
                            >
                                {category.moreThreadsToLoad && (
                                    <div ref={category.intersectionObserverRef}>
                                        <Loader />
                                    </div>
                                )}
                            </ThreadNavCategory>
                        ))}
                </div>
            </Fade>
        </ThreadNavContext.Provider>
    );
};

const ThreadNavCategory = memo(
    (props: {
        title: string;
        categoryValue: string;
        id: string;
        threads: Thread[];
        threadsThatHaveFavoritePropertyChanged: Thread[];
        children: ReactNode;
        onClickCategoryNavItem: () => void;
    }) => {
        const location = useLocation();
        const intl = useIntl();

        const activeThreadId = getNavItemIdFromPathName(location.pathname);
        const { unreadThreadIds } = useContext(ThreadNavContext);

        const styles = useStyles();

        const isThreadUnread = (id: string) => {
            return unreadThreadIds.has(id) && activeThreadId !== id;
        };

        return (
            <CopilotNavCategory value={props.categoryValue}>
                <SplitCopilotNavCategoryItem
                    id={props.id}
                    size={'small'}
                    expandIconTooltipContent={intl.formatMessage(ActivitiesResources.expandChatTooltip)}
                    collapseIconTooltipContent={intl.formatMessage(ActivitiesResources.collapseChatTooltip)}
                    className={styles.categoryHeader}
                    onClick={props.onClickCategoryNavItem}
                >
                    {props.title}
                </SplitCopilotNavCategoryItem>
                <CopilotNavSubItemGroup aria-labelledby={props.id} className={styles.categoryGroup}>
                    {[...props.threads, ...props.threadsThatHaveFavoritePropertyChanged].map(item => (
                        <ThreadItem key={item.id} item={item} isThreadUnread={isThreadUnread(item.id)} />
                    ))}
                    {props.children}
                </CopilotNavSubItemGroup>
            </CopilotNavCategory>
        );
    }
);

const Loader = memo(() => {
    const intl = useIntl();

    return Array.from({ length: 3 }).map((_, index) => {
        return (
            <Skeleton
                aria-label={intl.formatMessage(ActivitiesResources.threadsLoadingSkeletonAriaLabel)}
                key={index}
                style={skeletonStyle}
            >
                <SkeletonItem size={20} />
            </Skeleton>
        );
    });
});

export default memo(ThreadsNav);

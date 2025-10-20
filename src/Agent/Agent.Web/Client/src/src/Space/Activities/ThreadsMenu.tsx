import { Button } from '@fluentui/react-button';
import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    AccordionToggleEventHandler,
    makeStyles,
    mergeClasses,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { Dialog, DialogTrigger } from '@fluentui/react-dialog';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular, SearchRegular } from '@fluentui/react-icons';
import { Caption1, Text } from '@fluentui/react-text';
import { tokens } from '@fluentui/react-theme';
import { ForwardedRef, forwardRef, ReactNode, useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import Fade from '../Components/Fade';
import ThreadFilters from '../Components/ThreadFilters';
import ThreadItem from '../Components/ThreadItem';
import ThreadSearchDialog from '../Components/ThreadSearchDialog';
import { IThreadsMenuProps, ThreadMenuHandle } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { usePermissionContext } from '../Contracts/PermissionContext';
import { useThreadsMenu } from '../Hooks/useThreadsMenu';
import { getExpandCollapseButtonStyles, skeletonStyle, useThreadMenuStyle } from '../Styles/Activities.styles';

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('left');

const useAccordionHeaderStyles = makeStyles({
    accordionHeader: {
        position: 'sticky',
        top: '0',
        zIndex: '10',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '16px !important',
        overflow: 'hidden',
        paddingLeft: '10px', // Additional left padding for text
        // Target all internal elements including buttons
        '& > *': {
            borderRadius: '16px !important',
        },
        ':hover': {
            backgroundColor: `${tokens.colorNeutralBackground2} !important`, // Lighter than base background
            borderRadius: '16px !important',
            border: `1px solid ${tokens.colorNeutralStroke2} !important`, // Subtle border for depth
            // Ensure all child elements get the hover background and radius
            '& > *': {
                backgroundColor: `${tokens.colorNeutralBackground2} !important`, // Lighter than base background
                borderRadius: '16px !important',
            },
        },
    },
});

enum ThreadSection {
    Favorite,
    Chats,
}

export const ThreadsMenu = forwardRef<ThreadMenuHandle, IThreadsMenuProps>(
    (props: IThreadsMenuProps, ref: ForwardedRef<ThreadMenuHandle>) => {
        const { selectThread, deleteThread, collapsed, setCollapsed } = props;

        const excludedSources: ThreadSource[] = useMemo(() => [ThreadSource.incident, ThreadSource.dailyReport], []);

        const {
            threadListDivRef,
            threadItemDivsRef,
            threadListsState,
            unreadThreadIds,
            showUnreadOnly,
            setShowUnreadOnly,
            setIsFavoriteThreadListHidden,
            setIsRegularThreadListHidden,
            updateThreadFavoriteProperty,
            onScroll,
            favoriteThreadsIntersectionObserverRef,
            regularThreadsIntersectionObserverRef,
            isUpdatingThreadFavoriteProperty,
        } = useThreadsMenu(ref, excludedSources);

        const threadMenuStyles = useThreadMenuStyle();
        const { scrollable } = useScrollableComponentStyles();

        const { activeThreadId } = useContext(AgentContext);
        const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
        const { canWriteThreads } = usePermissionContext();
        const { logAmplitudeControlEvent } = useAzPortalContext();

        const [openThreadSections, setOpenThreadSections] = useState<ThreadSection[]>([ThreadSection.Favorite, ThreadSection.Chats]);

        const toggleThreadSection: AccordionToggleEventHandler<ThreadSection> = (_, item) => {
            setOpenThreadSections(item.openItems);
            setIsFavoriteThreadListHidden(!item.openItems.includes(ThreadSection.Favorite));
            setIsRegularThreadListHidden(!item.openItems.includes(ThreadSection.Chats));
        };

        const intl = useIntl();

        const onClickNewThread = useCallback(() => {
            selectThread(null);
            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'newThread',
                targetFriendlyName: 'New chat thread',
                valueObjectName: SpecialControlValue.DoAction,
                valueObjectFriendlyName: SpecialControlValue.DoAction,
            });
        }, [selectThread, logAmplitudeControlEvent]);

        const assignThreadItemDivRef = useCallback(
            (threadId: string, el: HTMLDivElement) => {
                threadItemDivsRef.current.set(threadId, el);
            },
            [threadItemDivsRef]
        );

        return (
            <div className={threadMenuStyles.root}>
                <div style={expandCollapseButtonStyles.container}>
                    <Button
                        style={expandCollapseButtonStyles.button}
                        icon={collapsed ? <PanelLeftExpandRegular /> : <PanelLeftContractRegular />}
                        onClick={() => {
                            if (collapsed) {
                                setCollapsed(false);
                            } else {
                                setCollapsed(true);
                            }
                        }}
                        aria-label={intl.formatMessage(
                            collapsed ? ActivitiesResources.showThreadMenuButtonText : ActivitiesResources.hideThreadMenuButtonText
                        )}
                        appearance="transparent"
                    />
                </div>
                <div className={threadMenuStyles.newItemButtonAndSearchBox}>
                    <PermissionedButton
                        canPerform={!!canWriteThreads && !!hasChatPermissions}
                        noPermissionTooltip={intl.formatMessage(ActivitiesResources.createThreadNoPermissionTooltip)}
                        style={{
                            borderRadius: tokens.borderRadiusLarge,
                            borderColor: tokens.colorNeutralBackground3Selected,
                            maxWidth: 'fit-content',
                            minWidth: 'unset',
                        }}
                        icon={<AddRegular />}
                        onClick={() => onClickNewThread()}
                        aria-label={intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                    >
                        {!collapsed && (
                            <Fade visible={true} appear={true} unmountOnExit>
                                <Text wrap={false}>{intl.formatMessage(ActivitiesResources.createThreadButtonText)}</Text>
                            </Fade>
                        )}
                    </PermissionedButton>
                    {hasChatPermissions && (
                        <Fade visible={!collapsed} unmountOnExit>
                            <div>
                                <Dialog>
                                    <DialogTrigger>
                                        <Button
                                            aria-label={intl.formatMessage(SreAgentResources.search)}
                                            icon={<SearchRegular />}
                                            style={{
                                                borderRadius: tokens.borderRadiusLarge,
                                                borderColor: tokens.colorNeutralBackground3Selected,
                                            }}
                                            onClick={() => {
                                                logAmplitudeControlEvent({
                                                    targetType: 'button',
                                                    targetAction: 'clicked',
                                                    targetName: 'searchThreads',
                                                    targetFriendlyName: 'Search threads',
                                                    valueObjectName: SpecialControlValue.DoAction,
                                                    valueObjectFriendlyName: SpecialControlValue.DoAction,
                                                });
                                            }}
                                        />
                                    </DialogTrigger>
                                    <ThreadSearchDialog
                                        threads={threadListsState.regularThreadListState.threads}
                                        selectThread={selectThread}
                                        activeThreadId={activeThreadId}
                                        excludedSources={excludedSources}
                                    />
                                </Dialog>
                            </div>
                        </Fade>
                    )}
                </div>
                {hasChatPermissions && (
                    <Fade visible={!collapsed} unmountOnExit>
                        <div>
                            <ThreadFilters
                                disabled={isUpdatingThreadFavoriteProperty}
                                unreadOnly={showUnreadOnly}
                                setUnreadOnly={setShowUnreadOnly}
                            />
                        </div>
                    </Fade>
                )}
                {hasChatPermissions && (
                    <Fade visible={!collapsed}>
                        <div
                            className={mergeClasses(scrollable, threadMenuStyles.threadListContainer)}
                            ref={threadListDivRef}
                            onScroll={onScroll}
                        >
                            <Accordion<ThreadSection> openItems={openThreadSections} onToggle={toggleThreadSection} multiple collapsible>
                                <ThreadListAccordion
                                    isFavorite={true}
                                    threads={threadListsState.favoriteThreadListState.threads}
                                    threadsThatHaveFavoritePropertyChanged={
                                        threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged
                                    }
                                    unreadThreadIds={unreadThreadIds}
                                    activeThreadId={activeThreadId}
                                    selectThread={selectThread}
                                    deleteThread={deleteThread}
                                    assignThreadItemDivRef={assignThreadItemDivRef}
                                    updateThreadFavoriteProperty={updateThreadFavoriteProperty}
                                >
                                    {threadListsState.favoriteThreadListState.moreThreadsToLoad && (
                                        <div ref={favoriteThreadsIntersectionObserverRef}>
                                            <Loader />
                                        </div>
                                    )}
                                </ThreadListAccordion>
                                <ThreadListAccordion
                                    isFavorite={false}
                                    threads={threadListsState.regularThreadListState.threads}
                                    threadsThatHaveFavoritePropertyChanged={
                                        threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged
                                    }
                                    unreadThreadIds={unreadThreadIds}
                                    activeThreadId={activeThreadId}
                                    selectThread={selectThread}
                                    deleteThread={deleteThread}
                                    assignThreadItemDivRef={assignThreadItemDivRef}
                                    updateThreadFavoriteProperty={updateThreadFavoriteProperty}
                                >
                                    {threadListsState.regularThreadListState.moreThreadsToLoad && (
                                        <div ref={regularThreadsIntersectionObserverRef}>
                                            <Loader />
                                        </div>
                                    )}
                                </ThreadListAccordion>
                            </Accordion>
                        </div>
                    </Fade>
                )}
            </div>
        );
    }
);

const ThreadListAccordion = ({
    children,
    isFavorite,
    threads,
    threadsThatHaveFavoritePropertyChanged,
    unreadThreadIds,
    activeThreadId,
    selectThread,
    deleteThread,
    assignThreadItemDivRef,
    updateThreadFavoriteProperty,
}: {
    children: ReactNode;
    isFavorite: boolean;
    threads: Thread[];
    threadsThatHaveFavoritePropertyChanged: Thread[];
    unreadThreadIds: Set<string>;
    activeThreadId: string;
    selectThread: (thread: Thread | null) => void;
    deleteThread: ((thread: Thread) => void) | undefined;
    assignThreadItemDivRef: (threadId: string, el: HTMLDivElement) => void;
    updateThreadFavoriteProperty: (threadId: string, favorite: boolean) => Promise<void>;
}) => {
    const accordionHeaderStyles = useAccordionHeaderStyles();
    return (
        <AccordionItem value={isFavorite ? ThreadSection.Favorite : ThreadSection.Chats} style={{ marginTop: '10px' }}>
            <AccordionHeader expandIconPosition="end" className={accordionHeaderStyles.accordionHeader}>
                <Caption1>
                    {isFavorite ? (
                        <FormattedMessage {...ActivitiesResources.favoriteThreadListTitle} />
                    ) : (
                        <FormattedMessage {...ActivitiesResources.regularThreadListTitle} />
                    )}
                </Caption1>
            </AccordionHeader>
            <AccordionPanel style={{ marginLeft: tokens.spacingHorizontalXXS, marginRight: '0px' }}>
                {threads.map(thread => {
                    return (
                        <ThreadItem
                            key={thread.id}
                            thread={thread}
                            selectThread={selectThread}
                            deleteThread={deleteThread}
                            isActive={activeThreadId === thread.id}
                            isThreadUnread={unreadThreadIds.has(thread.id)}
                            ref={(el: HTMLDivElement) => assignThreadItemDivRef(thread.id, el)}
                            favorite={isFavorite}
                            updateThreadFavoriteProperty={updateThreadFavoriteProperty}
                        />
                    );
                })}
                {threadsThatHaveFavoritePropertyChanged.map(thread => {
                    return (
                        <ThreadItem
                            key={thread.id}
                            thread={thread}
                            selectThread={selectThread}
                            deleteThread={deleteThread}
                            isActive={activeThreadId === thread.id}
                            isThreadUnread={unreadThreadIds.has(thread.id)}
                            ref={(el: HTMLDivElement) => assignThreadItemDivRef(thread.id, el)}
                            favorite={isFavorite}
                            updateThreadFavoriteProperty={updateThreadFavoriteProperty}
                        />
                    );
                })}
                {children}
            </AccordionPanel>
        </AccordionItem>
    );
};

const Loader = () => {
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
};

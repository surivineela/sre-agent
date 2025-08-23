import { Button } from '@fluentui/react-button';
import { mergeClasses, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { Dialog, DialogTrigger } from '@fluentui/react-dialog';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular, SearchRegular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { tokens } from '@fluentui/react-theme';
import { ForwardedRef, forwardRef, useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import Fade from '../Components/Fade';
import ThreadFilters from '../Components/ThreadFilters';
import ThreadItem from '../Components/ThreadItem';
import ThreadSearchDialog from '../Components/ThreadSearchDialog';
import { IThreadsMenuProps, ThreadMenuHandle } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { useThreadsMenu } from '../Hooks/useThreadsMenu';
import { getExpandCollapseButtonStyles, skeletonStyle, useThreadMenuStyle } from '../Styles/Activities.styles';

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('left');

export const ThreadsMenu = forwardRef<ThreadMenuHandle, IThreadsMenuProps>(
    (props: IThreadsMenuProps, ref: ForwardedRef<ThreadMenuHandle>) => {
        const { selectThread, deleteThread, collapsed, setCollapsed } = props;

        const {
            threads,
            threadListDivRef,
            intersectionObserverRef,
            showUnreadOnly,
            setShowUnreadOnly,
            unreadThreadIds,
            onScroll,
            moreThreadsToLoad,
            threadItemDivsRef,
        } = useThreadsMenu(ref);

        const threadMenuStyles = useThreadMenuStyle();
        const { scrollable } = useScrollableComponentStyles();

        const { activeThreadId } = useContext(AgentContext);
        const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
        const { logAmplitudeControlEvent } = useAzPortalContext();

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
                    <Button
                        style={{
                            borderRadius: tokens.borderRadiusLarge,
                            borderColor: tokens.colorNeutralBackground3Selected,
                            maxWidth: 'fit-content',
                            minWidth: 'unset',
                        }}
                        icon={<AddRegular />}
                        onClick={() => onClickNewThread()}
                        aria-label={intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                        disabled={!hasChatPermissions}
                    >
                        {!collapsed && (
                            <Fade visible={true} appear={true} unmountOnExit>
                                <Text wrap={false}>{intl.formatMessage(ActivitiesResources.createThreadButtonText)}</Text>
                            </Fade>
                        )}
                    </Button>
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
                                        threads={threads}
                                        selectThread={selectThread}
                                        activeThreadId={activeThreadId}
                                        excludedSources={[ThreadSource.incident]}
                                    />
                                </Dialog>
                            </div>
                        </Fade>
                    )}
                </div>
                {hasChatPermissions && (
                    <Fade visible={!collapsed} unmountOnExit>
                        <div>
                            <ThreadFilters unreadOnly={showUnreadOnly} setUnreadOnly={setShowUnreadOnly} />
                        </div>
                    </Fade>
                )}
                {hasChatPermissions && (
                    <Fade visible={!collapsed}>
                        <div
                            className={mergeClasses(scrollable, threadMenuStyles.threadListContainer)}
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
                                        ref={(el: HTMLDivElement) => threadItemDivsRef.current.set(thread.id, el)}
                                    />
                                );
                            })}
                            {moreThreadsToLoad && (
                                <Skeleton style={skeletonStyle} ref={intersectionObserverRef}>
                                    <SkeletonItem />
                                </Skeleton>
                            )}
                        </div>
                    </Fade>
                )}
            </div>
        );
    }
);

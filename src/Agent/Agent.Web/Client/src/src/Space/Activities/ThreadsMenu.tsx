import { Button } from '@fluentui/react-button';
import { mergeClasses, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { Dialog, DialogTrigger } from '@fluentui/react-dialog';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular, SearchRegular } from '@fluentui/react-icons';
import { tokens } from '@fluentui/react-theme';
import { ForwardedRef, forwardRef, useContext } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import ThreadFiltersAndIncidentStatus from '../Components/ThreadFiltersAndIncidentStatus';
import ThreadItem from '../Components/ThreadItem';
import ThreadSearchDialog from '../Components/ThreadSearchDialog';
import { IThreadsMenuProps, ThreadMenuHandle } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { useMetrics } from '../Hooks/useMetrics';
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
            threadFilters,
            updateThreadFilters,
            unreadThreadIds,
            oldestThreadModifiedTimestamp,
            onScroll,
            moreThreadsToLoad,
        } = useThreadsMenu(ref);

        const threadMenuStyles = useThreadMenuStyle();
        const { scrollable } = useScrollableComponentStyles();

        const { activeThreadId } = useContext(AgentContext);
        const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);

        const intl = useIntl();

        const { incidentMetrics } = useMetrics(oldestThreadModifiedTimestamp);

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
                            height: 'auto',
                            borderRadius: tokens.borderRadiusLarge,
                            borderColor: tokens.colorNeutralBackground3Selected,
                            maxWidth: 'fit-content',
                            minWidth: 'unset',
                        }}
                        icon={<AddRegular />}
                        onClick={() => selectThread(null)}
                        aria-label={intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                        disabled={!hasChatPermissions}
                    >
                        {collapsed ? null : intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                    </Button>
                    {!collapsed && hasChatPermissions && (
                        <Dialog>
                            <DialogTrigger>
                                <Button
                                    aria-label={intl.formatMessage(SreAgentResources.search)}
                                    icon={<SearchRegular />}
                                    style={{
                                        borderRadius: tokens.borderRadiusLarge,
                                        borderColor: tokens.colorNeutralBackground3Selected,
                                    }}
                                />
                            </DialogTrigger>
                            <ThreadSearchDialog threads={threads} selectThread={selectThread} activeThreadId={activeThreadId} />
                        </Dialog>
                    )}
                </div>
                {!collapsed && hasChatPermissions && (
                    <ThreadFiltersAndIncidentStatus
                        threadFilters={threadFilters}
                        updateThreadFilters={updateThreadFilters}
                        incidentMetrics={incidentMetrics}
                    />
                )}
                {!collapsed && hasChatPermissions && (
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
                                />
                            );
                        })}
                        {moreThreadsToLoad && (
                            <Skeleton style={skeletonStyle} ref={intersectionObserverRef}>
                                <SkeletonItem />
                            </Skeleton>
                        )}
                    </div>
                )}
            </div>
        );
    }
);

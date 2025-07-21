import { Button } from '@fluentui/react-button';
import { Dialog, DialogTrigger } from '@fluentui/react-dialog';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular, SearchRegular } from '@fluentui/react-icons';
import { Radio, RadioGroup } from '@fluentui/react-radio';
import { tokens } from '@fluentui/react-theme';
import { ForwardedRef, forwardRef, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import ThreadSearchDialog from '../Components/ThreadSearchDialog';
import ThreadsList from '../Components/ThreadsList';
import { IThreadsMenuProps, ThreadMenuHandle } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { useMetrics } from '../Hooks/useMetrics';
import { useThreadsMenu } from '../Hooks/useThreadsMenu';
import { getExpandCollapseButtonStyles, useThreadMenuStyle } from '../Styles/Activities.styles';
import IncidentStatusBar from './IncidentStatusBar';

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('left');

export const ThreadsMenu = forwardRef<ThreadMenuHandle, IThreadsMenuProps>(
    (props: IThreadsMenuProps, ref: ForwardedRef<ThreadMenuHandle>) => {
        const { selectThread, deleteThread, threadPollingTriggerId, collapsed, setCollapsed } = props;

        const {
            hasChatPermissions,
            threads,
            isLoadingInitialThreads,
            loadMoreOldThreads,
            hasMoreOldThreads,
            threadListHandleRef,
            threadSource,
            updateThreadSource,
            unreadThreadIds,
            oldestThreadModifiedTimestamp,
        } = useThreadsMenu(threadPollingTriggerId, ref);

        const threadMenuStyles = useThreadMenuStyle();

        const { activeThreadId } = useContext(AgentContext);

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
                    <RadioGroup
                        value={threadSource || ''}
                        onChange={(_e, data) => {
                            updateThreadSource(data.value as ThreadSource);
                        }}
                        layout="horizontal"
                        style={{ flexWrap: 'wrap', padding: '10px' }}
                    >
                        <Radio value={''} label={intl.formatMessage(SreAgentResources.allThreads)} />
                        <Radio value={ThreadSource.incident} label={intl.formatMessage(SreAgentResources.incidents)} />
                    </RadioGroup>
                )}
                {!collapsed && hasChatPermissions && threadSource && <IncidentStatusBar incidentMetrics={incidentMetrics} />}
                {!collapsed && hasChatPermissions && (
                    <ThreadsList
                        ref={threadListHandleRef}
                        threads={threads}
                        isLoadingInitialThreads={isLoadingInitialThreads}
                        selectThread={selectThread}
                        deleteThread={deleteThread}
                        hasMoreOldThreads={hasMoreOldThreads}
                        loadMoreOldThreads={loadMoreOldThreads}
                        activeThreadId={activeThreadId}
                        unreadThreadIds={unreadThreadIds}
                    />
                )}
            </div>
        );
    }
);

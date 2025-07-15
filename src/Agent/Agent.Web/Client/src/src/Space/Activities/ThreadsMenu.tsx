import { Button, InputOnChangeData, Radio, RadioGroup, SearchBox, SearchBoxChangeEvent, tokens } from '@fluentui/react-components';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular } from '@fluentui/react-icons';
import { ForwardedRef, forwardRef, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import ThreadsList from '../Components/ThreadsList';
import { IThreadsMenuProps, ThreadListHandle } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { useMetrics } from '../Hooks/useMetrics';
import { useThreadsMenu } from '../Hooks/useThreadsMenu';
import { getExpandCollapseButtonStyles, searchBoxStyle } from '../Styles/Activities.styles';
import ActivitiesStatusBar from './ActionsStatusBar';
import IncidentStatusBar from './IncidentStatusBar';
const expandCollapseButtonStyles = getExpandCollapseButtonStyles('left');

export const ThreadsMenu = forwardRef<ThreadListHandle, IThreadsMenuProps>(
    (props: IThreadsMenuProps, ref: ForwardedRef<ThreadListHandle>) => {
        const { selectThread, deleteThread, threadPollingTriggerId, collapsed, setCollapsed } = props;

        const {
            hasChatPermissions,
            threads,
            isLoadingInitialThreads,
            loadMoreOldThreads,
            hasMoreOldThreads,
            threadsListDivRef,
            onThreadSearchTextChange,
            threadSource,
            setThreadSource,
            oldestThreadModifiedTimestamp,
            setOldestThreadModifiedTimestamp,
            setThreadSeverity,
            unreadThreadIds,
        } = useThreadsMenu(threadPollingTriggerId, ref);

        const { activeThreadId } = useContext(AgentContext);

        const intl = useIntl();

        const { incidentMetrics } = useMetrics(oldestThreadModifiedTimestamp);

        return (
            <div style={{ display: 'contents' }}>
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
                <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    <Button
                        style={{
                            height: 'auto',
                            borderRadius: tokens.borderRadiusLarge,
                            borderColor: tokens.colorNeutralBackground3Selected,
                            maxWidth: 'fit-content',
                            marginLeft: '10px',
                            marginTop: '-10px',
                            marginRight: '10px',
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
                        <SearchBox
                            style={searchBoxStyle}
                            placeholder={intl.formatMessage(SreAgentResources.search)}
                            onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => onThreadSearchTextChange(data.value ?? '')}
                        />
                    )}
                </div>
                {!collapsed && hasChatPermissions && (
                    <RadioGroup
                        value={threadSource || ''}
                        onChange={(_e, data) => {
                            setThreadSeverity(undefined);
                            setThreadSource(data.value as ThreadSource);
                        }}
                        layout="horizontal"
                        style={{ flexWrap: 'wrap' }}
                    >
                        <Radio value={''} label={intl.formatMessage(SreAgentResources.allThreads)} />
                        <Radio value={ThreadSource.incident} label={intl.formatMessage(SreAgentResources.incidents)} />
                    </RadioGroup>
                )}
                {collapsed || !hasChatPermissions ? null : !threadSource ? (
                    <ActivitiesStatusBar selectedTime={oldestThreadModifiedTimestamp} setSelectedTime={setOldestThreadModifiedTimestamp} />
                ) : (
                    <IncidentStatusBar
                        selectedTime={oldestThreadModifiedTimestamp}
                        setSelectedTime={setOldestThreadModifiedTimestamp}
                        incidentMetrics={incidentMetrics}
                    />
                )}
                {!collapsed && hasChatPermissions && (
                    <ThreadsList
                        ref={threadsListDivRef}
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

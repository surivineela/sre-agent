import { Button, InputOnChangeData, Radio, RadioGroup, SearchBox, SearchBoxChangeEvent, tokens } from '@fluentui/react-components';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular } from '@fluentui/react-icons';
import { ForwardedRef, forwardRef, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSeverity } from '../../Common/Clients/ThreadClient';
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
        const { selectThread, threadPollingTriggerId, collapsed, setCollapsed } = props;

        const {
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

        const [isCriticalClicked, setIsCriticalClicked] = useState<boolean>(false);
        const [isWarningClicked, setIsWarningClicked] = useState<boolean>(false);

        const handleCriticalClick = useCallback(() => {
            setIsCriticalClicked(prev => {
                const next = !prev;
                setIsWarningClicked(false);
                setThreadSeverity(next ? ThreadSeverity.Critical : undefined);
                return next;
            });
        }, []);

        const handleWarningClick = useCallback(() => {
            setIsWarningClicked(prev => {
                const next = !prev;
                setIsCriticalClicked(false);
                setThreadSeverity(next ? ThreadSeverity.Warning : undefined);
                return next;
            });
        }, []);

        const { actionSeverityMetrics, incidentMetrics, actionStatusMetrics } = useMetrics(oldestThreadModifiedTimestamp);

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
                    >
                        {collapsed ? null : intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                    </Button>
                    {!collapsed && (
                        <SearchBox
                            style={searchBoxStyle}
                            placeholder={intl.formatMessage(SreAgentResources.search)}
                            onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => onThreadSearchTextChange(data.value ?? '')}
                        />
                    )}
                </div>
                {!collapsed && (
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
                {collapsed ? null : !threadSource ? (
                    <ActivitiesStatusBar
                        selectedTime={oldestThreadModifiedTimestamp}
                        setSelectedTime={setOldestThreadModifiedTimestamp}
                        setThreadSeverity={setThreadSeverity}
                        actionSeverityMetrics={actionSeverityMetrics}
                        actionStatusMetrics={actionStatusMetrics}
                        isWarningClicked={isWarningClicked}
                        onWarningClick={handleWarningClick}
                        isCriticalClicked={isCriticalClicked}
                        onCriticalClick={handleCriticalClick}
                    />
                ) : (
                    <IncidentStatusBar
                        selectedTime={oldestThreadModifiedTimestamp}
                        setSelectedTime={setOldestThreadModifiedTimestamp}
                        incidentMetrics={incidentMetrics}
                    />
                )}
                {!collapsed && (
                    <ThreadsList
                        ref={threadsListDivRef}
                        threads={threads}
                        isLoadingInitialThreads={isLoadingInitialThreads}
                        selectThread={selectThread}
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

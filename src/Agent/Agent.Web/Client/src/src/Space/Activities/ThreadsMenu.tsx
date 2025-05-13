import {
    Button,
    InputOnChangeData,
    mergeClasses,
    Radio,
    RadioGroup,
    SearchBox,
    SearchBoxChangeEvent,
    tokens,
} from '@fluentui/react-components';
import { AddRegular, PanelLeftContractRegular, PanelLeftExpandRegular } from '@fluentui/react-icons';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { Text } from '@fluentui/react/lib/Text';
import debounce from 'lodash/debounce';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentStatus, Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IThreadsMenuProps } from '../Contracts/Activities';
import { useMetrics } from '../Hooks/useMetrics';
import { getExpandCollapseButtonStyles, searchBoxStyle, shimmerStyle, useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import ActivitiesStatusBar from './ActionsStatusBar';
import { AgentContext } from './Activities.ReactView';
import IncidentStatusBar from './IncidentStatusBar';
import { SelectedTimes } from './TimeDropdown';

enum ThreadMode {
    threads = 'threads',
    incidents = 'incidents',
}

export enum ThreadActionFilter {
    all = 'all',
    warning = 'warning',
    critical = 'critical',
}
const expandCollapseButtonStyles = getExpandCollapseButtonStyles('left');

export const ThreadsMenu: FC<IThreadsMenuProps> = (props: IThreadsMenuProps) => {
    const { threads, selectThread, collapsed, setCollapsed } = props;
    const { threadsInitialized, activeThreadId } = useContext(AgentContext);
    const [searchString, setSearchString] = useState<string>();
    const [selectedTime, setSelectedTime] = useState<SelectedTimes>(SelectedTimes.OneDay);
    const [threadMode, setThreadMode] = useState<ThreadMode>(ThreadMode.threads);
    const [threadActionFilter, setThreadActionFilter] = useState<ThreadActionFilter>(ThreadActionFilter.all);
    const intl = useIntl();

    const [isCriticalClicked, setIsCriticalClicked] = useState<boolean>(false);
    const [isWarningClicked, setIsWarningClicked] = useState<boolean>(false);

    const handleCriticalClick = useCallback(() => {
        setIsCriticalClicked(prev => {
            const next = !prev;
            setIsWarningClicked(false);
            setThreadActionFilter(next ? ThreadActionFilter.critical : ThreadActionFilter.all);
            return next;
        });
    }, [setIsCriticalClicked, setThreadActionFilter, setIsWarningClicked]);

    const handleWarningClick = useCallback(() => {
        setIsWarningClicked(prev => {
            const next = !prev;
            setIsCriticalClicked(false);
            setThreadActionFilter(next ? ThreadActionFilter.warning : ThreadActionFilter.all);
            return next;
        });
    }, [setIsCriticalClicked, setThreadActionFilter, setIsWarningClicked]);

    const { actionSeverityMetrics, incidentMetrics, actionStatusMetrics } = useMetrics(selectedTime);

    const filteredThreads = useMemo(() => {
        let newThreads = threads;
        if (threadMode === ThreadMode.incidents) {
            newThreads = threads.filter(thread => thread.source === ThreadSource.incident);
            if (selectedTime) {
                const filterByDays = (time: string) => {
                    const days = time === SelectedTimes.OneDay ? 1 : time === SelectedTimes.SevenDays ? 7 : 30;
                    const now = new Date();
                    const cutoff = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
                    return newThreads.filter(item => new Date(item.modifiedTimestamp) > cutoff);
                };
                newThreads = filterByDays(selectedTime);
            }
        }

        if (selectedTime) {
            const filterByDays = (time: string) => {
                const days = time === SelectedTimes.OneDay ? 1 : time === SelectedTimes.SevenDays ? 7 : 30;
                const now = new Date();
                const cutoff = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
                return newThreads.filter(item => new Date(item.modifiedTimestamp) > cutoff);
            };
            newThreads = filterByDays(selectedTime);
        }

        if (threadActionFilter === ThreadActionFilter.critical) {
            newThreads = threads.filter(thread => !!thread.status?.actionsStatus?.hasCriticalActions);
        }
        if (threadActionFilter == ThreadActionFilter.warning) {
            newThreads = threads.filter(thread => !!thread.status?.actionsStatus?.hasWarningActions);
        }

        if (searchString) {
            return newThreads.filter(thread => thread.title.toLowerCase().includes(searchString.toLowerCase()));
        } else {
            return newThreads;
        }
    }, [threadMode, searchString, threads, selectedTime, threadActionFilter]);

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
                    disabled={!threadsInitialized}
                    onClick={() => selectThread(null)}
                    aria-label={intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                >
                    {collapsed ? null : intl.formatMessage(ActivitiesResources.createThreadButtonText)}
                </Button>
                {!collapsed && (
                    <SearchBox
                        style={searchBoxStyle}
                        disabled={!threadsInitialized}
                        placeholder={intl.formatMessage(SreAgentResources.search)}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchString(data.value ?? ''))}
                    />
                )}
            </div>
            {!collapsed && (
                <RadioGroup
                    value={threadMode}
                    onChange={(_e, data) => {
                        setThreadActionFilter(ThreadActionFilter.all);
                        setThreadMode(data.value as ThreadMode);
                    }}
                    layout="horizontal"
                    disabled={!threadsInitialized}
                    style={{ flexWrap: 'wrap' }}
                >
                    <Radio value={ThreadMode.threads} label={intl.formatMessage(SreAgentResources.allThreads)} />
                    <Radio value={ThreadMode.incidents} label={intl.formatMessage(SreAgentResources.incidents)} />
                </RadioGroup>
            )}
            {collapsed ? null : threadMode === ThreadMode.threads ? (
                <ActivitiesStatusBar
                    selectedTime={selectedTime}
                    setSelectedTime={setSelectedTime}
                    setThreadActionFilter={setThreadActionFilter}
                    actionSeverityMetrics={actionSeverityMetrics}
                    actionStatusMetrics={actionStatusMetrics}
                    isWarningClicked={isWarningClicked}
                    onWarningClick={handleWarningClick}
                    isCriticalClicked={isCriticalClicked}
                    onCriticalClick={handleCriticalClick}
                />
            ) : (
                <IncidentStatusBar selectedTime={selectedTime} setSelectedTime={setSelectedTime} incidentMetrics={incidentMetrics} />
            )}
            {!collapsed && (
                <Shimmer isDataLoaded={threadsInitialized} style={shimmerStyle}>
                    <ThreadsList threads={filteredThreads} selectThread={selectThread} activeThreadId={activeThreadId} />
                </Shimmer>
            )}
        </div>
    );
};

const ThreadsList = memo(
    ({
        threads,
        activeThreadId,
        selectThread,
    }: {
        threads: Thread[];
        activeThreadId: string;
        selectThread: (thread: Thread | null) => void;
    }) => {
        const ThreadMenuStyles = useThreadMenuStyle();
        const { scrollable } = useScrollableComponentStyles();

        return (
            <div className={mergeClasses(scrollable, ThreadMenuStyles.threadList)} role="tree">
                {threads.map(thread => {
                    return (
                        <ThreadItem key={thread.id} thread={thread} selectThread={selectThread} isActive={activeThreadId === thread.id} />
                    );
                })}
            </div>
        );
    }
);

const ThreadItem = memo(
    ({ thread, selectThread, isActive }: { thread: Thread; selectThread: (thread: Thread | null) => void; isActive: boolean }) => {
        const ThreadMenuStyles = useThreadMenuStyle();
        const styles = useActionsStatusBarStyles();
        const intl = useIntl();

        const getIncidentStatus = (thread: Thread) => {
            if (thread.status?.incidentStatus?.status) {
                switch (thread.status?.incidentStatus?.status) {
                    case IncidentStatus.acknowledged:
                        return intl.formatMessage(SreAgentResources.acknowledged);
                    case IncidentStatus.triggered:
                        return intl.formatMessage(SreAgentResources.triggered);
                    case IncidentStatus.mitigated:
                        return intl.formatMessage(SreAgentResources.mitigated);
                    case IncidentStatus.closed:
                        return intl.formatMessage(SreAgentResources.closed);
                    case IncidentStatus.resolved:
                        return intl.formatMessage(SreAgentResources.resolved);
                }
            }
            return intl.formatMessage(SreAgentResources.active);
        };

        return (
            <div
                onClick={() => selectThread(thread)}
                onKeyDown={e => {
                    if (e.key.toLowerCase() === 'enter') {
                        selectThread(thread);
                        e.stopPropagation();
                    }
                }}
                id={thread.id}
                tabIndex={0}
                role="treeitem"
                className={mergeStyles(ThreadMenuStyles.threadItem, isActive ? ThreadMenuStyles.activeThreadItem : undefined)}
            >
                {isActive && (<div className={ThreadMenuStyles.borderIndicator}/>)}
                <div className={ThreadMenuStyles.content}>
                    <div className={styles.threadTitleWithAction}>
                        <Text as="div" variant="medium" nowrap block>
                            {thread.title}
                        </Text>
                    </div>
                    {thread.source === ThreadSource.incident ? (
                        <div className={styles.subtitleContainer}>
                            <span className={styles.statusPill}>{getIncidentStatus(thread)}</span>
                            <Text className={styles.subtitle} as="div" variant="small" nowrap block>
                                {thread.lastMessage?.text}
                            </Text>
                        </div>
                    ) : (
                        <Text as="div" variant="small" nowrap block>
                            {thread.lastMessage?.text}
                        </Text>
                    )}
                </div>
            </div>
        );
    }
);

ThreadsList.displayName = 'ThreadsList';
ThreadItem.displayName = 'ThreadItem';

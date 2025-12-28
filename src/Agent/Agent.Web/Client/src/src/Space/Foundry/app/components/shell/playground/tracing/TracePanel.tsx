import { MessageBar, MessageBarType } from '@fluentui/react';
import { Link, Spinner, Text } from '@fluentui/react-components';
import { Info16Regular, Timer12Regular } from '@fluentui/react-icons';
import type { JSX } from 'react';
import { useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentDocument } from '../../../../../../../Common/Contracts/Azure/IncidentHandler';
import { Thread, ThreadSource } from '../../../../../../../Common/Contracts/DataPlane/Thread';
import { ThreadTraceResources } from '../../../../../../../Strings/SREAgentResources';
import { Dialog } from '../../../../../common/components/src/Dialog/Dialog';
import { MetricsBadge } from '../../../../../common/components/src/MetricsBadge/MetricsBadge';
import { TraceBadge } from '../../../../../common/components/src/TraceBadge/TraceBadge';
import { TraceTitleActions } from '../../../../../packages/components/tracing/src/traceTitleActions/TraceTitleActions';
import { TreeView } from '../../../../../packages/components/tracing/src/treeView/TreeView';
import {
    formatStartTime,
    getBadgeType,
    getSpanDurationSeconds,
    getSpanTitle,
} from '../../../../../packages/components/tracing/src/utils/traceHelper';
import { AgentHandoffTraceDetails } from './TraceDetails/AgentHandoffTraceDetails';
import { AgentResponseTraceDetails } from './TraceDetails/AgentResponseTraceDetails';
import { AgentThinkingTraceDetails } from './TraceDetails/AgentThinkingTraceDetails';
import { AgentTraceDetails } from './TraceDetails/AgentTraceDetails';
import { IncidentTraceDetails } from './TraceDetails/IncidentTraceDetails';
import { ModelGenerationTraceDetails } from './TraceDetails/ModelGenerationTraceDetails';
import { ToolTraceDetails } from './TraceDetails/ToolTraceDetails';
import { UserTraceDetails } from './TraceDetails/UserTraceDetails';
import { useTracePanelStyles } from './TracePanel.Styles';
import { useTracePanel } from './useTracePanel';

const spinnerStyle = { margin: 'auto', height: '100px', width: '100px' };
export interface ITracePanelProps {
    appInsightsAppId: string;
    thread: Thread;
    isOpen: boolean;
    onClose: () => void;
    focusRestorationRef: React.RefObject<HTMLElement>;
    incident?: IncidentDocument;
}

export function TracePanel({ isOpen, onClose, focusRestorationRef, appInsightsAppId, thread, incident }: ITracePanelProps): JSX.Element {
    const intl = useIntl();
    const styles = useTracePanelStyles();
    const { spans, loading, loadingFailure, parseErrorMessage, noDataMessage, warnings, errors } = useTracePanel(
        appInsightsAppId,
        thread.id,
        thread.source === ThreadSource.incident
    );
    const [selectedSpanId, setSelectedSpanId] = useState<string | undefined>(spans.at(0)?.context?.span_id);
    const selectedSpan = useMemo(() => spans.find(s => s.context?.span_id === selectedSpanId) ?? spans.at(0), [spans, selectedSpanId]);
    const rootSpan = useMemo(() => spans.find(span => span.parent_id === undefined), [spans]);
    const selectedSpanBadgeType = useMemo(() => (selectedSpan ? getBadgeType(selectedSpan) : undefined), [selectedSpan]);
    const selectedSpanStartTime = useMemo(() => formatStartTime(selectedSpan?.start_time.toISOString()), [selectedSpan?.start_time]);
    const seconds = useMemo(() => (selectedSpan ? getSpanDurationSeconds(selectedSpan) : undefined), [selectedSpan]);

    const [showWarnings, setShowWarnings] = useState(false);

    const description = useMemo(() => {
        const handlerName = thread.incidentDetails?.filterId;
        const incidentStatus = thread.status?.incidentStatus?.status;

        if (!handlerName && !incidentStatus) {
            return undefined;
        }
        return (
            <div className={styles.traceDescription}>
                {handlerName && (
                    <div className={styles.traceDescriptionField}>
                        {intl.formatMessage(ThreadTraceResources.incidentResponsePlan)}
                        <div>{handlerName}</div>
                    </div>
                )}
                {incidentStatus && (
                    <div className={styles.traceDescriptionField}>
                        {intl.formatMessage(ThreadTraceResources.incidentStatus)}
                        <div className={styles.traceDescriptionStatus}>{incidentStatus}</div>
                    </div>
                )}
            </div>
        );
    }, [thread, intl, styles]);

    return (
        <Dialog
            focusRestorationRef={focusRestorationRef}
            isOpen={isOpen}
            onClose={onClose}
            showCloseButton={true}
            size="large"
            title={thread.title}
            titleActions={<TraceTitleActions rootSpan={rootSpan} threadId={thread.id} />}
            dialogTitleProps={{ className: styles.dialogTitle }}
            dialogBodyProps={{ className: styles.dialogBody }}
            description={description}
        >
            {parseErrorMessage && (
                <MessageBar messageBarType={MessageBarType.warning} className={styles.messageBar}>
                    {parseErrorMessage}
                    <Link style={{ marginLeft: '8px' }} onClick={() => setShowWarnings(!showWarnings)}>
                        {showWarnings ? intl.formatMessage(ThreadTraceResources.hide) : intl.formatMessage(ThreadTraceResources.show)}
                    </Link>
                </MessageBar>
            )}
            {showWarnings ? (
                <pre>{JSON.stringify({ warnings, errors }, null, 2)}</pre>
            ) : loading ? (
                <Spinner size="huge" style={spinnerStyle} spinner={{ style: spinnerStyle }} />
            ) : loadingFailure || noDataMessage ? (
                <MessageBar messageBarType={MessageBarType.error}>{loadingFailure || noDataMessage}</MessageBar>
            ) : (
                <div className={styles.traceContainer} data-testid="trace-container" data-tree-layer="container">
                    <div className={styles.leftPane} data-testid="left-pane">
                        <TreeView onSpanIdSelect={setSelectedSpanId} selectedSpanId={selectedSpanId} spans={spans} thread={thread} />
                    </div>
                    <div className={styles.rightPane} data-testid="right-pane">
                        {selectedSpan ? (
                            <div className={styles.rightPaneInner}>
                                <div className={styles.rightPaneHeaderWrapper}>
                                    {selectedSpanBadgeType ? <TraceBadge type={selectedSpanBadgeType} /> : null}
                                    <div className={styles.rightPaneHeader}>
                                        <div className={styles.rightPaneHeaderText}>{getSpanTitle(selectedSpan, thread)}</div>
                                        {selectedSpan.start_time && <div>{selectedSpanStartTime}</div>}
                                    </div>
                                    {seconds !== undefined && seconds !== 0 ? (
                                        <MetricsBadge
                                            className={styles.rightPaneHeaderDuration}
                                            label={intl.formatMessage(ThreadTraceResources.secondsWithLabel, { seconds })}
                                            icon={<Timer12Regular aria-hidden="true" />}
                                        />
                                    ) : null}
                                </div>

                                <div className={styles.rightPaneContent}>
                                    {selectedSpan.kind === 'Incident' && (
                                        <IncidentTraceDetails span={selectedSpan} incident={incident} thread={thread} />
                                    )}
                                    {(selectedSpan.kind === 'Agent' || selectedSpan.kind === 'SubAgent') && (
                                        <AgentTraceDetails span={selectedSpan} />
                                    )}
                                    {selectedSpan.kind === 'Tool' && <ToolTraceDetails span={selectedSpan} />}
                                    {selectedSpan.kind === 'UserMessage' && <UserTraceDetails span={selectedSpan} />}
                                    {selectedSpan.kind === 'AgentHandoff' && <AgentHandoffTraceDetails span={selectedSpan} />}
                                    {selectedSpan.kind === 'AgentHandback' && (
                                        <AgentHandoffTraceDetails span={selectedSpan} isHandback={true} />
                                    )}
                                    {selectedSpan.kind === 'ModelGeneration' && <ModelGenerationTraceDetails span={selectedSpan} />}
                                    {selectedSpan.kind === 'AgentResponse' && <AgentResponseTraceDetails span={selectedSpan} />}
                                    {selectedSpan.kind === 'AgentThinking' && <AgentThinkingTraceDetails span={selectedSpan} />}
                                </div>
                            </div>
                        ) : (
                            <div className={styles.rightPaneInner}>
                                <div className={styles.rightPaneHeaderWrapper}>
                                    <Info16Regular aria-hidden={true} />
                                    <div className={styles.rightPaneHeader}>
                                        <div className={styles.rightPaneHeaderText}>
                                            {intl.formatMessage(ThreadTraceResources.traceInfoTitle)}
                                        </div>
                                    </div>
                                </div>
                                <div className={styles.rightPaneContent}>
                                    <div className={styles.rightPaneSection}>
                                        <Text>{intl.formatMessage(ThreadTraceResources.traceInfoDescription)}</Text>
                                    </div>
                                    <div className={styles.rightPaneSection}>
                                        <div className={styles.rightPaneSectionHeader}>
                                            <div className={styles.rightPaneSectionHeaderText}>
                                                {intl.formatMessage(ThreadTraceResources.traceDataAvailable)}
                                            </div>
                                        </div>
                                        <div className={styles.rightPaneSubsectionsContainer}>
                                            <Text style={{ whiteSpace: 'pre-line' }}>
                                                {intl.formatMessage(ThreadTraceResources.traceDataList)}
                                            </Text>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            )}
        </Dialog>
    );
}

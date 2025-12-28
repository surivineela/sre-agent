import { ArrowTurnDownRightRegular, ArrowTurnUpLeftRegular, Lightbulb20Regular } from '@fluentui/react-icons';
import { FC, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

interface AgentHandoffTraceDetailsProps {
    span: ISpan;
    isHandback?: boolean;
}

export const AgentHandoffTraceDetails: FC<AgentHandoffTraceDetailsProps> = ({ span, isHandback }) => {
    const intl = useIntl();
    const styles = useTracePanelStyles();
    const [reasoningExpanded, setReasoningExpanded] = useState(false);

    const { fromAgent, toAgent, handoffReasoning } = useMemo(
        () => ({
            fromAgent: span.attributes?.fromAgent ?? '-',
            toAgent: span.attributes?.toAgent ?? '-',
            handoffReasoning: span.attributes?.handoffReasoning,
        }),
        [span.attributes?.fromAgent, span.attributes?.toAgent, span.attributes?.handoffReasoning]
    );

    const { icon, header } = useMemo(
        () => ({
            icon: isHandback ? <ArrowTurnUpLeftRegular aria-hidden={true} /> : <ArrowTurnDownRightRegular aria-hidden={true} />,
            header: intl.formatMessage(ThreadTraceResources.agentHandoff),
        }),
        [intl, isHandback]
    );

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    {icon}
                    <div className={styles.rightPaneSectionHeaderText}>{header}</div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.handoffFromAgent)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{fromAgent}</div>
                    </div>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.handoffToAgent)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{toAgent}</div>
                    </div>
                </div>
            </div>
            {handoffReasoning && (
                <div className={styles.rightPaneSection}>
                    <div className={styles.rightPaneSectionHeader}>
                        <Lightbulb20Regular aria-hidden={true} />
                        <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.handoffReasoning)}</div>
                        <ExpandCollapseButton isExpanded={reasoningExpanded} setIsExpanded={setReasoningExpanded} />
                    </div>

                    <div className={styles.rightPaneSubsectionsContainer}>
                        <div className={styles.rightPaneSubsection}>
                            <pre className={reasoningExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                                {handoffReasoning}
                            </pre>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};

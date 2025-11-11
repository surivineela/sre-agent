import { ArrowTurnDownRightRegular, ArrowTurnUpLeftRegular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';

interface AgentHandoffTraceDetailsProps {
    span: ISpan;
    isHandback?: boolean;
}

export const AgentHandoffTraceDetails: FC<AgentHandoffTraceDetailsProps> = ({ span, isHandback }) => {
    const intl = useIntl();
    const styles = useTracePanelStyles();

    const { fromAgent, toAgent } = useMemo(
        () => ({
            fromAgent: span.attributes?.fromAgent ?? '-',
            toAgent: span.attributes?.toAgent ?? '-',
        }),
        [span.attributes?.fromAgent, span.attributes?.toAgent]
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
        </>
    );
};

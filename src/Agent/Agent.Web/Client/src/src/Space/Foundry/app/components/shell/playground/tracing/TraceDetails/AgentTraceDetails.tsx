import { AgentsRegular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
export interface AgentTraceDetailsProps {
    span: ISpan;
}

export const AgentTraceDetails: FC<AgentTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const styles = useTracePanelStyles();
    const { header, agentName } = useMemo(
        () => ({
            header: intl.formatMessage(span.kind === 'Agent' ? ThreadTraceResources.agentInvoked : ThreadTraceResources.subAgentInvoked),
            agentName: span.attributes?.agentName ?? '',
        }),
        [span, intl]
    );

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <AgentsRegular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{header}</div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.agentName)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{agentName}</div>
                    </div>
                </div>
            </div>
        </>
    );
};

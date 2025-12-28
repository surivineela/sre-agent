import { Chat20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

interface AgentResponseTraceDetailsProps {
    span: ISpan;
}

export const AgentResponseTraceDetails: FC<AgentResponseTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [responseExpanded, setResponseExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const message = useMemo(() => {
        if (!span.attributes?.message) {
            return '-';
        }
        try {
            const messageJson = JSON.parse(span.attributes.message);
            return JSON.stringify(messageJson, null, 2);
        } catch (e) {
            return span.attributes?.message;
        }
    }, [span]);
    useEffect(() => {
        setResponseExpanded(false);
    }, [span]);

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Chat20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.responseToUser)}</div>
                    <ExpandCollapseButton isExpanded={responseExpanded} setIsExpanded={setResponseExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>
                            {intl.formatMessage(ThreadTraceResources.messageVisibleToUser)}
                        </div>
                        <pre className={responseExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {message}
                        </pre>
                    </div>
                </div>
            </div>
        </>
    );
};

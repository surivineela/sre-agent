import { Chat20Regular, Wrench20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

const getDetailsFromSpan = (span: ISpan) => {
    return {
        description: span.attributes?.toolDescription ?? '-',
        output: span.attributes?.toolOutput ?? '-',
    };
};

interface ToolTraceDetailsProps {
    span: ISpan;
}

export const ToolTraceDetails: FC<ToolTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [toolDetailsExpanded, setToolDetailsExpanded] = useState(false);
    const [outputExpanded, setOutputExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const { description, output } = useMemo(() => getDetailsFromSpan(span), [span]);
    useEffect(() => {
        setToolDetailsExpanded(false);
        setOutputExpanded(false);
    }, [span]);

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Wrench20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.toolDetails)}</div>
                    <ExpandCollapseButton isExpanded={toolDetailsExpanded} setIsExpanded={setToolDetailsExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.description)}</div>
                        <div className={toolDetailsExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {description}
                        </div>
                    </div>
                </div>
            </div>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Chat20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.output)}</div>
                    <ExpandCollapseButton isExpanded={outputExpanded} setIsExpanded={setOutputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>
                            {intl.formatMessage(ThreadTraceResources.toolAndSubagentActivitySentToUser)}
                        </div>
                        <pre className={outputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {output}
                        </pre>
                    </div>
                </div>
            </div>
        </>
    );
};

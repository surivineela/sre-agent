import { ArrowEnter20Regular, ArrowExit20Regular, Wrench20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

const formatToolInput = (input?: string): string => {
    if (!input || input === '-') return '-';
    try {
        // Try to parse and pretty-print JSON
        const parsed = JSON.parse(input);
        return JSON.stringify(parsed, null, 2);
    } catch {
        return input;
    }
};

const getDetailsFromSpan = (span: ISpan) => {
    return {
        description: span.attributes?.toolDescription ?? '-',
        input: formatToolInput(span.attributes?.toolInput),
        output: span.attributes?.toolOutput ?? '-',
    };
};

interface ToolTraceDetailsProps {
    span: ISpan;
}

export const ToolTraceDetails: FC<ToolTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [toolDetailsExpanded, setToolDetailsExpanded] = useState(false);
    const [inputExpanded, setInputExpanded] = useState(false);
    const [outputExpanded, setOutputExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const { description, input, output } = useMemo(() => getDetailsFromSpan(span), [span]);
    useEffect(() => {
        setToolDetailsExpanded(false);
        setInputExpanded(false);
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
                    <ArrowEnter20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.input)}</div>
                    <ExpandCollapseButton isExpanded={inputExpanded} setIsExpanded={setInputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>
                            {intl.formatMessage(ThreadTraceResources.toolInputArguments)}
                        </div>
                        <pre className={inputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {input}
                        </pre>
                    </div>
                </div>
            </div>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <ArrowExit20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.output)}</div>
                    <ExpandCollapseButton isExpanded={outputExpanded} setIsExpanded={setOutputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.toolOutputResult)}</div>
                        <pre className={outputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {output}
                        </pre>
                    </div>
                </div>
            </div>
        </>
    );
};

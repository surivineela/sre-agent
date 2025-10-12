import { BrainCircuit20Regular, Chat20Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

const getDetailsFromSpan = (span: ISpan) => {
    return {
        modelName: span.usage_info?.modelName ?? '-',
        modelTemperature: span.usage_info?.temperature ?? '-',
        input: span.usage_info?.model_input ? JSON.stringify(span.usage_info.model_input, null, 2) : '-',
        output: span.usage_info?.model_output ? JSON.stringify(span.usage_info.model_output, null, 2) : '-',
    };
};

export interface ModelGenerationTraceDetailsProps {
    span: ISpan;
}

export const ModelGenerationTraceDetails: FC<ModelGenerationTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [inputExpanded, setInputExpanded] = useState(false);
    const [outputExpanded, setOutputExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const { modelName, modelTemperature, input, output } = useMemo(() => getDetailsFromSpan(span), [span]);
    useEffect(() => {
        setInputExpanded(false);
        setOutputExpanded(false);
    }, [span]);

    return (
        <>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <BrainCircuit20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.modelDetails)}</div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.modelName)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{modelName}</div>
                    </div>
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <div className={styles.rightPaneSubsectionHeader}>{intl.formatMessage(ThreadTraceResources.modelTemperature)}</div>
                        <div className={styles.rightPaneSubsectionBodyExpanded}>{modelTemperature}</div>
                    </div>
                </div>
            </div>
            <div className={styles.rightPaneSection}>
                <div className={styles.rightPaneSectionHeader}>
                    <Chat20Regular aria-hidden={true} />
                    <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.input)}</div>
                    <ExpandCollapseButton isExpanded={inputExpanded} setIsExpanded={setInputExpanded} />
                </div>

                <div className={styles.rightPaneSubsectionsContainer}>
                    <div className={styles.rightPaneSubsection}>
                        <pre className={inputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {input}
                        </pre>
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
                        <pre className={outputExpanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody}>
                            {output}
                        </pre>
                    </div>
                </div>
            </div>
        </>
    );
};

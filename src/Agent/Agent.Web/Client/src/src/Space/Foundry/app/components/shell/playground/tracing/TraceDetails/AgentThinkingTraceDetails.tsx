import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { BrainCircuit20Regular, Clock12Regular } from '@fluentui/react-icons';
import { FC, useEffect, useMemo, useState } from 'react';
import useIntl from 'react-intl/src/components/useIntl';
import { ThreadTraceResources } from '../../../../../../../../Strings/SREAgentResources';
import { Badge } from '../../../../../../common/components/src/Badge/Badge';
import { ISpan } from '../../../../../../packages/components/tracing/src/types/trace';
import { useTracePanelStyles } from '../TracePanel.Styles';
import { ExpandCollapseButton } from './Common/ExpandCollapseButton';

interface AgentThinkingTraceDetailsProps {
    span: ISpan;
}

const useThinkingStyles = makeStyles({
    stepsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        paddingTop: tokens.spacingVerticalXS,
    },
    step: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalS,
        position: 'relative',
        '&:not(:last-child)::after': {
            content: '""',
            position: 'absolute',
            left: '4px',
            top: '14px',
            bottom: '-8px',
            width: '1px',
            backgroundColor: tokens.colorNeutralStroke2,
            zIndex: 0,
        },
    },
    bulletPoint: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        backgroundColor: tokens.colorPaletteGrapeForeground2,
        flexShrink: 0,
        marginTop: tokens.spacingVerticalXS,
        zIndex: 1,
    },
    stepContent: {
        flex: 1,
        minWidth: 0,
    },
    stepHeader: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground1,
    },
    stepDetails: {
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        marginTop: tokens.spacingVerticalXXS,
    },
    timestampBadge: {
        marginTop: tokens.spacingVerticalXS,
        color: tokens.colorNeutralForeground3,
        backgroundColor: tokens.colorNeutralBackground4,
    },
    summary: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
});

const parseThinkingMessage = (message: string): { header: string; details: string } => {
    if (message.trimStart().startsWith('**')) {
        const startIndex = message.indexOf('**');
        const endIndex = message.indexOf('**', startIndex + 2);
        if (endIndex !== -1) {
            const header = message.substring(startIndex + 2, endIndex);
            const details = message.substring(endIndex + 2).trim();
            return { header, details };
        }
    }
    return { header: '', details: message };
};

export const AgentThinkingTraceDetails: FC<AgentThinkingTraceDetailsProps> = ({ span }) => {
    const intl = useIntl();
    const [expanded, setExpanded] = useState(false);
    const styles = useTracePanelStyles();
    const thinkingStyles = useThinkingStyles();

    const thinkingSteps = useMemo(() => {
        return span.attributes?.thinkingSteps ?? [];
    }, [span]);

    const parsedSteps = useMemo(() => {
        return thinkingSteps.map(step => ({
            ...parseThinkingMessage(step.message),
            timestamp: step.timestamp,
        }));
    }, [thinkingSteps]);

    useEffect(() => {
        setExpanded(false);
    }, [span]);

    const summary = useMemo(() => {
        const count = thinkingSteps.length;
        if (count === 1 && parsedSteps[0]?.header) {
            return parsedSteps[0].header;
        }
        return `${count} thinking step${count !== 1 ? 's' : ''}`;
    }, [thinkingSteps.length, parsedSteps]);

    return (
        <div className={styles.rightPaneSection}>
            <div className={styles.rightPaneSectionHeader}>
                <BrainCircuit20Regular aria-hidden={true} />
                <div className={styles.rightPaneSectionHeaderText}>{intl.formatMessage(ThreadTraceResources.agentThinking)}</div>
                <ExpandCollapseButton isExpanded={expanded} setIsExpanded={setExpanded} />
            </div>

            <div className={styles.rightPaneSubsectionsContainer}>
                <div className={styles.rightPaneSubsection}>
                    <div className={thinkingStyles.summary}>{summary}</div>
                    <div
                        className={mergeClasses(
                            thinkingStyles.stepsContainer,
                            expanded ? styles.rightPaneSubsectionBodyExpanded : styles.rightPaneSubsectionBody
                        )}
                    >
                        {parsedSteps.map((step, index) => (
                            <div key={index} className={thinkingStyles.step}>
                                <div className={thinkingStyles.bulletPoint} />
                                <div className={thinkingStyles.stepContent}>
                                    {step.header && <div className={thinkingStyles.stepHeader}>{step.header}</div>}
                                    {step.details && <div className={thinkingStyles.stepDetails}>{step.details}</div>}
                                    {step.timestamp && (
                                        <Badge
                                            className={thinkingStyles.timestampBadge}
                                            size="small"
                                            icon={<Clock12Regular aria-hidden="true" />}
                                        >
                                            {new Date(step.timestamp).toLocaleTimeString()}
                                        </Badge>
                                    )}
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

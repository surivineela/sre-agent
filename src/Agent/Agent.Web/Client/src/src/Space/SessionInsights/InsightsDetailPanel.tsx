import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Button,
    makeStyles,
    shorthands,
    Text,
    tokens,
} from '@fluentui/react-components';
import { Open20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { FormattedMessage } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { PrimaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        minHeight: 0,
        ...shorthands.overflow('hidden'),
    },
    header: {
        ...shorthands.padding('16px', '24px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('8px'),
        flexShrink: 0,
    },
    headerTop: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    threadTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    threadMeta: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },
    content: {
        flex: 1,
        minHeight: 0,
        overflowY: 'auto',
        overflowX: 'hidden',
        scrollbarGutter: 'stable',
        ...shorthands.padding('24px'),
        backgroundColor: tokens.colorNeutralBackground1,
    },
    contentInner: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('16px'),
        paddingBottom: '24px', // Extra padding at bottom to ensure content isn't cut off
    },
    introSection: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('20px'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    accordionWrapper: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('20px'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    accordionSectionTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: '16px',
    },
    accordion: {
        backgroundColor: 'transparent',
    },
    accordionItem: {
        backgroundColor: 'transparent',
        ...shorthands.border('none'),
        marginBottom: '16px',
        '&:last-child': {
            marginBottom: 0,
        },
    },
    accordionHeader: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300,
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
        color: tokens.colorNeutralForeground1,
    },
    accordionPanel: {
        ...shorthands.padding('12px', '0', '0', '0'),
        backgroundColor: 'transparent',
        maxHeight: '400px',
        overflowY: 'auto',
        overflowX: 'hidden',
        scrollbarGutter: 'stable',
    },
    insightContent: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground1,
        lineHeight: '1.6',
        '& h1': {
            fontSize: tokens.fontSizeBase500,
            fontWeight: tokens.fontWeightSemibold,
            marginTop: '24px',
            marginBottom: '12px',
        },
        '& h2': {
            fontSize: tokens.fontSizeBase400,
            fontWeight: tokens.fontWeightSemibold,
            marginTop: '20px',
            marginBottom: '10px',
        },
        '& h3': {
            fontSize: tokens.fontSizeBase300,
            fontWeight: tokens.fontWeightSemibold,
            marginTop: '16px',
            marginBottom: '8px',
        },
        '& ul, & ol': {
            marginTop: '8px',
            marginBottom: '8px',
            paddingLeft: '24px',
        },
        '& li': {
            marginBottom: '4px',
        },
        '& p': {
            marginBottom: '12px',
        },
        '& code': {
            backgroundColor: tokens.colorNeutralBackground3,
            ...shorthands.padding('2px', '6px'),
            ...shorthands.borderRadius(tokens.borderRadiusSmall),
            fontFamily: 'monospace',
        },
        '& pre': {
            backgroundColor: tokens.colorNeutralBackground3,
            ...shorthands.padding('12px'),
            ...shorthands.borderRadius(tokens.borderRadiusMedium),
            ...shorthands.overflow('auto'),
            marginBottom: '12px',
        },
    },
    fallbackCard: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('20px'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        ...shorthands.gap('12px'),
        color: tokens.colorNeutralForeground3,
    },
});

interface SessionInsightData {
    threadId: string;
    title: string;
    generatedTimestamp: string;
    insightMarkdown?: string;
    feedback?: any[];
    feedbackCount: number;
    positiveFeedbackCount: number;
    negativeFeedbackCount: number;
}

interface InsightsDetailPanelProps {
    insight: SessionInsightData;
    onInsightsGenerated?: () => void;
}

const InsightsDetailPanel: FC<InsightsDetailPanelProps> = ({ insight }) => {
    const styles = useStyles();
    const navigate = useAgentSiteNavigate();

    const handleGoToThread = useCallback(() => {
        navigate({
            primaryNavItemValue: PrimaryNavItemValues.Threads,
            threadId: insight.threadId,
        });
    }, [navigate, insight.threadId]);

    const sections = useMemo(() => {
        if (!insight.insightMarkdown) {
            return { intro: '', timeline: '', agentPerformance: '' };
        }

        const normalizeNewlines = (value: string) => value.replace(/\r\n/g, '\n');
        const markdown = normalizeNewlines(insight.insightMarkdown);
        const upper = markdown.toUpperCase();

        const timelineMarker = '\nTIMELINE';
        const agentMarker = '\nAGENT PERFORMANCE';

        const timelineIndex = upper.indexOf(timelineMarker);
        const agentIndex = upper.indexOf(agentMarker);

        const introEnd = timelineIndex >= 0 ? timelineIndex : agentIndex >= 0 ? agentIndex : markdown.length;
        const intro = markdown.slice(0, introEnd).trim();

        const timelineBlock =
            timelineIndex >= 0 ? markdown.slice(timelineIndex, agentIndex > timelineIndex ? agentIndex : markdown.length) : '';

        const agentBlock = agentIndex >= 0 ? markdown.slice(agentIndex) : '';

        const cleanSection = (block: string, heading: string) => {
            if (!block) return '';
            const trimmed = block.trim();
            const headingRegex = new RegExp(`^(?:#{1,6}\\s*)?${heading}\\s*\n?`, 'i');
            return trimmed.replace(headingRegex, '').trim();
        };

        return {
            intro,
            timeline: cleanSection(timelineBlock, 'TIMELINE'),
            agentPerformance: cleanSection(agentBlock, 'AGENT PERFORMANCE'),
        };
    }, [insight.insightMarkdown]);

    // Always show accordion if there's any markdown content
    const showAccordion = !!insight.insightMarkdown;

    const formatDate = (dateString: string | undefined) => {
        if (!dateString) return 'Unknown';
        try {
            const date = new Date(dateString);
            return date.toLocaleString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
            });
        } catch {
            return 'Invalid date';
        }
    };

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerTop}>
                    <Text className={styles.threadTitle}>{insight.title}</Text>
                    <Button appearance="primary" icon={<Open20Regular />} onClick={handleGoToThread}>
                        Go to Thread
                    </Button>
                </div>
                <div className={styles.threadMeta}>
                    <span>Generated: {formatDate(insight.generatedTimestamp)}</span>
                </div>
            </div>
            <div className={styles.content}>
                <div className={styles.contentInner}>
                    {insight.insightMarkdown ? (
                        <>
                            {sections.intro && (
                                <div className={styles.introSection}>
                                    <div className={styles.insightContent}>
                                        <ReactMarkdown>{sections.intro}</ReactMarkdown>
                                    </div>
                                </div>
                            )}
                            {showAccordion ? (
                                <div className={styles.accordionWrapper}>
                                    <Text className={styles.accordionSectionTitle}>
                                        <FormattedMessage {...SreAgentResources.sessionInsight} />
                                    </Text>
                                    <Accordion className={styles.accordion} collapsible multiple>
                                        {sections.timeline && (
                                            <AccordionItem value="timeline" className={styles.accordionItem}>
                                                <AccordionHeader className={styles.accordionHeader} size="large">
                                                    <FormattedMessage {...SreAgentResources.timeline} />
                                                </AccordionHeader>
                                                <AccordionPanel className={styles.accordionPanel}>
                                                    <div className={styles.insightContent}>
                                                        <ReactMarkdown>{sections.timeline}</ReactMarkdown>
                                                    </div>
                                                </AccordionPanel>
                                            </AccordionItem>
                                        )}
                                        {sections.agentPerformance && (
                                            <AccordionItem value="agentPerformance" className={styles.accordionItem}>
                                                <AccordionHeader className={styles.accordionHeader} size="large">
                                                    <FormattedMessage {...SreAgentResources.agentPerformance} />
                                                </AccordionHeader>
                                                <AccordionPanel className={styles.accordionPanel}>
                                                    <div className={styles.insightContent}>
                                                        <ReactMarkdown>{sections.agentPerformance}</ReactMarkdown>
                                                    </div>
                                                </AccordionPanel>
                                            </AccordionItem>
                                        )}
                                    </Accordion>
                                </div>
                            ) : (
                                <div className={styles.fallbackCard}>
                                    <div className={styles.insightContent}>
                                        <ReactMarkdown>{insight.insightMarkdown}</ReactMarkdown>
                                    </div>
                                </div>
                            )}
                        </>
                    ) : (
                        <div className={styles.emptyState}>
                            <Text size={400} weight="semibold">
                                <FormattedMessage {...SreAgentResources.noInsightContentAvailable} />
                            </Text>
                            <Text size={300}>
                                <FormattedMessage {...SreAgentResources.insightNoMarkdownContent} />
                            </Text>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default InsightsDetailPanel;

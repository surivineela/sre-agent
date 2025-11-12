import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Button,
    Card,
    Text,
    Textarea,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import {
    ChevronDown20Regular,
    ChevronUp20Regular,
    Lightbulb24Regular,
    Send20Regular,
    ThumbDislike20Regular,
    ThumbLike20Regular,
} from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    card: {
        border: `1px solid ${tokens.colorBrandStroke1}`,
        borderRadius: tokens.borderRadiusXLarge,
        padding: '16px',
        backgroundColor: tokens.colorNeutralBackground2,
        marginBottom: '8px',
    },
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        columnGap: '12px',
        marginBottom: '0px',
        cursor: 'pointer',
    },
    headerRowExpanded: {
        marginBottom: '12px',
    },
    icon: {
        color: tokens.colorBrandForeground1,
    },
    title: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
        color: tokens.colorNeutralForeground1,
    },
    badge: {
        marginLeft: 'auto',
    },
    content: {
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
    },
    accordion: {
        marginTop: '8px',
    },
    collapseButton: {
        minWidth: 'auto',
        padding: '4px',
    },
    collapsedPreview: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
        marginTop: '4px',
    },
    feedbackSection: {
        marginTop: '16px',
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusLarge,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    feedbackHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: '8px',
        cursor: 'pointer',
    },
    feedbackHeaderCollapsed: {
        marginBottom: '0',
    },
    feedbackTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    feedbackRating: {
        display: 'flex',
        gap: '8px',
    },
    feedbackInput: {
        marginBottom: '8px',
        width: '100%',
    },
    feedbackActions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '8px',
    },
    feedbackMessage: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
        marginTop: '8px',
    },
});

interface SessionInsightCardProps {
    insightText: string;
    onRequestRefinement?: (feedback: string) => Promise<void>;
}

const SessionInsightCard = ({ insightText, onRequestRefinement }: SessionInsightCardProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const [isExpanded, setIsExpanded] = useState(false);
    const [feedbackText, setFeedbackText] = useState('');
    const [feedbackRating, setFeedbackRating] = useState<'positive' | 'negative' | null>(null);
    const [feedbackSubmitted, setFeedbackSubmitted] = useState(false);
    const [feedbackExpanded, setFeedbackExpanded] = useState(false);

    // Remove emojis from text
    const removeEmojis = (text: string): string => {
        // Remove emoji patterns like 📋, ⚠️, 📊, ✅, 💬, 🔑, ⚪, 🟢, 🔴, 🟡
        return text.replace(/[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/gu, '').trim();
    };

    // Parse the insight text to extract sections
    const sections = useMemo(() => {
        const parsed: { title: string; content: string }[] = [];

        // Split by ## headers (but not # Session Insight)
        const parts = insightText.split(/^## /gm).filter(part => part.trim());

        parts.forEach(part => {
            const lines = part.split('\n');
            const title = removeEmojis(lines[0].trim());
            const content = removeEmojis(lines.slice(1).join('\n').trim());

            if (title && content && !title.startsWith('#')) {
                parsed.push({ title, content });
            }
        });

        return parsed;
    }, [insightText]);

    const toggleExpanded = () => {
        setIsExpanded(!isExpanded);
    };

    const handleFeedbackSubmit = useCallback(() => {
        if (onRequestRefinement && feedbackText.trim()) {
            const feedback = `Feedback on session insights:\n\n${feedbackText}`;
            onRequestRefinement(feedback);
        }

        setFeedbackSubmitted(true);

        // Reset feedback after 3 seconds
        setTimeout(() => {
            setFeedbackSubmitted(false);
            setFeedbackText('');
            setFeedbackRating(null);
        }, 3000);
    }, [feedbackText, onRequestRefinement]);

    const handleRatingClick = useCallback((rating: 'positive' | 'negative') => {
        setFeedbackRating(prevRating => (prevRating === rating ? null : rating));
    }, []);

    const toggleFeedback = useCallback(() => {
        setFeedbackExpanded(prev => !prev);
    }, []);

    return (
        <Card className={styles.card}>
            <div className={mergeClasses(styles.headerRow, isExpanded && styles.headerRowExpanded)} onClick={toggleExpanded}>
                <Lightbulb24Regular className={styles.icon} />
                <Text className={styles.title}>
                    <FormattedMessage {...SreAgentResources.sessionInsight} />
                </Text>
                <Badge appearance="tint" color="brand" className={styles.badge}>
                    <FormattedMessage {...SreAgentResources.insights} />
                </Badge>
                <Button
                    appearance="subtle"
                    icon={isExpanded ? <ChevronUp20Regular /> : <ChevronDown20Regular />}
                    className={styles.collapseButton}
                    onClick={e => {
                        e.stopPropagation();
                        toggleExpanded();
                    }}
                />
            </div>

            {!isExpanded && (
                <div className={styles.collapsedPreview}>
                    <FormattedMessage {...SreAgentResources.clickToViewSessionAnalysis} />
                </div>
            )}

            {isExpanded && (
                <div className={styles.content}>
                    {sections.length > 0 ? (
                        <>
                            <Accordion
                                defaultOpenItems={sections.map((_, idx) => `section-${idx}`)}
                                multiple
                                collapsible
                                className={styles.accordion}
                            >
                                {sections.map((section, index) => (
                                    <AccordionItem key={`section-${index}`} value={`section-${index}`}>
                                        <AccordionHeader size="large">
                                            <Text weight="semibold">{section.title}</Text>
                                        </AccordionHeader>
                                        <AccordionPanel>
                                            <ReactMarkdownComponent content={section.content} variant="chat" />
                                        </AccordionPanel>
                                    </AccordionItem>
                                ))}
                            </Accordion>

                            {/* Feedback Section */}
                            <div className={styles.feedbackSection}>
                                <div
                                    className={mergeClasses(styles.feedbackHeader, !feedbackExpanded && styles.feedbackHeaderCollapsed)}
                                    onClick={toggleFeedback}
                                >
                                    <Text className={styles.feedbackTitle}>
                                        {intl.formatMessage({ defaultMessage: 'Feedback', id: 'Ejhdi4' })}
                                        {feedbackExpanded ? <ChevronUp20Regular /> : <ChevronDown20Regular />}
                                    </Text>
                                    {!feedbackExpanded && (
                                        <div className={styles.feedbackRating}>
                                            <Button
                                                appearance={feedbackRating === 'positive' ? 'primary' : 'subtle'}
                                                icon={<ThumbLike20Regular />}
                                                onClick={e => {
                                                    e.stopPropagation();
                                                    handleRatingClick('positive');
                                                }}
                                                title={intl.formatMessage({
                                                    defaultMessage: 'This insight was helpful',
                                                    id: 'BD+V8L',
                                                })}
                                            />
                                            <Button
                                                appearance={feedbackRating === 'negative' ? 'primary' : 'subtle'}
                                                icon={<ThumbDislike20Regular />}
                                                onClick={e => {
                                                    e.stopPropagation();
                                                    handleRatingClick('negative');
                                                }}
                                                title={intl.formatMessage({
                                                    defaultMessage: 'This insight needs improvement',
                                                    id: '+XtuWl',
                                                })}
                                            />
                                        </div>
                                    )}
                                </div>
                                {feedbackExpanded && (
                                    <>
                                        <div className={styles.feedbackRating} style={{ marginBottom: '12px' }}>
                                            <Button
                                                appearance={feedbackRating === 'positive' ? 'primary' : 'subtle'}
                                                icon={<ThumbLike20Regular />}
                                                onClick={() => handleRatingClick('positive')}
                                                title={intl.formatMessage({
                                                    defaultMessage: 'This insight was helpful',
                                                    id: 'BD+V8L',
                                                })}
                                            />
                                            <Button
                                                appearance={feedbackRating === 'negative' ? 'primary' : 'subtle'}
                                                icon={<ThumbDislike20Regular />}
                                                onClick={() => handleRatingClick('negative')}
                                                title={intl.formatMessage({
                                                    defaultMessage: 'This insight needs improvement',
                                                    id: '+XtuWl',
                                                })}
                                            />
                                        </div>
                                        <Textarea
                                            className={styles.feedbackInput}
                                            placeholder={intl.formatMessage({
                                                defaultMessage: 'Share your thoughts about this insight... (optional)',
                                                id: '4CcHuj',
                                            })}
                                            value={feedbackText}
                                            onChange={(_, data) => setFeedbackText(data.value)}
                                            rows={3}
                                            resize="vertical"
                                        />
                                        <div className={styles.feedbackActions}>
                                            {feedbackSubmitted && (
                                                <Text className={styles.feedbackMessage}>
                                                    {intl.formatMessage({
                                                        defaultMessage: 'Thank you for your feedback!',
                                                        id: 'Nrc9ba',
                                                    })}
                                                </Text>
                                            )}
                                            <Button
                                                appearance="primary"
                                                icon={<Send20Regular />}
                                                onClick={handleFeedbackSubmit}
                                                disabled={feedbackSubmitted || !feedbackText.trim()}
                                            >
                                                {intl.formatMessage({ defaultMessage: 'Submit Feedback', id: '+ASm/B' })}
                                            </Button>
                                        </div>
                                    </>
                                )}
                            </div>
                        </>
                    ) : (
                        <ReactMarkdownComponent content={removeEmojis(insightText)} variant="chat" />
                    )}
                </div>
            )}
        </Card>
    );
};

export default SessionInsightCard;

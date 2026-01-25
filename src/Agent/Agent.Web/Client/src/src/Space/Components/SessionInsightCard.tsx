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
import { useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackResources, SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    card: {
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '12px',
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground3,
        marginBottom: '8px',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
            border: `1px solid ${tokens.colorNeutralStroke1Hover}`,
        },
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
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '48px',
        height: '48px',
        backgroundColor: tokens.colorNeutralBackground4,
        borderRadius: '8px',
        flexShrink: 0,
    },
    icon: {
        color: tokens.colorNeutralForeground2,
    },
    titleContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
        minWidth: 0,
    },
    title: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
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
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
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
    threadId: string;
    onFeedbackSaved?: () => Promise<void> | void;
}

const SessionInsightCard = ({ insightText, threadId, onFeedbackSaved }: SessionInsightCardProps) => {
    const styles = useStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [isExpanded, setIsExpanded] = useState(false);
    const [feedbackText, setFeedbackText] = useState('');
    const [feedbackRating, setFeedbackRating] = useState<'positive' | 'negative' | null>(null);
    const [feedbackSubmitted, setFeedbackSubmitted] = useState(false);
    const [feedbackExpanded, setFeedbackExpanded] = useState(false);
    const [feedbackError, setFeedbackError] = useState<string | null>(null);
    const [isSubmitting, setIsSubmitting] = useState(false);

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

    const handleFeedbackSubmit = useCallback(async () => {
        if (!feedbackRating && !feedbackText.trim()) {
            setFeedbackError('Please add a rating or a comment before submitting.');
            return;
        }

        setFeedbackError(null);
        setIsSubmitting(true);

        try {
            const feedbackId = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
            const response = await fetch(`${sreAgentEndpoint}/api/v1/threads/${threadId}/insights/feedback`, {
                method: 'POST',
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    feedbackId,
                    rating: feedbackRating,
                    comment: feedbackText,
                }),
            });

            if (response.ok) {
                setFeedbackSubmitted(true);

                setTimeout(() => {
                    setFeedbackSubmitted(false);
                    setFeedbackText('');
                    setFeedbackRating(null);
                    setFeedbackError(null);
                }, 3000);

                if (onFeedbackSaved) {
                    await onFeedbackSaved();
                }
            } else {
                const errorText = await response.text();
                setFeedbackError(errorText || 'Failed to submit feedback.');
            }
        } catch (error) {
            console.error('Error submitting feedback:', error);
            setFeedbackError('Error submitting feedback.');
        } finally {
            setIsSubmitting(false);
        }
    }, [feedbackRating, feedbackText, onFeedbackSaved, sreAgentEndpoint, threadId]);

    const handleRatingClick = useCallback((rating: 'positive' | 'negative') => {
        setFeedbackRating(prevRating => (prevRating === rating ? null : rating));
    }, []);

    const toggleFeedback = useCallback(() => {
        setFeedbackExpanded(prev => !prev);
    }, []);

    return (
        <Card className={styles.card}>
            <div className={mergeClasses(styles.headerRow, isExpanded && styles.headerRowExpanded)} onClick={toggleExpanded}>
                <div className={styles.iconContainer}>
                    <Lightbulb24Regular className={styles.icon} />
                </div>
                <div className={styles.titleContainer}>
                    <Text className={styles.title}>
                        <FormattedMessage {...SreAgentResources.sessionInsight} />
                    </Text>
                    {!isExpanded && (
                        <Text className={styles.collapsedPreview}>
                            <FormattedMessage {...SreAgentResources.clickToViewSessionAnalysis} />
                        </Text>
                    )}
                </div>
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
                                        {feedbackError && (
                                            <Text className={styles.feedbackMessage} role="alert">
                                                {feedbackError}
                                            </Text>
                                        )}
                                        <Textarea
                                            className={styles.feedbackInput}
                                            placeholder={intl.formatMessage(FeedbackResources.sessionInsightsFeedbackPlaceholder)}
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
                                                disabled={feedbackSubmitted || isSubmitting}
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

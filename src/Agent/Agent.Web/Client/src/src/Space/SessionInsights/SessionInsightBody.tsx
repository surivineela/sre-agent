import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Button,
    mergeClasses,
    Text,
    Textarea,
} from '@fluentui/react-components';
import { ChevronDown20Regular, ChevronUp20Regular, Send20Regular, ThumbDislike20Regular, ThumbLike20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdownComponent from '../../Common/Components/ReactMarkdownComponent';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SessionInsightData } from './SessionInsightTypes';
import { useStyles } from './Styles/InsightsDetailPanel.styles';

interface SessionInsightBodyProps {
    insight: SessionInsightData;
    sreAgentEndpoint: string;
    onFeedbackSaved?: () => void | Promise<void>;
}

const SessionInsightBody: FC<SessionInsightBodyProps> = ({ insight, sreAgentEndpoint, onFeedbackSaved }) => {
    const styles = useStyles();
    const intl = useIntl();

    const [feedbackText, setFeedbackText] = useState('');
    const [feedbackRating, setFeedbackRating] = useState<'positive' | 'negative' | null>(null);
    const [feedbackSubmitted, setFeedbackSubmitted] = useState(false);
    const [feedbackExpanded, setFeedbackExpanded] = useState(false);
    const [feedbackError, setFeedbackError] = useState<string | null>(null);

    // Remove emojis from text (same as SessionInsightCard)
    const removeEmojis = (text: string): string => {
        return text.replace(/[\u{1F300}-\u{1F9FF}]|[\u{2600}-\u{26FF}]|[\u{2700}-\u{27BF}]/gu, '').trim();
    };

    // Parse the insight markdown to extract sections dynamically (same approach as SessionInsightCard)
    const sections = useMemo(() => {
        if (!insight.insightMarkdown) {
            return [];
        }

        const parsed: { title: string; content: string }[] = [];

        // Split by ## headers (but not # Session Insight)
        const parts = insight.insightMarkdown.split(/^## /gm).filter(part => part.trim());

        parts.forEach(part => {
            const lines = part.split('\n');
            const title = removeEmojis(lines[0].trim());
            const content = removeEmojis(lines.slice(1).join('\n').trim());

            if (title && content && !title.startsWith('#')) {
                parsed.push({ title, content });
            }
        });

        return parsed;
    }, [insight.insightMarkdown]);

    const handleFeedbackSubmit = useCallback(async () => {
        if (!feedbackRating && !feedbackText.trim()) {
            setFeedbackError('Please add a rating or a comment before submitting.');
            return;
        }

        setFeedbackError(null);
        try {
            const feedbackId = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
            const response = await fetch(`${sreAgentEndpoint}/api/v1/threads/${insight.threadId}/insights/feedback`, {
                method: 'POST',
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    insightId: insight.id,
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
        }
    }, [feedbackRating, feedbackText, insight.threadId, onFeedbackSaved, sreAgentEndpoint]);

    const handleRatingClick = useCallback((rating: 'positive' | 'negative') => {
        setFeedbackRating(prevRating => (prevRating === rating ? null : rating));
    }, []);

    const toggleFeedback = useCallback(() => {
        setFeedbackExpanded(prev => !prev);
    }, []);

    return (
        <>
            {insight.insightMarkdown && sections.length > 0 ? (
                <>
                    <div className={styles.accordionWrapper}>
                        <Accordion
                            className={styles.accordion}
                            collapsible
                            multiple
                            defaultOpenItems={sections.map((_, idx) => `section-${idx}`)}
                        >
                            {sections.map((section, index) => (
                                <AccordionItem key={`section-${index}`} value={`section-${index}`} className={styles.accordionItem}>
                                    <AccordionHeader className={styles.accordionHeader} size="large">
                                        <Text weight="semibold">{section.title}</Text>
                                    </AccordionHeader>
                                    <AccordionPanel className={styles.accordionPanel}>
                                        <ReactMarkdownComponent content={section.content} variant="chat" />
                                    </AccordionPanel>
                                </AccordionItem>
                            ))}
                        </Accordion>
                    </div>

                    <div className={styles.feedbackSection}>
                        <div
                            className={mergeClasses(styles.feedbackHeader, !feedbackExpanded && styles.feedbackHeaderCollapsed)}
                            onClick={toggleFeedback}
                        >
                            <Text className={styles.feedbackTitle}>
                                Feedback
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
                                        title={intl.formatMessage(SreAgentResources.insightWasHelpful)}
                                    />
                                    <Button
                                        appearance={feedbackRating === 'negative' ? 'primary' : 'subtle'}
                                        icon={<ThumbDislike20Regular />}
                                        onClick={e => {
                                            e.stopPropagation();
                                            handleRatingClick('negative');
                                        }}
                                        title={intl.formatMessage(SreAgentResources.insightNeedsImprovement)}
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
                                        title={intl.formatMessage(SreAgentResources.insightWasHelpful)}
                                    />
                                    <Button
                                        appearance={feedbackRating === 'negative' ? 'primary' : 'subtle'}
                                        icon={<ThumbDislike20Regular />}
                                        onClick={() => handleRatingClick('negative')}
                                        title={intl.formatMessage(SreAgentResources.insightNeedsImprovement)}
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
                                            {intl.formatMessage(SreAgentResources.feedbackDialogTitle)}
                                        </Text>
                                    )}
                                    <Button
                                        appearance="primary"
                                        icon={<Send20Regular />}
                                        onClick={handleFeedbackSubmit}
                                        disabled={feedbackSubmitted}
                                    >
                                        Submit Feedback
                                    </Button>
                                </div>
                            </>
                        )}
                    </div>
                </>
            ) : (
                <div className={styles.emptyState}>
                    <Text size={400} weight="semibold">
                        {intl.formatMessage(SreAgentResources.noInsightContentAvailable)}
                    </Text>
                    <Text size={300}>{intl.formatMessage(SreAgentResources.insightNoMarkdownContent)}</Text>
                </div>
            )}
        </>
    );
};

export default SessionInsightBody;

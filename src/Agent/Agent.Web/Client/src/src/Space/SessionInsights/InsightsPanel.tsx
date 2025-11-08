import { Button, makeStyles, shorthands, Spinner, Text, Textarea, tokens } from '@fluentui/react-components';
import {
    ArrowSyncRegular,
    ChatMultiple20Regular,
    ChevronDown20Regular,
    ChevronUp20Regular,
    Open20Regular,
    Send20Regular,
    ThumbDislike20Regular,
    ThumbLike20Regular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import { useNavigate } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        ...shorthands.overflow('hidden'),
    },
    header: {
        ...shorthands.padding('16px', '24px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('8px'),
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
        display: 'flex',
        ...shorthands.gap('16px'),
        alignItems: 'center',
    },
    content: {
        flex: 1,
        ...shorthands.overflow('auto'),
        ...shorthands.padding('24px'),
    },
    loadingContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
    },
    insightCard: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('20px'),
        ...shorthands.margin('0', '0', '16px', '0'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    insightHeader: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
        marginBottom: '12px',
    },
    insightTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
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
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        ...shorthands.gap('12px'),
        color: tokens.colorNeutralForeground3,
    },
    actions: {
        display: 'flex',
        ...shorthands.gap('8px'),
        marginTop: '16px',
    },
    timelineContainer: {
        marginTop: '16px',
        marginBottom: '24px',
    },
    timelineItem: {
        display: 'flex',
        ...shorthands.gap('16px'),
        marginBottom: '24px',
        position: 'relative',
    },
    timelineMarker: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        minWidth: '40px',
    },
    timelineDot: {
        width: '16px',
        height: '16px',
        ...shorthands.borderRadius('50%'),
        backgroundColor: tokens.colorNeutralBackground1,
        flexShrink: 0,
    },
    timelineDotInitial: {
        ...shorthands.border('3px', 'solid', '#0078D4'),
    },
    timelineDotProgress: {
        ...shorthands.border('3px', 'solid', '#FDB022'),
    },
    timelineDotSuccess: {
        ...shorthands.border('3px', 'solid', '#107C10'),
    },
    timelineDotIssue: {
        ...shorthands.border('3px', 'solid', '#D13438'),
    },
    timelineDotResolved: {
        ...shorthands.border('3px', 'solid', '#8764B8'),
    },
    timelineLine: {
        width: '2px',
        flex: 1,
        backgroundColor: tokens.colorNeutralStroke2,
        marginTop: '4px',
        marginBottom: '4px',
    },
    timelineContent: {
        flex: 1,
        paddingBottom: '8px',
    },
    timelineTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: '4px',
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
    },
    timelineEmoji: {
        fontSize: '24px',
        lineHeight: '1',
        flexShrink: 0,
    },
    timelineStatus: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        marginBottom: '8px',
        fontWeight: tokens.fontWeightSemibold,
    },
    timelineDescription: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
        lineHeight: '1.5',
        whiteSpace: 'pre-wrap',
    },
    feedbackSection: {
        marginTop: '24px',
        ...shorthands.padding('16px'),
        backgroundColor: tokens.colorNeutralBackground3,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
    },
    feedbackHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        marginBottom: '12px',
        cursor: 'pointer',
        ...shorthands.gap('8px'),
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
        ...shorthands.gap('8px'),
    },
    feedbackRating: {
        display: 'flex',
        ...shorthands.gap('8px'),
    },
    feedbackInput: {
        marginBottom: '12px',
        width: '100%',
    },
    feedbackActions: {
        display: 'flex',
        justifyContent: 'flex-end',
        ...shorthands.gap('8px'),
    },
    feedbackMessage: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        fontStyle: 'italic',
        marginTop: '8px',
    },
});

interface SessionInsightData {
    threadId: string;
    title?: string;
    generatedTimestamp: string; // Keep as string for easier formatting
    insightMarkdown?: string;
    feedback?: Array<{
        rating: string;
        comment: string;
        submittedTimestamp: Date;
        userId?: string;
    }>;
    feedbackCount: number;
    positiveFeedbackCount: number;
    negativeFeedbackCount: number;
}

interface InsightsPanelProps {
    thread: Thread;
    onInsightsGenerated?: () => void;
}

const InsightsPanel: FC<InsightsPanelProps> = ({ thread, onInsightsGenerated }) => {
    const styles = useStyles();
    const intl = useIntl();
    const { resourceId } = useContext(EnvironmentContext);
    const navigate = useNavigate();
    const [loading, setLoading] = useState(true);
    const [insight, setInsight] = useState<SessionInsightData | null>(null);
    const [generating, setGenerating] = useState(false);
    const [feedbackText, setFeedbackText] = useState('');
    const [feedbackRating, setFeedbackRating] = useState<'positive' | 'negative' | null>(null);
    const [feedbackSubmitted, setFeedbackSubmitted] = useState(false);
    const [feedbackExpanded, setFeedbackExpanded] = useState(false);

    const loadInsights = useCallback(async () => {
        try {
            setLoading(true);

            // Try to get session insight from Cosmos DB
            const response = await fetch(`/api/v1/agents/${encodeURIComponent(resourceId)}/threads/${thread.id}/insights`);

            if (response.ok) {
                const data = await response.json();
                console.log('Loaded insight data:', data);

                setInsight({
                    threadId: data.threadId ?? data.ThreadId,
                    title: data.title ?? data.Title,
                    generatedTimestamp: data.generatedTimestamp ?? data.GeneratedTimestamp,
                    insightMarkdown: data.insightMarkdown ?? data.InsightMarkdown,
                    feedback: data.feedback ?? data.Feedback,
                    feedbackCount: data.feedbackCount ?? data.FeedbackCount ?? 0,
                    positiveFeedbackCount: data.positiveFeedbackCount ?? data.PositiveFeedbackCount ?? 0,
                    negativeFeedbackCount: data.negativeFeedbackCount ?? data.NegativeFeedbackCount ?? 0,
                });
            } else if (response.status === 404) {
                // No insight found in Cosmos DB - might be old format
                setInsight(null);
            } else {
                console.error('Failed to load insights:', response.statusText);
                setInsight(null);
            }
        } catch (error) {
            console.error('Failed to load insights:', error);
            setInsight(null);
        } finally {
            setLoading(false);
        }
    }, [thread.id, resourceId]);

    useEffect(() => {
        loadInsights();
    }, [loadInsights]);

    const handleGenerateInsights = useCallback(async () => {
        try {
            setGenerating(true);

            // Call the generate insights API
            const response = await fetch(`/api/v1/agents/${encodeURIComponent(resourceId)}/threads/${thread.id}/insights`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (response.ok) {
                // Reload insights after generation
                await loadInsights();

                // Notify parent component to refresh thread list
                if (onInsightsGenerated) {
                    onInsightsGenerated();
                }
            } else {
                console.error('Failed to generate insights:', response.statusText);
            }
        } catch (error) {
            console.error('Error generating insights:', error);
        } finally {
            setGenerating(false);
        }
    }, [thread.id, resourceId, loadInsights, onInsightsGenerated]);

    const handleGoToThread = useCallback(() => {
        navigate(`/views/activities/threads/${thread.id}`);
    }, [navigate, thread.id]);

    const handleFeedbackSubmit = useCallback(async () => {
        try {
            // Send feedback to backend API
            const response = await fetch(`/api/v1/agents/${encodeURIComponent(resourceId)}/threads/${thread.id}/insights/feedback`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    rating: feedbackRating,
                    comment: feedbackText,
                    userId: 'current-user', // TODO: Get actual user ID from context
                }),
            });

            if (response.ok) {
                setFeedbackSubmitted(true);

                // Reset feedback after 3 seconds
                setTimeout(() => {
                    setFeedbackSubmitted(false);
                    setFeedbackText('');
                    setFeedbackRating(null);
                }, 3000);

                // Reload insights to get updated feedback
                await loadInsights();
            } else {
                console.error('Failed to submit feedback:', response.statusText);
            }
        } catch (error) {
            console.error('Error submitting feedback:', error);
        }
    }, [thread.id, resourceId, feedbackRating, feedbackText, loadInsights]);

    const handleRatingClick = useCallback((rating: 'positive' | 'negative') => {
        setFeedbackRating(prevRating => (prevRating === rating ? null : rating));
    }, []);

    const toggleFeedback = useCallback(() => {
        setFeedbackExpanded(prev => !prev);
    }, []);

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

    if (loading) {
        return (
            <div className={styles.container}>
                <div className={styles.loadingContainer}>
                    <Spinner label="Loading insights..." />
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerTop}>
                    <Text className={styles.threadTitle}>{thread.title}</Text>
                    <Button appearance="primary" icon={<Open20Regular />} onClick={handleGoToThread}>
                        Go to Thread
                    </Button>
                </div>
                <div className={styles.threadMeta}>
                    <span>Thread ID: /views/activities/threads/{thread.id}</span>
                    <span>
                        <ChatMultiple20Regular style={{ marginRight: '4px', verticalAlign: 'middle' }} />
                        Created: {formatDate(thread.createdTimestamp)}
                    </span>
                    <span>Last Modified: {formatDate(thread.modifiedTimestamp)}</span>
                    <span>
                        Insights Generated:{' '}
                        {insight?.generatedTimestamp
                            ? formatDate(insight.generatedTimestamp)
                            : formatDate(thread.trajectoryGeneratedTimestamp)}
                    </span>
                </div>
            </div>
            <div className={styles.content}>
                {insight ? (
                    <div className={styles.insightCard}>
                        <div className={styles.insightHeader}>
                            <Text className={styles.insightTitle}>{insight.title || 'Session Insight'}</Text>
                        </div>

                        {/* Render markdown content - it includes timeline and all sections */}
                        {insight.insightMarkdown ? (
                            <div className={styles.insightContent}>
                                <ReactMarkdown>{insight.insightMarkdown}</ReactMarkdown>
                            </div>
                        ) : null}

                        <div className={styles.feedbackSection}>
                            <div
                                className={`${styles.feedbackHeader} ${!feedbackExpanded ? styles.feedbackHeaderCollapsed : ''}`}
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
                                    <Textarea
                                        className={styles.feedbackInput}
                                        placeholder="Share your thoughts about this insight... (optional)"
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
                        <div className={styles.actions}>
                            <Button
                                appearance="secondary"
                                icon={<ArrowSyncRegular />}
                                onClick={handleGenerateInsights}
                                disabled={generating}
                            >
                                {generating ? 'Regenerating...' : 'Regenerate Insights'}
                            </Button>
                        </div>
                    </div>
                ) : (
                    <div className={styles.emptyState}>
                        <Text size={400} weight="semibold">
                            {intl.formatMessage(SreAgentResources.noInsightsAvailable)}
                        </Text>
                        <Text size={300}>{intl.formatMessage(SreAgentResources.insightsNotGenerated)}</Text>
                        <Button
                            appearance="primary"
                            icon={<ArrowSyncRegular />}
                            onClick={handleGenerateInsights}
                            disabled={generating}
                            style={{ marginTop: '16px' }}
                        >
                            {generating ? 'Generating...' : 'Generate Insights'}
                        </Button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default InsightsPanel;

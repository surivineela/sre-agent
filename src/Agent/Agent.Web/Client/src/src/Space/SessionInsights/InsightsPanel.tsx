import { Button, makeStyles, shorthands, Spinner, Text, tokens } from '@fluentui/react-components';
import { ArrowSyncRegular, ChatMultiple20Regular, Open20Regular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { PrimaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';
import SessionInsightBody from './SessionInsightBody';
import { SessionInsightData } from './SessionInsightTypes';

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
        flexWrap: 'wrap',
    },
    content: {
        flex: 1,
        ...shorthands.overflow('auto'),
        ...shorthands.padding('24px'),
        backgroundColor: tokens.colorNeutralBackground1,
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
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.gap('12px'),
    },
    insightHeader: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
    },
    insightTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
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
        marginTop: '8px',
    },
});

interface InsightsPanelProps {
    thread: Thread;
    onInsightsGenerated?: () => void;
}

const InsightsPanel: FC<InsightsPanelProps> = ({ thread, onInsightsGenerated }) => {
    const styles = useStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const navigate = useAgentSiteNavigate();
    const [loading, setLoading] = useState(true);
    const [insight, setInsight] = useState<SessionInsightData | null>(null);
    const [generating, setGenerating] = useState(false);
    const loadInsights = useCallback(async () => {
        try {
            setLoading(true);

            // Try to get session insight from Cosmos DB
            const response = await fetch(`${sreAgentEndpoint}/api/v1/threads/${thread.id}/insights`, {
                headers: getAgentHeaders(),
            });

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
    }, [thread.id, sreAgentEndpoint]);

    useEffect(() => {
        loadInsights();
    }, [loadInsights]);

    const handleGenerateInsights = useCallback(async () => {
        try {
            setGenerating(true);

            // Call the generate insights API
            const response = await fetch(`${sreAgentEndpoint}/api/v1/threads/${thread.id}/insights`, {
                method: 'POST',
                headers: getAgentHeaders(),
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
    }, [thread.id, sreAgentEndpoint, loadInsights, onInsightsGenerated]);

    const handleGoToThread = useCallback(() => {
        navigate({
            primaryNavItemValue: PrimaryNavItemValues.Threads,
            threadId: thread.id,
        });
    }, [navigate, thread.id]);

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
                    <span>Thread ID: /views/thread/{thread.id}</span>
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
                        <SessionInsightBody insight={insight} sreAgentEndpoint={sreAgentEndpoint} onFeedbackSaved={loadInsights} />
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

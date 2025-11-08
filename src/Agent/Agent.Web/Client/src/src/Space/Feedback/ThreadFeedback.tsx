import { Button, makeStyles, shorthands, Spinner, Text, tokens } from '@fluentui/react-components';
import { ArrowSyncRegular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import FeedbackDetailPanel from './FeedbackDetailPanel';
import FeedbackList from './FeedbackList';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        minHeight: 0,
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.overflow('hidden'),
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        ...shorthands.padding('16px', '24px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        flexShrink: 0,
    },
    title: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    content: {
        display: 'flex',
        flexDirection: 'row',
        flex: 1,
        minHeight: 0,
        ...shorthands.overflow('hidden'),
    },
    threadsPanel: {
        width: '320px',
        ...shorthands.borderRight('1px', 'solid', tokens.colorNeutralStroke2),
        backgroundColor: tokens.colorNeutralBackground2,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        ...shorthands.overflow('hidden'),
    },
    feedbackPanel: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        ...shorthands.overflow('hidden'),
        backgroundColor: tokens.colorNeutralBackground1,
    },
    loadingContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        ...shorthands.gap('12px'),
    },
});

interface ThreadFeedbackData {
    threadId: string;
    threadTitle: string;
    messageFeedbackId: string;
    isPositive: boolean;
    feedbackText?: string;
    createdTimestamp: string;
}

const ThreadFeedback: FC = () => {
    const styles = useStyles();
    const { resourceId } = useContext(EnvironmentContext);

    const [feedbacks, setFeedbacks] = useState<ThreadFeedbackData[]>([]);
    const [selectedFeedbackId, setSelectedFeedbackId] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);

    const loadFeedbacks = useCallback(
        async (showSpinner = true) => {
            try {
                if (showSpinner) {
                    setLoading(true);
                } else {
                    setRefreshing(true);
                }

                // Fetch all threads first to get their feedbacks
                const threadsResponse = await fetch(`/api/v1/threads?skip=0&take=1000`, {
                    headers: getAgentHeaders(),
                });

                if (!threadsResponse.ok) {
                    console.error('Failed to fetch threads:', threadsResponse.status, threadsResponse.statusText);
                    setFeedbacks([]);
                    return;
                }

                const threadsData = await threadsResponse.json();
                const threads = threadsData?.threads ?? [];

                // Collect all feedbacks from all threads
                const allFeedbacks: ThreadFeedbackData[] = [];

                for (const thread of threads) {
                    if (thread.feedbacks && Array.isArray(thread.feedbacks) && thread.feedbacks.length > 0) {
                        for (const feedback of thread.feedbacks) {
                            allFeedbacks.push({
                                threadId: thread.id,
                                threadTitle: thread.title || 'Untitled Thread',
                                messageFeedbackId: feedback.id || feedback.messageFeedbackId,
                                isPositive: feedback.isPositive ?? false,
                                feedbackText: feedback.feedbackText || feedback.text,
                                createdTimestamp: feedback.createdTimestamp || feedback.timestamp || new Date().toISOString(),
                            });
                        }
                    }
                }

                // Sort by created timestamp (newest first)
                allFeedbacks.sort((a, b) => {
                    const dateA = new Date(a.createdTimestamp).getTime();
                    const dateB = new Date(b.createdTimestamp).getTime();
                    return dateB - dateA;
                });

                setFeedbacks(allFeedbacks);

                // Auto-select first feedback if none selected
                if (!selectedFeedbackId && allFeedbacks.length > 0) {
                    setSelectedFeedbackId(allFeedbacks[0].messageFeedbackId);
                }
            } catch (error) {
                console.error('Failed to load thread feedbacks:', error);
                setFeedbacks([]);
            } finally {
                setLoading(false);
                setRefreshing(false);
            }
        },
        [resourceId, selectedFeedbackId]
    );

    useEffect(() => {
        loadFeedbacks();
    }, []);

    const handleRefresh = useCallback(() => {
        loadFeedbacks(false);
    }, [loadFeedbacks]);

    const handleFeedbackSelect = useCallback((feedbackId: string) => {
        setSelectedFeedbackId(feedbackId);
    }, []);

    const selectedFeedback = useMemo(
        () => feedbacks.find(f => f.messageFeedbackId === selectedFeedbackId),
        [feedbacks, selectedFeedbackId]
    );

    if (loading) {
        return (
            <div className={styles.container}>
                <div className={styles.loadingContainer}>
                    <Spinner label="Loading thread feedbacks..." />
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text className={styles.title}>
                    <FormattedMessage {...SreAgentResources.threadFeedback} />
                </Text>
                <Button appearance="subtle" icon={<ArrowSyncRegular />} onClick={handleRefresh} disabled={refreshing}>
                    {refreshing ? 'Refreshing...' : 'Refresh'}
                </Button>
            </div>
            <div className={styles.content}>
                <div className={styles.threadsPanel}>
                    <FeedbackList feedbacks={feedbacks} selectedFeedbackId={selectedFeedbackId} onFeedbackSelect={handleFeedbackSelect} />
                </div>
                <div className={styles.feedbackPanel}>
                    {selectedFeedback ? (
                        <FeedbackDetailPanel feedback={selectedFeedback} />
                    ) : (
                        <div className={styles.emptyState}>
                            <Text size={400} weight="semibold">
                                <FormattedMessage {...SreAgentResources.noFeedbackSelected} />
                            </Text>
                            <Text size={300}>
                                {feedbacks.length === 0
                                    ? 'No thread feedback found. Submit feedback on threads to see them here.'
                                    : 'Select a feedback item from the list to view details'}
                            </Text>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default ThreadFeedback;

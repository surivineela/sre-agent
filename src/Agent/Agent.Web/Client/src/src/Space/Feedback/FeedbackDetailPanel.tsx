import { Button, makeStyles, shorthands, Text, tokens } from '@fluentui/react-components';
import { Open20Regular, ThumbDislikeRegular, ThumbLikeRegular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { FormattedMessage } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { SreAgentResources } from '../../Strings/SREAgentResources';

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
    feedbackSection: {
        backgroundColor: tokens.colorNeutralBackground2,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('20px'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        marginBottom: '16px',
    },
    sectionTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: '16px',
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
    },
    ratingIcon: {
        fontSize: '20px',
    },
    positive: {
        color: tokens.colorPaletteGreenForeground1,
    },
    negative: {
        color: tokens.colorPaletteRedForeground1,
    },
    feedbackText: {
        fontSize: tokens.fontSizeBase300,
        lineHeight: '1.6',
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
    },
    infoRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        ...shorthands.padding('8px', '0'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        ':last-child': {
            ...shorthands.border('none'),
        },
    },
    infoLabel: {
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
    },
    infoValue: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground1,
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

interface FeedbackDetailPanelProps {
    feedback: ThreadFeedbackData;
}

const FeedbackDetailPanel: FC<FeedbackDetailPanelProps> = ({ feedback }) => {
    const styles = useStyles();
    const navigate = useNavigate();

    const handleGoToThread = useCallback(() => {
        navigate(`/views/activities/threads/${feedback.threadId}`);
    }, [navigate, feedback.threadId]);

    const formatTimestamp = useCallback((timestamp: string) => {
        try {
            const date = new Date(timestamp);
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
    }, []);

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <div className={styles.headerTop}>
                    <Text className={styles.threadTitle}>{feedback.threadTitle}</Text>
                    <Button appearance="subtle" icon={<Open20Regular />} onClick={handleGoToThread}>
                        Open Thread
                    </Button>
                </div>
                <Text className={styles.threadMeta}>Submitted {formatTimestamp(feedback.createdTimestamp)}</Text>
            </div>

            <div className={styles.content}>
                <div className={styles.feedbackSection}>
                    <div className={styles.sectionTitle}>
                        <span className={`${styles.ratingIcon} ${feedback.isPositive ? styles.positive : styles.negative}`}>
                            {feedback.isPositive ? <ThumbLikeRegular /> : <ThumbDislikeRegular />}
                        </span>
                        <Text>{feedback.isPositive ? 'Positive Feedback' : 'Negative Feedback'}</Text>
                    </div>

                    {feedback.feedbackText ? (
                        <Text className={styles.feedbackText}>{feedback.feedbackText}</Text>
                    ) : (
                        <Text className={styles.feedbackText} style={{ fontStyle: 'italic', color: tokens.colorNeutralForeground3 }}>
                            <FormattedMessage {...SreAgentResources.noAdditionalComments} />
                        </Text>
                    )}
                </div>

                <div className={styles.feedbackSection}>
                    <div className={styles.sectionTitle}>
                        <Text>
                            <FormattedMessage {...SreAgentResources.detailsLabel} />
                        </Text>
                    </div>
                    <div className={styles.infoRow}>
                        <Text className={styles.infoLabel}>
                            <FormattedMessage {...SreAgentResources.threadId} />
                        </Text>
                        <Text className={styles.infoValue}>{feedback.threadId}</Text>
                    </div>
                    <div className={styles.infoRow}>
                        <Text className={styles.infoLabel}>
                            <FormattedMessage {...SreAgentResources.feedbackId} />
                        </Text>
                        <Text className={styles.infoValue}>{feedback.messageFeedbackId}</Text>
                    </div>
                    <div className={styles.infoRow}>
                        <Text className={styles.infoLabel}>
                            <FormattedMessage {...SreAgentResources.submitted} />
                        </Text>
                        <Text className={styles.infoValue}>{formatTimestamp(feedback.createdTimestamp)}</Text>
                    </div>
                    <div className={styles.infoRow}>
                        <Text className={styles.infoLabel}>
                            <FormattedMessage {...SreAgentResources.rating} />
                        </Text>
                        <Text className={styles.infoValue}>{feedback.isPositive ? 'Positive 👍' : 'Negative 👎'}</Text>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default FeedbackDetailPanel;

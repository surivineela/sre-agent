import { makeStyles, shorthands, Text, tokens } from '@fluentui/react-components';
import { ThumbDislikeRegular, ThumbLikeRegular } from '@fluentui/react-icons';
import { FC, useCallback } from 'react';
import { FormattedMessage } from 'react-intl';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
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
        ...shorthands.padding('16px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        flexShrink: 0,
    },
    headerText: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
    },
    list: {
        flex: 1,
        minHeight: 0,
        overflowY: 'auto',
        overflowX: 'hidden',
        scrollbarGutter: 'stable',
        ...shorthands.padding('8px'),
    },
    feedbackItem: {
        ...shorthands.padding('12px'),
        ...shorthands.margin('4px', '0'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        cursor: 'pointer',
        ...shorthands.transition('all', '0.2s', 'ease'),
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    feedbackItemSelected: {
        backgroundColor: tokens.colorNeutralBackground1Selected,
        ...shorthands.border('1px', 'solid', tokens.colorBrandBackground),
    },
    feedbackHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        marginBottom: '8px',
    },
    threadTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        flex: 1,
        ...shorthands.overflow('hidden'),
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    feedbackIcon: {
        fontSize: '16px',
        marginLeft: '8px',
        flexShrink: 0,
    },
    positive: {
        color: tokens.colorPaletteGreenForeground1,
    },
    negative: {
        color: tokens.colorPaletteRedForeground1,
    },
    feedbackPreview: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        ...shorthands.overflow('hidden'),
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        marginBottom: '4px',
    },
    timestamp: {
        fontSize: tokens.fontSizeBase100,
        color: tokens.colorNeutralForeground4,
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        height: '100%',
        ...shorthands.padding('24px'),
        textAlign: 'center',
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

interface FeedbackListProps {
    feedbacks: ThreadFeedbackData[];
    selectedFeedbackId: string | null;
    onFeedbackSelect: (feedbackId: string) => void;
}

const FeedbackList: FC<FeedbackListProps> = ({ feedbacks, selectedFeedbackId, onFeedbackSelect }) => {
    const styles = useStyles();
    const { scrollable } = useScrollableComponentStyles();

    const formatTimestamp = useCallback((timestamp: string) => {
        try {
            const date = new Date(timestamp);
            const now = new Date();
            const diffMs = now.getTime() - date.getTime();
            const diffMins = Math.floor(diffMs / 60000);
            const diffHours = Math.floor(diffMs / 3600000);
            const diffDays = Math.floor(diffMs / 86400000);

            if (diffMins < 1) return 'Just now';
            if (diffMins < 60) return `${diffMins}m ago`;
            if (diffHours < 24) return `${diffHours}h ago`;
            if (diffDays < 7) return `${diffDays}d ago`;

            return date.toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
            });
        } catch {
            return 'Unknown';
        }
    }, []);

    if (feedbacks.length === 0) {
        return (
            <div className={styles.container}>
                <div className={styles.emptyState}>
                    <Text size={300} weight="semibold">
                        <FormattedMessage {...SreAgentResources.noFeedbackYet} />
                    </Text>
                    <Text size={200}>
                        <FormattedMessage {...SreAgentResources.feedbackWillAppearHere} />
                    </Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text className={styles.headerText}>Feedback ({feedbacks.length})</Text>
            </div>
            <div className={`${styles.list} ${scrollable}`}>
                {feedbacks.map(feedback => (
                    <div
                        key={feedback.messageFeedbackId}
                        className={`${styles.feedbackItem} ${
                            selectedFeedbackId === feedback.messageFeedbackId ? styles.feedbackItemSelected : ''
                        }`}
                        onClick={() => onFeedbackSelect(feedback.messageFeedbackId)}
                    >
                        <div className={styles.feedbackHeader}>
                            <Text className={styles.threadTitle}>{feedback.threadTitle}</Text>
                            <span className={`${styles.feedbackIcon} ${feedback.isPositive ? styles.positive : styles.negative}`}>
                                {feedback.isPositive ? <ThumbLikeRegular /> : <ThumbDislikeRegular />}
                            </span>
                        </div>
                        {feedback.feedbackText && <Text className={styles.feedbackPreview}>{feedback.feedbackText.substring(0, 100)}</Text>}
                        <Text className={styles.timestamp}>{formatTimestamp(feedback.createdTimestamp)}</Text>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default FeedbackList;

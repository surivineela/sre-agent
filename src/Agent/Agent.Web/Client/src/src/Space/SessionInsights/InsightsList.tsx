import { makeStyles, shorthands, Text, tokens } from '@fluentui/react-components';
import { FC } from 'react';
import { FormattedMessage } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        ...shorthands.overflow('hidden'),
    },
    header: {
        ...shorthands.padding('12px', '16px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        flexShrink: 0,
    },
    headerText: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
    },
    list: {
        flex: 1,
        overflowY: 'auto',
        overflowX: 'hidden',
        ...shorthands.padding('8px'),
    },
    insightCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        ...shorthands.padding('12px'),
        ...shorthands.margin('0', '0', '8px', '0'),
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke2),
        cursor: 'pointer',
        ...shorthands.transition('all', '0.2s'),
        '&:hover': {
            ...shorthands.borderColor(tokens.colorBrandStroke1),
            boxShadow: tokens.shadow4,
        },
    },
    insightCardSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        ...shorthands.borderColor(tokens.colorBrandStroke1),
        '&:hover': {
            backgroundColor: tokens.colorBrandBackground2Hover,
        },
    },
    insightTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        marginBottom: '6px',
        lineHeight: '1.4',
    },
    insightMeta: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        display: 'flex',
        ...shorthands.gap('8px'),
        alignItems: 'center',
    },
    badge: {
        fontSize: tokens.fontSizeBase100,
        ...shorthands.padding('2px', '6px'),
        ...shorthands.borderRadius(tokens.borderRadiusSmall),
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
        fontWeight: tokens.fontWeightSemibold,
    },
    emptyState: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        ...shorthands.padding('24px'),
        textAlign: 'center',
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

interface InsightsListProps {
    insights: SessionInsightData[];
    selectedInsightId: string | null;
    onInsightSelect: (threadId: string) => void;
}

const InsightsList: FC<InsightsListProps> = ({ insights, selectedInsightId, onInsightSelect }) => {
    const styles = useStyles();

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

    if (insights.length === 0) {
        return (
            <div className={styles.container}>
                <div className={styles.header}>
                    <Text className={styles.headerText}>Insights ({insights.length})</Text>
                </div>
                <div className={styles.emptyState}>
                    <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                        <FormattedMessage {...SreAgentResources.noSessionInsightsFound} />
                    </Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text className={styles.headerText}>Insights ({insights.length})</Text>
            </div>
            <div className={styles.list}>
                {insights.map(insight => {
                    const isSelected = insight.threadId === selectedInsightId;
                    return (
                        <div
                            key={insight.threadId}
                            className={`${styles.insightCard} ${isSelected ? styles.insightCardSelected : ''}`}
                            onClick={() => onInsightSelect(insight.threadId)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={e => {
                                if (e.key === 'Enter' || e.key === ' ') {
                                    onInsightSelect(insight.threadId);
                                }
                            }}
                        >
                            <Text className={styles.insightTitle}>{insight.title}</Text>
                            <div className={styles.insightMeta}>
                                <span>{formatDate(insight.generatedTimestamp)}</span>
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default InsightsList;

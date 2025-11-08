import { makeStyles, shorthands, Text, tokens } from '@fluentui/react-components';
import { FC } from 'react';
import { FormattedMessage } from 'react-intl';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SreAgentResources } from '../../Strings/SREAgentResources';

interface ThreadWithInsight extends Thread {
    insightGeneratedTimestamp?: string;
}

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
    },
    headerText: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground2,
    },
    list: {
        flex: 1,
        ...shorthands.overflow('auto'),
        ...shorthands.padding('8px'),
    },
    threadItem: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.padding('12px'),
        ...shorthands.margin('4px', '0'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
        cursor: 'pointer',
        ...shorthands.transition('background-color', '0.2s'),
        '&:hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    threadItemSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        '&:hover': {
            backgroundColor: tokens.colorBrandBackground2Hover,
        },
    },
    threadTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        ...shorthands.overflow('hidden'),
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        marginBottom: '4px',
    },
    threadMeta: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
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

interface ThreadWithInsight extends Thread {
    insightGeneratedTimestamp?: string;
}

interface ThreadsListProps {
    threads: ThreadWithInsight[];
    selectedThreadId: string | null;
    onThreadSelect: (threadId: string) => void;
}

const ThreadsList: FC<ThreadsListProps> = ({ threads, selectedThreadId, onThreadSelect }) => {
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

    if (threads.length === 0) {
        return (
            <div className={styles.container}>
                <div className={styles.header}>
                    <Text className={styles.headerText}>
                        <FormattedMessage {...SreAgentResources.threadsWithInsightsCount} values={{ count: 0 }} />
                    </Text>
                </div>
                <div className={styles.emptyState}>
                    <Text size={300} style={{ color: tokens.colorNeutralForeground3 }}>
                        <FormattedMessage {...SreAgentResources.noThreadsWithInsights} />
                    </Text>
                </div>
            </div>
        );
    }

    return (
        <div className={styles.container}>
            <div className={styles.header}>
                <Text className={styles.headerText}>
                    <FormattedMessage {...SreAgentResources.threadsWithInsightsCount} values={{ count: threads.length }} />
                </Text>
            </div>
            <div className={styles.list}>
                {threads.map(thread => {
                    const isSelected = thread.id === selectedThreadId;
                    return (
                        <div
                            key={thread.id}
                            className={`${styles.threadItem} ${isSelected ? styles.threadItemSelected : ''}`}
                            onClick={() => onThreadSelect(thread.id)}
                            role="button"
                            tabIndex={0}
                            onKeyDown={e => {
                                if (e.key === 'Enter' || e.key === ' ') {
                                    onThreadSelect(thread.id);
                                }
                            }}
                        >
                            <Text className={styles.threadTitle} title={thread.title}>
                                {thread.title}
                            </Text>
                            <Text className={styles.threadMeta}>
                                {thread.insightGeneratedTimestamp || thread.trajectoryGeneratedTimestamp
                                    ? `Generated: ${formatDate(thread.insightGeneratedTimestamp || thread.trajectoryGeneratedTimestamp)}`
                                    : 'Manual insights'}
                            </Text>
                        </div>
                    );
                })}
            </div>
        </div>
    );
};

export default ThreadsList;

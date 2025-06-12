import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentStatus, Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

const ThreadItem = ({
    thread,
    selectThread,
    isActive,
    isThreadUnread,
}: {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    isActive: boolean;
    isThreadUnread: boolean;
}) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();

    const makeTextBold = useMemo(() => {
        return isThreadUnread && !isActive;
    }, [isThreadUnread, isActive]);

    const getIncidentStatus = (thread: Thread) => {
        if (thread.status?.incidentStatus?.status) {
            switch (thread.status?.incidentStatus?.status.toLowerCase()) {
                case IncidentStatus.acknowledged:
                    return intl.formatMessage(SreAgentResources.acknowledged);
                case IncidentStatus.triggered:
                    return intl.formatMessage(SreAgentResources.triggered);
                case IncidentStatus.mitigated:
                    return intl.formatMessage(SreAgentResources.mitigated);
                case IncidentStatus.closed:
                    return intl.formatMessage(SreAgentResources.closed);
                case IncidentStatus.resolved:
                    return intl.formatMessage(SreAgentResources.resolved);
            }
        }
        return intl.formatMessage(SreAgentResources.active);
    };

    return (
        <div
            onClick={() => selectThread(thread)}
            onKeyDown={e => {
                if (e.key.toLowerCase() === 'enter') {
                    selectThread(thread);
                    e.stopPropagation();
                }
            }}
            id={thread.id}
            data-testid={thread.id}
            tabIndex={0}
            role="treeitem"
            className={mergeStyles(ThreadMenuStyles.threadItem, isActive ? ThreadMenuStyles.activeThreadItem : undefined)}
        >
            {isActive && <div className={ThreadMenuStyles.borderIndicator} />}
            <div className={ThreadMenuStyles.content}>
                <div className={styles.threadTitleWithAction}>
                    <Text size={300} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.title}
                    </Text>
                </div>
                {thread.source === ThreadSource.incident ? (
                    <div className={styles.subtitleContainer}>
                        <span className={styles.statusPill}>{getIncidentStatus(thread)}</span>
                        <Text className={styles.subtitle} size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                            {thread.lastMessage?.text}
                        </Text>
                    </div>
                ) : (
                    <Text size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.lastMessage?.text}
                    </Text>
                )}
            </div>
        </div>
    );
};

export default memo(ThreadItem);

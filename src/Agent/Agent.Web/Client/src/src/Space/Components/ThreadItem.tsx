import { mergeStyles } from '@fluentui/react/lib/Styling';
import { Text } from '@fluentui/react/lib/Text';
import { memo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentStatus, Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

const ThreadItem = ({
    thread,
    selectThread,
    isActive,
}: {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    isActive: boolean;
}) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();

    const getIncidentStatus = (thread: Thread) => {
        if (thread.status?.incidentStatus?.status) {
            switch (thread.status?.incidentStatus?.status) {
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
                    <Text as="div" variant="medium" nowrap block>
                        {thread.title}
                    </Text>
                </div>
                {thread.source === ThreadSource.incident ? (
                    <div className={styles.subtitleContainer}>
                        <span className={styles.statusPill}>{getIncidentStatus(thread)}</span>
                        <Text className={styles.subtitle} as="div" variant="small" nowrap block>
                            {thread.lastMessage?.text}
                        </Text>
                    </div>
                ) : (
                    <Text as="div" variant="small" nowrap block>
                        {thread.lastMessage?.text}
                    </Text>
                )}
            </div>
        </div>
    );
};

export default memo(ThreadItem);

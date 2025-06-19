import { Menu, MenuButton, MenuItem, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-components';
import { CopyRegular, Delete20Regular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { memo, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import { IncidentStatus, Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

const ThreadItem = ({
    thread,
    selectThread,
    deleteThread,
    isActive,
    isThreadUnread,
}: {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    isActive: boolean;
    isThreadUnread: boolean;
}) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();
    const { resourceId } = useContext(EnvironmentContext);
    const threadDeepLink = useThreadDeepLink(resourceId, thread.id);

    const [isHovered, setIsHovered] = useState(false);
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

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
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}
            id={thread.id}
            data-testid={thread.id}
            tabIndex={0}
            role="treeitem"
            className={mergeStyles(
                ThreadMenuStyles.threadItem,
                isActive ? ThreadMenuStyles.activeThreadItem : undefined,
                isHovered && !isActive ? ThreadMenuStyles.hoveredThreadItem : undefined
            )}
        >
            {isActive && <div className={ThreadMenuStyles.borderIndicator} />}
            <div className={ThreadMenuStyles.content}>
                <div className={styles.threadTitleWithAction}>
                    <Text size={300} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.title}
                    </Text>
                    <Menu>
                        <MenuTrigger disableButtonEnhancement>
                            <MenuButton
                                appearance="subtle"
                                size="small"
                                icon={<MoreHorizontal20Regular />}
                                onClick={e => {
                                    e.stopPropagation();
                                }}
                                style={{
                                    opacity: isHovered ? 1 : 0,
                                    visibility: isHovered ? 'visible' : 'hidden',
                                }}
                            />
                        </MenuTrigger>
                        <MenuPopover>
                            <MenuList>
                                <MenuItem icon={<CopyRegular />} onClick={() => copyToClipboard(threadDeepLink)}>
                                    {intl.formatMessage(SreAgentResources.copyLinkToThread)}
                                </MenuItem>
                                {deleteThread && (
                                    <MenuItem
                                        icon={<Delete20Regular />}
                                        onClick={e => {
                                            e.stopPropagation();
                                            setIsDeleteDialogOpen(true);
                                        }}
                                    >
                                        {intl.formatMessage(SreAgentResources.delete)}
                                    </MenuItem>
                                )}
                            </MenuList>
                        </MenuPopover>
                    </Menu>
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

            <DeleteThreadDialog
                thread={thread}
                isOpen={isDeleteDialogOpen}
                onOpenChange={setIsDeleteDialogOpen}
                onConfirmDelete={() => deleteThread && deleteThread(thread)}
                source="ThreadItem"
            />
        </div>
    );
};

export default memo(ThreadItem);

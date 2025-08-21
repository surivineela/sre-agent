import { Menu, MenuButton, MenuItem, MenuList, MenuPopover, MenuTrigger } from '@fluentui/react-components';
import { CopyRegular, Delete20Regular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { forwardRef, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import { IncidentStatus } from '../../Common/Contracts/Azure/SreAgent';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

interface IThreadItemProps {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    isActive: boolean;
    isThreadUnread: boolean;
}

const ThreadItem = forwardRef<HTMLDivElement, IThreadItemProps>(({ thread, selectThread, deleteThread, isActive, isThreadUnread }, ref) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();
    const { resourceId } = useContext(EnvironmentContext);
    const { logAmplitudeControlEvent } = useAzPortalContext();
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

    const onSelectThread = useCallback(() => {
        if (isActive) return;

        selectThread(thread);
        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'selectThread',
            targetFriendlyName: 'Select thread',
            valueObjectName: thread.id,
            valueObjectFriendlyName: thread.id,
        });
    }, [logAmplitudeControlEvent, thread, isActive, selectThread]);

    const onConfirmDeleteThread = useCallback(() => {
        if (!deleteThread) return;

        deleteThread(thread);
        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'confirmDeleteThread',
            targetFriendlyName: 'Confirm delete thread',
            valueObjectName: thread.id,
            valueObjectFriendlyName: thread.id,
        });
    }, [logAmplitudeControlEvent, thread, deleteThread]);

    return (
        <div
            ref={ref}
            onClick={() => {
                onSelectThread();
            }}
            onKeyDown={e => {
                if (e.key.toLowerCase() === 'enter') {
                    onSelectThread();
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
                    <Text className={styles.title} size={300} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.title}
                    </Text>
                    <Menu>
                        <MenuTrigger disableButtonEnhancement>
                            <MenuButton
                                appearance="transparent"
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
                                <MenuItem
                                    icon={<CopyRegular />}
                                    onClick={e => {
                                        e.stopPropagation();
                                        copyToClipboard(threadDeepLink);
                                    }}
                                >
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
                        <Text className={styles.title} size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                            {thread.lastMessage?.text}
                        </Text>
                    </div>
                ) : (
                    <Text className={styles.title} size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.lastMessage?.text}
                    </Text>
                )}
            </div>

            <DeleteThreadDialog
                thread={thread}
                isOpen={isDeleteDialogOpen}
                onOpenChange={setIsDeleteDialogOpen}
                onConfirmDelete={() => onConfirmDeleteThread()}
                source="ThreadItem"
            />
        </div>
    );
});

export default memo(ThreadItem);

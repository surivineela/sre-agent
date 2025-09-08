import {
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    useRestoreFocusSource,
    useRestoreFocusTarget,
} from '@fluentui/react-components';
import { CopyRegular, Delete20Regular, MoreHorizontal20Regular, StarOffRegular, StarRegular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import emojiRegex from 'emoji-regex-xs';
import { forwardRef, memo, useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import removeMd from 'remove-markdown';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import { IncidentStatus } from '../../Common/Contracts/Azure/SreAgent';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import Fade from './Fade';

interface IThreadItemProps {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    isActive: boolean;
    isThreadUnread: boolean;
    favorite: boolean;
    updateThreadFavoriteProperty: (threadId: string, isFavorite: boolean) => Promise<void>;
}

const ThreadItem = forwardRef<HTMLDivElement, IThreadItemProps>(
    ({ thread, selectThread, deleteThread, isActive, isThreadUnread, favorite, updateThreadFavoriteProperty }, ref) => {
        const ThreadMenuStyles = useThreadMenuStyle();
        const styles = useActionsStatusBarStyles();
        const intl = useIntl();
        const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
        const { logAmplitudeControlEvent } = useAzPortalContext();
        const threadDeepLink = useThreadDeepLink(thread.id, resourceId, sreAgentEndpoint);

        const [isHovered, setIsHovered] = useState(false);
        const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
        const [isFavoriteSwitchButtonDisabled, setIsFavoriteSwitchButtonDisabled] = useState(false);

        const restoreFocusSourceAttributes = useRestoreFocusSource();
        const restoreFocusTargetAttributes = useRestoreFocusTarget();

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

        const cleanSubTitle = useMemo(() => {
            const subTitle = thread.lastMessage?.text.substring(0, 128) || '';
            const cleanString = removeMd(subTitle);

            // Using emoji-regex-xs package which is a smaller version of emoji-regex to remove emojis from the string.
            // It doesn't encompass all emojis but it is sufficient for our use case and has a smaller bundle size.
            // If we need a more comprehensive solution in the future, we can consider using the full emoji-regex package
            const eRegex = emojiRegex();
            return cleanString.replace(eRegex, '');
        }, [thread.lastMessage]);

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
                onFocus={() => setIsHovered(true)}
                onBlur={() => setIsHovered(false)}
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
                    <Text className={styles.title} size={300} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.title}
                    </Text>
                    {thread.source === ThreadSource.incident ? (
                        <div className={styles.subtitleContainer}>
                            <span className={styles.statusPill}>{getIncidentStatus(thread)}</span>
                            <Text className={styles.title} size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                                {cleanSubTitle}
                            </Text>
                        </div>
                    ) : (
                        <Text className={styles.title} size={200} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                            {cleanSubTitle}
                        </Text>
                    )}
                </div>
                <Fade visible={isHovered} appear={true} unmountOnExit={true}>
                    <div>
                        <Menu>
                            <MenuTrigger disableButtonEnhancement>
                                <MenuButton
                                    appearance="transparent"
                                    size="small"
                                    icon={<MoreHorizontal20Regular />}
                                    onClick={e => {
                                        e.stopPropagation();
                                    }}
                                    {...restoreFocusTargetAttributes}
                                />
                            </MenuTrigger>
                            <MenuPopover>
                                <MenuList>
                                    <MenuItem
                                        disabled={isFavoriteSwitchButtonDisabled}
                                        icon={favorite ? <StarOffRegular /> : <StarRegular />}
                                        onClick={e => {
                                            e.stopPropagation();
                                            setIsFavoriteSwitchButtonDisabled(true);
                                            updateThreadFavoriteProperty(thread.id, !favorite).finally(() =>
                                                setIsFavoriteSwitchButtonDisabled(false)
                                            );
                                        }}
                                    >
                                        {favorite ? (
                                            <FormattedMessage {...ActivitiesResources.removeFromFavorites} />
                                        ) : (
                                            <FormattedMessage {...ActivitiesResources.addToFavorites} />
                                        )}
                                    </MenuItem>
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
                </Fade>

                <DeleteThreadDialog
                    restoreFocusSourceAttributes={restoreFocusSourceAttributes}
                    thread={thread}
                    isOpen={isDeleteDialogOpen}
                    onOpenChange={setIsDeleteDialogOpen}
                    onConfirmDelete={() => onConfirmDeleteThread()}
                    source="ThreadItem"
                />
            </div>
        );
    }
);

export default memo(ThreadItem);

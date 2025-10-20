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
import { CopyRegular, Delete20Regular, EditRegular, MoreHorizontal20Regular, StarOffRegular, StarRegular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { forwardRef, memo, useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import PermissionedMenuItem from '../../Common/Components/PermissionedMenuItem';
import RenameThreadDialog from '../../Common/Components/RenameThreadDialog';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { ActivitiesResources, ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { AgentContext } from '../Contracts/Context';
import { usePermissionContext } from '../Contracts/PermissionContext';
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
        const { updateThreadTitle } = useContext(AgentContext);
        const { logAmplitudeControlEvent } = useAzPortalContext();
        const threadDeepLink = useThreadDeepLink(thread.id, resourceId, sreAgentEndpoint);

        const [isHovered, setIsHovered] = useState(false);
        const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
        const [isThreadRenamingDialogOpen, setIsThreadRenamingDialogOpen] = useState(false);
        const [isFavoriteSwitchButtonDisabled, setIsFavoriteSwitchButtonDisabled] = useState(false);
        const { canDeleteThreads: canDelete, canWriteThreads } = usePermissionContext();

        const restoreFocusSourceAttributes = useRestoreFocusSource();
        const restoreFocusTargetAttributes = useRestoreFocusTarget();

        const makeTextBold = useMemo(() => {
            return isThreadUnread && !isActive;
        }, [isThreadUnread, isActive]);

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
                metadata: {
                    threadId: thread.id,
                    threadType: thread.source ?? 'unknown',
                },
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

        const onUpdateThreadTitle = useCallback(
            (newTitle: string) => {
                setIsThreadRenamingDialogOpen(false);
                updateThreadTitle(thread.id, newTitle);
            },
            [thread.id, updateThreadTitle]
        );

        return (
            <>
                <div
                    ref={ref}
                    onClick={() => onSelectThread()}
                    onKeyDown={e => {
                        if (e.key.toLowerCase() === 'enter') {
                            // Ensure that the event is only triggered when pressing Enter on the container itself, not on its children
                            if (e.target === e.currentTarget) {
                                onSelectThread();
                            }
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
                                        <PermissionedMenuItem
                                            canPerform={canWriteThreads}
                                            disabledReason={isFavoriteSwitchButtonDisabled}
                                            noPermissionTooltip={
                                                <FormattedMessage {...ActivitiesResources.favoriteThreadNoPermissionTooltip} />
                                            }
                                            icon={favorite ? <StarOffRegular /> : <StarRegular />}
                                            onClick={e => {
                                                e.stopPropagation();
                                                if (!canWriteThreads) return;
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
                                        </PermissionedMenuItem>
                                        <MenuItem
                                            icon={<CopyRegular />}
                                            onClick={e => {
                                                e.stopPropagation();
                                                copyToClipboard(threadDeepLink);
                                            }}
                                        >
                                            {intl.formatMessage(SreAgentResources.copyLinkToThread)}
                                        </MenuItem>
                                        <PermissionedMenuItem
                                            {...restoreFocusTargetAttributes}
                                            canPerform={canWriteThreads}
                                            noPermissionTooltip={intl.formatMessage(SreAgentResources.renamePermissionsError)}
                                            icon={<EditRegular />}
                                            onClick={e => {
                                                e.stopPropagation();
                                                setIsThreadRenamingDialogOpen(true);
                                            }}
                                        >
                                            {intl.formatMessage(SreAgentResources.rename)}
                                        </PermissionedMenuItem>
                                        {deleteThread && (
                                            <PermissionedMenuItem
                                                canPerform={canDelete}
                                                noPermissionTooltip={
                                                    <FormattedMessage
                                                        {...ActivitiesThreadHeaderResources.deleteThreadNoPermissionTooltip}
                                                    />
                                                }
                                                icon={<Delete20Regular />}
                                                onClick={e => {
                                                    e.stopPropagation();
                                                    if (canDelete) {
                                                        setIsDeleteDialogOpen(true);
                                                    }
                                                }}
                                            >
                                                {intl.formatMessage(SreAgentResources.delete)}
                                            </PermissionedMenuItem>
                                        )}
                                    </MenuList>
                                </MenuPopover>
                            </Menu>
                        </div>
                    </Fade>
                    <div onClick={e => e.stopPropagation()}>
                        <DeleteThreadDialog
                            restoreFocusSourceAttributes={restoreFocusSourceAttributes}
                            thread={thread}
                            isOpen={isDeleteDialogOpen}
                            onOpenChange={setIsDeleteDialogOpen}
                            onConfirmDelete={() => onConfirmDeleteThread()}
                            source="ThreadItem"
                        />
                    </div>
                </div>
                {/** Keep the dialog out of the menu to prevent unintentional event bubbling to other onClick events*/}
                <RenameThreadDialog
                    thread={thread}
                    isOpen={isThreadRenamingDialogOpen}
                    onOpenChange={setIsThreadRenamingDialogOpen}
                    onUpdateThreadTitle={onUpdateThreadTitle}
                />
            </>
        );
    }
);

export default memo(ThreadItem);

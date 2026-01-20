import {
    Button,
    Menu,
    MenuItem,
    MenuList,
    MenuOpenChangeData,
    MenuOpenEvent,
    MenuPopover,
    MenuTrigger,
    useRestoreFocusSource,
    useRestoreFocusTarget,
} from '@fluentui/react-components';
import {
    CopyRegular,
    DeleteRegular,
    EditRegular,
    InfoRegular,
    MoreHorizontal20Regular,
    StarOffRegular,
    StarRegular,
} from '@fluentui/react-icons';
import { memo, useCallback, useContext, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import PermissionedMenuItem from '../../Common/Components/PermissionedMenuItem';
import RenameThreadDialog from '../../Common/Components/RenameThreadDialog';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { ActivitiesResources, ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { usePermissionContext } from '../Contracts/PermissionContext';
import ThreadInfoDialog from './ThreadInfoDialog';

interface ThreadActionsMenuProps {
    trigger?: JSX.Element | null | ((val: any) => JSX.Element);
    thread: Thread;
    handleThreadDelete: () => void;
    hideCopyDeeplink?: boolean;
    hideDelete?: boolean;
    updateThreadTitle?: (threadId: string, newTitle: string) => void;
    updateThreadFavorite?: (threadId: string, isFavorite: boolean) => void;
    // Leave it undefined if you don't want to control the open state from outside
    open?: boolean;
    // Leave it undefined if you don't want to control the open state from outside
    onOpenChange?: (e: MenuOpenEvent, data: MenuOpenChangeData) => void;
}

const ThreadActionsMenu = ({
    trigger,
    open,
    onOpenChange,
    thread,
    handleThreadDelete,
    hideCopyDeeplink,
    hideDelete,
    updateThreadTitle,
    updateThreadFavorite,
}: ThreadActionsMenuProps) => {
    const intl = useIntl();
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadDeepLink = useThreadDeepLink(thread.id, resourceId, sreAgentEndpoint);

    const [isInfoDialogOpen, setIsInfoDialogOpen] = useState(false);
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
    const [isThreadRenamingDialogOpen, setIsThreadRenamingDialogOpen] = useState(false);
    const [isFavoriteSwitchButtonDisabled, setIsFavoriteSwitchButtonDisabled] = useState(false);

    const onUpdateThreadTitle = useCallback(
        (newTitle: string) => {
            setIsThreadRenamingDialogOpen(false);
            updateThreadTitle?.(thread.id, newTitle);
        },
        [updateThreadTitle, thread.id]
    );

    const onUpdateThreadFavorite = useCallback(
        (isFavorite: boolean) => {
            setIsFavoriteSwitchButtonDisabled(true);
            updateThreadFavorite?.(thread.id, isFavorite);
            setIsFavoriteSwitchButtonDisabled(false);
        },
        [updateThreadFavorite, thread.id]
    );

    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const restoreFocusTargetAttributes = useRestoreFocusTarget();

    const { canDeleteThreads: canDelete, canWriteThreads } = usePermissionContext();

    return (
        <>
            <Menu open={open} onOpenChange={onOpenChange}>
                <MenuTrigger>
                    {trigger || (
                        <Button
                            style={{ marginTop: '3px' }}
                            appearance="transparent"
                            icon={<MoreHorizontal20Regular />}
                            aria-label={intl.formatMessage(SreAgentResources.moreOptions)}
                            {...restoreFocusTargetAttributes}
                        />
                    )}
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        {updateThreadFavorite && (
                            <PermissionedMenuItem
                                canPerform={canWriteThreads}
                                disabledReason={isFavoriteSwitchButtonDisabled}
                                noPermissionTooltip={<FormattedMessage {...ActivitiesResources.favoriteThreadNoPermissionTooltip} />}
                                icon={thread.favorite ? <StarOffRegular /> : <StarRegular />}
                                onClick={() => {
                                    onUpdateThreadFavorite(!thread.favorite);
                                }}
                            >
                                {thread.favorite ? (
                                    <FormattedMessage {...ActivitiesResources.removeFromFavorites} />
                                ) : (
                                    <FormattedMessage {...ActivitiesResources.addToFavorites} />
                                )}
                            </PermissionedMenuItem>
                        )}
                        <MenuItem icon={<InfoRegular />} onClick={() => setIsInfoDialogOpen(true)}>
                            {intl.formatMessage(SreAgentResources.info)}
                        </MenuItem>
                        {!hideCopyDeeplink && (
                            <MenuItem icon={<CopyRegular />} onClick={() => copyToClipboard(threadDeepLink)}>
                                {intl.formatMessage(SreAgentResources.copyLinkToThread)}
                            </MenuItem>
                        )}
                        {updateThreadTitle && (
                            <PermissionedMenuItem
                                {...restoreFocusTargetAttributes}
                                canPerform={canWriteThreads}
                                noPermissionTooltip={intl.formatMessage(SreAgentResources.renamePermissionsError)}
                                icon={<EditRegular />}
                                onClick={() => {
                                    setIsThreadRenamingDialogOpen(true);
                                }}
                            >
                                {intl.formatMessage(SreAgentResources.rename)}
                            </PermissionedMenuItem>
                        )}
                        {!hideDelete && (
                            <PermissionedMenuItem
                                canPerform={canDelete}
                                noPermissionTooltip={intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadNoPermissionTooltip)}
                                icon={<DeleteRegular />}
                                onClick={() => setIsDeleteDialogOpen(true)}
                            >
                                {intl.formatMessage(SreAgentResources.delete)}
                            </PermissionedMenuItem>
                        )}
                    </MenuList>
                </MenuPopover>
            </Menu>

            <ThreadInfoDialog
                thread={thread}
                isOpen={isInfoDialogOpen}
                onClose={() => setIsInfoDialogOpen(false)}
                resourceId={resourceId}
                sreAgentEndpoint={sreAgentEndpoint}
                restoreFocusSourceAttributes={restoreFocusSourceAttributes}
            />
            <RenameThreadDialog
                thread={thread}
                isOpen={isThreadRenamingDialogOpen}
                onOpenChange={setIsThreadRenamingDialogOpen}
                onUpdateThreadTitle={onUpdateThreadTitle}
            />
            <DeleteThreadDialog
                restoreFocusSourceAttributes={restoreFocusSourceAttributes}
                thread={thread}
                isOpen={isDeleteDialogOpen}
                onOpenChange={setIsDeleteDialogOpen}
                onConfirmDelete={handleThreadDelete}
                source="ThreadActionsMenu"
            />
        </>
    );
};

export default memo(ThreadActionsMenu);

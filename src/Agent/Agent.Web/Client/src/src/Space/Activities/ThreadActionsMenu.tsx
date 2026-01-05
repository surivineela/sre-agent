import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    Menu,
    MenuItem,
    MenuList,
    MenuOpenChangeData,
    MenuOpenEvent,
    MenuPopover,
    MenuTrigger,
    tokens,
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
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import CopyButton from '../../Common/Components/CopyButton';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import { useDialogStyles } from '../../Common/Components/Dialog.styles';
import PermissionedMenuItem from '../../Common/Components/PermissionedMenuItem';
import RenameThreadDialog from '../../Common/Components/RenameThreadDialog';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { copyToClipboard } from '../../Common/Helpers/Clipboard';
import { useThreadDeepLink } from '../../Common/Hooks/useThreadDeepLink';
import { ActivitiesResources, ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { usePermissionContext } from '../Contracts/PermissionContext';

const useStyles = makeStyles({
    infoContent: {
        fontFamily: 'SF Mono, Monaco, Inconsolata, "Roboto Mono", Consolas, "Courier New", monospace',
        fontSize: '13px',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '16px',
        borderRadius: tokens.borderRadiusMedium,
        overflowX: 'auto',
        whiteSpace: 'pre-wrap',
        wordWrap: 'break-word',
        lineHeight: '1.4',
    },
    threadIdHighlight: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
        padding: '2px 6px',
        borderRadius: tokens.borderRadiusSmall,
        fontWeight: '600',
        display: 'inline-flex',
        alignItems: 'center',
        gap: '8px',
        marginBottom: '12px',
    },
    section: {
        marginBottom: '16px',
    },
    sectionTitle: {
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        marginBottom: '8px',
        fontSize: '14px',
    },
});

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
    const { infoContent, threadIdHighlight, section, sectionTitle } = useStyles();
    const { dialogSurface } = useDialogStyles();
    const intl = useIntl();
    const { resourceId, isCrossTenantPortalMode, sreAgentEndpoint } = useContext(EnvironmentContext);
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

    const formattedThreadInfoText = useMemo(() => {
        const createdDate = new Date(thread.createdTimestamp).toLocaleDateString();
        const modifiedDate = new Date(thread.modifiedTimestamp).toLocaleDateString();
        const sourceValue = thread.source || intl.formatMessage(SreAgentResources.NA);

        const lines = [
            thread.title,
            `${intl.formatMessage(SreAgentResources.created)}: ${createdDate}`,
            `${intl.formatMessage(SreAgentResources.modified)}: ${modifiedDate}`,
            `${intl.formatMessage(SreAgentResources.source)}: ${sourceValue}`,
        ];

        if (resourceId) {
            lines.push(`${intl.formatMessage(SreAgentResources.agentId)}: ${resourceId}`);
        }

        if (sreAgentEndpoint) {
            lines.push(`${intl.formatMessage(SreAgentResources.agentEndpoint)}: ${sreAgentEndpoint}`);
        }

        lines.push('', `${intl.formatMessage(SreAgentResources.threadId)}: ${thread.id}`);

        return lines.join('\n');
    }, [intl, resourceId, sreAgentEndpoint, thread.createdTimestamp, thread.modifiedTimestamp, thread.source, thread.title, thread.id]);

    const renderInfoContent = () => (
        <div>
            <div className={threadIdHighlight}>
                <span>{thread.id}</span>
                <CopyButton textToCopy={thread.id} buttonAppearance="transparent" />
            </div>

            <div className={section}>
                <div className={sectionTitle}>{thread.title}</div>
                <div>
                    {intl.formatMessage(SreAgentResources.created)} {new Date(thread.createdTimestamp).toLocaleDateString()}
                </div>
                <div>
                    {intl.formatMessage(SreAgentResources.modified)} {new Date(thread.modifiedTimestamp).toLocaleDateString()}
                </div>
                {thread.source && (
                    <div>
                        {intl.formatMessage(SreAgentResources.source)}: {thread.source}
                    </div>
                )}
                {resourceId && (
                    <div>
                        {intl.formatMessage(SreAgentResources.agentId)}: {resourceId}
                    </div>
                )}
                {sreAgentEndpoint && (
                    <div>
                        {intl.formatMessage(SreAgentResources.agentEndpoint)}: {sreAgentEndpoint}
                    </div>
                )}
            </div>

            {(thread.status?.actionsStatus?.hasCriticalActions || thread.status?.actionsStatus?.hasWarningActions) && (
                <div className={section}>
                    <div className={sectionTitle}>{intl.formatMessage(SreAgentResources.actions)}</div>
                    {thread.status?.actionsStatus?.hasCriticalActions && (
                        <div>🔴 {intl.formatMessage(SreAgentResources.criticalActionsPresent)}</div>
                    )}
                    {thread.status?.actionsStatus?.hasWarningActions && (
                        <div>🟡 {intl.formatMessage(SreAgentResources.warningActionsPresent)}</div>
                    )}
                </div>
            )}

            {thread.status?.incidentStatus?.incidentId && (
                <div className={section}>
                    <div className={sectionTitle}>{intl.formatMessage(SreAgentResources.incident)}</div>
                    <div>
                        {intl.formatMessage(SreAgentResources.idLabel)}: {thread.status.incidentStatus.incidentId}
                    </div>
                    {thread.status.incidentStatus.status && (
                        <div>
                            {intl.formatMessage(SreAgentResources.status)}: {thread.status.incidentStatus.status}
                        </div>
                    )}
                </div>
            )}
        </div>
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
                        {!isCrossTenantPortalMode && !hideCopyDeeplink && (
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

            {/* Thread info Dialog */}
            <Dialog open={isInfoDialogOpen} onOpenChange={(_, data) => setIsInfoDialogOpen(data.open)} {...restoreFocusSourceAttributes}>
                <DialogSurface mountNode={{ className: dialogSurface }}>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(SreAgentResources.threadInfo)}</DialogTitle>
                        <DialogContent>
                            <div className={infoContent}>{renderInfoContent()}</div>
                        </DialogContent>
                        <DialogActions>
                            <CopyButton textToCopy={formattedThreadInfoText} buttonAppearance="secondary" showCopyText />
                            <Button appearance="primary" onClick={() => setIsInfoDialogOpen(false)}>
                                {intl.formatMessage(SreAgentResources.close)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
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

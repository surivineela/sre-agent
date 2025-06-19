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
    MenuPopover,
    MenuTrigger,
    tokens,
} from '@fluentui/react-components';
import { DeleteRegular, InfoRegular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { memo, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import CopyButton from '../../Common/Components/CopyButton';
import DeleteThreadDialog from '../../Common/Components/DeleteThreadDialog';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { SreAgentResources } from '../../Strings/SREAgentResources';

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
    thread: Thread;
    handleThreadDelete: () => void;
}

const ThreadActionsMenu = ({ thread, handleThreadDelete }: ThreadActionsMenuProps) => {
    const { infoContent, threadIdHighlight, section, sectionTitle } = useStyles();
    const intl = useIntl();

    const [isInfoDialogOpen, setIsInfoDialogOpen] = useState(false);
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);

    const formattedThreadInfoText = useMemo(() => {
        const created = new Date(thread.createdTimestamp).toLocaleDateString();
        const modified = new Date(thread.modifiedTimestamp).toLocaleDateString();

        return `${thread.title}
Created: ${created}
Modified: ${modified}
Source: ${thread.source || 'N/A'}

Thread ID: ${thread.id}`;
    }, [thread]);

    const renderInfoContent = () => (
        <div>
            <div className={threadIdHighlight}>
                <span>{thread.id}</span>
                <CopyButton textToCopy={thread.id} buttonAppearance="transparent" />
            </div>

            <div className={section}>
                <div className={sectionTitle}>{thread.title}</div>
                <div>Created {new Date(thread.createdTimestamp).toLocaleDateString()}</div>
                <div>Modified {new Date(thread.modifiedTimestamp).toLocaleDateString()}</div>
                {thread.source && <div>Source: {thread.source}</div>}
            </div>

            {(thread.status?.actionsStatus?.hasCriticalActions || thread.status?.actionsStatus?.hasWarningActions) && (
                <div className={section}>
                    <div className={sectionTitle}>Actions</div>
                    {thread.status?.actionsStatus?.hasCriticalActions && <div>🔴 Critical actions present</div>}
                    {thread.status?.actionsStatus?.hasWarningActions && <div>🟡 Warning actions present</div>}
                </div>
            )}

            {thread.status?.incidentStatus?.incidentId && (
                <div className={section}>
                    <div className={sectionTitle}>Incident</div>
                    <div>ID: {thread.status.incidentStatus.incidentId}</div>
                    {thread.status.incidentStatus.status && <div>Status: {thread.status.incidentStatus.status}</div>}
                </div>
            )}
        </div>
    );

    return (
        <>
            <Menu>
                <MenuTrigger>
                    <Button
                        style={{ marginTop: '3px' }}
                        appearance="transparent"
                        icon={<MoreHorizontal20Regular />}
                        aria-label={intl.formatMessage(SreAgentResources.moreOptions)}
                    />
                </MenuTrigger>
                <MenuPopover>
                    <MenuList>
                        <MenuItem icon={<InfoRegular />} onClick={() => setIsInfoDialogOpen(true)}>
                            {intl.formatMessage(SreAgentResources.info)}
                        </MenuItem>
                        <MenuItem icon={<DeleteRegular />} onClick={() => setIsDeleteDialogOpen(true)}>
                            {intl.formatMessage(SreAgentResources.delete)}
                        </MenuItem>
                    </MenuList>
                </MenuPopover>
            </Menu>

            {/* Thread info Dialog */}
            <Dialog open={isInfoDialogOpen} onOpenChange={(_, data) => setIsInfoDialogOpen(data.open)}>
                <DialogSurface>
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

            <DeleteThreadDialog
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

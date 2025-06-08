import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    makeStyles,
    Menu,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    tokens,
} from '@fluentui/react-components';
import { CopyRegular, DeleteRegular, InfoRegular, MoreHorizontal20Regular } from '@fluentui/react-icons';
import { memo, useState } from 'react';
import { useIntl } from 'react-intl';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    dangerButton: {
        backgroundColor: tokens.colorStatusDangerBackground3,
        color: `${tokens.colorNeutralForegroundInverted} !important`,
        ':hover': {
            backgroundColor: tokens.colorStatusDangerBackground3Hover,
        },
        ':active': {
            backgroundColor: tokens.colorStatusDangerBackground3Pressed,
        },
    },
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
    threadIdCopyButton: {
        minWidth: '24px',
        height: '24px',
        padding: '0',
        fontSize: '12px',
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
    const { dangerButton, infoContent, threadIdHighlight, threadIdCopyButton, section, sectionTitle } = useStyles();
    const intl = useIntl();
    const [isInfoDialogOpen, setIsInfoDialogOpen] = useState(false);
    const [copied, setCopied] = useState(false);

    const formatThreadInfo = (thread: Thread) => {
        const created = new Date(thread.createdTimestamp).toLocaleDateString();
        const modified = new Date(thread.modifiedTimestamp).toLocaleDateString();

        return `${thread.title}
Created: ${created}
Modified: ${modified}
Source: ${thread.source || 'N/A'}

Thread ID: ${thread.id}`;
    };

    const handleCopyToClipboard = async () => {
        try {
            await navigator.clipboard.writeText(formatThreadInfo(thread));
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
        } catch (err) {
            console.error('Failed to copy to clipboard:', err);
        }
    };

    const handleCopyThreadId = async () => {
        try {
            await navigator.clipboard.writeText(thread.id);
        } catch (err) {
            console.error('Failed to copy thread ID to clipboard:', err);
        }
    };

    const renderInfoContent = () => (
        <div>
            <div className={threadIdHighlight}>
                <span>{thread.id}</span>
                <Button
                    className={threadIdCopyButton}
                    icon={<CopyRegular />}
                    onClick={handleCopyThreadId}
                    appearance="transparent"
                    size="small"
                    title="Copy Thread ID"
                />
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
            {/* Delete Dialog */}
            <Dialog modalType="alert">
                <Menu>
                    <MenuTrigger>
                        <Button
                            style={{ display: 'inline-block' }}
                            appearance="transparent"
                            icon={<MoreHorizontal20Regular />}
                            aria-label="More options"
                        />
                    </MenuTrigger>
                    <MenuPopover>
                        <MenuList>
                            <MenuItem icon={<InfoRegular />} onClick={() => setIsInfoDialogOpen(true)}>
                                Info
                            </MenuItem>
                            <DialogTrigger disableButtonEnhancement>
                                <MenuItem icon={<DeleteRegular />}>Delete</MenuItem>
                            </DialogTrigger>
                        </MenuList>
                    </MenuPopover>
                </Menu>

                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogTitle)}</DialogTitle>
                        <DialogContent>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogDescription)}</DialogContent>
                        <DialogActions>
                            <DialogTrigger>
                                <Button className={dangerButton} onClick={() => handleThreadDelete()}>
                                    {intl.formatMessage(SreAgentResources.yes)}
                                </Button>
                            </DialogTrigger>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary">{intl.formatMessage(SreAgentResources.no)}</Button>
                            </DialogTrigger>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>

            {/* Thread Info Dialog */}
            <Dialog open={isInfoDialogOpen} onOpenChange={(_, data) => setIsInfoDialogOpen(data.open)}>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>Thread Info</DialogTitle>
                        <DialogContent>
                            <div className={infoContent}>{renderInfoContent()}</div>
                        </DialogContent>
                        <DialogActions>
                            <Button
                                icon={<CopyRegular />}
                                onClick={handleCopyToClipboard}
                                appearance="secondary"
                                style={{
                                    color: copied ? '#16a34a' : undefined,
                                    transition: 'color 0.2s',
                                }}
                            >
                                {copied ? 'Copied!' : 'Copy'}
                            </Button>
                            <Button appearance="primary" onClick={() => setIsInfoDialogOpen(false)}>
                                {intl.formatMessage(SreAgentResources.closed)}
                            </Button>
                        </DialogActions>
                    </DialogBody>
                </DialogSurface>
            </Dialog>
        </>
    );
};

export default memo(ThreadActionsMenu);

import {
    Button,
    Caption1,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Divider,
    makeStyles,
    Text,
    tokens,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { ErrorCircle16Regular, Warning16Regular } from '@fluentui/react-icons';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import CopyButton from '../../Common/Components/CopyButton';
import { useDialogStyles } from '../../Common/Components/Dialog.styles';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SreAgentResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    infoContent: {
        overflowX: 'auto',
    },
    infoGrid: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        gap: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
        alignItems: 'center',
    },
    label: {
        color: tokens.colorNeutralForeground3,
    },
    value: {
        wordBreak: 'break-word',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalS}`,
        borderRadius: tokens.borderRadiusSmall,
    },
    valueWithCopy: {
        wordBreak: 'break-word',
        backgroundColor: tokens.colorNeutralBackground2,
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS} ${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
        borderRadius: tokens.borderRadiusSmall,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
    },
    threadIdValue: {
        wordBreak: 'break-word',
        backgroundColor: tokens.colorBrandBackground2,
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS} ${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
        borderRadius: tokens.borderRadiusSmall,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: tokens.spacingHorizontalS,
    },
    section: {
        marginTop: tokens.spacingVerticalM,
    },
    sectionTitle: {
        marginBottom: tokens.spacingVerticalS,
        fontWeight: 600,
    },
    statusItem: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginBottom: tokens.spacingVerticalXS,
    },
    criticalIcon: {
        color: tokens.colorPaletteRedForeground1,
    },
    warningIcon: {
        color: tokens.colorPaletteYellowForeground1,
    },
    divider: {
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalM,
    },
});

interface ThreadInfoDialogProps {
    thread: Thread;
    isOpen: boolean;
    onClose: () => void;
    resourceId?: string;
    sreAgentEndpoint?: string;
    restoreFocusSourceAttributes?: ReturnType<typeof useRestoreFocusSource>;
}

export const ThreadInfoDialog: FC<ThreadInfoDialogProps> = ({
    thread,
    isOpen,
    onClose,
    resourceId,
    sreAgentEndpoint,
    restoreFocusSourceAttributes,
}) => {
    const styles = useStyles();
    const { dialogSurface } = useDialogStyles();
    const intl = useIntl();

    const formattedThreadInfoText = useMemo(() => {
        const createdDate = new Date(thread.createdTimestamp).toLocaleDateString();
        const modifiedDate = new Date(thread.modifiedTimestamp).toLocaleDateString();
        const sourceValue = thread.source || intl.formatMessage(SreAgentResources.NA);

        const lines = [
            `${intl.formatMessage(SreAgentResources.threadId)}: ${thread.id}`,
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

        return lines.join('\n');
    }, [intl, resourceId, sreAgentEndpoint, thread.createdTimestamp, thread.modifiedTimestamp, thread.source, thread.id]);

    const hasActionsStatus =
        thread.status?.actionsStatus?.hasCriticalActions || thread.status?.actionsStatus?.hasWarningActions;
    const hasIncidentStatus = thread.status?.incidentStatus?.incidentId;

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && onClose()} {...restoreFocusSourceAttributes}>
            <DialogSurface mountNode={{ className: dialogSurface }}>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(SreAgentResources.threadInfo)}</DialogTitle>
                    <DialogContent>
                        <div className={styles.infoContent}>
                            <div className={styles.infoGrid}>
                                <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.threadId)}</Caption1>
                                <div className={styles.threadIdValue}>
                                    <Text>{thread.id}</Text>
                                    <CopyButton textToCopy={thread.id} buttonAppearance="transparent" />
                                </div>

                                <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.created)}</Caption1>
                                <Text className={styles.value}>{new Date(thread.createdTimestamp).toLocaleDateString()}</Text>

                                <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.modified)}</Caption1>
                                <Text className={styles.value}>{new Date(thread.modifiedTimestamp).toLocaleDateString()}</Text>

                                {thread.source && (
                                    <>
                                        <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.source)}</Caption1>
                                        <Text className={styles.value}>{thread.source}</Text>
                                    </>
                                )}

                                {resourceId && (
                                    <>
                                        <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.agentId)}</Caption1>
                                        <div className={styles.valueWithCopy}>
                                            <Text>{resourceId}</Text>
                                            <CopyButton textToCopy={resourceId} buttonAppearance="transparent" />
                                        </div>
                                    </>
                                )}

                                {sreAgentEndpoint && (
                                    <>
                                        <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.agentEndpoint)}</Caption1>
                                        <div className={styles.valueWithCopy}>
                                            <Text>{sreAgentEndpoint}</Text>
                                            <CopyButton textToCopy={sreAgentEndpoint} buttonAppearance="transparent" />
                                        </div>
                                    </>
                                )}
                            </div>

                            {hasActionsStatus && (
                                <>
                                    <Divider className={styles.divider} />
                                    <div className={styles.section}>
                                        <Text className={styles.sectionTitle}>{intl.formatMessage(SreAgentResources.actions)}</Text>
                                        {thread.status?.actionsStatus?.hasCriticalActions && (
                                            <div
                                                className={styles.statusItem}
                                                aria-label={intl.formatMessage(SreAgentResources.criticalActionsPresent)}
                                            >
                                                <ErrorCircle16Regular className={styles.criticalIcon} />
                                                <Text>{intl.formatMessage(SreAgentResources.criticalActionsPresent)}</Text>
                                            </div>
                                        )}
                                        {thread.status?.actionsStatus?.hasWarningActions && (
                                            <div
                                                className={styles.statusItem}
                                                aria-label={intl.formatMessage(SreAgentResources.warningActionsPresent)}
                                            >
                                                <Warning16Regular className={styles.warningIcon} />
                                                <Text>{intl.formatMessage(SreAgentResources.warningActionsPresent)}</Text>
                                            </div>
                                        )}
                                    </div>
                                </>
                            )}

                            {hasIncidentStatus && (
                                <>
                                    <Divider className={styles.divider} />
                                    <div className={styles.section}>
                                        <Text className={styles.sectionTitle}>{intl.formatMessage(SreAgentResources.incident)}</Text>
                                        <div className={styles.infoGrid}>
                                            <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.idLabel)}</Caption1>
                                            <Text className={styles.value}>{thread.status?.incidentStatus?.incidentId}</Text>
                                            {thread.status?.incidentStatus?.status && (
                                                <>
                                                    <Caption1 className={styles.label}>{intl.formatMessage(SreAgentResources.status)}</Caption1>
                                                    <Text className={styles.value}>{thread.status.incidentStatus.status}</Text>
                                                </>
                                            )}
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <CopyButton textToCopy={formattedThreadInfoText} buttonAppearance="secondary" showCopyText />
                        <Button appearance="primary" onClick={onClose}>
                            {intl.formatMessage(SreAgentResources.close)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

export default ThreadInfoDialog;

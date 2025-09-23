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
    tokens,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread, ThreadSource } from '../Contracts/DataPlane/Thread';
import { useDialogStyles } from './Dialog.styles';

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
});

interface DeleteThreadDialogProps {
    thread: Thread;
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    onConfirmDelete: () => void;
    source: 'ThreadItem' | 'ThreadActionsMenu';
    restoreFocusSourceAttributes: ReturnType<typeof useRestoreFocusSource>;
}

const DeleteThreadDialog = ({
    thread,
    isOpen,
    onOpenChange,
    onConfirmDelete,
    source,
    restoreFocusSourceAttributes,
}: DeleteThreadDialogProps) => {
    const { dangerButton } = useStyles();
    const { dialogSurface } = useDialogStyles();
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);

    const handleConfirmedDelete = useCallback(() => {
        azPortalContext.log({
            action: 'deleteThread',
            actionModifier: 'started',
            logLevel: 'info',
            resourceId: thread.id,
            data: {
                source,
            },
        });
        onConfirmDelete();
        onOpenChange(false);
    }, [azPortalContext, onConfirmDelete, onOpenChange, thread.id, source]);

    const { title, description } = useMemo(() => {
        switch (thread.source) {
            case ThreadSource.incident:
                return {
                    title: ActivitiesThreadHeaderResources.deleteIncidentDialogTitle,
                    description: ActivitiesThreadHeaderResources.deleteIncidentDialogDescription,
                };
            case ThreadSource.dailyReport:
                return {
                    title: ActivitiesThreadHeaderResources.deleteReportDialogTitle,
                    description: ActivitiesThreadHeaderResources.deleteReportDialogDescription,
                };
            default:
                return {
                    title: ActivitiesThreadHeaderResources.deleteThreadDialogTitle,
                    description: ActivitiesThreadHeaderResources.deleteThreadDialogDescription,
                };
        }
    }, [thread.source]);

    return (
        <Dialog modalType="alert" open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)} {...restoreFocusSourceAttributes}>
            <DialogSurface mountNode={{ className: dialogSurface }}>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(title)}</DialogTitle>
                    <DialogContent>{intl.formatMessage(description)}</DialogContent>
                    <DialogActions>
                        <DialogTrigger>
                            <Button className={dangerButton} onClick={handleConfirmedDelete}>
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
    );
};

export default DeleteThreadDialog;

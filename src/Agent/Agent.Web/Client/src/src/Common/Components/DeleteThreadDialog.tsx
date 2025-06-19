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
} from '@fluentui/react-components';
import { useContext } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesThreadHeaderResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread } from '../Contracts/Azure/SreAgent';

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
}

const DeleteThreadDialog = ({ thread, isOpen, onOpenChange, onConfirmDelete, source }: DeleteThreadDialogProps) => {
    const { dangerButton } = useStyles();
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);

    const handleConfirmedDelete = () => {
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
    };

    return (
        <Dialog modalType="alert" open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogTitle)}</DialogTitle>
                    <DialogContent>{intl.formatMessage(ActivitiesThreadHeaderResources.deleteThreadDialogDescription)}</DialogContent>
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

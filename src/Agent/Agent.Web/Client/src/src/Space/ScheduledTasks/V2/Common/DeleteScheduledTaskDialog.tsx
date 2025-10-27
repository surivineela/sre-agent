import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    tokens,
} from '@fluentui/react-components';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';

interface DeleteScheduledTaskDialogProps {
    dialogTrigger?: React.ReactElement;
    deleteTasks: () => void;
}

export const DeleteScheduledTaskDialog: FC<DeleteScheduledTaskDialogProps> = ({ dialogTrigger, deleteTasks }) => {
    const intl = useIntl();

    const dialogSurface = useMemo(() => {
        return (
            <DialogSurface aria-labelledby="delete-dialog-title" aria-describedby="delete-dialog-content">
                <DialogBody>
                    <DialogTitle id="delete-dialog-title">
                        {intl.formatMessage(ScheduledTasksResources.deleteScheduledTasksConfirmationTitle)}
                    </DialogTitle>
                    <DialogContent id="delete-dialog-content">
                        {intl.formatMessage(ScheduledTasksResources.deleteScheduledTasksConfirmationMessage)}
                    </DialogContent>
                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button
                                appearance="primary"
                                style={{ backgroundColor: tokens.colorStatusDangerBackground3 }}
                                onClick={deleteTasks}
                            >
                                {intl.formatMessage(SreAgentResources.delete)}
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">{intl.formatMessage(SreAgentResources.cancel)}</Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        );
    }, [deleteTasks, intl]);

    // If there is no dialogTrigger, the trigger is external. Dialog wrapper is not needed, as it should already be provided.
    return dialogTrigger ? (
        <Dialog>
            <DialogTrigger disableButtonEnhancement>{dialogTrigger}</DialogTrigger>
            {dialogSurface}
        </Dialog>
    ) : (
        dialogSurface
    );
};

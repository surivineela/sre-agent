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
import { SreAgentResources } from '../../../Strings/SREAgentResources';

interface ScheduledTaskDeleteDialogProps {
    dialogTrigger?: React.ReactElement;
    deleteTasks: () => void;
    title: string;
    content: string;
}

export const ScheduledTaskDeleteDialog: FC<ScheduledTaskDeleteDialogProps> = ({ dialogTrigger, deleteTasks, title, content }) => {
    const intl = useIntl();

    const dialogSurface = useMemo(() => {
        return (
            <DialogSurface aria-labelledby="delete-dialog-title" aria-describedby="delete-dialog-content">
                <DialogBody>
                    <DialogTitle id="delete-dialog-title">{title}</DialogTitle>
                    <DialogContent id="delete-dialog-content">{content}</DialogContent>
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
    }, [content, deleteTasks, intl, title]);

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

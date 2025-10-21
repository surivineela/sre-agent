import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
} from '@fluentui/react-components';
import { Formik } from 'formik';
import { FC, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ScheduledTask } from '../../../Contracts/ScheduledTasks';
import { ScheduledTasksContext } from '../Hooks/ScheduledTasksContext';
import { useScheduledTaskSettings } from '../Hooks/useScheduledTaskSettings';
import { ScheduledTaskFormProps } from '../ScheduledTasksUtilities';
import { ScheduledTaskForm } from './ScheduledTaskForm';

interface CreateOrEditScheduledTaskDialog {
    dialogTrigger: React.ReactElement;
    mode: ScheduledTaskDialogMode;
    scheduledTask?: ScheduledTask;
}

export enum ScheduledTaskDialogMode {
    Create,
    Edit,
}

export const CreateOrEditScheduledTaskDialog: FC<CreateOrEditScheduledTaskDialog> = ({ dialogTrigger, mode, scheduledTask }) => {
    const intl = useIntl();
    const { refreshTasks } = useContext(ScheduledTasksContext);
    const { initialValues, save: saveScheduledTaskSettings } = useScheduledTaskSettings(
        mode === ScheduledTaskDialogMode.Edit ? scheduledTask : undefined
    );
    const [isDialogOpen, setIsDialogOpen] = useState<boolean>(false);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)} modalType="alert">
            <DialogTrigger disableButtonEnhancement>{dialogTrigger}</DialogTrigger>

            <Formik<ScheduledTaskFormProps>
                initialValues={initialValues}
                onSubmit={values => {
                    saveScheduledTaskSettings(values).then(response => {
                        if (response.isSuccessful) {
                            setIsDialogOpen(false);
                            refreshTasks();
                        }
                    });
                }}
            >
                {({ submitForm }) => {
                    return (
                        <DialogSurface
                            style={{ minWidth: 'fit-content' }}
                            aria-labelledby="task-dialog-title"
                            aria-describedby="task-dialog-content"
                        >
                            <DialogBody>
                                <DialogTitle id="task-dialog-title">
                                    {intl.formatMessage(ScheduledTasksResources.createAScheduledTask)}
                                </DialogTitle>
                                <DialogContent id="task-dialog-content">
                                    <ScheduledTaskForm />
                                </DialogContent>
                                <DialogActions>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="primary" onClick={submitForm}>
                                            {intl.formatMessage(ScheduledTasksResources.createTask)}
                                        </Button>
                                    </DialogTrigger>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary">{intl.formatMessage(SreAgentResources.cancel)}</Button>
                                    </DialogTrigger>
                                </DialogActions>
                            </DialogBody>
                        </DialogSurface>
                    );
                }}
            </Formik>
        </Dialog>
    );
};

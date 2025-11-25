import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik } from 'formik';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../Contracts/ExtendedAgentGraph';
import { ScheduledTask } from '../../Contracts/ScheduledTasks';
import { ScheduledTasksContext } from '../Hooks/ScheduledTasksContext';
import { useScheduledTaskSettings } from '../Hooks/useScheduledTaskSettings';
import { ScheduledTaskFormProps } from '../ScheduledTasksUtilities';
import { ScheduledTaskForm } from './ScheduledTaskForm';

interface ScheduledTaskCreateOrEditDialog {
    dialogTrigger?: React.ReactElement;
    isDialogOpen: boolean;
    setIsDialogOpen: (open: boolean) => void;
    mode: ScheduledTaskDialogMode;
    scheduledTask?: ScheduledTask;
    agents?: ExtendedAgent[];
    startingAgent?: string;
}

export enum ScheduledTaskDialogMode {
    Create,
    Edit,
}

export const ScheduledTaskCreateOrEditDialog: FC<ScheduledTaskCreateOrEditDialog> = ({
    dialogTrigger,
    isDialogOpen,
    setIsDialogOpen,
    mode,
    scheduledTask,
    agents,
    startingAgent,
}) => {
    const intl = useIntl();
    const { refreshTasks, isOperationInProgress } = useContext(ScheduledTasksContext);
    const {
        initialValues,
        validationSchema,
        save: saveScheduledTaskSettings,
    } = useScheduledTaskSettings(mode, mode === ScheduledTaskDialogMode.Edit ? scheduledTask : undefined, startingAgent);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)} modalType="alert">
            {dialogTrigger ? <DialogTrigger disableButtonEnhancement>{dialogTrigger}</DialogTrigger> : <></>}

            <Formik<ScheduledTaskFormProps>
                initialValues={initialValues}
                validationSchema={validationSchema}
                onSubmit={values => {
                    saveScheduledTaskSettings(values).then(response => {
                        if (response?.isSuccessful) {
                            setIsDialogOpen(false);
                            if (mode === ScheduledTaskDialogMode.Create) {
                                refreshTasks(
                                    values.subAgent
                                        ? { entityType: 'Agent', entityName: values.subAgent }
                                        : { entityType: 'Trigger', entityName: values.name }
                                );
                            } else {
                                refreshTasks({ entityType: 'Trigger', entityName: values.name });
                            }
                        }
                    });
                }}
            >
                {({ submitForm, dirty, isValid }) => {
                    return (
                        <DialogSurface
                            style={{ minWidth: 'fit-content' }}
                            aria-labelledby="task-dialog-title"
                            aria-describedby="task-dialog-content"
                        >
                            <DialogBody>
                                <DialogTitle
                                    id="task-dialog-title"
                                    action={
                                        <ToolbarButton
                                            aria-label={intl.formatMessage(SreAgentResources.close)}
                                            appearance="transparent"
                                            icon={<Dismiss24Regular />}
                                            onClick={() => setIsDialogOpen(false)}
                                        />
                                    }
                                >
                                    {mode === ScheduledTaskDialogMode.Create
                                        ? intl.formatMessage(ScheduledTasksResources.createAScheduledTask)
                                        : intl.formatMessage(ScheduledTasksResources.editAScheduledTask)}
                                </DialogTitle>
                                <DialogContent id="task-dialog-content">
                                    <ScheduledTaskForm agents={agents} />
                                </DialogContent>
                                <DialogActions>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button
                                            appearance="primary"
                                            onClick={submitForm}
                                            disabled={!dirty || !isValid || isOperationInProgress}
                                        >
                                            {mode === ScheduledTaskDialogMode.Create
                                                ? intl.formatMessage(ScheduledTasksResources.createTask)
                                                : intl.formatMessage(SreAgentResources.save)}
                                        </Button>
                                    </DialogTrigger>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary" onClick={e => e.stopPropagation()}>
                                            {intl.formatMessage(SreAgentResources.cancel)}
                                        </Button>
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

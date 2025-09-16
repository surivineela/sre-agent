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
import { Add16Regular, ArrowClockwise16Regular, Delete16Regular, Pause16Regular, Play16Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask } from '../Contracts/ScheduledTasks';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';

export interface ScheduledTasksToolbarProps {
    onRefreshClick: () => void;
    onNewTaskClick: () => void;
    onDeleteTaskClick: () => void;
    onPauseResumeTaskClick: () => void;
    selectedTask?: ScheduledTask;
    loading?: boolean;
}

const ScheduledTasksToolbar: FC<ScheduledTasksToolbarProps> = ({
    onRefreshClick,
    onNewTaskClick,
    onDeleteTaskClick,
    onPauseResumeTaskClick,
    selectedTask,
    loading = false,
}) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();

    const isTaskSelected = !!selectedTask;
    const isTaskActive = selectedTask?.status === 'Active';
    const isTaskPaused = selectedTask?.status === 'Paused';

    return (
        <div className={styles.toolbar}>
            <Button icon={<Add16Regular />} appearance="transparent" className={styles.button} onClick={onNewTaskClick} disabled={loading}>
                {intl.formatMessage(ScheduledTasksResources.createScheduledTask)}
            </Button>
            <Button
                icon={<ArrowClockwise16Regular />}
                appearance="transparent"
                className={styles.button}
                onClick={onRefreshClick}
                disabled={loading}
            >
                {intl.formatMessage({ defaultMessage: 'Refresh', id: 'rELDbB' })}
            </Button>
            <div className={styles.divider} />

            {/* Pause/Resume Button */}
            {(isTaskActive || isTaskPaused) && (
                <Button
                    icon={isTaskActive ? <Pause16Regular /> : <Play16Regular />}
                    appearance="transparent"
                    className={styles.button}
                    onClick={onPauseResumeTaskClick}
                    disabled={!isTaskSelected || loading}
                >
                    {isTaskActive ? intl.formatMessage(ScheduledTasksResources.pause) : intl.formatMessage(ScheduledTasksResources.resume)}
                </Button>
            )}

            {/* Delete Button with Confirmation */}
            <Dialog modalType="alert">
                <DialogTrigger disableButtonEnhancement>
                    <Button
                        icon={<Delete16Regular />}
                        appearance="transparent"
                        className={styles.button}
                        disabled={!isTaskSelected || loading}
                    >
                        {intl.formatMessage(SreAgentResources.delete)}
                    </Button>
                </DialogTrigger>
                <DialogSurface>
                    <DialogBody>
                        <DialogTitle>{intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmation)}</DialogTitle>
                        <DialogContent>
                            {selectedTask && (
                                <>Are you sure you want to delete the scheduled task "{selectedTask.name}"? This action cannot be undone.</>
                            )}
                        </DialogContent>
                        <DialogActions>
                            <DialogTrigger>
                                <Button className={styles.dangerButton} onClick={onDeleteTaskClick}>
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
        </div>
    );
};

export default ScheduledTasksToolbar;

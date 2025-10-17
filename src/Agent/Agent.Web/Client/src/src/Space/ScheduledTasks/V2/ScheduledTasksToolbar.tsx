import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    SearchBox,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import {
    AddRegular,
    ArrowClockwise20Regular,
    ArrowClockwiseRegular,
    ArrowRightRegular,
    DeleteRegular,
    PlayRegular,
    RecordStopRegular,
    ReplayRegular,
} from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { getLocaleTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';
import { TaskStatusFilterKey } from './ScheduledTasksUtilities';

interface ScheduledTasksToolbarProps {
    selectedTasks?: ScheduledTask[];
    isLoading?: boolean;
    searchQuery: string;
    setSearchQuery: (query: string) => void;
    statusFilter: TaskStatusFilterKey;
    setStatusFilter: (status: TaskStatusFilterKey) => void;
}

export const ScheduledTasksToolbar: FC<ScheduledTasksToolbarProps> = ({
    selectedTasks,
    isLoading = false,
    searchQuery,
    setSearchQuery,
    statusFilter,
    setStatusFilter,
}) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const { refreshTasks, pauseTask, resumeTask, deleteTask } = useContext(ScheduledTasksContext);
    const [lastUpdated, setLastUpdated] = useState<string>();

    const isPauseButtonDisabled = useMemo(() => {
        const hasActiveTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Active);
        return !hasActiveTaskSelected || isLoading;
    }, [isLoading, selectedTasks]);

    const isResumeButtonDisabled = useMemo(() => {
        const hasPausedTaskSelected = selectedTasks?.some(task => task.status === ScheduledTaskStatus.Paused);
        return !hasPausedTaskSelected || isLoading;
    }, [isLoading, selectedTasks]);

    const isRunTaskNowButtonDisabled = useMemo(() => selectedTasks?.length === 0 || isLoading, [isLoading, selectedTasks?.length]);

    const isDeleteButtonDisabled = useMemo(() => selectedTasks?.length === 0 || isLoading, [isLoading, selectedTasks]);

    const onCreateTask = useCallback(() => {
        // TODO: Open create task panel
    }, []);

    const onCreateTaskFromTemplate = useCallback(() => {}, []);

    const onRefresh = useCallback(async () => {
        await refreshTasks();
    }, [refreshTasks]);

    const onPauseTasks = useCallback(async () => {
        const activeTasks = selectedTasks?.filter(task => task.status === ScheduledTaskStatus.Active) || [];
        // start notification ?
        const responses = await Promise.all(activeTasks.map(task => pauseTask(task.id)));
        if (responses.every(response => response.isSuccessful)) {
            await refreshTasks();
        }
    }, [pauseTask, refreshTasks, selectedTasks]);

    const onResumeTasks = useCallback(async () => {
        const pausedTasks = selectedTasks?.filter(task => task.status === ScheduledTaskStatus.Paused) || [];
        // start notification ?
        const responses = await Promise.all(pausedTasks.map(task => resumeTask(task.id)));
        if (responses.every(response => response.isSuccessful)) {
            await refreshTasks();
        }
    }, [resumeTask, refreshTasks, selectedTasks]);

    const onRunTasksNow = useCallback(async () => {
        // TODO: Implement triggering task manually
    }, []);

    const onDeleteTasks = useCallback(async () => {
        // start notification ?
        const responses = await Promise.all(selectedTasks?.map(task => deleteTask(task.id)) || []);
        if (responses.every(response => response.isSuccessful)) {
            await refreshTasks();
        }
    }, [deleteTask, refreshTasks, selectedTasks]);

    useEffect(() => {
        if (!isLoading) {
            setLastUpdated(getLocaleTimeHHMM(new Date()));
        }
    }, [isLoading]);

    return (
        <div className={styles.toolbar}>
            <div style={{ display: 'flex', gap: '12px' }}>
                <Toolbar style={{ padding: 0 }}>
                    <ToolbarButton className={styles.toolbarButton} icon={<AddRegular />} onClick={onCreateTask}>
                        {intl.formatMessage(ScheduledTasksResources.createTask)}
                    </ToolbarButton>
                    <ToolbarButton className={styles.toolbarButton} icon={<ArrowRightRegular />} onClick={onCreateTaskFromTemplate}>
                        {intl.formatMessage(ScheduledTasksResources.createFromTemplate)}
                    </ToolbarButton>
                    <ToolbarButton className={styles.toolbarButton} icon={<ArrowClockwiseRegular />} onClick={onRefresh}>
                        {intl.formatMessage(ScheduledTasksResources.updateList)}
                    </ToolbarButton>
                    <ToolbarDivider />
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<RecordStopRegular />}
                        onClick={onPauseTasks}
                        disabled={isPauseButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOff)}
                    </ToolbarButton>
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<ReplayRegular />}
                        onClick={onResumeTasks}
                        disabled={isResumeButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.turnOn)}
                    </ToolbarButton>
                    <ToolbarButton
                        className={styles.toolbarButton}
                        icon={<PlayRegular />}
                        onClick={onRunTasksNow}
                        disabled={isRunTaskNowButtonDisabled}
                    >
                        {intl.formatMessage(ScheduledTasksResources.runTaskNow)}
                    </ToolbarButton>
                    <DeleteToolbarButtonAndDialog deleteTasks={onDeleteTasks} disabled={isDeleteButtonDisabled} />
                </Toolbar>
                <ScheduledTasksFilters
                    searchQuery={searchQuery}
                    setSearchQuery={setSearchQuery}
                    statusFilter={statusFilter}
                    setStatusFilter={setStatusFilter}
                />
            </div>
            {lastUpdated && (
                <div className={styles.menuItems}>
                    <ArrowClockwise20Regular />
                    <Text>{`${intl.formatMessage(ScheduledTasksResources.lastUpdated)}: ${lastUpdated}`}</Text>
                </div>
            )}
        </div>
    );
};

interface DeleteToolbarButtonAndDialogProps {
    deleteTasks: () => void;
    disabled: boolean;
}

const DeleteToolbarButtonAndDialog: FC<DeleteToolbarButtonAndDialogProps> = ({ disabled, deleteTasks }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const [isDialogOpen, setIsDialogOpen] = useState<boolean>(false);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogTrigger disableButtonEnhancement>
                <ToolbarButton className={styles.toolbarButton} icon={<DeleteRegular />} disabled={disabled}>
                    {intl.formatMessage(SreAgentResources.delete)}
                </ToolbarButton>
            </DialogTrigger>
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
                            <Button appearance="primary" onClick={deleteTasks}>
                                {intl.formatMessage(SreAgentResources.delete)}
                            </Button>
                        </DialogTrigger>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary">{intl.formatMessage(SreAgentResources.cancel)}</Button>
                        </DialogTrigger>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

interface ScheduledTasksFiltersProps {
    searchQuery: string;
    setSearchQuery: (query: string) => void;
    statusFilter: TaskStatusFilterKey;
    setStatusFilter: (status: TaskStatusFilterKey) => void;
}

const ScheduledTasksFilters: FC<ScheduledTasksFiltersProps> = ({ searchQuery, setSearchQuery, statusFilter, setStatusFilter }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();

    const statusOptions = useMemo(
        () => [
            {
                key: TaskStatusFilterKey.All,
                label: intl.formatMessage(ScheduledTasksResources.all),
            },
            {
                key: TaskStatusFilterKey.On,
                label: intl.formatMessage(ScheduledTasksResources.on),
            },
            {
                key: TaskStatusFilterKey.Off,
                label: intl.formatMessage(ScheduledTasksResources.off),
            },
            {
                key: TaskStatusFilterKey.Completed,
                label: intl.formatMessage(ScheduledTasksResources.completed),
            },
        ],
        [intl]
    );

    return (
        <div className={styles.filters}>
            <SearchBox
                value={searchQuery}
                onChange={(_, data) => setSearchQuery(data.value)}
                placeholder={intl.formatMessage(ScheduledTasksResources.filterTasks)}
            />
            <PillFilter
                label={`${intl.formatMessage(SreAgentResources.status)}`}
                filterType="combobox"
                options={statusOptions}
                selectedKeys={[statusFilter]}
                onApply={keys => {
                    setStatusFilter(keys[0] as TaskStatusFilterKey);
                }}
            />
        </div>
    );
};

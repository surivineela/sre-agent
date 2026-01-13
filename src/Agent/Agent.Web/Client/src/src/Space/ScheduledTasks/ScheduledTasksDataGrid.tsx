import {
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridProps,
    DataGridRow,
    Dialog,
    DialogTrigger,
    makeStyles,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    mergeClasses,
    OnSelectionChangeData,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    Text,
    tokens,
} from '@fluentui/react-components';
import { DeleteRegular, EditRegular, MoreHorizontalRegular, PauseRegular, PlayRegular, ReplayRegular } from '@fluentui/react-icons';
import { Link } from '@fluentui/react/lib/Link';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../Common/Clients/ArmClient';
import { getHumanReadableCronExpression } from '../../Common/Helpers/CronExpression';
import { getLocaleDateTimeHHMM } from '../../Common/Helpers/Date';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { ScheduledTasksResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../Contracts/ScheduledTasks';
import { ScheduledTaskCreateOrEditDialog, ScheduledTaskDialogMode } from './Common/ScheduledTaskCreateOrEditDialog';
import { ScheduledTaskDeleteDialog } from './Common/ScheduledTaskDeleteDialog';
import ScheduledTaskStatusBadge from './Common/ScheduledTaskStatusBadge';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';

enum ScheduledTaskDataGridColumns {
    name = 'name',
    actions = 'actions',
    status = 'status',
    schedule = 'schedule',
    createdBy = 'createdBy',
    lastRun = 'lastRun',
    nextRun = 'nextRun',
    runs = 'runs',
}

interface ScheduledTasksDataGridProps {
    scheduledTasks: ScheduledTask[];
    isScheduledTasksLoading: boolean; // TODO: Use it when loading state will be implemented
    selectedTaskIds: string[];
    setSelectedTaskIds: (tasks: string[]) => void;
    onTaskClick?: (task: ScheduledTask) => void;
}

const useStyle = makeStyles({
    dataGrid: {
        maxWidth: '100%',
        overflowX: 'auto',
    },
    dataGridHeader: {
        fontWeight: '600',
        position: 'sticky',
        top: '0',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: '1',
    },
});

export const ScheduledTasksDataGrid: FC<ScheduledTasksDataGridProps> = ({
    scheduledTasks,
    selectedTaskIds,
    setSelectedTaskIds,
    onTaskClick,
}) => {
    const intl = useIntl();
    const styles = useStyle();
    const { scrollable } = useScrollableComponentStyles();

    const azPortalContext = useContext(AzPortalContext);
    const { refreshTasks, pauseTask, resumeTask, runTask, deleteTask, getTaskExecutions, isOperationInProgress, setIsOperationInProgress } =
        useContext(ScheduledTasksContext);

    const [editingTask, setEditingTask] = useState<ScheduledTask | null>(null);
    const [isEditDialogOpen, setIsEditDialogOpen] = useState(false);

    const onSelectionChange: DataGridProps['onSelectionChange'] = useCallback(
        (_: any, data: OnSelectionChangeData) => {
            setSelectedTaskIds(Array.from(data.selectedItems) as string[]);
        },
        [setSelectedTaskIds]
    );

    const onPauseTask = useCallback(
        async (task: ScheduledTask) => {
            const { id, name } = task;

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationTitleSingle),
                intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationInProgressSingle, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await pauseTask(id);
                if (response.isSuccessful) {
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationSuccessSingle, { name: name ?? id })
                    );
                    await refreshTasks();
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationError, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.pauseScheduledTaskNotificationError, {
                        errorMessage: getErrorMessageOrStringify(error),
                    })
                );
            } finally {
                setIsOperationInProgress(false);
            }
        },
        [azPortalContext, intl, setIsOperationInProgress, pauseTask, refreshTasks]
    );

    const onResumeTask = useCallback(
        async (task: ScheduledTask) => {
            const { id, name } = task;

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationTitleSingle),
                intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationInProgressSingle, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await resumeTask(id);
                if (response.isSuccessful) {
                    await refreshTasks();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationSuccessSingle, { name: name ?? id })
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationError, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.resumeScheduledTaskNotificationError, {
                        errorMessage: getErrorMessageOrStringify(error),
                    })
                );
            } finally {
                setIsOperationInProgress(false);
            }
        },
        [azPortalContext, intl, setIsOperationInProgress, resumeTask, refreshTasks]
    );

    const onRunTask = useCallback(
        async (task: ScheduledTask) => {
            const { id, name } = task;

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationTitleSingle),
                intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationInProgressSingle, { name: name ?? id })
            );

            console.log(await getTaskExecutions(id));

            setIsOperationInProgress(true);
            const response = await runTask(id);
            if (response.isSuccessful) {
                await refreshTasks();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationSuccessSingle, { name: name ?? id })
                );
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.runScheduledTaskNotificationError, { errorMessage: response.error })
                );
            }
            setIsOperationInProgress(false);
        },
        [azPortalContext, intl, getTaskExecutions, setIsOperationInProgress, runTask, refreshTasks]
    );

    const onDeleteTask = useCallback(
        async (task: ScheduledTask) => {
            const { id, name } = task;

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleSingle),
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationInProgressSingle, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await deleteTask(id);
                if (response.isSuccessful) {
                    await refreshTasks();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationSuccessSingle, { name: name ?? id })
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationError, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationError, {
                        errorMessage: getErrorMessageOrStringify(error),
                    })
                );
            } finally {
                setIsOperationInProgress(false);
            }
        },
        [azPortalContext, intl, setIsOperationInProgress, deleteTask, refreshTasks]
    );

    const onEditTask = useCallback((task: ScheduledTask) => {
        setEditingTask(task);
        setIsEditDialogOpen(true);
    }, []);

    const onRenderName = useCallback(
        (item: ScheduledTask) => {
            return <Link onClick={() => onTaskClick?.(item)}>{item.name}</Link>;
        },
        [onTaskClick]
    );

    const onRenderActions = useCallback(
        (item: ScheduledTask) => {
            return (
                <Dialog>
                    <Menu>
                        <MenuTrigger>
                            <MenuButton appearance="transparent" icon={<MoreHorizontalRegular />} disabled={isOperationInProgress} />
                        </MenuTrigger>

                        <MenuPopover>
                            <MenuList>
                                <MenuItem icon={<EditRegular />} onClick={() => onEditTask(item)}>
                                    {intl.formatMessage(ScheduledTasksResources.editTask)}
                                </MenuItem>
                                {item.status !== ScheduledTaskStatus.Completed &&
                                    (item.status === ScheduledTaskStatus.Active ? (
                                        <MenuItem icon={<PauseRegular />} onClick={() => onPauseTask(item)}>
                                            {intl.formatMessage(ScheduledTasksResources.turnOff)}
                                        </MenuItem>
                                    ) : (
                                        <MenuItem icon={<ReplayRegular />} onClick={() => onResumeTask(item)}>
                                            {intl.formatMessage(ScheduledTasksResources.turnOn)}
                                        </MenuItem>
                                    ))}
                                <MenuItem icon={<PlayRegular />} onClick={() => onRunTask(item)}>
                                    {intl.formatMessage(ScheduledTasksResources.runTaskNow)}
                                </MenuItem>
                                <DialogTrigger disableButtonEnhancement>
                                    <MenuItem icon={<DeleteRegular />}>{intl.formatMessage(SreAgentResources.delete)}</MenuItem>
                                </DialogTrigger>
                            </MenuList>
                        </MenuPopover>
                    </Menu>

                    <ScheduledTaskDeleteDialog
                        deleteTasks={() => onDeleteTask(item)}
                        title={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleSingle)}
                        content={intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmationDescriptionSingle)}
                    />
                </Dialog>
            );
        },
        [intl, isOperationInProgress, onDeleteTask, onEditTask, onPauseTask, onResumeTask, onRunTask]
    );

    const onRenderStatus = useCallback((item: ScheduledTask) => {
        const status = item.status;
        return <ScheduledTaskStatusBadge status={status} />;
    }, []);

    const onRenderSchedule = useCallback(
        (item: ScheduledTask) => {
            return getHumanReadableCronExpression(item.cronExpression, intl);
        },
        [intl]
    );

    const onRenderCreatedBy = useCallback(
        (item: ScheduledTask) => {
            return item.createdBy === 'api' ? intl.formatMessage(SreAgentResources.agent) : item.createdBy;
        },
        [intl]
    );

    const onRenderLastRun = useCallback(
        (item: ScheduledTask) => {
            return item.lastExecutionTime
                ? getLocaleDateTimeHHMM(new Date(item.lastExecutionTime))
                : intl.formatMessage(SreAgentResources.never);
        },
        [intl]
    );

    const onRenderNextRun = useCallback(
        (item: ScheduledTask) => {
            if (!item.nextExecutionTime || item.status !== ScheduledTaskStatus.Active) {
                return intl.formatMessage(SreAgentResources.notScheduled);
            }

            const date = new Date(item.nextExecutionTime);
            return getLocaleDateTimeHHMM(date);
        },
        [intl]
    );

    const onRenderRuns = useCallback((item: ScheduledTask) => {
        return `${item.executionCount}`;
    }, []);

    const columns: TableColumnDefinition<ScheduledTask>[] = useMemo(
        () => [
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.name,
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.name)}</Text>,
                renderCell: onRenderName,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.actions,
                renderHeaderCell: () => '',
                renderCell: onRenderActions,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.status,
                compare: (a, b) => a.status.localeCompare(b.status),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.taskStatus)}</Text>,
                renderCell: onRenderStatus,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.schedule,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.schedule)}</Text>,
                renderCell: onRenderSchedule,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.createdBy,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.createdBy)}</Text>,
                renderCell: onRenderCreatedBy,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.lastRun,
                compare: (a, b) => {
                    const aTime = a.lastExecutionTime ? new Date(a.lastExecutionTime).getTime() : 0;
                    const bTime = b.lastExecutionTime ? new Date(b.lastExecutionTime).getTime() : 0;
                    return aTime - bTime;
                },
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.lastRun)}</Text>,
                renderCell: onRenderLastRun,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.nextRun,
                compare: (a, b) => {
                    const aTime = a.nextExecutionTime ? new Date(a.nextExecutionTime).getTime() : 0;
                    const bTime = b.nextExecutionTime ? new Date(b.nextExecutionTime).getTime() : 0;
                    return aTime - bTime;
                },
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.nextRun)}</Text>,
                renderCell: onRenderNextRun,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.runs,
                compare: (a, b) => a.executionCount - b.executionCount,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.completedRuns)}</Text>,
                renderCell: onRenderRuns,
            }),
        ],
        [
            intl,
            onRenderActions,
            onRenderCreatedBy,
            onRenderLastRun,
            onRenderName,
            onRenderNextRun,
            onRenderRuns,
            onRenderSchedule,
            onRenderStatus,
        ]
    );

    return (
        <>
            <DataGrid
                items={scheduledTasks || []}
                columns={columns}
                sortable
                resizableColumns
                columnSizingOptions={columnSizingOptions}
                selectionMode="multiselect"
                selectedItems={selectedTaskIds}
                onSelectionChange={onSelectionChange}
                getRowId={item => item.id}
                className={mergeClasses(styles.dataGrid, scrollable)}
                style={{ minWidth: 'unset' }}
            >
                <DataGridHeader className={styles.dataGridHeader}>
                    <DataGridRow
                        selectionCell={{
                            checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectAllRowsAriaLabel) },
                        }}
                    >
                        {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
                    </DataGridRow>
                </DataGridHeader>
                <DataGridBody<ScheduledTask>>
                    {({ item, rowId }) => (
                        <DataGridRow<ScheduledTask>
                            key={rowId}
                            selectionCell={{
                                checkboxIndicator: { 'aria-label': intl.formatMessage(SreAgentResources.selectRowAriaLabel) },
                            }}
                        >
                            {({ renderCell, columnId }) => (
                                <DataGridCell focusMode={getCellFocusMode(columnId)} onClick={e => e.stopPropagation()}>
                                    <TableCellLayout truncate>{renderCell(item)}</TableCellLayout>
                                </DataGridCell>
                            )}
                        </DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>

            {editingTask && (
                <ScheduledTaskCreateOrEditDialog
                    isDialogOpen={isEditDialogOpen}
                    setIsDialogOpen={open => {
                        setIsEditDialogOpen(open);
                        if (!open) {
                            setEditingTask(null);
                        }
                    }}
                    mode={ScheduledTaskDialogMode.Edit}
                    scheduledTask={editingTask}
                />
            )}
        </>
    );
};

const columnSizingOptions = {
    name: {
        minWidth: 300,
        defaultWidth: 350,
    },
    actions: {
        minWidth: 50,
        defaultWidth: 50,
    },
    status: {
        minWidth: 100,
        defaultWidth: 150,
    },
    schedule: {
        minWidth: 200,
        defaultWidth: 250,
    },
    createdBy: {
        minWidth: 200,
        defaultWidth: 250,
    },
    lastRun: {
        minWidth: 150,
        defaultWidth: 200,
    },
    nextRun: {
        minWidth: 150,
        defaultWidth: 200,
    },
    runs: {
        minWidth: 75,
        defaultWidth: 75,
    },
};

const getCellFocusMode = (columnId: TableColumnId) => {
    switch (columnId) {
        case ScheduledTaskDataGridColumns.name:
        case ScheduledTaskDataGridColumns.actions:
            return 'none';
        default:
            return 'cell';
    }
};

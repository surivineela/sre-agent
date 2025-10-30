import {
    Badge,
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
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    OnSelectionChangeData,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    Text,
} from '@fluentui/react-components';
import { DeleteRegular, MoreHorizontalRegular, PauseRegular, ReplayRegular } from '@fluentui/react-icons';
import { Link } from '@fluentui/react/lib/Link';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../../Common/Clients/ArmClient';
import { getLocaleDateTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { CreateOrEditScheduledTaskDialog, ScheduledTaskDialogMode } from './Common/CreateOrEditScheduledTaskDialog';
import { DeleteScheduledTaskDialog } from './Common/DeleteScheduledTaskDialog';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { getHumanReadableCronExpression } from './ScheduledTasksUtilities';

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
}

export const ScheduledTasksDataGrid: FC<ScheduledTasksDataGridProps> = ({ scheduledTasks, selectedTaskIds, setSelectedTaskIds }) => {
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);
    const { refreshTasks, pauseTask, resumeTask, deleteTask, isOperationInProgress, setIsOperationInProgress } =
        useContext(ScheduledTasksContext);

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
                intl.formatMessage(ScheduledTasksResources.pauseTaskTitle),
                intl.formatMessage(ScheduledTasksResources.pauseTaskInProgress, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await pauseTask(id);
                if (response.isSuccessful) {
                    await refreshTasks();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.taskPausedSuccessfully)
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.failedToPauseTask, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToPauseTask, { errorMessage: getErrorMessageOrStringify(error) })
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
                intl.formatMessage(ScheduledTasksResources.resumeTaskTitle),
                intl.formatMessage(ScheduledTasksResources.resumeTaskInProgress, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await resumeTask(id);
                if (response.isSuccessful) {
                    await refreshTasks();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.taskResumedSuccessfully)
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.failedToResumeTask, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToResumeTask, { errorMessage: getErrorMessageOrStringify(error) })
                );
            } finally {
                setIsOperationInProgress(false);
            }
        },
        [azPortalContext, intl, setIsOperationInProgress, resumeTask, refreshTasks]
    );

    const onDeleteTask = useCallback(
        async (task: ScheduledTask) => {
            const { id, name } = task;

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.deleteTaskTitle),
                intl.formatMessage(ScheduledTasksResources.deleteTaskInProgress, { name: name ?? id })
            );

            try {
                setIsOperationInProgress(true);
                const response = await deleteTask(id);
                if (response.isSuccessful) {
                    await refreshTasks();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.taskDeletedSuccessfully)
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.failedToDeleteTask, { errorMessage: response.error })
                    );
                }
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ScheduledTasksResources.failedToDeleteTask, { errorMessage: getErrorMessageOrStringify(error) })
                );
            } finally {
                setIsOperationInProgress(false);
            }
        },
        [azPortalContext, intl, setIsOperationInProgress, deleteTask, refreshTasks]
    );

    const onRenderName = useCallback((item: ScheduledTask) => {
        return (
            <CreateOrEditScheduledTaskDialog
                dialogTrigger={<Link>{item.name}</Link>}
                mode={ScheduledTaskDialogMode.Edit}
                scheduledTask={item}
            />
        );
    }, []);

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
                                <DialogTrigger disableButtonEnhancement>
                                    <MenuItem icon={<DeleteRegular />}>{intl.formatMessage(SreAgentResources.delete)}</MenuItem>
                                </DialogTrigger>
                            </MenuList>
                        </MenuPopover>
                    </Menu>

                    <DeleteScheduledTaskDialog
                        deleteTasks={() => onDeleteTask(item)}
                        title={intl.formatMessage(ScheduledTasksResources.deleteTaskConfirmationTitle)}
                        content={intl.formatMessage(ScheduledTasksResources.deleteTaskConfirmationMessage, { name: item.name ?? item.id })}
                    />
                </Dialog>
            );
        },
        [intl, isOperationInProgress, onDeleteTask, onPauseTask, onResumeTask]
    );

    const onRenderStatus = useCallback(
        (item: ScheduledTask) => {
            const status = item.status;
            switch (status) {
                case ScheduledTaskStatus.Active:
                    return (
                        <Badge appearance="tint" color="success">
                            {intl.formatMessage(ScheduledTasksResources.on)}
                        </Badge>
                    );
                case ScheduledTaskStatus.Paused:
                    return (
                        <Badge appearance="tint" color="severe">
                            {intl.formatMessage(ScheduledTasksResources.off)}
                        </Badge>
                    );
                case ScheduledTaskStatus.Completed:
                    return (
                        <Badge appearance="tint" color="informative">
                            {intl.formatMessage(ScheduledTasksResources.completed)}
                        </Badge>
                    );
                default:
                    return (
                        <Badge appearance="tint" color="informative">
                            {status}
                        </Badge>
                    );
            }
        },
        [intl]
    );

    const onRenderSchedule = useCallback(
        (item: ScheduledTask) => {
            return getHumanReadableCronExpression(item.cronExpression, intl);
        },
        [intl]
    );

    const onRenderCreatedBy = useCallback(
        (item: ScheduledTask) => {
            return item.createdBy === 'Sub-Agent Builder' ? intl.formatMessage(SreAgentResources.agent) : item.createdBy;
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
        >
            <DataGridHeader>
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

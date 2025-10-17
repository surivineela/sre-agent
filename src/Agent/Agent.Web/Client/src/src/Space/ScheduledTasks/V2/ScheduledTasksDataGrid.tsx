import {
    Badge,
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridProps,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
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
import { DeleteRegular, MoreHorizontalRegular, PauseRegular, PlayRegular, ReplayRegular } from '@fluentui/react-icons';
import { Link } from '@fluentui/react/lib/Link';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { getLocaleDateTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { ScheduledTasksContext } from './Hooks/ScheduledTasksContext';
import { useScheduledTasksStyles } from './ScheduledTasks.styles';

enum ScheduledTaskDataGridColumns {
    name = 'name',
    actions = 'actions',
    status = 'status',
    schedule = 'schedule',
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
    const styles = useScheduledTasksStyles();
    const { refreshTasks, pauseTask, resumeTask, deleteTask } = useContext(ScheduledTasksContext);

    const onSelectionChange: DataGridProps['onSelectionChange'] = useCallback(
        (_: any, data: OnSelectionChangeData) => {
            setSelectedTaskIds(Array.from(data.selectedItems) as string[]);
        },
        [setSelectedTaskIds]
    );

    const onPauseTask = useCallback(
        async (id: string) => {
            const response = await pauseTask(id);
            if (response.isSuccessful) {
                await refreshTasks();
            }
        },
        [pauseTask, refreshTasks]
    );

    const onResumeTask = useCallback(
        async (id: string) => {
            const response = await resumeTask(id);
            if (response.isSuccessful) {
                await refreshTasks();
            }
        },
        [refreshTasks, resumeTask]
    );

    const onRunTaskNow = useCallback(async () => {
        // TODO: Implement triggering task manually
    }, []);

    const onDeleteTask = useCallback(
        async (id: string) => {
            const response = await deleteTask(id);
            if (response.isSuccessful) {
                await refreshTasks();
            }
        },
        [deleteTask, refreshTasks]
    );

    const onRenderName = useCallback((item: ScheduledTask) => {
        // TODO: Replace onClick to task details page
        return (
            <TableCellLayout truncate>
                <Link onClick={() => {}}>{item.name}</Link>
            </TableCellLayout>
        );
    }, []);

    const onRenderActions = useCallback(
        (item: ScheduledTask) => {
            return (
                <Dialog>
                    <Menu>
                        <MenuTrigger>
                            <MenuButton appearance="transparent" icon={<MoreHorizontalRegular />} />
                        </MenuTrigger>

                        <MenuPopover>
                            <MenuList>
                                {item.status !== ScheduledTaskStatus.Completed &&
                                    (item.status === ScheduledTaskStatus.Active ? (
                                        <MenuItem icon={<PauseRegular />} onClick={() => onPauseTask(item.id)}>
                                            {intl.formatMessage(ScheduledTasksResources.turnOff)}
                                        </MenuItem>
                                    ) : (
                                        <MenuItem icon={<ReplayRegular />} onClick={() => onResumeTask(item.id)}>
                                            {intl.formatMessage(ScheduledTasksResources.turnOn)}
                                        </MenuItem>
                                    ))}
                                <MenuItem icon={<PlayRegular />} onClick={() => onRunTaskNow()}>
                                    {intl.formatMessage(ScheduledTasksResources.runTaskNow)}
                                </MenuItem>
                                <DialogTrigger disableButtonEnhancement>
                                    <MenuItem icon={<DeleteRegular />}>{intl.formatMessage(SreAgentResources.delete)}</MenuItem>
                                </DialogTrigger>
                            </MenuList>
                        </MenuPopover>
                    </Menu>

                    <DialogSurface aria-labelledby="delete-task-dialog-title" aria-describedby="delete-task-dialog-content">
                        <DialogBody>
                            <DialogTitle id="delete-task-dialog-title">
                                {intl.formatMessage(ScheduledTasksResources.deleteScheduledTasksConfirmationTitle)}
                            </DialogTitle>
                            <DialogContent id="delete-task-dialog-content">
                                {intl.formatMessage(ScheduledTasksResources.deleteScheduledTasksConfirmationMessage)}
                            </DialogContent>
                            <DialogActions>
                                <DialogTrigger disableButtonEnhancement>
                                    <Button appearance="primary" onClick={() => onDeleteTask(item.id)}>
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
        },
        [intl, onDeleteTask, onPauseTask, onResumeTask, onRunTaskNow, styles.menuItems]
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

    const onRenderSchedule = useCallback((item: ScheduledTask) => {
        // Convert common cron expressions to human-readable format
        const cronToHuman = (cron: string) => {
            const cronMap: { [key: string]: string } = {
                '0 * * * *': 'Every hour',
                '*/5 * * * *': 'Every 5 minutes',
                '*/15 * * * *': 'Every 15 minutes',
                '*/30 * * * *': 'Every 30 minutes',
                '0 */2 * * *': 'Every 2 hours',
                '0 */6 * * *': 'Every 6 hours',
                '0 */12 * * *': 'Every 12 hours',
                '0 0 * * *': 'Daily at midnight',
                '0 9 * * *': 'Daily at 9 AM',
                '0 0 * * 0': 'Weekly on Sunday',
                '0 0 1 * *': 'Monthly on 1st',
            };

            return cronMap[cron] || cron;
        };

        const humanReadable = cronToHuman(item.cronExpression);

        return <TableCellLayout truncate>{humanReadable}</TableCellLayout>;
    }, []);

    const onRenderLastRun = useCallback(
        (item: ScheduledTask) => {
            if (!item.lastExecutionTime) {
                return <TableCellLayout truncate>{intl.formatMessage(SreAgentResources.never)}</TableCellLayout>;
            }

            const date = new Date(item.lastExecutionTime);
            return <TableCellLayout truncate>{getLocaleDateTimeHHMM(date)}</TableCellLayout>;
        },
        [intl]
    );

    const onRenderNextRun = useCallback(
        (item: ScheduledTask) => {
            if (!item.nextExecutionTime || item.status !== ScheduledTaskStatus.Active) {
                return <TableCellLayout truncate>{intl.formatMessage(SreAgentResources.notScheduled)}</TableCellLayout>;
            }

            const date = new Date(item.nextExecutionTime);
            return <TableCellLayout truncate>{getLocaleDateTimeHHMM(date)}</TableCellLayout>;
        },
        [intl]
    );

    const onRenderRuns = useCallback((item: ScheduledTask) => {
        return <Text>{item.executionCount}</Text>;
    }, []);

    const columns: TableColumnDefinition<ScheduledTask>[] = useMemo(
        () => [
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.name,
                compare: (a, b) => a.name.localeCompare(b.name),
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.name),
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
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.status),
                renderCell: onRenderStatus,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.schedule,
                compare: (a, b) => a.cronExpression.localeCompare(b.cronExpression),
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.schedule),
                renderCell: onRenderSchedule,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.lastRun,
                compare: (a, b) => {
                    const aTime = a.lastExecutionTime ? new Date(a.lastExecutionTime).getTime() : 0;
                    const bTime = b.lastExecutionTime ? new Date(b.lastExecutionTime).getTime() : 0;
                    return aTime - bTime;
                },
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.lastRun),
                renderCell: onRenderLastRun,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.nextRun,
                compare: (a, b) => {
                    const aTime = a.nextExecutionTime ? new Date(a.nextExecutionTime).getTime() : 0;
                    const bTime = b.nextExecutionTime ? new Date(b.nextExecutionTime).getTime() : 0;
                    return aTime - bTime;
                },
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.nextRun),
                renderCell: onRenderNextRun,
            }),
            createTableColumn<ScheduledTask>({
                columnId: ScheduledTaskDataGridColumns.runs,
                compare: (a, b) => a.executionCount - b.executionCount,
                renderHeaderCell: () => intl.formatMessage(ScheduledTasksResources.runs),
                renderCell: onRenderRuns,
            }),
        ],
        [intl, onRenderActions, onRenderLastRun, onRenderName, onRenderNextRun, onRenderRuns, onRenderSchedule, onRenderStatus]
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
                style={{ minWidth: '800px' }}
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
                                <DataGridCell focusMode={getCellFocusMode(columnId)}>{renderCell(item)}</DataGridCell>
                            )}
                        </DataGridRow>
                    )}
                </DataGridBody>
            </DataGrid>
        </>
    );
};

const columnSizingOptions = {
    name: {
        minWidth: 350,
        defaultWidth: 450,
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
        minWidth: 150,
        defaultWidth: 200,
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

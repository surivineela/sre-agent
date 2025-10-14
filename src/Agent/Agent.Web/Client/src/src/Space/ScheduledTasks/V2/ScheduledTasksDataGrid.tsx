import {
    Badge,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    Text,
} from '@fluentui/react-components';
import {
    CheckmarkCircleRegular,
    ClockSparkleRegular,
    DeleteRegular,
    MoreHorizontalRegular,
    PauseRegular,
    PlayRegular,
} from '@fluentui/react-icons';
import { Link } from '@fluentui/react/lib/Link';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { getLocaleDateTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ScheduledTask, ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
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
}

export const ScheduledTasksDataGrid: FC<ScheduledTasksDataGridProps> = ({ scheduledTasks }) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();

    const onRenderName = useCallback((item: ScheduledTask) => {
        // TODO: Replace onClick to task details page
        return (
            <TableCellLayout truncate>
                <Link onClick={() => {}}>{item.name}</Link>
            </TableCellLayout>
        );
    }, []);

    const onRenderActions = useCallback(
        (_item: ScheduledTask) => {
            return (
                <Menu>
                    <MenuTrigger>
                        <MenuButton appearance="transparent" icon={<MoreHorizontalRegular />} />
                    </MenuTrigger>

                    <MenuPopover>
                        <MenuList>
                            <MenuItem>
                                <div className={styles.menuItems}>
                                    <PauseRegular />
                                    {intl.formatMessage(ScheduledTasksResources.pause)}
                                </div>
                            </MenuItem>
                            <MenuItem>
                                <div className={styles.menuItems}>
                                    <PlayRegular />
                                    {intl.formatMessage(ScheduledTasksResources.runOnceNow)}
                                </div>
                            </MenuItem>
                            <MenuItem>
                                <div className={styles.menuItems}>
                                    <DeleteRegular />
                                    {intl.formatMessage(SreAgentResources.delete)}
                                </div>
                            </MenuItem>
                        </MenuList>
                    </MenuPopover>
                </Menu>
            );
        },
        [intl, styles.menuItems]
    );

    const onRenderStatus = useCallback(
        (item: ScheduledTask) => {
            const status = item.status;
            switch (status) {
                case ScheduledTaskStatus.Active:
                    return (
                        <Badge appearance="tint" color="success" icon={<ClockSparkleRegular />}>
                            {intl.formatMessage(ScheduledTasksResources.active)}
                        </Badge>
                    );
                case ScheduledTaskStatus.Paused:
                    return (
                        <Badge appearance="tint" color="important" icon={<PauseRegular />}>
                            {intl.formatMessage(ScheduledTasksResources.paused)}
                        </Badge>
                    );
                case ScheduledTaskStatus.Completed:
                    return (
                        <Badge appearance="tint" color="informative" icon={<CheckmarkCircleRegular />}>
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
        [intl, onRenderActions, onRenderLastRun, onRenderName, onRenderNextRun, onRenderRuns, onRenderStatus]
    );

    return (
        <DataGrid
            items={scheduledTasks || []}
            columns={columns}
            sortable
            resizableColumns
            columnSizingOptions={columnSizingOptions}
            selectionMode="multiselect"
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

import {
    Badge,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Link,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    Text,
} from '@fluentui/react-components';
import { CheckmarkCircleFilled, DismissCircleFilled } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { getLocaleDateTimeHHMM } from '../../../Common/Helpers/Date';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { ScheduledTaskExecution } from '../../Contracts/ScheduledTasks';
import { useScheduledTasksStyles } from '../ScheduledTasks.styles';

enum ScheduledTaskExecutionsDataGridColumns {
    startTime = 'startTime',
    runStatus = 'runStatus',
    threadName = 'threadName',
}

interface ScheduledTaskExecutionsDataGridProps {
    executions: ScheduledTaskExecution[];
    isLoading?: boolean;
    threadNames?: Map<string, string>;
}

export const ScheduledTaskExecutionsDataGrid: FC<ScheduledTaskExecutionsDataGridProps> = ({
    executions,
    isLoading = false,
    threadNames,
}) => {
    const intl = useIntl();
    const styles = useScheduledTasksStyles();
    const navigate = useNavigate();

    const onRenderStartTime = useCallback((item: ScheduledTaskExecution) => {
        if (!item.executionTime) {
            return '-';
        }
        return getLocaleDateTimeHHMM(new Date(item.executionTime));
    }, []);

    const onRenderRunStatus = useCallback(
        (item: ScheduledTaskExecution) => {
            if (item.success) {
                return (
                    <Badge appearance="tint" color="success" icon={<CheckmarkCircleFilled />}>
                        {intl.formatMessage(ScheduledTasksResources.executionSuccess)}
                    </Badge>
                );
            }
            return (
                <Badge appearance="tint" color="danger" icon={<DismissCircleFilled />}>
                    {intl.formatMessage(ScheduledTasksResources.executionFailed)}
                </Badge>
            );
        },
        [intl]
    );

    const handleThreadClick = useCallback(
        (threadId: string) => {
            navigate(`/views/activities/threads/${threadId}`);
        },
        [navigate]
    );

    const onRenderThreadName = useCallback(
        (item: ScheduledTaskExecution) => {
            if (!item.threadId) {
                return intl.formatMessage(ScheduledTasksResources.noThreadFound);
            }
            const threadName = threadNames?.get(item.threadId);
            return !threadName ? (
                intl.formatMessage(ScheduledTasksResources.noThreadFound)
            ) : (
                <Link onClick={() => handleThreadClick(item.threadId!)}>{threadName}</Link>
            );
        },
        [handleThreadClick, intl, threadNames]
    );

    const columns: TableColumnDefinition<ScheduledTaskExecution>[] = useMemo(
        () => [
            createTableColumn<ScheduledTaskExecution>({
                columnId: ScheduledTaskExecutionsDataGridColumns.startTime,
                compare: (a, b) => {
                    const aTime = a.executionTime ? new Date(a.executionTime).getTime() : 0;
                    const bTime = b.executionTime ? new Date(b.executionTime).getTime() : 0;
                    return bTime - aTime; // Sort descending by default (newest first)
                },
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.startTime)}</Text>,
                renderCell: onRenderStartTime,
            }),
            createTableColumn<ScheduledTaskExecution>({
                columnId: ScheduledTaskExecutionsDataGridColumns.runStatus,
                compare: (a, b) => (a.success === b.success ? 0 : a.success ? -1 : 1),
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.runStatus)}</Text>,
                renderCell: onRenderRunStatus,
            }),
            createTableColumn<ScheduledTaskExecution>({
                columnId: ScheduledTaskExecutionsDataGridColumns.threadName,
                renderHeaderCell: () => <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.threadName)}</Text>,
                renderCell: onRenderThreadName,
            }),
        ],
        [intl, onRenderStartTime, onRenderRunStatus, onRenderThreadName]
    );

    const getRowId = useCallback((item: ScheduledTaskExecution) => `${item.executionTime}-${item.threadId ?? 'no-thread'}`, []);

    if (!isLoading && executions.length === 0) {
        return (
            <div className={styles.emptyState}>
                <Text weight="semibold">{intl.formatMessage(ScheduledTasksResources.noExecutions)}</Text>
                <Text>{intl.formatMessage(ScheduledTasksResources.noExecutionsDescription)}</Text>
            </div>
        );
    }

    return (
        <DataGrid
            items={executions}
            columns={columns}
            sortable
            resizableColumns
            columnSizingOptions={columnSizingOptions}
            getRowId={getRowId}
            aria-label={intl.formatMessage(ScheduledTasksResources.executionHistoryTableAriaLabel)}
        >
            <DataGridHeader>
                <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
            </DataGridHeader>
            <DataGridBody<ScheduledTaskExecution>>
                {({ item, rowId }) => (
                    <DataGridRow<ScheduledTaskExecution> key={rowId}>
                        {({ renderCell, columnId }) => (
                            <DataGridCell focusMode={getCellFocusMode(columnId)}>
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
    startTime: {
        minWidth: 180,
        defaultWidth: 220,
    },
    runStatus: {
        minWidth: 100,
        defaultWidth: 120,
    },
    threadName: {
        minWidth: 150,
    },
};

const getCellFocusMode = (columnId: TableColumnId) => {
    switch (columnId) {
        case ScheduledTaskExecutionsDataGridColumns.threadName:
            return 'none';
        default:
            return 'cell';
    }
};

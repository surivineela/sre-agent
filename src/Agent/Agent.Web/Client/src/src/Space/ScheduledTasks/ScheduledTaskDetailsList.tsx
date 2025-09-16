import {
    CheckmarkCircle16Filled,
    DismissCircle16Filled,
    Pause16Regular,
    Play16Regular,
    SubtractCircle16Regular,
} from '@fluentui/react-icons';
import {
    CheckboxVisibility,
    ConstrainMode,
    DetailsListLayoutMode,
    IColumn,
    Selection,
    SelectionMode,
} from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { FC, useCallback, useEffect, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';
import { ScheduledTask } from '../Contracts/ScheduledTasks';

export type ISortedDetailsListColumn = IColumn & {
    sort?: (items: any[], isSortedDescending: boolean) => any[];
    disableColumnClick?: boolean;
};

enum ScheduledTaskListColumnKey {
    selected = 'selected',
    name = 'name',
    status = 'status',
    schedule = 'schedule',
    lastExecution = 'lastExecution',
    nextExecution = 'nextExecution',
    executionCount = 'executionCount',
}

export type ScheduledTaskPickerProps = {
    scheduledTasks: ScheduledTask[];
    scheduledTasksLoading: boolean;
    onTaskSelected?: (task: ScheduledTask) => void;
    selectedTask?: ScheduledTask;
    onSelectionChanged?: (task: ScheduledTask | null) => void;
};

const ScheduledTaskDetailsList: FC<ScheduledTaskPickerProps> = (props: ScheduledTaskPickerProps) => {
    const { scheduledTasks, scheduledTasksLoading, onTaskSelected, selectedTask, onSelectionChanged } = props;
    const intl = useIntl();
    // Selection handling
    const selection = useMemo(() => {
        return new Selection({
            onSelectionChanged: () => {
                const selectedItems = selection.getSelection() as ScheduledTask[];
                onSelectionChanged?.(selectedItems.length > 0 ? selectedItems[0] : null);
            },
        });
    }, [onSelectionChanged]);

    // Update selection when selectedTask changes
    useEffect(() => {
        if (selectedTask) {
            const index = scheduledTasks.findIndex(task => task.id === selectedTask.id);
            if (index >= 0) {
                selection.setIndexSelected(index, true, false);
            }
        } else {
            selection.setAllSelected(false);
        }
    }, [selectedTask, scheduledTasks, selection]);

    const onRenderName = useCallback(
        (item: ScheduledTask) => {
            const MAX_PROMPT_PREVIEW = 120;
            const truncate = (text: string, max: number) => (text.length > max ? text.substring(0, max - 1) + '…' : text);
            const promptPreview = item.agentPrompt ? truncate(item.agentPrompt.replace(/\s+/g, ' ').trim(), MAX_PROMPT_PREVIEW) : null;
            return (
                <div
                    onClick={() => onTaskSelected?.(item)}
                    style={{
                        cursor: 'pointer',
                        userSelect: 'text',
                    }}
                >
                    <div style={{ userSelect: 'text', fontSize: '13px', fontWeight: 600 }}>{item.name}</div>
                    {item.description && (
                        <div style={{ userSelect: 'text', fontSize: '12px', color: '#605e5c', marginTop: '2px' }}>{item.description}</div>
                    )}
                    {promptPreview && (
                        <div
                            style={{
                                userSelect: 'text',
                                fontSize: '11px',
                                color: '#8a8886',
                                marginTop: '4px',
                                fontFamily: 'Monaco, monospace',
                                whiteSpace: 'nowrap',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                maxWidth: '100%',
                            }}
                            title={item.agentPrompt}
                        >
                            {promptPreview}
                        </div>
                    )}
                </div>
            );
        },
        [onTaskSelected]
    );

    const onRenderStatus = useCallback((item: ScheduledTask) => {
        const getStatusIcon = (status: string) => {
            switch (status) {
                case 'Active':
                    return <Play16Regular style={{ color: '#107c10', marginRight: '6px' }} />;
                case 'Paused':
                    return <Pause16Regular style={{ color: '#ff8c00', marginRight: '6px' }} />;
                case 'Completed':
                    return <CheckmarkCircle16Filled style={{ color: '#0078d4', marginRight: '6px' }} />;
                case 'Failed':
                    return <DismissCircle16Filled style={{ color: '#d13438', marginRight: '6px' }} />;
                default:
                    return <SubtractCircle16Regular style={{ color: '#666', marginRight: '6px' }} />;
            }
        };

        return (
            <div style={{ userSelect: 'text', display: 'flex', alignItems: 'center' }}>
                {getStatusIcon(item.status)}
                {item.status}
            </div>
        );
    }, []);

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

        return <div style={{ userSelect: 'text' }}>{humanReadable}</div>;
    }, []);

    const onRenderLastExecution = useCallback((item: ScheduledTask) => {
        if (!item.lastExecutionTime) {
            return <div style={{ userSelect: 'text' }}>Never</div>;
        }

        const date = new Date(item.lastExecutionTime);
        return (
            <div style={{ userSelect: 'text' }}>
                {date.toLocaleDateString()} {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </div>
        );
    }, []);

    const onRenderNextExecution = useCallback((item: ScheduledTask) => {
        if (!item.nextExecutionTime || item.status !== 'Active') {
            return <div style={{ userSelect: 'text' }}>Not scheduled</div>;
        }

        const date = new Date(item.nextExecutionTime);
        return (
            <div style={{ userSelect: 'text' }}>
                {date.toLocaleDateString()} {date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </div>
        );
    }, []);

    const onRenderExecutionCount = useCallback((item: ScheduledTask) => {
        return <div style={{ userSelect: 'text' }}>{item.executionCount}</div>;
    }, []);

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columnWidth = '14';

        return [
            {
                key: ScheduledTaskListColumnKey.name,
                name: intl.formatMessage(ScheduledTasksResources.name),
                fieldName: ScheduledTaskListColumnKey.name,
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                isMultiline: true,
                onRender: onRenderName,
                styles: { root: { width: `16%` } },
            },
            {
                key: ScheduledTaskListColumnKey.status,
                name: intl.formatMessage(ScheduledTasksResources.status),
                fieldName: ScheduledTaskListColumnKey.status,
                isResizable: true,
                isMultiline: true,
                minWidth: 100,
                maxWidth: 150,
                onRender: onRenderStatus,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: ScheduledTaskListColumnKey.schedule,
                name: intl.formatMessage(ScheduledTasksResources.schedule),
                fieldName: ScheduledTaskListColumnKey.schedule,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderSchedule,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: ScheduledTaskListColumnKey.lastExecution,
                name: intl.formatMessage(ScheduledTasksResources.lastExecution),
                fieldName: ScheduledTaskListColumnKey.lastExecution,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 200,
                onRender: onRenderLastExecution,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: ScheduledTaskListColumnKey.nextExecution,
                name: intl.formatMessage(ScheduledTasksResources.nextExecution),
                fieldName: ScheduledTaskListColumnKey.nextExecution,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 200,
                onRender: onRenderNextExecution,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: ScheduledTaskListColumnKey.executionCount,
                name: intl.formatMessage(ScheduledTasksResources.executionCount),
                fieldName: ScheduledTaskListColumnKey.executionCount,
                isResizable: true,
                minWidth: 80,
                maxWidth: 120,
                onRender: onRenderExecutionCount,
                styles: { root: { width: `${columnWidth}%` } },
            },
        ];
    }, [intl, onRenderName, onRenderStatus, onRenderSchedule, onRenderLastExecution, onRenderNextExecution, onRenderExecutionCount]);

    return (
        <div data-is-scrollable="true" style={{ userSelect: 'text' }}>
            <ShimmeredDetailsList
                columns={columns}
                constrainMode={ConstrainMode.horizontalConstrained}
                items={scheduledTasks ?? []}
                layoutMode={DetailsListLayoutMode.justified}
                compact={true}
                enableShimmer={scheduledTasksLoading}
                checkboxVisibility={CheckboxVisibility.always}
                useReducedRowRenderer={true}
                styles={{
                    root: {
                        width: '100%',
                        userSelect: 'text',
                    },
                }}
                selectionPreservedOnEmptyClick={true}
                selection={selection}
                selectionMode={SelectionMode.single}
                setKey="scheduledTaskList"
                getKey={(item, index) => (item && item.id ? item.id : `shimmer-${index}`)}
            />
        </div>
    );
};

export default ScheduledTaskDetailsList;

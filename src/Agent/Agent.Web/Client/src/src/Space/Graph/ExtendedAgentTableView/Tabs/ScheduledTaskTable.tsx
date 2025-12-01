import { Badge, Button, TableCell, TableHeaderCell, Text, Toolbar, ToolbarButton, ToolbarDivider } from '@fluentui/react-components';
import { ArrowClockwise20Regular, Delete16Regular } from '@fluentui/react-icons';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getHumanReadableCronExpression } from '../../../../Common/Helpers/CronExpression';
import { ExtendedAgentsGraphResources, ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentNodeType, ExtendedTrigger } from '../../../Contracts/ExtendedAgentGraph';
import { ScheduledTasksContext } from '../../../ScheduledTasks/Hooks/ScheduledTasksContext';
import { ScheduledTasksFilters } from '../../../ScheduledTasks/ScheduledTasksToolbar';
import { TaskStatusFilterKey } from '../../../ScheduledTasks/ScheduledTasksUtilities';
import { EntityDeleteConfirmDialog } from '../Common/EntityDeleteConfirmDialog';
import { EntityTable } from '../Common/EntityTable';
import { BaseTableItem, EntityTableProps, EntityToolbarProps, ScheduledTaskItem, STATUS } from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';
import { getFilterKeyFromTriggerStatus } from '../ExtendedAgentTableView.Utilities';

interface ScheduledTaskTableProps extends EntityTableProps {
    scheduledTaskTriggers: ExtendedTrigger[];
}

export const ScheduledTaskTable: FC<ScheduledTaskTableProps> = ({
    scheduledTaskTriggers,
    openInfoPanel,
    refresh,
    lastUpdated,
    isLoading,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [statusFilter, setStatusFilter] = useState<TaskStatusFilterKey>(TaskStatusFilterKey.All);
    const [selectedTasks, setSelectedTasks] = useState<ExtendedTrigger[]>([]);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);

    const handleSearchTextChange = useCallback((text: string) => {
        setSearchText(text);
    }, []);

    const handleStatusFilterChange = useCallback((filter: TaskStatusFilterKey) => {
        setStatusFilter(filter);
    }, []);

    const handleSelectedTasksChange = useCallback((items: BaseTableItem[]) => {
        setSelectedTasks(items as ExtendedTrigger[]);
    }, []);

    const scheduledTaskItems = useMemo<ScheduledTaskItem[]>(() => {
        const query = searchText?.trim().toLowerCase();
        let filtered = scheduledTaskTriggers;

        if (query) {
            filtered = scheduledTaskTriggers.filter(trigger => trigger.name?.toLowerCase().includes(query));
        }
        if (statusFilter !== TaskStatusFilterKey.All) {
            filtered = filtered.filter(trigger => getFilterKeyFromTriggerStatus(trigger.status) === statusFilter);
        }

        return filtered.map(trigger => ({
            id: trigger.id ?? trigger.name,
            name: trigger.name || EMPTY_DISPLAY,
            status: trigger.status || EMPTY_DISPLAY,
            schedule: getHumanReadableCronExpression(trigger.schedule || trigger.cronExpression || EMPTY_DISPLAY, intl),
            completedRuns: trigger.executionCount || 0,
            data: trigger,
        }));
    }, [EMPTY_DISPLAY, intl, scheduledTaskTriggers, searchText, statusFilter]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.scheduledTriggerNameTitle)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.scheduleTitle)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ScheduledTasksResources.completedRuns)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const scheduledItem = item as ScheduledTaskItem;
            return (
                <>
                    <TableCell tabIndex={0} role="gridcell">
                        <Button
                            appearance="transparent"
                            onClick={() => openInfoPanel?.(scheduledItem.name, ExtendedAgentNodeType.Trigger)}
                            className={styles.transparentButton}
                        >
                            <Text className={styles.clickableText}>{scheduledItem.name}</Text>
                        </Button>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {(() => {
                            const isActive = scheduledItem.status?.toLowerCase() === STATUS.ACTIVE;
                            const isDisabled = scheduledItem.status?.toLowerCase() === STATUS.DISABLED;
                            const isCompleted = scheduledItem.status?.toLowerCase() === STATUS.COMPLETED;

                            if (isActive) {
                                return (
                                    <Badge appearance="tint" color="success">
                                        {intl.formatMessage(ExtendedAgentsGraphResources.onLabel)}
                                    </Badge>
                                );
                            } else if (isDisabled) {
                                return (
                                    <Badge appearance="tint" color="danger">
                                        {intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
                                    </Badge>
                                );
                            } else if (isCompleted) {
                                return (
                                    <Badge appearance="tint" color="subtle">
                                        {intl.formatMessage(ScheduledTasksResources.ended)}
                                    </Badge>
                                );
                            } else {
                                return <Text>{scheduledItem.status}</Text>;
                            }
                        })()}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{scheduledItem.schedule}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{scheduledItem.completedRuns}</Text>
                    </TableCell>
                </>
            );
        },
        [styles, intl, openInfoPanel]
    );

    return (
        <div className={styles.entityTable}>
            <ScheduledTaskTableToolbar
                searchText={searchText}
                setSearchText={handleSearchTextChange}
                statusFilter={statusFilter}
                setStatusFilter={handleStatusFilterChange}
                selectedTasks={selectedTasks}
                refresh={refresh}
                lastUpdated={lastUpdated}
                isLoading={isLoading}
            />
            <EntityTable
                activeTab="scheduledTasks"
                searchText={searchText}
                items={scheduledTaskItems}
                setSelectedItems={handleSelectedTasksChange}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};

interface ScheduledTaskTableToolbarProps extends EntityToolbarProps {
    selectedTasks: ExtendedTrigger[];
    statusFilter: TaskStatusFilterKey;
    setStatusFilter: (status: TaskStatusFilterKey) => void;
}

const ScheduledTaskTableToolbar = memo<ScheduledTaskTableToolbarProps>(
    ({ selectedTasks = [], searchText, setSearchText, statusFilter, setStatusFilter, refresh, lastUpdated }) => {
        const intl = useIntl();
        const styles = useListViewStyles();
        const azPortalContext = useContext(AzPortalContext);
        const { deleteTask, isOperationInProgress } = useContext(ScheduledTasksContext);
        const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);

        const isDeleteDisabled = useMemo(
            () => selectedTasks.length === 0 || isDeleting || isOperationInProgress,
            [isDeleting, isOperationInProgress, selectedTasks.length]
        );

        const handleDelete = useCallback(async () => {
            setIsDeleting(true);
            setShowDeleteConfirmationDialog(false);
            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleMultiple),
                intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationInProgressMultiple)
            );

            try {
                const responses = await Promise.all(selectedTasks?.map(task => deleteTask(task.id ?? '')) || []);
                if (responses.some(response => response.isSuccessful)) {
                    await refresh();
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationSuccessMultiple)
                    );
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationError, {
                            errorMessage: responses.find(r => !r.isSuccessful)?.error,
                        })
                    );
                }
            } finally {
                setIsDeleting(false);
            }
        }, [azPortalContext, deleteTask, intl, refresh, selectedTasks]);

        return (
            <div className={styles.toolbar}>
                <div className={styles.searchAndToolbar}>
                    <Toolbar className={styles.toolbarButtons}>
                        <ToolbarButton
                            appearance="subtle"
                            className={styles.toolbarButton}
                            icon={<Delete16Regular />}
                            onClick={() => setShowDeleteConfirmationDialog(true)}
                            disabled={isDeleteDisabled}
                        >
                            {intl.formatMessage(SreAgentResources.delete)}
                        </ToolbarButton>
                        <ToolbarDivider />
                    </Toolbar>
                    <ScheduledTasksFilters
                        searchQuery={searchText ?? ''}
                        setSearchQuery={setSearchText}
                        statusFilter={statusFilter}
                        setStatusFilter={setStatusFilter}
                    />
                    <EntityDeleteConfirmDialog
                        showDialog={showDeleteConfirmationDialog}
                        setShowDialog={setShowDeleteConfirmationDialog}
                        handleDelete={handleDelete}
                        numItems={selectedTasks.length}
                    />
                </div>
                {lastUpdated && (
                    <div className={styles.lastUpdated}>
                        <ArrowClockwise20Regular />
                        <Text>{`${intl.formatMessage(ScheduledTasksResources.lastUpdated)}: ${lastUpdated}`}</Text>
                    </div>
                )}
            </div>
        );
    }
);

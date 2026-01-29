import {
    Badge,
    Button,
    InputOnChangeData,
    SearchBox,
    TableCell,
    TableHeaderCell,
    Text,
    Toolbar,
    ToolbarButton,
    ToolbarDivider,
} from '@fluentui/react-components';
import { ArrowClockwise20Regular, Delete16Regular } from '@fluentui/react-icons';
import { SearchBoxChangeEvent } from '@fluentui/react-search';
import { FC, memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../../Common/Clients/IncidentHandlerClient';
import { PillFilter } from '../../../../Common/Components/PillFilter/PillFilter';
import { ExtendedAgentsGraphResources, ScheduledTasksResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentNodeType, ExtendedTrigger } from '../../../Contracts/ExtendedAgentGraph';
import { EntityDeleteConfirmDialog } from '../Common/EntityDeleteConfirmDialog';
import { EntityTable } from '../Common/EntityTable';
import {
    ALL_FILTER_KEY,
    BaseTableItem,
    EntityTableProps,
    EntityToolbarProps,
    IncidentTriggerItem,
    STATUS,
} from '../ExtendedAgentTableView.Contracts';
import { useListViewStyles } from '../ExtendedAgentTableView.Styles';

interface IncidentTriggerTableProps extends EntityTableProps {
    incidentTriggers: ExtendedTrigger[];
}

export const IncidentTriggerTable: FC<IncidentTriggerTableProps> = ({
    incidentTriggers,
    openInfoPanel,
    refresh,
    lastUpdated,
    isLoading,
}) => {
    const intl = useIntl();
    const styles = useListViewStyles();
    const [searchText, setSearchText] = useState<string>();
    const [statusFilter, setStatusFilter] = useState<string>(ALL_FILTER_KEY);
    const [selectedTriggers, setSelectedTriggers] = useState<ExtendedTrigger[]>([]);

    const EMPTY_DISPLAY = useMemo(() => intl.formatMessage(SreAgentResources.none), [intl]);

    const incidentTriggerItems = useMemo<IncidentTriggerItem[]>(() => {
        const query = searchText?.trim().toLowerCase();
        let filtered = incidentTriggers;

        if (query) {
            filtered = incidentTriggers.filter(trigger => trigger.name?.toLowerCase().includes(query));
        }

        if (statusFilter !== ALL_FILTER_KEY) {
            filtered = filtered.filter(trigger => trigger.status?.toLowerCase() === statusFilter);
        }

        return filtered.map(trigger => ({
            name: trigger.name || EMPTY_DISPLAY,
            status: trigger.status || EMPTY_DISPLAY,
            subAgent: trigger.agentName || EMPTY_DISPLAY,
            severity: trigger.priorities?.join(', ') || EMPTY_DISPLAY,
            incidentType: trigger.incidentType || EMPTY_DISPLAY,
            impactedService: trigger.service || EMPTY_DISPLAY,
            description: trigger.description || EMPTY_DISPLAY,
            titleContains: trigger.titleContains || EMPTY_DISPLAY,
            data: trigger,
        }));
    }, [EMPTY_DISPLAY, incidentTriggers, searchText, statusFilter]);

    const renderTableHeaders = useCallback(() => {
        return (
            <>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.incidentTriggerNameTitle)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.subagentName)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.severityLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.incidentImpactedService)}
                </TableHeaderCell>
                <TableHeaderCell className={styles.tableHeader}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.incidentTitleContains)}
                </TableHeaderCell>
            </>
        );
    }, [intl, styles.tableHeader]);

    const renderTableCells = useCallback(
        (item: BaseTableItem) => {
            const incidentItem = item as IncidentTriggerItem;
            return (
                <>
                    <TableCell tabIndex={0} role="gridcell">
                        <Button
                            appearance="transparent"
                            onClick={() => openInfoPanel?.(incidentItem.name, ExtendedAgentNodeType.Trigger)}
                            className={styles.transparentButton}
                        >
                            <Text className={styles.clickableText}>{incidentItem.name}</Text>
                        </Button>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        {(() => {
                            const isActive = incidentItem.status?.toLowerCase() === STATUS.ACTIVE;
                            const isDisabled = incidentItem.status?.toLowerCase() === STATUS.DISABLED;

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
                            } else {
                                return <Text>{incidentItem.status}</Text>;
                            }
                        })()}
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{incidentItem.subAgent}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{incidentItem.severity}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{incidentItem.incidentType}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{incidentItem.impactedService}</Text>
                    </TableCell>
                    <TableCell tabIndex={0} role="gridcell">
                        <Text>{incidentItem.titleContains}</Text>
                    </TableCell>
                </>
            );
        },
        [styles, intl, openInfoPanel]
    );

    return (
        <div className={styles.entityTable}>
            <IncidentTriggerTableToolbar
                searchText={searchText}
                setSearchText={setSearchText}
                statusFilter={statusFilter}
                setStatusFilter={setStatusFilter}
                selectedTriggers={selectedTriggers}
                refresh={refresh}
                lastUpdated={lastUpdated}
            />
            <EntityTable
                activeTab="incidentTriggers"
                searchText={searchText}
                items={incidentTriggerItems}
                setSelectedItems={(items: BaseTableItem[]) => setSelectedTriggers(items as ExtendedTrigger[])}
                renderTableHeaders={renderTableHeaders}
                renderTableCells={renderTableCells}
                isLoading={isLoading}
            />
        </div>
    );
};

interface IncidentTriggerTableToolbarProps extends EntityToolbarProps {
    selectedTriggers: ExtendedTrigger[];
    statusFilter: string;
    setStatusFilter: (statusFilter: string) => void;
}

const IncidentTriggerTableToolbar = memo<IncidentTriggerTableToolbarProps>(
    ({ selectedTriggers = [], searchText, setSearchText, statusFilter, setStatusFilter, refresh, lastUpdated }) => {
        const intl = useIntl();
        const styles = useListViewStyles();
        const { sreAgentEndpoint } = useContext(EnvironmentContext);
        const azPortalContext = useContext(AzPortalContext);
        const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);
        const incidentHandlerClient = useMemo(
            () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
            [sreAgentEndpoint, azPortalContext]
        );

        const statusFilterOptions = useMemo(
            () => [
                { key: ALL_FILTER_KEY, label: intl.formatMessage(SreAgentResources.all) },
                { key: STATUS.ACTIVE, label: intl.formatMessage(ExtendedAgentsGraphResources.onLabel) },
                { key: STATUS.DISABLED, label: intl.formatMessage(ExtendedAgentsGraphResources.offLabel) },
            ],
            [intl]
        );

        const isDeleteDisabled = useMemo(() => selectedTriggers.length === 0 || isDeleting, [isDeleting, selectedTriggers.length]);

        const handleDelete = useCallback(async () => {
            setIsDeleting(true);
            setShowDeleteConfirmationDialog(false);
            const triggerNames = selectedTriggers.map(trigger => trigger.name);

            azPortalContext.log({
                action: 'delete-incident-triggers',
                actionModifier: 'start',
                logLevel: 'info',
                data: { triggerNames },
            });

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationTitle, { count: selectedTriggers.length }),
                intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationInProgress, {
                    count: selectedTriggers.length,
                    name: triggerNames[0],
                })
            );

            const responses = await Promise.all(selectedTriggers.map(trigger => incidentHandlerClient.deleteIncidentFilter(trigger.name)));
            if (responses.some(response => response.isSuccessful)) {
                azPortalContext.log({
                    action: 'delete-incident-triggers',
                    actionModifier: 'success',
                    logLevel: 'info',
                    data: { triggerNames },
                });

                await refresh();
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationSuccess, {
                        count: selectedTriggers.length,
                        name: triggerNames[0],
                    })
                );
            } else {
                const errorMessage = responses.find(r => !r.isSuccessful)?.error;
                azPortalContext.log({
                    action: 'delete-incident-triggers',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    data: { triggerNames, errorMessage },
                });

                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationFailure, {
                        count: selectedTriggers.length,
                        name: triggerNames[0],
                        errorMessage,
                    })
                );
            }
            setIsDeleting(false);
        }, [azPortalContext, incidentHandlerClient, intl, refresh, selectedTriggers]);

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
                    <div className={styles.searchBoxAndFilters}>
                        <SearchBox
                            className={styles.searchBox}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchByIncidentTrigger)}
                            value={searchText}
                            onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? '')}
                            size={'small'}
                        />
                        <PillFilter
                            label={`${intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}`}
                            filterType="combobox"
                            options={statusFilterOptions}
                            selectedKeys={[statusFilter]}
                            onApply={keys => {
                                setStatusFilter(keys[0]);
                            }}
                        />
                    </div>
                    <EntityDeleteConfirmDialog
                        showDialog={showDeleteConfirmationDialog}
                        setShowDialog={setShowDeleteConfirmationDialog}
                        handleDelete={handleDelete}
                        numItems={selectedTriggers.length}
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

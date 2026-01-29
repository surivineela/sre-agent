import { format } from '@fluentui/react';
import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    InputOnChangeData,
    Link,
    makeStyles,
    mergeClasses,
    SearchBox,
    SearchBoxChangeEvent,
    SkeletonItem,
    Spinner,
    TableCellLayout,
    TableColumnDefinition,
    TableColumnId,
    Text,
    tokens,
    Tooltip,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { ArrowClockwise16Regular, Branch16Regular, Flash16Regular } from '@fluentui/react-icons';
import debounce from 'lodash/debounce';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import {
    FilterPropsWithKey,
    RemovableFilterProps,
    TimeRangeKeyLabelPair,
    TimeRangeValue,
    TimespanKeys,
} from '../../../Common/Components/PillFilter/Contracts';
import { LabelKeyPair } from '../../../Common/Components/PillFilter/ListWithSearch';
import { PillFilterSet } from '../../../Common/Components/PillFilter/PillFilterSet';
import { icmIncidentUrlTemplate } from '../../../Common/Constants/Links';
import { IncidentDocument, IncidentFilter } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { InvestigationStatus, Thread } from '../../../Common/Contracts/DataPlane/Thread';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/ConfigSettings';
import {
    ActivitiesThreadHeaderResources,
    IncidentManagementResources,
    SreAgentResources,
    TriggerIncidentManagementResources,
} from '../../../Strings/SREAgentResources';
import ThreadActionsMenu from '../../Activities/ThreadActionsMenu';
import { ChatBoxSidePanelData } from '../../Contracts/Activities';
import { IncidentsOverviewContext, SreAgentContext } from '../../Contracts/Context';
import { ExtendedAgentAnchorEntity } from '../../Contracts/ExtendedAgentGraph';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { TracePanel } from '../../Foundry/app/components/shell/playground/tracing/TracePanel';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import IncidentChatDrawer from '../Common/IncidentChatDrawer';
import { IncidentManagementEmptyState } from '../Common/IncidentManagementEmptyState';
import { PlatformConnectionMessageBar } from '../Common/PlatformConnectionMessageBar';
import { HandlerCreateOrEditInfo, IncidentsListColumnKey } from '../CreateIncidentHandler/Contracts';
import CreateIncidentHandlerConsolidated from '../CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import IncidentChat from '../IncidentChat';
import {
    getColumnInfo,
    getIncidentStatusIntlString,
    getInvestigationStatusIntlString,
    getPlatformSpecificStrings,
    getPriorities,
} from '../Utilities';
import ResponsePlanDetailsDrawer, { ResponsePlanDetailsDrawerProps } from '../Watchtower/Components/ResponsePlanDetailsDrawer';
import { BulkDeleteDialog } from './BulkDeleteDialog';
import { IncidentsSummary } from './IncidentsSummary';
import { ResponsePlanLinkWithIcon } from './ResponsePlanLinkWithIcon';
import { StatusLabel } from './StatusLabel';
import TriggerAgentDrawer from './TriggerAgentDrawer';
import { useIncidentThreadList } from './useIncidentThreadList';

interface SelectedThreadInfo {
    thread: Thread;
    fullScreen: boolean;
    showTrace: boolean;
}

interface IncidentsOverviewProps {
    agentAppInsightsAppId?: string;
    showControlPlaneDependentFeatures?: boolean;
}

interface IncidentsSkeletonLoaderProps {
    showControlPlaneDependentFeatures?: boolean;
    rowCount?: number;
    className?: string;
}

const IncidentsSkeletonLoader: FC<IncidentsSkeletonLoaderProps> = ({ showControlPlaneDependentFeatures, rowCount = 5, className }) => {
    return (
        <div>
            {Array.from({ length: rowCount }).map((_, index) => (
                <div key={index} className={className}>
                    <SkeletonItem size={16} style={{ width: '30px' }} />
                    <SkeletonItem size={16} style={{ width: '100px' }} />
                    <SkeletonItem size={16} style={{ width: '200px', flex: 1 }} />
                    <SkeletonItem size={16} style={{ width: '60px' }} />
                    <SkeletonItem size={16} style={{ width: '100px' }} />
                    <SkeletonItem size={16} style={{ width: '150px' }} />
                    <SkeletonItem size={16} style={{ width: '120px' }} />
                    {showControlPlaneDependentFeatures && <SkeletonItem size={16} style={{ width: '120px' }} />}
                    <SkeletonItem size={16} style={{ width: '120px' }} />
                </div>
            ))}
        </div>
    );
};

const IncidentsOverview: FC<IncidentsOverviewProps> = ({ agentAppInsightsAppId, showControlPlaneDependentFeatures }) => {
    const navigate = useAgentSiteNavigate();
    const showThreadTraceUI = useConfigSetting(SettingNames.ShowThreadTraceUI);

    const {
        incidentManagement: { incidentPlatformType, hasFilters },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const incidentManagementConfigured = useMemo(
        () => incidentPlatformType && incidentPlatformType !== IncidentManagementType.None,
        [incidentPlatformType]
    );

    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );
    const [selectedIncidentDetails, setSelectedIncidentDetails] = useState<IncidentDocument>();
    const [initialSidePanelDataMap, setInitialSidePanelDataMap] = useState<Map<string, ChatBoxSidePanelData>>(new Map());

    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const localStyles = useIncidentsOverviewStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>();
    const [selectedPriorities, setSelectedPriorities] = useState<string[]>([]);
    const [selectedStatuses, setSelectedStatuses] = useState<string[]>([]);
    const [selectedInvestigationStatuses, setSelectedInvestigationStatuses] = useState<string[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<IncidentsListColumnKey | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(true);
    const [refreshCounter, setRefreshCounter] = useState<number>(0);

    const [selectedThreadInfo, setSelectedThreadInfo] = useState<SelectedThreadInfo | null>(null);
    const [selectedThreads, setSelectedThreads] = useState<Set<string>>(new Set());
    const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
    const [isViewResponsePlanPanelOpen, setIsViewResponsePlanPanelOpen] = useState(false);
    const [openedResponsePlan, setOpenedResponsePlan] = useState<ResponsePlanDetailsDrawerProps['responsePlan'] | null>(null);
    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [isTriggerAgentDrawerOpen, setIsTriggerAgentDrawerOpen] = useState(false);

    const traceFocusRestorationRef = useRef<HTMLButtonElement>(null);

    const onInitialSidePanelDataChanged = useCallback((threadId: string, data: ChatBoxSidePanelData | undefined | null) => {
        setInitialSidePanelDataMap(prev => {
            const newMap = new Map(prev);
            if (!data) {
                newMap.delete(threadId);
            } else {
                newMap.set(threadId, data);
            }
            return newMap;
        });
    }, []);

    const openThreadDrawer = useCallback((thread: Thread) => {
        setIsTriggerAgentDrawerOpen(false);
        setIsViewResponsePlanPanelOpen(false);
        setSelectedThreadInfo({ thread, fullScreen: false, showTrace: false });
    }, []);

    const openThreadFullScreen = useCallback(() => {
        if (selectedThreadInfo) {
            setSelectedThreadInfo({ ...selectedThreadInfo, fullScreen: true });
        }
    }, [selectedThreadInfo]);

    const openThreadTrace = useCallback(() => {
        if (selectedThreadInfo) {
            setSelectedThreadInfo({ ...selectedThreadInfo, showTrace: true });
        }
    }, [selectedThreadInfo]);

    const closeThreadTrace = useCallback(() => {
        if (selectedThreadInfo) {
            setSelectedThreadInfo({ ...selectedThreadInfo, showTrace: false });
        }
    }, [selectedThreadInfo]);

    const closeThread = useCallback(() => {
        setSelectedThreadInfo(null);
    }, []);

    const openResponsePlanDrawer = useCallback((responsePlanName: string, autonomyLevel: string) => {
        setSelectedThreadInfo(null);
        setOpenedResponsePlan({ responsePlanName, autonomyLevel });
        setIsViewResponsePlanPanelOpen(true);
    }, []);

    const handleEditHandler = useCallback((filter: IncidentFilter | undefined, handlerId: string | undefined) => {
        setHandlerCreateOrEditInfo({ filter, handlerId });
        setIsViewResponsePlanPanelOpen(false);
        setIsTriggerAgentDrawerOpen(false);
    }, []);

    useEffect(() => {
        let isSubscribed = true;
        setSelectedIncidentDetails(undefined);

        if (selectedThreadInfo && selectedThreadInfo.thread.status?.incidentStatus?.incidentId) {
            incidentHandlerClient.getIncident(selectedThreadInfo.thread.status?.incidentStatus?.incidentId).then(response => {
                if (isSubscribed && response.isSuccessful && response.content) {
                    setSelectedIncidentDetails(response.content);
                }
            });
        }

        return () => {
            isSubscribed = false;
        };
    }, [selectedThreadInfo, incidentHandlerClient]);

    const {
        handlersMap,
        filtersMap,
        hasAnyIncidents,
        threadCounts,
        threads: incidentThreads,
        isLoadingInitialThreadsAndCounts: incidentThreadsLoading,
        moreThreadsToLoad,
        threadListDivRef,
        intersectionObserverRef,
        onScroll,
        deleteThread,
    } = useIncidentThreadList(
        undefined,
        searchText,
        selectedPriorities,
        selectedStatuses,
        selectedInvestigationStatuses,
        selectedTimeRange,
        sortColumnKey as IncidentsListColumnKey | undefined,
        isSortedDescending,
        !selectedThreadInfo?.fullScreen,
        refreshCounter
    );

    const proxy = useContext(AzPortalContext);

    const handleThreadDelete = useCallback(() => {
        if (selectedThreadInfo?.thread) {
            proxy.log({
                action: 'deleteIncidentThread',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId: selectedThreadInfo.thread.id,
            });

            const id = proxy.startNotification(
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentTitle, { title: selectedThreadInfo.thread.title }),
                intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentInProgressDescription)
            );

            deleteThread(selectedThreadInfo.thread.id).then(response => {
                if (response.isSuccessful) {
                    closeThread();

                    proxy.log({
                        action: 'deleteIncidentThread',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId: selectedThreadInfo.thread.id,
                    });

                    proxy.stopNotification(id, true, intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentSuccessDescription));
                } else {
                    proxy.log({
                        action: 'deleteIncidentThread',
                        actionModifier: 'failure',
                        logLevel: 'error',
                        resourceId: selectedThreadInfo.thread.id,
                        data: {
                            error: getDataPlaneErrorMessage(response.error),
                        },
                    });

                    proxy.stopNotification(
                        id,
                        false,
                        intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentFailureDescription, {
                            errorMessage: response.error?.message || response.error?.response?.data,
                        })
                    );
                }
            });
        }
    }, [intl, proxy, deleteThread, closeThread, selectedThreadInfo?.thread]);

    const handleBulkDelete = useCallback(() => {
        const selectedThreadIds = Array.from(selectedThreads);
        const selectedThreadsList = incidentThreads.filter(t => selectedThreadIds.includes(t.id));

        if (selectedThreadsList.length === 0) return;

        setIsDeleteDialogOpen(false);

        proxy.log({
            action: 'bulkDeleteIncidentThreads',
            actionModifier: 'start',
            logLevel: 'info',
            data: { count: selectedThreadsList.length },
        });

        const title =
            selectedThreadsList.length === 1
                ? selectedThreadsList[0].title
                : `${selectedThreadsList.length} ${intl.formatMessage(IncidentManagementResources.incidents)}`;

        const description =
            selectedThreadsList.length === 1
                ? intl.formatMessage(ActivitiesThreadHeaderResources.deletingIncident, { title: selectedThreadsList[0].title })
                : intl.formatMessage(ActivitiesThreadHeaderResources.deletingIncidents, {
                      titles: selectedThreadsList.map((t: Thread) => t.title).join(', '),
                  });

        const id = proxy.startNotification(
            intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentTitle, {
                title: title,
            }),
            description
        );

        Promise.all(selectedThreadsList.map((thread: Thread) => deleteThread(thread.id))).then(responses => {
            const successCount = responses.filter((r: any) => r.isSuccessful).length;
            const failureCount = responses.length - successCount;

            if (failureCount === 0) {
                setSelectedThreads(new Set());

                proxy.log({
                    action: 'bulkDeleteIncidentThreads',
                    actionModifier: 'success',
                    logLevel: 'info',
                    data: { count: successCount },
                });

                proxy.stopNotification(id, true, intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentSuccessDescription));
            } else {
                proxy.log({
                    action: 'bulkDeleteIncidentThreads',
                    actionModifier: 'failure',
                    logLevel: 'error',
                    data: { successCount, failureCount },
                });

                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(ActivitiesThreadHeaderResources.deleteIncidentFailureDescription, {
                        errorMessage: `${failureCount} of ${responses.length} failed`,
                    })
                );
            }
        });
    }, [proxy, intl, deleteThread, selectedThreads, incidentThreads]);

    const disableAllControls = useMemo(() => {
        return incidentThreadsLoading;
    }, [incidentThreadsLoading]);

    // Time Range
    const timeRangeOptions: TimeRangeKeyLabelPair[] = useMemo(
        () => [
            {
                key: TimespanKeys.All,
                label: intl.formatMessage(IncidentManagementResources.all),
            },
            {
                key: TimespanKeys.OneHour,
                label: intl.formatMessage(IncidentManagementResources.lastHour),
            },
            {
                key: TimespanKeys.SixHours,
                label: intl.formatMessage(IncidentManagementResources.last6Hours),
            },
            {
                key: TimespanKeys.TwelveHours,
                label: intl.formatMessage(IncidentManagementResources.last12Hours),
            },
            {
                key: TimespanKeys.TwentyFourHours,
                label: intl.formatMessage(IncidentManagementResources.last24Hours),
            },
            {
                key: TimespanKeys.ThreeDays,
                label: intl.formatMessage(IncidentManagementResources.last3Days),
            },
            {
                key: TimespanKeys.SevenDays,
                label: intl.formatMessage(IncidentManagementResources.last7Days),
            },
        ],
        [intl]
    );
    // End: Time Range

    // Priority
    const priorityOptions: LabelKeyPair[] = useMemo(() => {
        const priorities = getPriorities(incidentPlatformType);
        return priorities
            ? priorities.map(priority => ({
                  key: priority.key,
                  label: intl.formatMessage(priority.intlString),
              }))
            : [];
    }, [incidentPlatformType, intl]);
    // End: Priority

    // Status
    const statusOptions: LabelKeyPair[] = useMemo(() => {
        return Object.values(IncidentStatus).map(status => {
            const intlString = getIncidentStatusIntlString(status);
            return {
                key: status,
                label: intlString ? intl.formatMessage(intlString) : '-',
            };
        });
    }, [intl]);
    // End: Status

    // InvestigationStatuses
    const investigationStatusOptions: LabelKeyPair[] = useMemo(
        () =>
            Object.values(InvestigationStatus).map(investigationStatus => {
                const intlString = getInvestigationStatusIntlString(investigationStatus);
                return {
                    key: investigationStatus ?? '',
                    label: intlString ? intl.formatMessage(intlString) : '-',
                };
            }),
        [intl]
    );
    // End: InvestigationStatuses Filter

    const onRenderTitle = useCallback(
        (item: Thread) => {
            const titleText = getColumnInfo(IncidentsListColumnKey.title).getColumnValue(item) as string;
            return (
                <Tooltip content={titleText} relationship="label">
                    <Link
                        className={localStyles.titleLink}
                        onClick={e => {
                            e.stopPropagation();
                            openThreadDrawer(item);
                        }}
                        disabled={disableAllControls}
                    >
                        {titleText}
                    </Link>
                </Tooltip>
            );
        },
        [localStyles, openThreadDrawer, disableAllControls]
    );

    const columns = useMemo<TableColumnDefinition<Thread>[]>(() => {
        const cols: TableColumnDefinition<Thread>[] = [
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.incidentId,
                compare: (a, b) => {
                    const aVal = getColumnInfo(IncidentsListColumnKey.incidentId).getColumnValue(a) as string;
                    const bVal = getColumnInfo(IncidentsListColumnKey.incidentId).getColumnValue(b) as string;
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertIdLabel)}</span>
                ),
                renderCell: item => {
                    const incidentId = getColumnInfo(IncidentsListColumnKey.incidentId).getColumnValue(item) as string;
                    // We assume this is an ICM incident if the current platform type is ICM and the incident ID is numeric
                    const isIcmIncident = incidentPlatformType === IncidentManagementType.Icm && incidentId && /^\d+$/.test(incidentId);
                    const icmIncidentUrl = isIcmIncident ? format(icmIncidentUrlTemplate, incidentId) : undefined;
                    return (
                        <TableCellLayout truncate>
                            <Tooltip content={incidentId} relationship="label">
                                {icmIncidentUrl ? (
                                    <Link href={icmIncidentUrl} target="_blank" rel="noopener noreferrer">
                                        {incidentId}
                                    </Link>
                                ) : (
                                    <span>{incidentId}</span>
                                )}
                            </Tooltip>
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.title,
                compare: (a, b) => {
                    const aVal = getColumnInfo(IncidentsListColumnKey.title).getColumnValue(a) as string;
                    const bVal = getColumnInfo(IncidentsListColumnKey.title).getColumnValue(b) as string;
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertTitleLabel)}</span>
                ),
                renderCell: item => <TableCellLayout truncate>{onRenderTitle(item)}</TableCellLayout>,
            }),
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.priority,
                compare: (a, b) => {
                    const aVal = getColumnInfo(IncidentsListColumnKey.priority).getColumnValue(a) as string;
                    const bVal = getColumnInfo(IncidentsListColumnKey.priority).getColumnValue(b) as string;
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel)}</span>
                ),
                renderCell: item => {
                    const priorityText = getColumnInfo(IncidentsListColumnKey.priority).getColumnValue(item) as string;
                    return (
                        <Tooltip content={priorityText} relationship="label">
                            <TableCellLayout truncate>
                                <span>{priorityText}</span>
                            </TableCellLayout>
                        </Tooltip>
                    );
                },
            }),
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.incidentStatus,
                compare: (a, b) => {
                    const aVal = a.status?.incidentStatus?.status ?? '';
                    const bVal = b.status?.incidentStatus?.status ?? '';
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertStatusLabel)}</span>
                ),
                renderCell: item => {
                    return (
                        <TableCellLayout truncate>
                            <StatusLabel type="incidentStatus" status={item.status?.incidentStatus?.status as IncidentStatus} />
                        </TableCellLayout>
                    );
                },
            }),
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.agentStatus,
                compare: (a, b) => {
                    const aVal = a.incidentDetails?.investigationStatus ?? '';
                    const bVal = b.incidentDetails?.investigationStatus ?? '';
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(IncidentManagementResources.agentStatus)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        {item.incidentDetails?.investigationStatus ? (
                            <StatusLabel type="investigationStatus" status={item.incidentDetails?.investigationStatus} />
                        ) : (
                            '-'
                        )}
                    </TableCellLayout>
                ),
            }),
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.createdTimestamp,
                compare: (a, b) => {
                    const aVal = a.createdTimestamp ?? '';
                    const bVal = b.createdTimestamp ?? '';
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(IncidentManagementResources.incidentCreated)}</span>
                ),
                renderCell: item => {
                    const timestampText = getColumnInfo(IncidentsListColumnKey.createdTimestamp).getColumnValue(item) as string;
                    return (
                        <Tooltip content={timestampText} relationship="label">
                            <TableCellLayout truncate>
                                <span>{timestampText}</span>
                            </TableCellLayout>
                        </Tooltip>
                    );
                },
            }),
        ];

        if (incidentPlatformType !== IncidentManagementType.AzMonitor) {
            cols.push(
                createTableColumn<Thread>({
                    columnId: IncidentsListColumnKey.impactedService,
                    compare: (a, b) => {
                        const aVal = getColumnInfo(IncidentsListColumnKey.impactedService).getColumnValue(a) as string;
                        const bVal = getColumnInfo(IncidentsListColumnKey.impactedService).getColumnValue(b) as string;
                        return aVal.localeCompare(bVal);
                    },
                    renderHeaderCell: () => (
                        <span style={{ fontWeight: 600 }}>{intl.formatMessage(IncidentManagementResources.impactedService)}</span>
                    ),
                    renderCell: item => {
                        const serviceText = getColumnInfo(IncidentsListColumnKey.impactedService).getColumnValue(item) as string;
                        return (
                            <Tooltip content={serviceText} relationship="label">
                                <TableCellLayout truncate>
                                    <span>{serviceText}</span>
                                </TableCellLayout>
                            </Tooltip>
                        );
                    },
                })
            );
        }

        cols.push(
            createTableColumn<Thread>({
                columnId: IncidentsListColumnKey.handler,
                compare: (a, b) => {
                    const aVal = getColumnInfo(IncidentsListColumnKey.handler).getColumnValue(a) as string;
                    const bVal = getColumnInfo(IncidentsListColumnKey.handler).getColumnValue(b) as string;
                    return aVal.localeCompare(bVal);
                },
                renderHeaderCell: () => <span style={{ fontWeight: 600 }}>{intl.formatMessage(IncidentManagementResources.handler)}</span>,
                renderCell: item => {
                    // Priority 1: Use thread's handlerId if set (from incidentDetails)
                    const handlerId = item.incidentDetails?.handlerId;
                    if (handlerId) {
                        const anchorEntity: ExtendedAgentAnchorEntity = {
                            entityType: 'Agent',
                            entityName: handlerId,
                        };
                        return (
                            <ResponsePlanLinkWithIcon
                                type="handlingAgent"
                                value={handlerId}
                                onClick={e => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    navigate({
                                        primaryNavItemValue: PrimaryNavItemValues.Builder,
                                        secondaryNavItemValue: SecondaryNavItemValues.ExtendedAgentsGraph,
                                        options: {
                                            state: { anchorEntity },
                                        },
                                    });
                                }}
                            />
                        );
                    }

                    // Move variable declarations closer to where they're used
                    const filterId = getColumnInfo(IncidentsListColumnKey.handler).getColumnValue(item) as string;
                    const filterMatch = !filterId || filterId === '-' ? undefined : filtersMap.get(filterId);

                    // Priority 2: Use filter's handlingAgent (legacy behavior)
                    if (filterMatch?.handlingAgent) {
                        const anchorEntity: ExtendedAgentAnchorEntity = {
                            entityType: 'IncidentTrigger',
                            entityName: filterMatch.id,
                        };
                        return (
                            <ResponsePlanLinkWithIcon
                                type="agentTrigger"
                                value={filterMatch.id}
                                onClick={e => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    navigate({
                                        primaryNavItemValue: PrimaryNavItemValues.Builder,
                                        secondaryNavItemValue: SecondaryNavItemValues.ExtendedAgentsGraph,
                                        options: {
                                            state: { anchorEntity },
                                        },
                                    });
                                }}
                            />
                        );
                    }

                    // Priority 3: Show response plan link (filter-based)
                    if (filterMatch) {
                        return (
                            <ResponsePlanLinkWithIcon
                                type="responsePlan"
                                value={filterMatch.id}
                                onClick={e => {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    openResponsePlanDrawer(filterMatch.id, filterMatch.agentMode || '');
                                    setIsViewResponsePlanPanelOpen(true);
                                }}
                            />
                        );
                    }

                    return (
                        <TableCellLayout truncate>
                            <span>-</span>
                        </TableCellLayout>
                    );
                },
            })
        );

        return cols;
    }, [intl, onRenderTitle, platformSpecificStrings, incidentPlatformType, filtersMap, handlersMap]);

    const restoreFocusSourceAttributes = useRestoreFocusSource();

    const timeFilterProps: RemovableFilterProps = useMemo(
        () => ({
            label: intl.formatMessage(SreAgentResources.timeRange),
            disabled: disableAllControls || !!selectedThreadInfo,
            onRemove: () => setSelectedTimeRange(undefined),
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'timeRange' as const,
            options: timeRangeOptions,
            customTimeRangeProps: {
                addCustomOption: true,
            },
            onApply: setSelectedTimeRange,
            selectedValue: selectedTimeRange || { key: TimespanKeys.All },
        }),
        [disableAllControls, intl, selectedTimeRange, selectedThreadInfo, timeRangeOptions]
    );

    const statusFilterProps: RemovableFilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.status),
            disabled: disableAllControls || !!selectedThreadInfo,
            onRemove: () => setSelectedStatuses([]),
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: statusOptions,
            onApply: setSelectedStatuses,
            selectedKeys: selectedStatuses,
            multiSelect: true,
            addAllOption: true,
        }),
        [disableAllControls, intl, selectedStatuses, selectedThreadInfo, statusOptions]
    );

    const priorityFilterProps: RemovableFilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.priority),
            disabled: disableAllControls || !!selectedThreadInfo,
            onRemove: () => setSelectedPriorities([]),
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: priorityOptions,
            onApply: setSelectedPriorities,
            selectedKeys: selectedPriorities,
            multiSelect: true,
            addAllOption: true,
        }),
        [disableAllControls, intl, selectedPriorities, selectedThreadInfo, priorityOptions]
    );

    const investigationStatusFilterProps: RemovableFilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.agentStatus),
            disabled: disableAllControls || !!selectedThreadInfo,
            onRemove: () => setSelectedInvestigationStatuses([]),
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: investigationStatusOptions,
            onApply: setSelectedInvestigationStatuses,
            selectedKeys: selectedInvestigationStatuses,
            multiSelect: true,
            addAllOption: true,
        }),
        [disableAllControls, intl, selectedInvestigationStatuses, selectedThreadInfo, investigationStatusOptions]
    );

    const dynamicFilters: FilterPropsWithKey[] = useMemo(() => {
        const filters = [
            {
                key: IncidentsListColumnKey.createdTimestamp,
                props: timeFilterProps,
            },
            {
                key: IncidentsListColumnKey.incidentStatus,
                props: statusFilterProps,
            },
            {
                key: IncidentsListColumnKey.priority,
                props: priorityFilterProps,
            },
            {
                key: IncidentsListColumnKey.agentStatus,
                props: investigationStatusFilterProps,
            },
        ];

        return filters;
    }, [timeFilterProps, statusFilterProps, priorityFilterProps, investigationStatusFilterProps]);

    const emptyState = useMemo(() => {
        if (incidentThreads.length || incidentThreadsLoading || hasAnyIncidents) {
            return null;
        }

        if (!incidentManagementConfigured) {
            return (
                <IncidentManagementEmptyState
                    type="noPlatform"
                    onButtonClick={() =>
                        navigate({
                            primaryNavItemValue: PrimaryNavItemValues.Settings,
                            secondaryNavItemValue: SecondaryNavItemValues.IncidentPlatform,
                        })
                    }
                />
            );
        }

        if (!hasFilters) {
            return (
                <IncidentManagementEmptyState
                    type="noHandlers"
                    onButtonClick={() =>
                        navigate({
                            primaryNavItemValue: PrimaryNavItemValues.Builder,
                            secondaryNavItemValue: SecondaryNavItemValues.ResponsePlans,
                        })
                    }
                />
            );
        }

        return (
            <div className={localStyles.noItemsContainer}>
                <img
                    src={'AiSearchWarningSpotIllustration.svg'}
                    alt={intl.formatMessage(SreAgentResources.warning)}
                    style={{ height: 180, width: 180 }}
                />
                <div className={localStyles.textContainer}>
                    <Text className={localStyles.primaryTitle}>{intl.formatMessage(IncidentManagementResources.noIncidentsFound)}</Text>
                    <Text className={localStyles.description}>
                        {intl.formatMessage(IncidentManagementResources.noIncidentsFoundDescription)}
                    </Text>
                </div>
            </div>
        );
    }, [
        incidentThreads.length,
        incidentThreadsLoading,
        hasAnyIncidents,
        incidentManagementConfigured,
        hasFilters,
        localStyles,
        intl,
        navigate,
    ]);

    const columnSizingOptions = useMemo(
        () => ({
            [IncidentsListColumnKey.incidentId]: {
                minWidth: 120,
                idealWidth: 150,
            },
            [IncidentsListColumnKey.title]: {
                minWidth: 200,
                idealWidth: 250,
            },
            [IncidentsListColumnKey.priority]: {
                minWidth: 80,
                idealWidth: 100,
            },
            [IncidentsListColumnKey.incidentStatus]: {
                minWidth: 120,
                idealWidth: 150,
            },
            [IncidentsListColumnKey.agentStatus]: {
                minWidth: 120,
                idealWidth: 150,
            },
            [IncidentsListColumnKey.createdTimestamp]: {
                minWidth: 130,
                idealWidth: 180,
            },
            [IncidentsListColumnKey.impactedService]: {
                minWidth: 120,
                idealWidth: 150,
            },
            [IncidentsListColumnKey.handler]: {
                minWidth: 120,
                idealWidth: 150,
            },
        }),
        []
    );

    const closeTriggerAgentDrawer = useCallback(
        (needRefresh: boolean) => {
            setIsTriggerAgentDrawerOpen(false);
            if (needRefresh) {
                setRefreshCounter(prev => prev + 1);
            }
        },
        [setIsTriggerAgentDrawerOpen, setRefreshCounter]
    );

    return (
        <IncidentsOverviewContext.Provider value={{ initialSidePanelDataMap, onInitialSidePanelDataChanged }}>
            {handlerCreateOrEditInfo ? (
                <CreateIncidentHandlerConsolidated
                    exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                    setHandlerOperationStatus={() => {}}
                    handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                />
            ) : selectedThreadInfo?.fullScreen ? (
                <IncidentChat
                    selectedThread={selectedThreadInfo.thread}
                    exitToHome={closeThread}
                    isExpandedView={true}
                    handleThreadDelete={handleThreadDelete}
                    titleActions={
                        showThreadTraceUI && showControlPlaneDependentFeatures ? (
                            <Button
                                ref={traceFocusRestorationRef}
                                icon={<Branch16Regular />}
                                className={localStyles.traceViewButton}
                                onClick={openThreadTrace}
                                disabled={!agentAppInsightsAppId}
                            >
                                {intl.formatMessage(IncidentManagementResources.viewTrace)}
                            </Button>
                        ) : undefined
                    }
                />
            ) : (
                <>
                    <IncidentChatDrawer
                        {...restoreFocusSourceAttributes}
                        isOpen={!!selectedThreadInfo && !selectedThreadInfo.fullScreen}
                        thread={selectedThreadInfo?.thread}
                        onClose={closeThread}
                        onDeleteThread={handleThreadDelete}
                        onEnterFullScreen={openThreadFullScreen}
                        size="large"
                        titleActions={
                            showThreadTraceUI && showControlPlaneDependentFeatures ? (
                                <Button
                                    ref={traceFocusRestorationRef}
                                    icon={<Branch16Regular />}
                                    style={{
                                        fontWeight: 'normal',
                                        fontSize: '12px',
                                        lineHeight: '16px',
                                        padding: '2px 8px 2px 4px',
                                        margin: 'auto',
                                    }}
                                    onClick={openThreadTrace}
                                    disabled={!agentAppInsightsAppId}
                                >
                                    {intl.formatMessage(IncidentManagementResources.viewTrace)}
                                </Button>
                            ) : undefined
                        }
                        titleSuffix={
                            selectedThreadInfo?.thread ? (
                                <ThreadActionsMenu
                                    thread={selectedThreadInfo.thread}
                                    handleThreadDelete={handleThreadDelete}
                                    hideCopyDeeplink={true}
                                />
                            ) : undefined
                        }
                    />
                    <div className={styles.navPanelWrapper}>
                        <div className={mergeClasses(styles.navPanelContent, styles.navPanelPadding)}>
                            <div className={styles.fullHeightFlexContainer}>
                                <PlatformConnectionMessageBar />
                                <div className={styles.incidentFiltersContainer}>
                                    <SearchBox
                                        className={styles.searchBox}
                                        placeholder={intl.formatMessage(SreAgentResources.search)}
                                        value={searchText}
                                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) =>
                                            setSearchText(data.value ?? '')
                                        )}
                                        disabled={!!selectedThreadInfo}
                                        size={'small'}
                                    />
                                    <PillFilterSet dynamicFilters={dynamicFilters} disabled={disableAllControls || !!selectedThreadInfo} />
                                    {incidentPlatformType !== IncidentManagementType.None &&
                                        incidentPlatformType !== IncidentManagementType.AzMonitor && (
                                            <Button
                                                icon={<Flash16Regular />}
                                                appearance="transparent"
                                                className={styles.button}
                                                disabled={disableAllControls || !!selectedThreadInfo}
                                                onClick={() => setIsTriggerAgentDrawerOpen(true)}
                                            >
                                                {intl.formatMessage(TriggerIncidentManagementResources.triggerAgent)}
                                            </Button>
                                        )}
                                    <Button
                                        icon={<ArrowClockwise16Regular />}
                                        appearance="transparent"
                                        className={styles.button}
                                        onClick={() => setRefreshCounter(prev => prev + 1)}
                                    >
                                        {intl.formatMessage(IncidentManagementResources.refresh)}
                                    </Button>
                                    <BulkDeleteDialog
                                        isOpen={isDeleteDialogOpen}
                                        onOpenChange={setIsDeleteDialogOpen}
                                        selectedThreads={selectedThreads}
                                        incidentThreads={incidentThreads}
                                        onConfirmDelete={handleBulkDelete}
                                        disabled={disableAllControls || !!selectedThreadInfo}
                                        className={styles.button}
                                    />
                                </div>
                                <IncidentsSummary threadCounts={threadCounts} loading={incidentThreadsLoading} />
                                <div className={localStyles.tableContainer}>
                                    {incidentThreadsLoading && incidentThreads.length === 0 ? (
                                        <IncidentsSkeletonLoader
                                            showControlPlaneDependentFeatures={showControlPlaneDependentFeatures}
                                            className={localStyles.skeletonRow}
                                        />
                                    ) : (
                                        <div
                                            data-is-scrollable="true"
                                            user-select="text"
                                            className={localStyles.scrollableList}
                                            ref={threadListDivRef}
                                            onScroll={onScroll}
                                        >
                                            <DataGrid
                                                items={incidentThreads ?? []}
                                                columns={columns}
                                                sortable
                                                sortState={{
                                                    sortColumn: sortColumnKey as TableColumnId,
                                                    sortDirection: isSortedDescending ? 'descending' : 'ascending',
                                                }}
                                                onSortChange={(_, nextSortState) => {
                                                    if (nextSortState.sortColumn) {
                                                        setSortColumnKey(nextSortState.sortColumn as IncidentsListColumnKey);
                                                        setIsSortedDescending(nextSortState.sortDirection === 'descending');
                                                    }
                                                }}
                                                selectionMode="multiselect"
                                                selectedItems={selectedThreads}
                                                onSelectionChange={(_, data) => setSelectedThreads(data.selectedItems as Set<string>)}
                                                getRowId={item => item.id}
                                                resizableColumns
                                                className={localStyles.dataGrid}
                                                columnSizingOptions={columnSizingOptions}
                                                size="small"
                                            >
                                                <DataGridHeader className={localStyles.stickyHeader}>
                                                    <DataGridRow>
                                                        {({ renderHeaderCell }) => (
                                                            <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                                                        )}
                                                    </DataGridRow>
                                                </DataGridHeader>
                                                <DataGridBody<Thread>>
                                                    {({ item, rowId }) => (
                                                        <DataGridRow<Thread> key={rowId} className={localStyles.dataGridRow}>
                                                            {({ renderCell, columnId }) => (
                                                                <DataGridCell
                                                                    focusMode={
                                                                        columnId === IncidentsListColumnKey.title ? 'none' : undefined
                                                                    }
                                                                >
                                                                    {renderCell(item)}
                                                                </DataGridCell>
                                                            )}
                                                        </DataGridRow>
                                                    )}
                                                </DataGridBody>
                                            </DataGrid>
                                            {moreThreadsToLoad && !incidentThreadsLoading ? (
                                                // TODO (andimarc): use shimmer row instead
                                                <div ref={intersectionObserverRef} className={localStyles.spinnerContainer}>
                                                    <Spinner
                                                        size="tiny"
                                                        aria-label={intl.formatMessage(SreAgentResources.loadingMoreRows)}
                                                    />
                                                </div>
                                            ) : (
                                                emptyState
                                            )}
                                        </div>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>
                </>
            )}
            {!!selectedThreadInfo?.showTrace && agentAppInsightsAppId && (
                <TracePanel
                    appInsightsAppId={agentAppInsightsAppId}
                    thread={selectedThreadInfo.thread}
                    incident={selectedIncidentDetails}
                    isOpen={!!selectedThreadInfo?.showTrace}
                    onClose={closeThreadTrace}
                    focusRestorationRef={traceFocusRestorationRef}
                />
            )}
            {openedResponsePlan && (
                <ResponsePlanDetailsDrawer
                    isOpen={isViewResponsePlanPanelOpen}
                    onClose={() => setIsViewResponsePlanPanelOpen(false)}
                    responsePlan={openedResponsePlan}
                    onEditHandler={handleEditHandler}
                />
            )}
            <TriggerAgentDrawer isOpen={isTriggerAgentDrawerOpen} onClose={closeTriggerAgentDrawer} />
        </IncidentsOverviewContext.Provider>
    );
};

const useIncidentsOverviewStyles = makeStyles({
    traceViewButton: {
        fontWeight: 'normal',
        fontSize: '12px',
        lineHeight: '16px',
        paddingTop: '2px',
        paddingBottom: '2px',
        paddingLeft: '4px',
        paddingRight: '8px',
        marginLeft: 'auto',
    },
    titleLink: {
        fontSize: '13px',
        lineHeight: '20px',
        overflow: 'hidden',
        width: '100%',
        '> a': {
            textOverflow: 'ellipsis',
        },
    },
    tableContainer: {
        flex: '1',
        display: 'flex',
        flexDirection: 'column',
        minHeight: '0',
        minWidth: '0',
        overflow: 'hidden',
    },
    scrollableList: {
        flex: '1',
        overflowY: 'auto',
        overflowX: 'auto',
        minHeight: '0',
        minWidth: '0',
        scrollbarGutter: 'stable',
    },
    spinnerContainer: {
        height: '20px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        paddingTop: '10px',
        paddingBottom: '10px',
    },
    emptyState: {
        textAlign: 'center',
    },
    skeletonRow: {
        display: 'flex',
        padding: '8px 12px',
        alignItems: 'center',
        gap: '12px',
    },
    dataGrid: {
        width: '100%',
        maxWidth: '100%',
        paddingTop: '5px',
        boxSizing: 'border-box',
    },
    stickyHeader: {
        fontWeight: '600',
        position: 'sticky',
        top: '0',
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: '1',
    },
    dataGridRow: {
        width: '100%',
    },
    noItemsContainer: {
        marginTop: '40px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXXL,
    },
    textContainer: {
        maxWidth: '600px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
    },
    primaryTitle: {
        fontSize: '18px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'nowrap',
    },
    description: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '20px',
        whiteSpace: 'nowrap',
    },
});

export default IncidentsOverview;

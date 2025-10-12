import {
    Button,
    Drawer,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    InputOnChangeData,
    Link,
    SearchBox,
    SearchBoxChangeEvent,
    Spinner,
    tokens,
    Toolbar,
    ToolbarButton,
    ToolbarGroup,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import { ArrowClockwise16Regular, Branch16Regular, Dismiss24Regular, FullScreenMaximize16Regular } from '@fluentui/react-icons';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { debounce } from 'lodash';
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
import { IncidentDocument } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { InvestigationStatus, Thread } from '../../../Common/Contracts/DataPlane/Thread';
import Url from '../../../Common/Helpers/Url';
import { ActivitiesThreadHeaderResources, IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import ThreadActionsMenu from '../../Activities/ThreadActionsMenu';
import { SreAgentContext } from '../../Contracts/Context';
import { TracePanel } from '../../Foundry/app/components/shell/playground/tracing/TracePanel';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import { PlatformConnectionMessageBar } from '../Common/PlatformConnectionMessageBar';
import { IncidentsListColumnKey } from '../CreateIncidentHandler/Contracts';
import IncidentChat from '../IncidentChat';
import {
    getColumnInfo,
    getIncidentStatusIntlString,
    getInvestigationStatusIntlString,
    getPlatformSpecificStrings,
    getPriorities,
} from '../Utilities';
import { IncidentsSummary } from './IncidentsSummary';
import { StatusLabel } from './StatusLabel';
import { useIncidentThreadList } from './useIncidentThreadList';

type ISortedDetailsListColumn<T> = IColumn & {
    sort?: (items: T[], isSortedDescending: boolean) => T[];
    disableColumnClick?: boolean;
};

const showThreadTraceUI = Url.getFeatureValue('showThreadTraceUI') === 'true';

interface SelectedThreadInfo {
    thread: Thread;
    fullScreen: boolean;
    showTrace: boolean;
}

interface IncidentsOverviewProps {
    agentAppInsightsAppId?: string;
    showControlPlaneDependentFeatures?: boolean;
}

const IncidentsOverview: FC<IncidentsOverviewProps> = ({ agentAppInsightsAppId, showControlPlaneDependentFeatures }) => {
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );
    const [selectedIncidentDetails, setSelectedIncidentDetails] = useState<IncidentDocument>();

    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>();
    const [selectedPriorities, setSelectedPriorities] = useState<string[]>([]);
    const [selectedStatuses, setSelectedStatuses] = useState<string[]>([]);
    const [selectedInvestigationStatuses, setSelectedInvestigationStatuses] = useState<string[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<IncidentsListColumnKey | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(true);
    const [refreshCounter, setRefreshCounter] = useState<number>(0);

    const [selectedThreadInfo, setSelectedThreadInfo] = useState<SelectedThreadInfo | null>(null);

    const traceFocusRestorationRef = useRef<HTMLButtonElement>(null);

    const openThreadDrawer = useCallback((thread: Thread) => {
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

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as IncidentsListColumnKey);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending]
    );

    const disableAllControls = useMemo(() => {
        return incidentThreadsLoading;
    }, [incidentThreadsLoading]);

    // Time Range
    const timeRangeOptions: TimeRangeKeyLabelPair[] = useMemo(
        () => [
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
            return (
                <Link style={{ fontSize: '13px' }} onClick={() => openThreadDrawer(item)} disabled={disableAllControls}>
                    {getColumnInfo(IncidentsListColumnKey.title).getColumnValue(item)}
                </Link>
            );
        },
        [openThreadDrawer, disableAllControls]
    );

    const columns = useMemo<ISortedDetailsListColumn<Thread>[]>(() => {
        const columns: ISortedDetailsListColumn<Thread>[] = [
            {
                key: IncidentsListColumnKey.incidentId,
                name: intl.formatMessage(platformSpecificStrings.incidentOrAlertIdLabel),
                fieldName: IncidentsListColumnKey.incidentId,
                isResizable: true,
                minWidth: 100,
                maxWidth: 150,
                isMultiline: true,
                onRender: item => getColumnInfo(IncidentsListColumnKey.incidentId).getColumnValue(item),
                isSorted: sortColumnKey === IncidentsListColumnKey.incidentId,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.title,
                name: intl.formatMessage(platformSpecificStrings.incidentOrAlertTitleLabel),
                fieldName: IncidentsListColumnKey.title,
                isResizable: true,
                minWidth: 150,
                maxWidth: 800,
                isMultiline: true,
                onRender: item => onRenderTitle(item),
                isSorted: sortColumnKey === IncidentsListColumnKey.title,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
        ];

        columns.push(
            {
                key: IncidentsListColumnKey.priority,
                name: intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel),
                fieldName: IncidentsListColumnKey.priority,
                isResizable: true,
                isMultiline: true,
                minWidth: 75,
                maxWidth: 75,
                onRender: item => getColumnInfo(IncidentsListColumnKey.priority).getColumnValue(item),
                isSorted: sortColumnKey === IncidentsListColumnKey.priority,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.incidentStatus,
                name: intl.formatMessage(platformSpecificStrings.incidentOrAlertStatusLabel),
                fieldName: IncidentsListColumnKey.incidentStatus,
                isResizable: true,
                minWidth: 100,
                maxWidth: 250,
                onRender: item => <StatusLabel type="incidentStatus" status={item.status?.incidentStatus?.status} />,
                isSorted: sortColumnKey === IncidentsListColumnKey.incidentStatus,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.agentStatus,
                name: intl.formatMessage(IncidentManagementResources.agentStatus),
                fieldName: IncidentsListColumnKey.agentStatus,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: item =>
                    item.incidentDetails?.investigationStatus ? (
                        <StatusLabel type="investigationStatus" status={item.incidentDetails?.investigationStatus} />
                    ) : (
                        '-'
                    ),
                isSorted: sortColumnKey === IncidentsListColumnKey.agentStatus,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.createdTimestamp,
                name: intl.formatMessage(IncidentManagementResources.incidentCreated),
                fieldName: IncidentsListColumnKey.createdTimestamp,
                isResizable: true,
                minWidth: 100,
                maxWidth: 250,
                isMultiline: true,
                onRender: item => getColumnInfo(IncidentsListColumnKey.createdTimestamp).getColumnValue(item),
                isSorted: sortColumnKey === IncidentsListColumnKey.createdTimestamp,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            }
        );

        if (incidentPlatformType !== IncidentManagementType.AzMonitor) {
            columns.push({
                key: IncidentsListColumnKey.impactedService,
                name: intl.formatMessage(IncidentManagementResources.impactedService),
                fieldName: IncidentsListColumnKey.impactedService,
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: item => getColumnInfo(IncidentsListColumnKey.impactedService).getColumnValue(item),
                isSorted: sortColumnKey === IncidentsListColumnKey.impactedService,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            });
        }

        columns.push({
            key: IncidentsListColumnKey.handler,
            name: intl.formatMessage(IncidentManagementResources.responsePlanName),
            fieldName: IncidentsListColumnKey.handler,
            isResizable: true,
            minWidth: 150,
            maxWidth: 250,
            onRender: item => getColumnInfo(IncidentsListColumnKey.handler).getColumnValue(item),
            isSorted: sortColumnKey === IncidentsListColumnKey.handler,
            isSortedDescending: isSortedDescending,
            onColumnClick: (_, col) => handleColumnClick(col),
        });

        return columns;
    }, [intl, sortColumnKey, isSortedDescending, onRenderTitle, handleColumnClick, platformSpecificStrings, incidentPlatformType]);

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
            selectedValue: selectedTimeRange || { key: TimespanKeys.SevenDays },
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

    return (
        <>
            {selectedThreadInfo?.fullScreen ? (
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
                />
            ) : (
                <>
                    <Drawer
                        {...restoreFocusSourceAttributes}
                        type="overlay"
                        separator
                        open={!!selectedThreadInfo && !selectedThreadInfo.fullScreen}
                        modalType="non-modal"
                        position="end"
                        size="large"
                        style={{ marginTop: '50px', marginBottom: '8px', borderRadius: tokens.borderRadiusXLarge }}
                    >
                        <DrawerHeader style={{ padding: '16px 16px 7px 16px' }}>
                            <DrawerHeaderTitle
                                heading={{
                                    style: {
                                        display: 'flex',
                                        flexDirection: 'row',
                                        gap: 8,
                                        alignItems: 'center',
                                        justifyContent: 'start',
                                        overflow: 'hidden',
                                    },
                                }}
                                action={
                                    <Toolbar>
                                        <ToolbarGroup style={{ display: 'flex', flexDirection: 'row', gap: 8 }}>
                                            <Button
                                                icon={<FullScreenMaximize16Regular />}
                                                style={{
                                                    fontWeight: 'normal',
                                                    fontSize: '12px',
                                                    lineHeight: '16px',
                                                    padding: '2px 8px 2px 4px',
                                                    margin: 'auto',
                                                }}
                                                onClick={openThreadFullScreen}
                                            >
                                                {intl.formatMessage(IncidentManagementResources.fullPage)}
                                            </Button>
                                            {showThreadTraceUI && (
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
                                                >
                                                    {intl.formatMessage(IncidentManagementResources.viewTrace)}
                                                </Button>
                                            )}
                                            <ToolbarButton
                                                aria-label={intl.formatMessage(IncidentManagementResources.closePanel)}
                                                appearance="transparent"
                                                icon={<Dismiss24Regular />}
                                                onClick={closeThread}
                                            />
                                        </ToolbarGroup>
                                    </Toolbar>
                                }
                            >
                                <div style={{ whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                    {selectedThreadInfo?.thread.title}
                                </div>
                                {selectedThreadInfo?.thread && (
                                    <ThreadActionsMenu
                                        thread={selectedThreadInfo.thread}
                                        handleThreadDelete={handleThreadDelete}
                                        hideCopyDeeplink={true}
                                    />
                                )}
                            </DrawerHeaderTitle>
                        </DrawerHeader>
                        <DrawerBody style={{ padding: '0px 16px 0px 0px' }}>
                            {selectedThreadInfo?.thread && (
                                <IncidentChat
                                    selectedThread={selectedThreadInfo.thread}
                                    exitToHome={closeThread}
                                    handleThreadDelete={handleThreadDelete}
                                />
                            )}
                        </DrawerBody>
                    </Drawer>
                    <div className={styles.navPanelWrapper}>
                        <div className={styles.navPanelContent}>
                            <div className={styles.navPanelPadding}>
                                <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
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
                                        />
                                        <PillFilterSet
                                            dynamicFilters={dynamicFilters}
                                            disabled={disableAllControls || !!selectedThreadInfo}
                                        />
                                        <Button
                                            icon={<ArrowClockwise16Regular />}
                                            appearance="transparent"
                                            className={styles.button}
                                            onClick={() => setRefreshCounter(prev => prev + 1)}
                                        >
                                            {intl.formatMessage(IncidentManagementResources.refresh)}
                                        </Button>
                                    </div>
                                    <IncidentsSummary threadCounts={threadCounts} loading={incidentThreadsLoading} />
                                    <div
                                        data-is-scrollable="true"
                                        user-select="text"
                                        style={{
                                            overflowY: 'auto',
                                            overflowX: 'auto',
                                            minHeight: incidentThreads.length < 4 ? 'fit-content' : '200px',
                                        }}
                                        ref={threadListDivRef}
                                        onScroll={onScroll}
                                    >
                                        <ShimmeredDetailsList
                                            columns={columns}
                                            constrainMode={ConstrainMode.horizontalConstrained}
                                            items={incidentThreads ?? []}
                                            layoutMode={DetailsListLayoutMode.justified}
                                            compact={true}
                                            enableShimmer={incidentThreadsLoading}
                                            checkboxVisibility={CheckboxVisibility.always}
                                            useReducedRowRenderer={true}
                                            styles={{
                                                root: {
                                                    width: '100%',
                                                    userSelect: 'text',
                                                },
                                            }}
                                            detailsListStyles={{
                                                root: { overflowX: 'visible', overflowY: 'visible' },
                                                headerWrapper: {
                                                    '& > div': {
                                                        paddingTop: '0px !important',
                                                    },
                                                },
                                            }}
                                            selectionMode={SelectionMode.none}
                                            setKey="incidentFilterList"
                                            getKey={(item, index) => (item && item.id ? item.id : `shimmer-${index}`)}
                                        />
                                        {moreThreadsToLoad && !incidentThreadsLoading ? (
                                            // TODO (andimarc): use shimmer row instead
                                            <div
                                                ref={intersectionObserverRef}
                                                style={{
                                                    height: '20px',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    padding: '10px',
                                                }}
                                            >
                                                <Spinner size="tiny" />
                                            </div>
                                        ) : incidentThreads.length === 0 && !incidentThreadsLoading ? (
                                            <div style={{ textAlign: 'center' }}>
                                                {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
                                            </div>
                                        ) : null}
                                    </div>
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
        </>
    );
};

export default IncidentsOverview;

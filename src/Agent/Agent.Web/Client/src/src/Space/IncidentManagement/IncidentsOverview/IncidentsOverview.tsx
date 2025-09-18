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
    Text,
    tokens,
    Toolbar,
    ToolbarButton,
    ToolbarGroup,
    useRestoreFocusSource,
} from '@fluentui/react-components';
import {
    ArrowClockwise16Regular,
    CheckmarkCircle16Filled,
    Dismiss24Regular,
    FullScreenMaximize16Regular,
    SpinnerIos16Filled,
    Warning16Filled,
} from '@fluentui/react-icons';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { debounce } from 'lodash';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import {
    FilterPropsWithKey,
    RemovableFilterProps,
    TimeRangeKeyLabelPair,
    TimeRangeValue,
    TimespanKeys,
} from '../../../Common/Components/PillFilter/Contracts';
import { LabelKeyPair } from '../../../Common/Components/PillFilter/ListWithSearch';
import { PillFilterSet } from '../../../Common/Components/PillFilter/PillFilterSet';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Thread } from '../../../Common/Contracts/DataPlane/Thread';
import Url from '../../../Common/Helpers/Url';
import { IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import ThreadActionsMenu from '../../Activities/ThreadActionsMenu';
import { SreAgentContext } from '../../Contracts/Context';
import { IncidentManagementPlatform } from '../../Contracts/IncidentManagement';
import { getIncidentManagementPlatform } from '../../Settings/Hooks/useIncidentManagementSettings';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import IncidentChat from '../IncidentChat';
import { getPriorityOrSeverityStrings } from '../Utilities';
import { SortColumn, useIncidentThreadList } from './useIncidentThreadList';

type ISortedDetailsListColumn<T> = IColumn & {
    sort?: (items: T[], isSortedDescending: boolean) => T[];
    disableColumnClick?: boolean;
};

enum IncidentsListColumnKey {
    incidentId = 'incidentId',
    title = 'title',
    createdTimestamp = 'createdTimestamp',
    priority = 'priority',
    status = 'incidentStatus',
    investigation = 'investigation',
    handler = 'handler',
}

enum Priorities {
    P1 = 'P1',
    P2 = 'P2',
}

enum InvestigationStatus {
    AttentionNeeded = 'AttentionNeeded',
    MitigatedByAgent = 'MitigatedByAgent',
    ResolvedByAgent = 'ResolvedByAgent',
    InProgress = 'InvestigationInProgress',
}

interface SelectedThreadInfo {
    thread: Thread;
    fullScreen: boolean;
}

const IncidentsOverview: FC = () => {
    const showMockedComponents = useMemo(() => Url.getFeatureValue('showIncidentOverviewMocked') === 'true', []);

    const sreAgentContext = useContext(SreAgentContext);
    const incidentPlatform = useMemo(() => getIncidentManagementPlatform(sreAgentContext.agentObj), [sreAgentContext.agentObj]);
    const priorityOrSeverityStrings = useMemo(() => getPriorityOrSeverityStrings(incidentPlatform), [incidentPlatform]);

    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>();
    const [selectedPriorities, setSelectedPriorities] = useState<string[]>([]);
    const [selectedStatuses, setSelectedStatuses] = useState<string[]>([]);
    const [selectedActions, setSelectedActions] = useState<string[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<IncidentsListColumnKey | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(true);
    const [refreshCounter, setRefreshCounter] = useState<number>(0);

    const [selectedThreadInfo, setSelectedThreadInfo] = useState<SelectedThreadInfo | null>(null);

    const openThreadDrawer = useCallback((thread: Thread) => {
        setSelectedThreadInfo({ thread, fullScreen: false });
    }, []);

    const openThreadFullScreen = useCallback(() => {
        if (selectedThreadInfo) {
            setSelectedThreadInfo({ ...selectedThreadInfo, fullScreen: true });
        }
    }, [selectedThreadInfo]);

    const closeThread = useCallback(() => {
        setSelectedThreadInfo(null);
    }, []);

    const {
        threads: incidentThreads,
        isLoadingInitialThreads: incidentThreadsLoading,
        moreThreadsToLoad,
        threadListDivRef,
        intersectionObserverRef,
        onScroll,
    } = useIncidentThreadList(
        undefined,
        searchText,
        selectedStatuses,
        selectedTimeRange,
        sortColumnKey as SortColumn | undefined,
        isSortedDescending,
        !selectedThreadInfo?.fullScreen,
        refreshCounter
    );

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
    const priorityOptions: LabelKeyPair[] = useMemo(
        () => [
            {
                key: Priorities.P1,
                label: intl.formatMessage(IncidentManagementResources.p1),
            },
            {
                key: Priorities.P2,
                label: intl.formatMessage(IncidentManagementResources.p2),
            },
        ],
        [intl]
    );
    // End: Priority

    // Status
    const statusOptions: LabelKeyPair[] = useMemo(
        () => [
            {
                key: IncidentStatus.triggered,
                label: intl.formatMessage(SreAgentResources.triggered),
            },
            {
                key: IncidentStatus.new,
                label: intl.formatMessage(SreAgentResources.new),
            },
            {
                key: IncidentStatus.active,
                label: intl.formatMessage(SreAgentResources.active),
            },
            {
                key: IncidentStatus.acknowledged,
                label: intl.formatMessage(SreAgentResources.acknowledged),
            },
            {
                key: IncidentStatus.mitigated,
                label: intl.formatMessage(SreAgentResources.mitigated),
            },
            {
                key: IncidentStatus.closed,
                label: intl.formatMessage(SreAgentResources.closed),
            },
            {
                key: IncidentStatus.resolved,
                label: intl.formatMessage(SreAgentResources.resolved),
            },
        ],
        [intl]
    );

    const getStatusText = useCallback(
        (status?: string): string => {
            if (status) {
                switch (status.toLowerCase()) {
                    case IncidentStatus.acknowledged:
                        return intl.formatMessage(SreAgentResources.acknowledged);
                    case IncidentStatus.triggered:
                        return intl.formatMessage(SreAgentResources.triggered);
                    case IncidentStatus.mitigated:
                        return intl.formatMessage(SreAgentResources.mitigated);
                    case IncidentStatus.closed:
                        return intl.formatMessage(SreAgentResources.closed);
                    case IncidentStatus.resolved:
                        return intl.formatMessage(SreAgentResources.resolved);
                    case IncidentStatus.active:
                        return intl.formatMessage(SreAgentResources.active);
                    case IncidentStatus.new:
                        return intl.formatMessage(SreAgentResources.new);
                }
            }
            return intl.formatMessage(
                incidentPlatform === IncidentManagementPlatform.AzMonitor
                    ? SreAgentResources.new
                    : incidentPlatform === IncidentManagementPlatform.PagerDuty
                      ? SreAgentResources.triggered
                      : SreAgentResources.active
            );
        },
        [incidentPlatform, intl]
    );
    // End: Status

    // Actions
    const getInvestigationProps = useCallback(
        (investigationStatus?: InvestigationStatus) => {
            switch (investigationStatus) {
                case InvestigationStatus.AttentionNeeded:
                    return {
                        icon: <Warning16Filled className={styles.warningIcon} aria-label={''} />,
                        text: intl.formatMessage(IncidentManagementResources.attentionNeeded),
                    };
                case InvestigationStatus.MitigatedByAgent:
                    return {
                        icon: <CheckmarkCircle16Filled className={styles.greenCheckIcon} aria-label={''} />,
                        text: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                    };
                case InvestigationStatus.ResolvedByAgent:
                    return {
                        icon: <CheckmarkCircle16Filled className={styles.greenCheckIcon} aria-label={''} />,
                        text: intl.formatMessage(IncidentManagementResources.resolvedByAgent),
                    };
                case InvestigationStatus.InProgress:
                    return {
                        icon: <SpinnerIos16Filled className={styles.spinnerIcon} aria-label={''} />,
                        text: intl.formatMessage(IncidentManagementResources.investigationInProgress),
                    };
            }
            return {};
        },
        [styles, intl]
    );

    const actionOptions: LabelKeyPair[] = useMemo(
        () =>
            Object.values(InvestigationStatus).map(action => ({
                key: action ?? '',
                label: getInvestigationProps(action as InvestigationStatus).text ?? '',
            })),
        [getInvestigationProps]
    );
    // End: Actions Filter

    const onRenderTitle = useCallback(
        (item: Thread) => {
            return (
                <Link style={{ fontSize: '13px' }} onClick={() => openThreadDrawer(item)} disabled={disableAllControls}>
                    {item.title}
                </Link>
            );
        },
        [openThreadDrawer, disableAllControls]
    );

    const onRenderPriority = useCallback((_item: Thread) => {
        // Use a random priority for demonstration purposes
        const priority = Math.random() > 0.5 ? Priorities.P1 : Priorities.P2;
        return (
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'row',
                    alignItems: 'stretch',
                    gap: '8px',
                    position: 'relative',
                }}
            >
                <div
                    style={{
                        border: `2px solid ${getPriorityColor(priority)}`,
                        borderRadius: '4px',
                        top: 0,
                        bottom: 0,
                    }}
                />
                <div>{priority}</div>
            </div>
        );
    }, []);

    const onRenderInvestigation = useCallback(
        (_item: Thread) => {
            // Use a random state for demonstration purposes
            const randomNum = Math.random();
            const state =
                randomNum < 0.25
                    ? InvestigationStatus.AttentionNeeded
                    : randomNum < 0.5
                      ? InvestigationStatus.MitigatedByAgent
                      : randomNum < 0.75
                        ? InvestigationStatus.ResolvedByAgent
                        : InvestigationStatus.InProgress;

            const { icon, text } = getInvestigationProps(state);

            return icon && text ? (
                <div className={styles.setUp}>
                    {icon}
                    <Link style={{ fontSize: '13px' }} onClick={() => {}} disabled={disableAllControls}>
                        {text}
                    </Link>
                </div>
            ) : null;
        },
        [disableAllControls, getInvestigationProps, styles.setUp]
    );

    const columns = useMemo<ISortedDetailsListColumn<Thread>[]>(() => {
        const columns: ISortedDetailsListColumn<Thread>[] = [
            {
                key: IncidentsListColumnKey.incidentId,
                name: intl.formatMessage(IncidentManagementResources.incidentId),
                fieldName: IncidentsListColumnKey.incidentId,
                isResizable: true,
                minWidth: 100,
                maxWidth: 150,
                isMultiline: true,
                onRender: (item: Thread) => item.status?.incidentStatus?.incidentId,
                isSorted: sortColumnKey === IncidentsListColumnKey.incidentId,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.title,
                name: intl.formatMessage(IncidentManagementResources.title),
                fieldName: IncidentsListColumnKey.title,
                isResizable: true,
                minWidth: 150,
                maxWidth: 800,
                isMultiline: true,
                onRender: onRenderTitle,
                isSorted: sortColumnKey === IncidentsListColumnKey.title,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentsListColumnKey.createdTimestamp,
                name: intl.formatMessage(IncidentManagementResources.processedTime),
                fieldName: IncidentsListColumnKey.createdTimestamp,
                isResizable: true,
                minWidth: 100,
                maxWidth: 250,
                isMultiline: true,
                onRender: (item: Thread) => item.createdTimestamp,
                isSorted: sortColumnKey === IncidentsListColumnKey.createdTimestamp,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
        ];

        if (showMockedComponents) {
            columns.push({
                key: IncidentsListColumnKey.priority,
                name: intl.formatMessage(priorityOrSeverityStrings.fieldLabel),
                fieldName: IncidentsListColumnKey.priority,
                isResizable: true,
                isMultiline: true,
                minWidth: 75,
                maxWidth: 75,
                onRender: onRenderPriority,
                isSorted: sortColumnKey === IncidentsListColumnKey.priority,
                isSortedDescending: isSortedDescending,
                onColumnClick: (_, col) => handleColumnClick(col),
            });
        }

        columns.push({
            key: IncidentsListColumnKey.status,
            name: intl.formatMessage(IncidentManagementResources.status),
            fieldName: IncidentsListColumnKey.status,
            isResizable: true,
            minWidth: 100,
            maxWidth: 250,
            onRender: item => getStatusText(item.status?.incidentStatus?.status),
            isSorted: sortColumnKey === IncidentsListColumnKey.status,
            isSortedDescending: isSortedDescending,
            onColumnClick: (_, col) => handleColumnClick(col),
        });

        if (showMockedComponents) {
            columns.push(
                {
                    key: IncidentsListColumnKey.investigation,
                    name: intl.formatMessage(IncidentManagementResources.investigation),
                    fieldName: IncidentsListColumnKey.investigation,
                    isResizable: true,
                    isMultiline: true,
                    minWidth: 150,
                    maxWidth: 250,
                    onRender: onRenderInvestigation,
                    isSorted: sortColumnKey === IncidentsListColumnKey.investigation,
                    isSortedDescending: isSortedDescending,
                    onColumnClick: (_, col) => handleColumnClick(col),
                },
                {
                    key: IncidentsListColumnKey.handler,
                    name: intl.formatMessage(IncidentManagementResources.handler),
                    fieldName: IncidentsListColumnKey.handler,
                    isResizable: true,
                    minWidth: 150,
                    maxWidth: 250,
                    onRender: () => '<Handler name>',
                    isSorted: sortColumnKey === IncidentsListColumnKey.investigation,
                    isSortedDescending: isSortedDescending,
                    onColumnClick: (_, col) => handleColumnClick(col),
                }
            );
        }

        return columns;
    }, [
        intl,
        sortColumnKey,
        isSortedDescending,
        onRenderTitle,
        showMockedComponents,
        handleColumnClick,
        priorityOrSeverityStrings.fieldLabel,
        onRenderPriority,
        getStatusText,
        onRenderInvestigation,
    ]);

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

    const actionFilterProps: RemovableFilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.investigation),
            disabled: disableAllControls || !!selectedThreadInfo,
            onRemove: () => setSelectedActions([]),
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: actionOptions,
            onApply: setSelectedActions,
            selectedKeys: selectedActions,
            multiSelect: true,
            addAllOption: true,
        }),
        [disableAllControls, intl, selectedActions, selectedThreadInfo, actionOptions]
    );

    const dynamicFilters: FilterPropsWithKey[] = useMemo(() => {
        const filters = [
            {
                key: IncidentsListColumnKey.createdTimestamp,
                props: timeFilterProps,
            },
            {
                key: IncidentsListColumnKey.status,
                props: statusFilterProps,
            },
        ];

        if (showMockedComponents) {
            filters.push(
                {
                    key: IncidentsListColumnKey.priority,
                    props: priorityFilterProps,
                },
                {
                    key: IncidentsListColumnKey.investigation,
                    props: actionFilterProps,
                }
            );
        }

        return filters;
    }, [timeFilterProps, statusFilterProps, showMockedComponents, priorityFilterProps, actionFilterProps]);

    return selectedThreadInfo?.fullScreen ? (
        <IncidentChat selectedThread={selectedThreadInfo.thread} exitToHome={closeThread} isExpandedView={true} />
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
                            style: { whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' },
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
                        {selectedThreadInfo?.thread.title}
                        {selectedThreadInfo?.thread && (
                            <ThreadActionsMenu
                                thread={selectedThreadInfo.thread}
                                handleThreadDelete={() => {}}
                                hideCopyDeeplink={true}
                                hideDelete={true}
                            />
                        )}
                    </DrawerHeaderTitle>
                </DrawerHeader>
                <DrawerBody style={{ padding: '0px 16px 0px 0px' }}>
                    {selectedThreadInfo?.thread && <IncidentChat selectedThread={selectedThreadInfo.thread} exitToHome={closeThread} />}
                </DrawerBody>
            </Drawer>
            <div className={styles.navPanelWrapper}>
                <div className={styles.navPanelContent}>
                    <div className={styles.navPanelPadding}>
                        <div style={{ width: '100%', height: '100%', display: 'flex', flexDirection: 'column' }}>
                            <div className={styles.incidentFiltersContainer}>
                                <SearchBox
                                    className={styles.searchBox}
                                    placeholder={intl.formatMessage(SreAgentResources.search)}
                                    value={searchText}
                                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) =>
                                        setSearchText(data.value ?? '')
                                    )}
                                    disabled={disableAllControls || !!selectedThreadInfo}
                                />
                                <PillFilterSet dynamicFilters={dynamicFilters} disabled={disableAllControls || !!selectedThreadInfo} />
                                <Button
                                    icon={<ArrowClockwise16Regular />}
                                    appearance="transparent"
                                    className={styles.button}
                                    onClick={() => setRefreshCounter(prev => prev + 1)}
                                >
                                    {intl.formatMessage(IncidentManagementResources.refresh)}
                                </Button>
                            </div>
                            {showMockedComponents && (
                                <div style={{ display: 'flex', flexDirection: 'row', gap: '20px', margin: '20px 0px 20px -3px' }}>
                                    <SummaryBox
                                        title={intl.formatMessage(priorityOrSeverityStrings.fieldLabelPlural)}
                                        fields={[
                                            {
                                                color: getPriorityColor(Priorities.P1),
                                                label: intl.formatMessage(IncidentManagementResources.p1),
                                                value: 0,
                                            },
                                            {
                                                color: getPriorityColor(Priorities.P2),
                                                label: intl.formatMessage(IncidentManagementResources.p2),
                                                value: 0,
                                            },
                                        ]}
                                    />
                                    <SummaryBox
                                        title={intl.formatMessage(IncidentManagementResources.investigations)}
                                        fields={[
                                            {
                                                color: '',
                                                label: intl.formatMessage(IncidentManagementResources.attentionNeeded),
                                                value: 0,
                                            },
                                            {
                                                color: '',
                                                label: intl.formatMessage(IncidentManagementResources.inProgress),
                                                value: 0,
                                            },
                                            {
                                                color: '',
                                                label: intl.formatMessage(IncidentManagementResources.acknowledged),
                                                value: 0,
                                            },
                                        ]}
                                    />
                                </div>
                            )}
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
    );
};

export default IncidentsOverview;

const getPriorityColor = (priority: Priorities) => {
    return priority === Priorities.P1 ? tokens.colorStatusDangerBackground3 : tokens.colorStatusWarningBackground3;
};

const SummaryBox: FC<{ title: string; fields: { color: string; label: string; value: number }[] }> = ({ title, fields }) => {
    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'column',
                gap: '10px',
                padding: '8px 12px',
                marginLeft: '4px',
                boxShadow: '0px 1.6px 3.6px 0px #00000021, 0px 0.3px 0.9px 0px #0000001A',
                borderRadius: tokens.borderRadiusXLarge,
            }}
        >
            <Text weight="semibold">{title}</Text>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'row',
                    gap: '16px',
                }}
            >
                {fields.map(field => (
                    <div
                        key={field.label}
                        style={{
                            display: 'flex',
                            flexDirection: 'column',
                            borderLeft: `4px solid ${field.color}`,
                            paddingLeft: '8px',
                            paddingRight: '8px',
                        }}
                    >
                        <Text>{field.label}</Text>
                        <Text weight="bold">{field.value}</Text>
                    </div>
                ))}
            </div>
        </div>
    );
};

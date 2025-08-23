import {
    Button,
    Drawer,
    DrawerBody,
    DrawerHeader,
    DrawerHeaderTitle,
    Dropdown,
    InputOnChangeData,
    Link,
    Option,
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
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Thread } from '../../../Common/Contracts/DataPlane/Thread';
import Url from '../../../Common/Helpers/Url';
import { IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import IncidentChat from '../IncidentChat';
import { useIncidentThreadList } from './useIncidentThreadList';

type ISortedDetailsListColumn<T> = IColumn & {
    sort?: (items: T[], isSortedDescending: boolean) => T[];
    disableColumnClick?: boolean;
};

enum IncidentsListColumnKey {
    id = 'id',
    title = 'title',
    priority = 'priority',
    status = 'status',
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

const all = 'all';

type LabelValuePair = { label: string; value: string };

interface SelectedThreadInfo {
    thread: Thread;
    fullScreen: boolean;
}

const IncidentsOverview: FC = () => {
    const showMockedComponents = useMemo(() => Url.getFeatureValue('showIncidentOverviewMocked') === 'true', []);

    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [priority, setPriority] = useState<string>(all);
    const [selectedStatuses, setSelectedStatuses] = useState<string[]>([]);
    const [action, setAction] = useState<string>(all);
    const [priorityOptions, setIncidentPriorities] = useState<LabelValuePair[]>([]);
    const [actionOptions, setActionOptions] = useState<LabelValuePair[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<IncidentsListColumnKey | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);
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
        isLoadingInitialChatMessages: incidentThreadsLoading,
        moreThreadsToLoad,
        threadListDivRef,
        intersectionObserverRef,
        onScroll,
    } = useIncidentThreadList(undefined, searchText, selectedStatuses, !selectedThreadInfo?.fullScreen, refreshCounter);

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            if (!showMockedComponents) {
                return;
            }
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as IncidentsListColumnKey);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending, showMockedComponents]
    );

    const disableAllControls = useMemo(() => {
        return incidentThreadsLoading;
    }, [incidentThreadsLoading]);

    const isIncidentFilterEmpty = useMemo(() => {
        return priority === all && selectedStatuses.length === 0 && action === all && searchText.trim() === '';
    }, [priority, selectedStatuses, action, searchText]);

    // Priorities Filter
    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        // TODO (andimarc): get priorities from API
        setIncidentPriorities([
            { value: all, label: intl.formatMessage(IncidentManagementResources.allPriorities) },
            { value: Priorities.P1, label: intl.formatMessage(IncidentManagementResources.p1) },
            { value: Priorities.P1, label: intl.formatMessage(IncidentManagementResources.p2) },
        ]);
    }, [isIncidentFilterEmpty, intl]);

    const getPriorityOptionLabel = (option: string): string => {
        switch (option) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allPriorities);
            default:
                return option;
        }
    };

    const onPriorityChange = useCallback(
        (priority: string) => {
            setPriority(priority);
        },
        [setPriority]
    );
    // End: Priorities Filter

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
                }
            }
            return intl.formatMessage(SreAgentResources.active);
        },
        [intl]
    );

    // Statuses Filter
    const statusOptions: LabelValuePair[] = useMemo(
        () => [
            {
                value: all,
                label: intl.formatMessage(IncidentManagementResources.allStatuses),
            },
            {
                value: IncidentStatus.triggered,
                label: intl.formatMessage(SreAgentResources.triggered),
            },
            {
                value: IncidentStatus.active,
                label: intl.formatMessage(SreAgentResources.active),
            },
            {
                value: IncidentStatus.acknowledged,
                label: intl.formatMessage(SreAgentResources.acknowledged),
            },
            {
                value: IncidentStatus.mitigated,
                label: intl.formatMessage(SreAgentResources.mitigated),
            },
            {
                value: IncidentStatus.closed,
                label: intl.formatMessage(SreAgentResources.closed),
            },
            {
                value: IncidentStatus.resolved,
                label: intl.formatMessage(SreAgentResources.resolved),
            },
        ],
        [isIncidentFilterEmpty, incidentThreads, intl]
    );

    const getStatusLabel = useCallback(
        (values: string[]): string => {
            if (values.length === 0 || values.length === statusOptions.length) {
                return intl.formatMessage(IncidentManagementResources.allStatuses);
            }
            return values.map(value => getStatusText(value)).join(', ');
        },
        [intl, statusOptions.length]
    );

    const onStatusChange = useCallback(
        (values: string[]) => {
            setSelectedStatuses(currentValues => {
                if (values.length === 0) {
                    //return [];
                    return statusOptions.map(option => option.value);
                }

                const wasAllOptionPreviouslySelected = currentValues.some(currentValue => currentValue === all);

                if (values.some(value => value === all)) {
                    // The "All" option is selected.

                    if (wasAllOptionPreviouslySelected) {
                        // The "All" was already selected. This means something else changed (was deselected), so we should also deselect the "All" option.
                        return values.filter(value => value !== all);
                    }

                    // The "All" option was not already selected, and now it is. This means we should select everything.
                    return statusOptions.map(option => option.value);
                }

                // The "All" option is not selected.

                if (wasAllOptionPreviouslySelected) {
                    // The "All" option was selected, but now it isn't. This means we should deselect everything.
                    return [];
                }

                // The "All" option was not already selected, and still isn't selected.
                // If everything but the "All" option is selected, we should select everything.
                // Otherwise, we should use the selected values as they are.
                return values.length === statusOptions.length - 1 ? statusOptions.map(option => option.value) : values;
            });
        },
        [setSelectedStatuses, statusOptions]
    );
    // End: Statuses Filter

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

    // Actions Filter
    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        // TODO (andimarc): get options from API
        const uniqueActions = Object.values(InvestigationStatus);
        const actionOptions = uniqueActions.map(action => ({
            value: action ?? '',
            label: getInvestigationProps(action as InvestigationStatus).text ?? '',
        }));

        setActionOptions([{ value: all, label: intl.formatMessage(IncidentManagementResources.allActions) }, ...actionOptions]);
    }, [isIncidentFilterEmpty, incidentThreads, intl]);

    const getActionLabel = (value: string): string => {
        switch (value) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allActions);
            default:
                return getInvestigationProps(value as InvestigationStatus).text ?? '';
        }
    };

    const onActionChange = useCallback(
        (action: string) => {
            setAction(action);
        },
        [setAction]
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
        [disableAllControls, styles.greenCheckIcon, styles.setUp, styles.spinnerIcon, styles.warningIcon]
    );

    const columns = useMemo<ISortedDetailsListColumn<Thread>[]>(() => {
        const columns: ISortedDetailsListColumn<Thread>[] = [
            {
                key: IncidentsListColumnKey.id,
                name: intl.formatMessage(IncidentManagementResources.incidentId),
                fieldName: IncidentsListColumnKey.id,
                isResizable: true,
                minWidth: 100,
                maxWidth: 150,
                isMultiline: true,
                onRender: (item: Thread) => item.status?.incidentStatus?.incidentId,
                isSorted: sortColumnKey === (IncidentsListColumnKey.id as IncidentsListColumnKey),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.id as IncidentsListColumnKey) ? isSortedDescending : undefined,
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
                isSorted: sortColumnKey === (IncidentsListColumnKey.title as IncidentsListColumnKey),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.title as IncidentsListColumnKey) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
        ];

        if (showMockedComponents) {
            columns.push({
                key: IncidentsListColumnKey.priority,
                name: intl.formatMessage(IncidentManagementResources.priority),
                fieldName: IncidentsListColumnKey.priority,
                isResizable: true,
                isMultiline: true,
                minWidth: 75,
                maxWidth: 75,
                onRender: onRenderPriority,
                isSorted: sortColumnKey === (IncidentsListColumnKey.priority as IncidentsListColumnKey),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.priority as IncidentsListColumnKey) ? isSortedDescending : undefined,
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
            onRender: item => getStatusText(item.status?.incidentStatus?.status || IncidentStatus.active),
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
                    isSorted: sortColumnKey === (IncidentsListColumnKey.investigation as IncidentsListColumnKey),
                    onColumnClick: (_, col) => handleColumnClick(col),
                    isSortedDescending:
                        sortColumnKey === (IncidentsListColumnKey.investigation as IncidentsListColumnKey) ? isSortedDescending : undefined,
                },
                {
                    key: IncidentsListColumnKey.handler,
                    name: intl.formatMessage(IncidentManagementResources.handler),
                    fieldName: IncidentsListColumnKey.handler,
                    isResizable: true,
                    minWidth: 150,
                    maxWidth: 250,
                    onRender: () => '<Handler name>',
                }
            );
        }

        return columns;
    }, [
        intl,
        sortColumnKey,
        isSortedDescending,
        onRenderPriority,
        onRenderInvestigation,
        onRenderTitle,
        handleColumnClick,
        showMockedComponents,
    ]);

    const restoreFocusSourceAttributes = useRestoreFocusSource();

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
                            <div>
                                <div className={styles.incidentFiltersContainer}>
                                    <SearchBox
                                        className={styles.searchBox}
                                        placeholder={intl.formatMessage(SreAgentResources.search)}
                                        value={searchText}
                                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) =>
                                            setSearchText(data.value ?? '')
                                        )}
                                    />
                                    <Dropdown
                                        onOptionSelect={(_e, data) => onStatusChange(data.selectedOptions)}
                                        value={getStatusLabel(selectedStatuses)}
                                        selectedOptions={selectedStatuses}
                                        button={
                                            <span style={{ whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                                                {getStatusLabel(selectedStatuses)}
                                            </span>
                                        }
                                        className={styles.searchBox}
                                        disabled={disableAllControls}
                                        multiselect={true}
                                    >
                                        {statusOptions.map(option => (
                                            <Option key={option.value} value={option.value} text={option.label}>
                                                {option.label}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                    {showMockedComponents && (
                                        <>
                                            <Dropdown
                                                onOptionSelect={(_e, data) => onPriorityChange((data.optionValue as string) ?? all)}
                                                value={priority}
                                                selectedOptions={[priority]}
                                                button={<span>{getPriorityOptionLabel(priority)}</span>}
                                                className={styles.searchBox}
                                                disabled={disableAllControls}
                                            >
                                                {priorityOptions.map(option => (
                                                    <Option value={option.value} text={option.label}>
                                                        {option.label}
                                                    </Option>
                                                ))}
                                            </Dropdown>
                                            <Dropdown
                                                onOptionSelect={(_e, data) => onActionChange(data.optionValue ?? all)}
                                                value={action}
                                                selectedOptions={[action]}
                                                button={<span>{getActionLabel(action)}</span>}
                                                className={styles.searchBox}
                                                disabled={disableAllControls}
                                            >
                                                {actionOptions.map(option => (
                                                    <Option value={option.value} text={option.label}>
                                                        {option.label}
                                                    </Option>
                                                ))}
                                            </Dropdown>
                                        </>
                                    )}
                                    <Button
                                        icon={<ArrowClockwise16Regular />}
                                        appearance="transparent"
                                        className={styles.button}
                                        onClick={() => setRefreshCounter(prev => prev + 1)}
                                    >
                                        {intl.formatMessage(IncidentManagementResources.refresh)}
                                    </Button>
                                </div>
                            </div>
                            {showMockedComponents && (
                                <div style={{ display: 'flex', flexDirection: 'row', gap: '20px', margin: '20px 0px 20px -3px' }}>
                                    <SummaryBox
                                        title={intl.formatMessage(IncidentManagementResources.priorities)}
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
                                    detailsListStyles={{ root: { overflowX: 'visible', overflowY: 'visible' } }}
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

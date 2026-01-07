import { Checkbox, InputOnChangeData, Link, SearchBox, SearchBoxChangeEvent, Tooltip } from '@fluentui/react-components';
import { CheckmarkCircle16Regular } from '@fluentui/react-icons';
import { ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import debounce from 'lodash/debounce';
import { Dispatch, FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { FilterProps } from '../../Common/Components/PillFilter/Contracts';
import { LabelKeyPair } from '../../Common/Components/PillFilter/ListWithSearch';
import { PillFilterSet } from '../../Common/Components/PillFilter/PillFilterSet';
import { IncidentFilter, IncidentHandler } from '../../Common/Contracts/Azure/IncidentHandler';
import { AgentMode, IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../Hooks/useAgentSiteNavigate';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { IncidentManagementEmptyState } from './Common/IncidentManagementEmptyState';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import { getPlatformSpecificStrings } from './Utilities';

export type ISortedDetailsListColumn = IColumn & {
    sort?: (items: any[], isSortedDescending: boolean) => any[];
    disableColumnClick?: boolean;
};

enum IncidentsListColumnKey {
    selected = 'selected',
    id = 'id',
    impactedService = 'impactedService',
    priority = 'priority',
    status = 'status',
    type = 'incidentType',
    titleContains = 'titleContains',
    customHandler = 'customHandler',
    agentMode = 'agentMode',
}

export type LabelValuePair = { label: string; value: string };

export type IncidentFilterType = { incidentType: string; impactedService: string; priority: string };

export type ReponsePlanGridProps = {
    incidentFilters: IncidentFilter[];
    incidentFiltersLoading: boolean;
    filterIdToHandlerMap: Record<string, IncidentHandler>;
    selectedFilter: IncidentFilter | undefined;
    setSelectedFilter: Dispatch<React.SetStateAction<IncidentFilter | undefined>>;
    openHandlerCreate: (handlerCreateOrEditInfo: HandlerCreateOrEditInfo) => void;
    handlerOperationStatus: OperationStatus | undefined;
    disabled: boolean;
    canWriteIncidentManagement?: boolean;
};

const ResponsePlanGrid: FC<ReponsePlanGridProps> = (props: ReponsePlanGridProps) => {
    const {
        incidentFilters,
        incidentFiltersLoading,
        openHandlerCreate,
        handlerOperationStatus,
        filterIdToHandlerMap,
        selectedFilter,
        setSelectedFilter,
        disabled,
        canWriteIncidentManagement = true,
    } = props;
    const intl = useIntl();
    const navigate = useAgentSiteNavigate();
    const styles = useIncidentManagementStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [selectedIncidentTypes, setSelectedIncidentTypes] = useState<string[]>([]);
    const [selectedImpactedServices, setSelectedImpactedServices] = useState<string[]>([]);
    const [selectedPriorities, setSelectedPriorities] = useState<string[]>([]);
    const [priorityOptions, setIncidentPriorities] = useState<LabelKeyPair[]>([]);
    const [incidentTypeOptions, setIncidentTypes] = useState<LabelKeyPair[]>([]);
    const [impactedServiceOptions, setImpactedServices] = useState<LabelKeyPair[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentFilter | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const incidentManagementConfigured = useMemo(
        () => incidentPlatformType && incidentPlatformType !== IncidentManagementType.None,
        [incidentPlatformType]
    );

    const filteredGridItems = useMemo(() => {
        let filteredGridItems = incidentFilters;
        if (searchText.trim() !== '') {
            filteredGridItems = filteredGridItems.filter(item => item.id.includes(searchText.trim()));
        }
        if (selectedIncidentTypes.length && selectedIncidentTypes.length !== incidentTypeOptions.length) {
            filteredGridItems = filteredGridItems.filter(item => selectedIncidentTypes.includes(item.incidentType));
        }
        if (selectedImpactedServices.length && selectedImpactedServices.length !== impactedServiceOptions.length) {
            filteredGridItems = filteredGridItems.filter(item => selectedImpactedServices.includes(item.impactedService));
        }
        if (selectedPriorities.length && selectedPriorities.length !== priorityOptions.length) {
            filteredGridItems = filteredGridItems.filter(item => selectedPriorities.includes(item.priority));
        }

        return filteredGridItems;
    }, [
        incidentFilters,
        searchText,
        selectedIncidentTypes,
        incidentTypeOptions.length,
        selectedImpactedServices,
        impactedServiceOptions.length,
        selectedPriorities,
        priorityOptions.length,
    ]);

    const isIncidentFilterEmpty = useMemo(() => {
        return (
            (!selectedIncidentTypes.length || selectedIncidentTypes.length === incidentTypeOptions.length) &&
            (!selectedImpactedServices.length || selectedImpactedServices.length === impactedServiceOptions.length) &&
            (!selectedPriorities.length || selectedPriorities.length === priorityOptions.length) &&
            searchText.trim() === ''
        );
    }, [
        selectedIncidentTypes.length,
        incidentTypeOptions.length,
        selectedImpactedServices.length,
        impactedServiceOptions.length,
        selectedPriorities.length,
        priorityOptions.length,
        searchText,
    ]);

    const sortedItems = useMemo(() => {
        if (!sortColumnKey) return filteredGridItems;

        return [...filteredGridItems].sort((a, b) => {
            const valA = a[sortColumnKey] ?? '';
            const valB = b[sortColumnKey] ?? '';

            if (valA === valB) return 0;
            return (valA > valB ? 1 : -1) * (isSortedDescending ? -1 : 1);
        });
    }, [filteredGridItems, sortColumnKey, isSortedDescending]);

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as keyof IncidentFilter);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending]
    );

    const disableAllControls = useMemo(() => {
        return handlerOperationStatus === 'inprogress' || disabled || incidentFiltersLoading || !incidentManagementConfigured;
    }, [handlerOperationStatus, disabled, incidentFiltersLoading, incidentManagementConfigured]);

    const getDisplayValueOrAll = useCallback(
        (value: string | undefined): string => {
            return value && value.trim() !== '' ? value : intl.formatMessage(SreAgentResources.all);
        },
        [intl]
    );

    const getDisplayValueOrDash = useCallback((value: string | undefined): string => {
        return value && value.trim() !== '' ? value : '-';
    }, []);

    const isAzMonitorFilter = useCallback((filter: IncidentFilter): boolean => {
        return filter.documentType?.toLowerCase().includes('azmonitor') ?? false;
    }, []);

    const shouldHideAzMonitorColumns = useMemo(() => {
        const isAzureMonitor = incidentPlatformType === IncidentManagementType.AzMonitor;
        const hasOtherPlatformFilters = incidentFilters.some(filter => !isAzMonitorFilter(filter));
        return isAzureMonitor && !hasOtherPlatformFilters;
    }, [incidentPlatformType, incidentFilters, isAzMonitorFilter]);

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueIncidentPriorities = Array.from(
            new Set(incidentFilters.map(item => item.priority).filter(priority => priority && priority.trim() !== ''))
        );

        const incidentTypeOptions = uniqueIncidentPriorities.map(priority => ({
            key: priority ?? '',
            value: priority ?? '',
            label: priority ?? '',
        }));

        setIncidentPriorities(incidentTypeOptions);
    }, [isIncidentFilterEmpty, incidentFilters, intl, platformSpecificStrings]);

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueIncidentTypes = Array.from(
            new Set(incidentFilters.map(item => item.incidentType).filter(type => type && type.trim() !== ''))
        );

        const incidentTypeOptions = uniqueIncidentTypes.map(type => ({
            key: type ?? '',
            value: type ?? '',
            label: type ?? '',
        }));

        setIncidentTypes(incidentTypeOptions);
    }, [isIncidentFilterEmpty, incidentFilters, intl]);

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueImpactedServices = Array.from(
            new Set(incidentFilters.map(item => item.impactedService).filter(service => service && service.trim() !== ''))
        );

        const impactedServiceOptions = uniqueImpactedServices.map(name => ({
            key: name ?? '',
            value: name ?? '',
            label: name ?? '',
        }));

        setImpactedServices(impactedServiceOptions);
    }, [isIncidentFilterEmpty, incidentFilters, intl]);

    const onIdClick = useCallback(
        (item: IncidentFilter) => {
            const handler = filterIdToHandlerMap[item.id ?? ''];
            openHandlerCreate({ filter: item, handlerId: handler?.id });
        },
        [filterIdToHandlerMap, openHandlerCreate]
    );

    const disableEditActions = disableAllControls || !canWriteIncidentManagement;

    const onRenderId = useCallback(
        (item: IncidentFilter) => {
            const tooltipMsg =
                disableEditActions && !canWriteIncidentManagement
                    ? intl.formatMessage(IncidentManagementResources.noPermissionEditIncidentHandler)
                    : null;
            const displayValue = getDisplayValueOrAll(item.id);
            const link = (
                <Link style={{ userSelect: 'text', fontSize: '13px' }} onClick={() => onIdClick(item)} disabled={disableEditActions}>
                    {displayValue}
                </Link>
            );
            return tooltipMsg ? (
                <Tooltip relationship="label" content={tooltipMsg}>
                    {link}
                </Tooltip>
            ) : (
                link
            );
        },
        [onIdClick, disableEditActions, canWriteIncidentManagement, intl, getDisplayValueOrAll]
    );

    const onRenderStatus = useCallback(
        (item: IncidentFilter) => {
            return (
                <div style={{ userSelect: 'text' }}>
                    {item.isEnabled
                        ? intl.formatMessage(IncidentManagementResources.on)
                        : intl.formatMessage(IncidentManagementResources.off)}
                </div>
            );
        },
        [intl]
    );

    const onRenderType = useCallback(
        (item: IncidentFilter) => {
            const displayValue = isAzMonitorFilter(item)
                ? intl.formatMessage(SreAgentResources.NA)
                : getDisplayValueOrAll(item.incidentType);
            return <div style={{ userSelect: 'text' }}>{displayValue}</div>;
        },
        [getDisplayValueOrAll, isAzMonitorFilter, intl]
    );

    const onRenderPriority = useCallback(
        (item: IncidentFilter) => {
            const displayValue = getDisplayValueOrAll(item.priority);
            return <div style={{ userSelect: 'text' }}>{displayValue}</div>;
        },
        [getDisplayValueOrAll]
    );

    const onRenderImpactedService = useCallback(
        (item: IncidentFilter) => {
            const displayValue = isAzMonitorFilter(item)
                ? intl.formatMessage(SreAgentResources.NA)
                : getDisplayValueOrAll(item.impactedService);
            return <div style={{ userSelect: 'text' }}>{displayValue}</div>;
        },
        [getDisplayValueOrAll, isAzMonitorFilter, intl]
    );

    const onRenderTitleContains = useCallback(
        (item: IncidentFilter) => {
            const displayValue = getDisplayValueOrDash(item.titleContains);
            return <div style={{ userSelect: 'text' }}>{displayValue}</div>;
        },
        [getDisplayValueOrDash]
    );

    const onRenderAgentMode = useCallback(
        (item: IncidentFilter) => {
            // Uses AgentMode enum, but can only be review or autonomous for IncidentFilters
            const displayName =
                item.agentMode === AgentMode.autonomous
                    ? intl.formatMessage(IncidentManagementResources.autonomousWord)
                    : intl.formatMessage(IncidentManagementResources.reviewWord);
            return <div style={{ userSelect: 'text' }}>{displayName}</div>;
        },
        [intl]
    );

    const onRenderIncidentHandler = useCallback(
        (item: IncidentFilter) => {
            // If handling agent is set, show the agent name as a link to the extended agent graph
            if (item.handlingAgent) {
                return (
                    <Link
                        style={{ fontSize: '13px' }}
                        onClick={() => {
                            navigate({
                                primaryNavItemValue: PrimaryNavItemValues.Builder,
                                secondaryNavItemValue: SecondaryNavItemValues.ExtendedAgentsGraph,
                                options: {
                                    state: {
                                        anchorEntity: {
                                            entityType: 'Agent',
                                            entityName: item.handlingAgent,
                                        },
                                    },
                                },
                            });
                        }}
                    >
                        {item.handlingAgent}
                    </Link>
                );
            }

            // Otherwise show the custom handler status
            const handler = filterIdToHandlerMap[item.id ?? ''];
            if (handler) {
                return (
                    <div className={styles.setUp}>
                        <CheckmarkCircle16Regular
                            className={styles.greenCheckIcon}
                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                        />
                        <div>{intl.formatMessage(IncidentManagementResources.created)}</div>
                    </div>
                );
            }
            return (() => {
                const tooltipMsg =
                    disableEditActions && !canWriteIncidentManagement
                        ? intl.formatMessage(IncidentManagementResources.noPermissionEditIncidentHandler)
                        : null;
                const link = (
                    <Link
                        style={{ fontSize: '13px' }}
                        onClick={() => {
                            openHandlerCreate({ filter: item });
                        }}
                        disabled={disableEditActions}
                    >
                        {intl.formatMessage(IncidentManagementResources.setUp)}
                    </Link>
                );
                return tooltipMsg ? (
                    <Tooltip relationship="label" content={tooltipMsg}>
                        {link}
                    </Tooltip>
                ) : (
                    link
                );
            })();
        },
        [
            filterIdToHandlerMap,
            intl,
            navigate,
            openHandlerCreate,
            styles.greenCheckIcon,
            styles.setUp,
            disableEditActions,
            canWriteIncidentManagement,
        ]
    );

    const incidentTypeFilterProps: FilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.incidentType),
            disabled: disableAllControls,
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: incidentTypeOptions,
            onApply: setSelectedIncidentTypes,
            selectedKeys: selectedIncidentTypes,
            multiSelect: true,
            addAllOption: true,
        }),
        [disableAllControls, incidentTypeOptions, intl, setSelectedIncidentTypes, selectedIncidentTypes]
    );

    const impactedServiceFilterProps: FilterProps = useMemo(
        () => ({
            label: intl.formatMessage(IncidentManagementResources.impactedService),
            disabled: disableAllControls,
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: impactedServiceOptions,
            onApply: setSelectedImpactedServices,
            selectedKeys: selectedImpactedServices,
            multiSelect: true,
            addAllOption: true,
        }),
        [intl, disableAllControls, impactedServiceOptions, setSelectedImpactedServices, selectedImpactedServices]
    );

    const priorityFilterProps: FilterProps = useMemo(
        () => ({
            label: intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel),
            disabled: disableAllControls,
            labelDelimiter: intl.formatMessage(SreAgentResources.equals),
            filterType: 'combobox' as const,
            showValueAs: 'list',
            options: priorityOptions,
            onApply: setSelectedPriorities,
            selectedKeys: selectedPriorities,
            multiSelect: true,
            addAllOption: true,
        }),
        [intl, platformSpecificStrings.severityOrPriorityLabel, disableAllControls, priorityOptions, selectedPriorities]
    );

    const staticFilters: FilterProps[] = useMemo(() => {
        const filters = [incidentTypeFilterProps, impactedServiceFilterProps, priorityFilterProps];

        // Filter out Incident Type and Impacted Service filters for Azure Monitor
        // ONLY if Azure Monitor is connected AND there are no non-AzMonitor filters
        return shouldHideAzMonitorColumns ? [priorityFilterProps] : filters;
    }, [incidentTypeFilterProps, impactedServiceFilterProps, priorityFilterProps, shouldHideAzMonitorColumns]);

    const onRenderCheckbox = useCallback(
        (item: IncidentFilter) => {
            return (
                <Checkbox
                    checked={selectedFilter?.id === item.id}
                    onChange={(_, data) => setSelectedFilter(data.checked ? item : undefined)}
                    disabled={disableAllControls}
                    input={{
                        style: { width: 16 },
                    }}
                    indicator={{
                        style: { margin: 'auto' },
                    }}
                    aria-label={intl.formatMessage(SreAgentResources.selectRowAriaLabel)}
                />
            );
        },
        [selectedFilter, setSelectedFilter, disableAllControls, intl]
    );

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columnWidth = '14';
        const { severityOrPriorityLabel } = getPlatformSpecificStrings(incidentPlatformType);

        const columns: ISortedDetailsListColumn[] = [
            {
                key: IncidentsListColumnKey.selected,
                name: '',
                ariaLabel: intl.formatMessage(SreAgentResources.selectRowAriaLabel),
                fieldName: IncidentsListColumnKey.selected,
                minWidth: 30,
                maxWidth: 30,
                isResizable: false,
                onRenderHeader: () => null,
                onRender: onRenderCheckbox,
                isMultiline: false,
                isSorted: false,
            },
            {
                key: IncidentsListColumnKey.id,
                name: intl.formatMessage(IncidentManagementResources.incidentHandler),
                fieldName: IncidentsListColumnKey.id,
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                isMultiline: true,
                onRender: onRenderId,
                isSorted: sortColumnKey === (IncidentsListColumnKey.id as keyof IncidentFilter),
                isSortedDescending: sortColumnKey === (IncidentsListColumnKey.id as keyof IncidentFilter) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
                styles: { root: { width: `16%` } },
            },
            {
                key: IncidentsListColumnKey.type,
                name: intl.formatMessage(IncidentManagementResources.incidentType),
                fieldName: IncidentsListColumnKey.type,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderType,
                isSorted: sortColumnKey === (IncidentsListColumnKey.type as keyof IncidentFilter),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.type as keyof IncidentFilter) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.impactedService,
                name: intl.formatMessage(IncidentManagementResources.impactedService),
                fieldName: IncidentsListColumnKey.impactedService,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderImpactedService,
                isSorted: sortColumnKey === (IncidentsListColumnKey.impactedService as keyof IncidentFilter),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.impactedService as keyof IncidentFilter) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.priority,
                name: intl.formatMessage(severityOrPriorityLabel),
                fieldName: IncidentsListColumnKey.priority,
                isResizable: true,
                isMultiline: true,
                minWidth: 100,
                maxWidth: 150,
                onRender: onRenderPriority,
                isSorted: sortColumnKey === (IncidentsListColumnKey.priority as keyof IncidentFilter),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.priority as keyof IncidentFilter) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.titleContains,
                name: intl.formatMessage(IncidentManagementResources.titleContains),
                fieldName: IncidentsListColumnKey.titleContains,
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderTitleContains,
                isSorted: sortColumnKey === (IncidentsListColumnKey.titleContains as keyof IncidentFilter),
                onColumnClick: (_, col) => handleColumnClick(col),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.titleContains as keyof IncidentFilter) ? isSortedDescending : undefined,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.customHandler,
                name: intl.formatMessage(IncidentManagementResources.customHandler),
                fieldName: IncidentsListColumnKey.customHandler,
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderIncidentHandler,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.status,
                name: intl.formatMessage(IncidentManagementResources.status),
                fieldName: IncidentsListColumnKey.status,
                isResizable: true,
                minWidth: 100,
                maxWidth: 250,
                onRender: onRenderStatus,
                styles: { root: { width: `${columnWidth}%` } },
            },
            {
                key: IncidentsListColumnKey.agentMode,
                name: intl.formatMessage(IncidentManagementResources.autonomyLevel),
                fieldName: IncidentsListColumnKey.agentMode,
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderAgentMode,
                isSorted: sortColumnKey === (IncidentsListColumnKey.agentMode as keyof IncidentFilter),
                isSortedDescending:
                    sortColumnKey === (IncidentsListColumnKey.agentMode as keyof IncidentFilter) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
                styles: { root: { width: `${columnWidth}%` } },
            },
        ];

        // Filter out Incident Type and Impacted Service columns for Azure Monitor
        // ONLY if Azure Monitor is the connected platform AND there are no non-AzMonitor filters
        const filteredColumns = shouldHideAzMonitorColumns
            ? columns.filter(col => col.key !== IncidentsListColumnKey.type && col.key !== IncidentsListColumnKey.impactedService)
            : columns;

        return filteredColumns;
    }, [
        intl,
        incidentPlatformType,
        onRenderCheckbox,
        onRenderId,
        sortColumnKey,
        isSortedDescending,
        onRenderType,
        onRenderImpactedService,
        onRenderPriority,
        onRenderTitleContains,
        onRenderAgentMode,
        onRenderIncidentHandler,
        onRenderStatus,
        handleColumnClick,
        shouldHideAzMonitorColumns,
    ]);

    const emptyState = useMemo(() => {
        if (incidentFilters.length || incidentFiltersLoading) {
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

        return <IncidentManagementEmptyState type="noHandlers" onButtonClick={() => openHandlerCreate({})} />;
    }, [incidentFilters.length, incidentFiltersLoading, incidentManagementConfigured, navigate, openHandlerCreate]);

    return (
        <div style={{ width: '100%' }}>
            <div>
                <div className={styles.incidentFiltersContainer}>
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(SreAgentResources.search)}
                        value={searchText}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                        disabled={disableAllControls}
                        size={'small'}
                    />
                    <PillFilterSet staticFilters={staticFilters} disabled={disableAllControls} />
                </div>
            </div>
            <div data-is-scrollable="true" user-select="text">
                <ShimmeredDetailsList
                    columns={columns}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    items={sortedItems ?? []}
                    layoutMode={DetailsListLayoutMode.justified}
                    compact={true}
                    enableShimmer={incidentFiltersLoading}
                    useReducedRowRenderer={true}
                    styles={{
                        root: {
                            width: '100%',
                            userSelect: 'text',
                        },
                    }}
                    selectionMode={SelectionMode.none}
                    setKey="incidentFilterList"
                    getKey={(item, index) => (item && item.id ? item.id : `shimmer-${index}`)}
                />
                {emptyState}
            </div>
        </div>
    );
};

export default ResponsePlanGrid;

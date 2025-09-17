import { Button, Dropdown, InputOnChangeData, Link, Option, SearchBox, SearchBoxChangeEvent } from '@fluentui/react-components';
import { CheckmarkCircle16Regular } from '@fluentui/react-icons';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { Selection } from '@fluentui/react/lib/Selection';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { debounce } from 'lodash';
import { Dispatch, FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentFilter, IncidentHandler } from '../../Common/Contracts/Azure/IncidentHandler';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { getIncidentManagementPlatform } from '../Settings/Hooks/useIncidentManagementSettings';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { IncidentFilterFormProps } from './CreateIncidentFilterDialog';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import { getPriorityOrSeverityStrings } from './Utilities';

export type ISortedDetailsListColumn = IColumn & {
    sort?: (items: any[], isSortedDescending: boolean) => any[];
    disableColumnClick?: boolean;
};

enum IncidentsListColumnKey {
    id = 'id',
    impactedService = 'impactedService',
    priority = 'priority',
    status = 'status',
    type = 'incidentType',
    titleContains = 'titleContains',
    customHandler = 'customHandler',
    agentMode = 'agentMode',
}

const all = 'all';

export type LabelValuePair = { label: string; value: string };

export type IncidentFilterType = { incidentType: string; impactedService: string; priority: string };

export type IncidentsFiltersGridProps = {
    incidentFilters: IncidentFilter[];
    incidentFiltersLoading: boolean;
    setIsCreateIncidentFilterDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    filterIdToHandlerMap: Record<string, IncidentHandler>;
    setSelectedFilter: Dispatch<React.SetStateAction<IncidentFilter | undefined>>;
    setIsEditFilterMode: Dispatch<React.SetStateAction<boolean>>;
    setInitialValues: Dispatch<React.SetStateAction<IncidentFilterFormProps | undefined>>;
    openHandlerCreate: (handlerCreateOrEditInfo: HandlerCreateOrEditInfo) => void;
    handlerOperationStatus: OperationStatus | undefined;
    useConsolidatedCreate: boolean;
    disabled: boolean;
};

const IncidentsFiltersGrid: FC<IncidentsFiltersGridProps> = (props: IncidentsFiltersGridProps) => {
    const {
        incidentFilters,
        incidentFiltersLoading,
        openHandlerCreate,
        handlerOperationStatus,
        useConsolidatedCreate,
        setIsCreateIncidentFilterDialogOpen,
        filterIdToHandlerMap,
        setSelectedFilter,
        setIsEditFilterMode,
        setInitialValues,
        disabled,
    } = props;
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const [searchText, setSearchText] = useState<string>('');
    const [incidentType, setIncidentType] = useState<string>(all);
    const [impactedService, setImpactedService] = useState<string>(all);
    const [priority, setPriority] = useState<string>(all);
    const [priorityOptions, setIncidentPriorities] = useState<LabelValuePair[]>([]);
    const [incidentTypeOptions, setIncidentTypes] = useState<LabelValuePair[]>([]);
    const [impactedServiceOptions, setImpactedServices] = useState<LabelValuePair[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentFilter | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    const sreAgentContext = useContext(SreAgentContext);
    const incidentPlatform = useMemo(() => getIncidentManagementPlatform(sreAgentContext.agentObj), [sreAgentContext.agentObj]);
    const priorityOrSeverityStrings = useMemo(() => getPriorityOrSeverityStrings(incidentPlatform), [incidentPlatform]);

    const filteredGridItems = useMemo(() => {
        let filteredGridItems = incidentFilters;
        if (searchText.trim() !== '') {
            filteredGridItems = filteredGridItems.filter(item => item.id.includes(searchText.trim()));
        }
        if (incidentType !== all) {
            filteredGridItems = filteredGridItems.filter(item => item.incidentType === incidentType);
        }
        if (impactedService !== all) {
            filteredGridItems = filteredGridItems.filter(item => item.impactedService === impactedService);
        }
        if (priority !== all) {
            filteredGridItems = filteredGridItems.filter(item => item.priority === priority);
        }

        return filteredGridItems;
    }, [impactedService, incidentFilters, incidentType, priority, searchText]);

    const isIncidentFilterEmpty = useMemo(() => {
        return incidentType === all && impactedService === all && priority === all && searchText.trim() === '';
    }, [incidentType, impactedService, priority, searchText]);

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
        return handlerOperationStatus === 'inprogress' || disabled || incidentFiltersLoading;
    }, [handlerOperationStatus, disabled, incidentFiltersLoading]);

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueIncidentPriorities = Array.from(
            new Set(incidentFilters.map(item => item.priority).filter(priority => priority && priority.trim() !== ''))
        );

        const incidentTypeOptions = uniqueIncidentPriorities.map(priority => ({
            value: priority ?? '',
            label: priority ?? '',
        }));

        setIncidentPriorities([
            { value: all, label: intl.formatMessage(priorityOrSeverityStrings.allOptionLabel) },
            ...incidentTypeOptions,
        ]);
    }, [isIncidentFilterEmpty, incidentFilters, intl, priorityOrSeverityStrings]);

    const getPriorityOptionLabel = useCallback(
        (option: string): string => {
            switch (option) {
                case all:
                    return intl.formatMessage(priorityOrSeverityStrings.allOptionLabel);
                default:
                    return option;
            }
        },
        [intl, priorityOrSeverityStrings]
    );

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueIncidentTypes = Array.from(
            new Set(incidentFilters.map(item => item.incidentType).filter(type => type && type.trim() !== ''))
        );

        const incidentTypeOptions = uniqueIncidentTypes.map(type => ({
            value: type ?? '',
            label: type ?? '',
        }));

        setIncidentTypes([{ value: all, label: intl.formatMessage(IncidentManagementResources.allIncidentTypes) }, ...incidentTypeOptions]);
    }, [isIncidentFilterEmpty, incidentFilters, intl]);

    const getIncidentTypeLabel = (option: string): string => {
        switch (option) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allIncidentTypes);
            default:
                return option;
        }
    };

    useEffect(() => {
        if (!isIncidentFilterEmpty) return;

        const uniqueImpactedServices = Array.from(
            new Set(incidentFilters.map(item => item.impactedService).filter(service => service && service.trim() !== ''))
        );

        const impactedServiceOptions = uniqueImpactedServices.map(name => ({
            value: name ?? '',
            label: name ?? '',
        }));

        setImpactedServices([
            { value: all, label: intl.formatMessage(IncidentManagementResources.allImpactedServices) },
            ...impactedServiceOptions,
        ]);
    }, [isIncidentFilterEmpty, incidentFilters, intl]);

    const getImpactedServicesLabel = (option: string): string => {
        switch (option) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allImpactedServices);
            default:
                return option;
        }
    };

    const selection = useRef(
        new Selection({
            onSelectionChanged: () => {
                const items = (selection.current.getSelection() as IncidentFilter[]) ?? [];
                setSelectedFilter(items.length > 0 ? items[0] : undefined);
            },
        })
    );

    const onIdClick = useCallback(
        (item: IncidentFilter) => {
            if (useConsolidatedCreate) {
                const handler = filterIdToHandlerMap[item.id ?? ''];
                openHandlerCreate({ filter: item, handlerId: handler?.id });
            } else {
                setIsEditFilterMode(true);
                setInitialValues({
                    id: item.id ?? '',
                    incidentType: item.incidentType ?? '',
                    impactedService: item.impactedService ?? '',
                    priority: item.priority ?? '',
                    titleContains: item.titleContains ?? '',
                    agentMode: item.agentMode,
                });
                setIsCreateIncidentFilterDialogOpen(true);
            }
        },
        [
            setInitialValues,
            setIsCreateIncidentFilterDialogOpen,
            setIsEditFilterMode,
            filterIdToHandlerMap,
            openHandlerCreate,
            useConsolidatedCreate,
        ]
    );

    const onRenderId = useCallback(
        (item: IncidentFilter) => {
            return (
                <Link style={{ userSelect: 'text', fontSize: '13px' }} onClick={() => onIdClick(item)} disabled={disableAllControls}>
                    {item.id ?? ''}
                </Link>
            );
        },
        [onIdClick, disabled, disableAllControls, handlerOperationStatus, incidentFiltersLoading]
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

    const onRenderType = useCallback((item: IncidentFilter) => {
        return <div style={{ userSelect: 'text' }}>{item.incidentType ?? ''}</div>;
    }, []);

    const onRenderPriority = useCallback((item: IncidentFilter) => {
        return <div style={{ userSelect: 'text' }}>{item.priority ?? ''}</div>;
    }, []);

    const onRenderImpactedService = useCallback((item: IncidentFilter) => {
        return <div style={{ userSelect: 'text' }}>{item.impactedService ?? ''}</div>;
    }, []);

    const onRenderTitleContains = useCallback((item: IncidentFilter) => {
        return <div style={{ userSelect: 'text' }}>{item.titleContains ?? ''}</div>;
    }, []);

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
            const handler = filterIdToHandlerMap[item.id ?? ''];
            if (handler) {
                return (
                    <div className={styles.setUp}>
                        <CheckmarkCircle16Regular
                            className={styles.greenCheckIcon}
                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                        />
                        <div>{intl.formatMessage(IncidentManagementResources.created)}</div>
                        {!useConsolidatedCreate && (
                            <Link
                                style={{ fontSize: '13px' }}
                                onClick={() => {
                                    openHandlerCreate({ filter: item, handlerId: handler.id, quickEdit: true });
                                }}
                                disabled={disableAllControls}
                            >{`(${intl.formatMessage(IncidentManagementResources.goToHandler)})`}</Link>
                        )}
                    </div>
                );
            }
            return (
                <Link
                    style={{ fontSize: '13px' }}
                    onClick={() => {
                        openHandlerCreate({ filter: item });
                    }}
                    disabled={disableAllControls}
                >
                    {intl.formatMessage(IncidentManagementResources.setUp)}
                </Link>
            );
        },
        [
            handlerOperationStatus,
            filterIdToHandlerMap,
            intl,
            openHandlerCreate,
            styles.greenCheckIcon,
            styles.setUp,
            useConsolidatedCreate,
            disabled,
            disableAllControls,
            incidentFiltersLoading,
        ]
    );

    const onIncidentTypeChange = useCallback(
        (incidentType: string) => {
            setIncidentType(incidentType);
        },
        [setIncidentType]
    );

    const onImpactedServiceChange = useCallback(
        (impactedService: string) => {
            setImpactedService(impactedService);
        },
        [setImpactedService]
    );

    const onPriorityChange = useCallback(
        (priority: string) => {
            setPriority(priority);
        },
        [setPriority]
    );

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columnWidth = '14';
        const { fieldLabel: priorityOrSeverityLabel } = getPriorityOrSeverityStrings(incidentPlatform);

        const columns: ISortedDetailsListColumn[] = [
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
                name: intl.formatMessage(priorityOrSeverityLabel),
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

        return columns;
    }, [
        intl,
        incidentPlatform,
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
    ]);

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
                    />
                    <Dropdown
                        onOptionSelect={(_e, data) => onIncidentTypeChange(data.optionValue ?? all)}
                        value={incidentType}
                        selectedOptions={[incidentType]}
                        button={<span>{getIncidentTypeLabel(incidentType)}</span>}
                        className={styles.searchBox}
                        disabled={disableAllControls}
                    >
                        {incidentTypeOptions.map(option => (
                            <Option value={option.value} text={option.label}>
                                {option.label}
                            </Option>
                        ))}
                    </Dropdown>
                    <Dropdown
                        onOptionSelect={(_e, data) => onImpactedServiceChange(data.optionValue ?? all)}
                        value={impactedService}
                        selectedOptions={[impactedService]}
                        button={<span>{getImpactedServicesLabel(impactedService)}</span>}
                        className={styles.searchBox}
                        disabled={disableAllControls}
                    >
                        {impactedServiceOptions.map(option => (
                            <Option value={option.value} text={option.label}>
                                {option.label}
                            </Option>
                        ))}
                    </Dropdown>
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
                    checkboxVisibility={CheckboxVisibility.always}
                    useReducedRowRenderer={true}
                    styles={{
                        root: {
                            width: '100%',
                            userSelect: 'text',
                        },
                    }}
                    selectionPreservedOnEmptyClick={true}
                    selection={selection.current}
                    selectionMode={SelectionMode.single}
                    setKey="incidentFilterList"
                    getKey={(item, index) => (item && item.id ? item.id : `shimmer-${index}`)}
                />
                {incidentFilters.length === 0 && !incidentFiltersLoading && (
                    <div className={styles.emptyState}>
                        <div>
                            <img src="./NewFilter.svg" alt="NewFilter" />
                        </div>
                        <div className={styles.emptyStateTitle}>{intl.formatMessage(IncidentManagementResources.getStarted)}</div>
                        <Button
                            appearance="primary"
                            onClick={() => {
                                if (useConsolidatedCreate) {
                                    openHandlerCreate({});
                                } else {
                                    setIsEditFilterMode(false);
                                    setInitialValues(undefined);
                                    setIsCreateIncidentFilterDialogOpen(true);
                                }
                            }}
                            className={styles.newIncidentFilterButton}
                            disabled={disableAllControls}
                        >
                            {intl.formatMessage(IncidentManagementResources.newIncidentHandler)}
                        </Button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default IncidentsFiltersGrid;

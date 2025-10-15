import {
    Badge,
    Card,
    Dropdown,
    InputOnChangeData,
    Link,
    mergeClasses,
    Option,
    SearchBox,
    SearchBoxChangeEvent,
    Subtitle2,
    Text,
} from '@fluentui/react-components';
import { ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { debounce } from 'lodash';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ISortedDetailsListColumn } from '../../../../Common/Components/DetailsList/Constants';
import { AgentMode } from '../../../../Common/Contracts/Azure/SreAgent';
import { getLocalizedAgentMode } from '../../../../Common/Helpers/AgentMode';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentHandlerItem } from '../../Analysis';

const all = 'all';

enum IncidentResponsePlanGridColumnKey {
    name = 'name',
    autonomyLevel = 'autonomyLevel',
    customPlan = 'customPlan',
    incidentsReviewed = 'incidentsReviewed',
    assistedByAgent = 'assistedByAgent',
    mitigatedByAgent = 'mitigatedByAgent',
    mitigatedByUser = 'mitigatedByUser',
    pendingUserAction = 'pendingUserAction',
}

interface IncidentResponsePlanGridProps {
    responsePlans: IncidentHandlerItem[];
    setOpenedResponsePlan: (responsePlan: IncidentHandlerItem | undefined) => void;
    disabled?: boolean;
    isLoading?: boolean;
}

export const IncidentResponsePlanGrid = ({ responsePlans, setOpenedResponsePlan, disabled, isLoading }: IncidentResponsePlanGridProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const isDarkMode = useIsDarkMode();

    const [searchText, setSearchText] = useState<string>('');
    const [autonomyLevelFilter, setAutonomyLevelFilter] = useState<string>(all);
    const [customPlanFilter, setCustomPlanFilter] = useState<string>(all);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentHandlerItem | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    const autonomyLevelFilterOptions = useMemo<{ label: string; value: string }[]>(() => {
        return [
            { label: intl.formatMessage(SreAgentResources.all), value: all },
            { label: getLocalizedAgentMode(AgentMode.readonly, intl), value: AgentMode.readonly },
            { label: getLocalizedAgentMode(AgentMode.review, intl), value: AgentMode.review },
            { label: getLocalizedAgentMode(AgentMode.autonomous, intl), value: AgentMode.autonomous },
        ];
    }, [intl]);

    const customPlanFilterOptions = useMemo<{ label: string; value: string }[]>(() => {
        return [
            { label: intl.formatMessage(SreAgentResources.all), value: all },
            { label: intl.formatMessage(SreAgentResources.yes), value: 'yes' },
            { label: intl.formatMessage(SreAgentResources.no), value: 'no' },
        ];
    }, [intl]);

    const autonomyLevelFilterLabel = useMemo<string>(() => {
        switch (autonomyLevelFilter) {
            case all:
                return intl.formatMessage(SreAgentResources.allAutonomyLevels);
            default:
                return autonomyLevelFilterOptions.find(option => option.value === autonomyLevelFilter)?.label || autonomyLevelFilter;
        }
    }, [intl, autonomyLevelFilter, autonomyLevelFilterOptions]);

    const customPlanFilterLabel = useMemo<string>(() => {
        switch (customPlanFilter) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allCustomPlans);
            default:
                return customPlanFilterOptions.find(option => option.value === customPlanFilter)?.label || customPlanFilter;
        }
    }, [intl, customPlanFilter, customPlanFilterOptions]);

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as keyof IncidentHandlerItem);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending]
    );

    const onRenderResponsePlanName = useCallback(
        (item: IncidentHandlerItem) => {
            return (
                <Link
                    onClick={() => {
                        setOpenedResponsePlan(item);
                    }}
                >
                    {item.responsePlanName || intl.formatMessage(SreAgentResources.default)}
                </Link>
            );
        },
        [intl, setOpenedResponsePlan]
    );

    const onRenderAutonomyLevel = useCallback(
        (item: IncidentHandlerItem) => {
            // Don't think this should happen, but it did in the test data, so just in case
            if (!item.autonomyLevel) return '-';

            return (
                <Badge appearance="tint" color="informative" shape="rounded">
                    {getLocalizedAgentMode(item.autonomyLevel, intl)}
                </Badge>
            );
        },
        [intl]
    );

    const onRenderCustomPlan = useCallback(
        (item: IncidentHandlerItem) => {
            return (
                <Text>
                    {item.planType === 'Default' ? intl.formatMessage(SreAgentResources.no) : intl.formatMessage(SreAgentResources.yes)}
                </Text>
            );
        },
        [intl]
    );

    const onRenderIncidentsReviewed = useCallback((item: IncidentHandlerItem) => {
        return <Text>{item.distinctIncidentCount}</Text>;
    }, []);

    const onRenderAssistedByAgent = useCallback((item: IncidentHandlerItem) => {
        return <Text>{item.agentAssisted}</Text>;
    }, []);

    const onRenderMitigatedByAgent = useCallback((item: IncidentHandlerItem) => {
        return <Text>{item.agentMitigated}</Text>;
    }, []);

    const onRenderMitigatedByUser = useCallback((item: IncidentHandlerItem) => {
        return <Text>{item.userMitigated}</Text>;
    }, []);

    const onRenderPendingUserAction = useCallback((item: IncidentHandlerItem) => {
        return <Text>{item.pendingUserAction}</Text>;
    }, []);

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columns: ISortedDetailsListColumn[] = [
            {
                key: IncidentResponsePlanGridColumnKey.name,
                name: intl.formatMessage(IncidentManagementResources.responsePlanName),
                isResizable: true,
                minWidth: 150,
                maxWidth: 250,
                onRender: onRenderResponsePlanName,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.name as keyof IncidentHandlerItem),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.name as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentResponsePlanGridColumnKey.autonomyLevel,
                name: intl.formatMessage(IncidentManagementResources.autonomyLevel),
                isResizable: true,
                minWidth: 100,
                maxWidth: 150,
                onRender: onRenderAutonomyLevel,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.autonomyLevel as keyof IncidentHandlerItem),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.autonomyLevel as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentResponsePlanGridColumnKey.customPlan,
                name: intl.formatMessage(IncidentManagementResources.customPlan),
                isResizable: true,
                minWidth: 100,
                maxWidth: 150,
                onRender: onRenderCustomPlan,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.customPlan as keyof IncidentHandlerItem),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.customPlan as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentResponsePlanGridColumnKey.incidentsReviewed,
                name: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                isResizable: true,
                minWidth: 125,
                maxWidth: 250,
                onRender: onRenderIncidentsReviewed,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.incidentsReviewed as keyof IncidentHandlerItem),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.incidentsReviewed as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: IncidentResponsePlanGridColumnKey.assistedByAgent,
                name: intl.formatMessage(IncidentManagementResources.assistedByAgent),
                isResizable: true,
                minWidth: 125,
                maxWidth: 250,
                onRender: onRenderAssistedByAgent,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.assistedByAgent as keyof IncidentHandlerItem),
                onColumnClick: (_, col) => handleColumnClick(col),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.assistedByAgent as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
            },
            {
                key: IncidentResponsePlanGridColumnKey.mitigatedByAgent,
                name: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                isResizable: true,
                minWidth: 125,
                maxWidth: 250,
                onRender: onRenderMitigatedByAgent,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.mitigatedByAgent as keyof IncidentHandlerItem),
                onColumnClick: (_, col) => handleColumnClick(col),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.mitigatedByAgent as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
            },
            {
                key: IncidentResponsePlanGridColumnKey.mitigatedByUser,
                name: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                isResizable: true,
                minWidth: 125,
                maxWidth: 250,
                onRender: onRenderMitigatedByUser,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.mitigatedByUser as keyof IncidentHandlerItem),
                onColumnClick: (_, col) => handleColumnClick(col),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.mitigatedByUser as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
            },
            {
                key: IncidentResponsePlanGridColumnKey.pendingUserAction,
                name: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                isResizable: true,
                minWidth: 130,
                maxWidth: 150,
                onRender: onRenderPendingUserAction,
                isSorted: sortColumnKey === (IncidentResponsePlanGridColumnKey.pendingUserAction as keyof IncidentHandlerItem),
                onColumnClick: (_, col) => handleColumnClick(col),
                isSortedDescending:
                    sortColumnKey === (IncidentResponsePlanGridColumnKey.pendingUserAction as keyof IncidentHandlerItem)
                        ? isSortedDescending
                        : undefined,
            },
        ];

        return columns;
    }, [
        intl,
        onRenderResponsePlanName,
        onRenderAutonomyLevel,
        onRenderCustomPlan,
        onRenderIncidentsReviewed,
        onRenderAssistedByAgent,
        onRenderMitigatedByAgent,
        onRenderMitigatedByUser,
        onRenderPendingUserAction,
        sortColumnKey,
        isSortedDescending,
        handleColumnClick,
    ]);

    const filteredGridItems = useMemo(() => {
        let filteredGridItems = responsePlans;

        if (searchText.trim() !== '') {
            filteredGridItems = filteredGridItems.filter(item =>
                item.responsePlanName === ''
                    ? intl.formatMessage(SreAgentResources.default).includes(searchText.trim())
                    : item.responsePlanName.includes(searchText.trim())
            );
        }

        if (autonomyLevelFilter !== all) {
            filteredGridItems = filteredGridItems.filter(item => item.autonomyLevel === autonomyLevelFilter);
        }

        if (customPlanFilter !== all) {
            filteredGridItems = filteredGridItems.filter(item =>
                customPlanFilter === 'yes' ? item.planType !== 'Default' : item.planType === 'Default'
            );
        }

        return filteredGridItems;
    }, [intl, responsePlans, autonomyLevelFilter, customPlanFilter, searchText]);

    const sortedItems = useMemo(() => {
        if (!sortColumnKey) return filteredGridItems;

        return [...filteredGridItems].sort((a, b) => {
            const valA = a[sortColumnKey] ?? '';
            const valB = b[sortColumnKey] ?? '';

            if (valA === valB) return 0;
            return (valA > valB ? 1 : -1) * (isSortedDescending ? -1 : 1);
        });
    }, [filteredGridItems, sortColumnKey, isSortedDescending]);

    return (
        <Card style={{ width: '100%', height: '100%' }} appearance={isDarkMode ? 'filled-alternative' : undefined}>
            <Subtitle2>{intl.formatMessage(IncidentManagementResources.incidentResponsePlan)}</Subtitle2>

            <div className={styles.incidentFiltersContainer} style={{ marginBottom: 0 }}>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(IncidentManagementResources.searchResponsePlans)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                    disabled={disabled}
                />
                <Dropdown
                    onOptionSelect={(_e, data) => setAutonomyLevelFilter(data.optionValue ?? all)}
                    value={autonomyLevelFilter}
                    selectedOptions={[autonomyLevelFilter]}
                    button={autonomyLevelFilterLabel}
                    aria-label={intl.formatMessage(IncidentManagementResources.filterByAutonomyLevel)}
                    className={styles.searchBox}
                    disabled={disabled}
                >
                    {autonomyLevelFilterOptions.map(option => (
                        <Option value={option.value} text={option.label}>
                            {option.label}
                        </Option>
                    ))}
                </Dropdown>
                <Dropdown
                    onOptionSelect={(_e, data) => setCustomPlanFilter(data.optionValue ?? all)}
                    value={customPlanFilter}
                    selectedOptions={[customPlanFilter]}
                    button={customPlanFilterLabel}
                    aria-label={intl.formatMessage(IncidentManagementResources.filterByCustomPlan)}
                    className={styles.searchBox}
                    disabled={disabled}
                >
                    {customPlanFilterOptions.map(option => (
                        <Option value={option.value} text={option.label}>
                            {option.label}
                        </Option>
                    ))}
                </Dropdown>
            </div>

            <div data-is-scrollable="true" user-select="text" style={{ overflowY: 'auto' }}>
                <ShimmeredDetailsList
                    columns={columns}
                    items={sortedItems}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    layoutMode={DetailsListLayoutMode.justified}
                    selectionMode={SelectionMode.none}
                    enableShimmer={isLoading}
                    className={mergeClasses(styles.detailsListBase, isDarkMode ? styles.detailsListDarkModeBackground : undefined)}
                    styles={{
                        root: {
                            width: '100%',
                            userSelect: 'text',
                        },
                    }}
                    compact
                />

                {responsePlans.length === 0 && !isLoading && (
                    <Text align="center" block>
                        {intl.formatMessage(IncidentManagementResources.noResponsePlansFound)}
                    </Text>
                )}
            </div>
        </Card>
    );
};

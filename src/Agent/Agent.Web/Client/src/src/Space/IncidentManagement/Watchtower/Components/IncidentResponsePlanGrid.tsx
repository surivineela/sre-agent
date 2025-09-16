import { DataVizPalette, getColorFromToken, Sparkline } from '@fluentui/react-charting';
import {
    Badge,
    Card,
    Dropdown,
    InputOnChangeData,
    Link,
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
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentHandlerItem } from '../../Analysis';

const sparklineDummyData = {
    chartTitle: '10.21',
    lineChartData: [
        {
            legend: '19.64',
            color: getColorFromToken(DataVizPalette.color1),
            data: [
                {
                    x: 1,
                    y: 58.13,
                },
                {
                    x: 3,
                    y: 20,
                },
                {
                    x: 6,
                    y: 13.28,
                },
                {
                    x: 7,
                    y: 31.32,
                },
                {
                    x: 8,
                    y: 10.21,
                },
            ],
        },
    ],
};

const all = 'all';

enum IncidentResponsePlanGridColumnKey {
    name = 'name',
    autonomyLevel = 'autonomyLevel',
    customPlan = 'customPlan',
    incidentsReviewed = 'incidentsReviewed',
    mitigatedByAgent = 'mitigatedByAgent',
    mitigatedByUser = 'mitigatedByUser',
    pendingUserAction = 'pendingUserAction',
}

interface IncidentResponsePlanGridProps {
    responsePlans: IncidentHandlerItem[];
    disabled?: boolean;
    isLoading?: boolean;
}

export const IncidentResponsePlanGrid = ({ responsePlans, disabled, isLoading }: IncidentResponsePlanGridProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    const [searchText, setSearchText] = useState<string>('');
    const [autonomyLevelFilter, setAutonomyLevelFilter] = useState<string>(all);
    const [customPlanFilter, setCustomPlanFilter] = useState<string>(all);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentHandlerItem | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    const autonomyLevelFilterLabel = useMemo<string>(() => {
        switch (autonomyLevelFilter) {
            case all:
                return intl.formatMessage(SreAgentResources.all);
            default:
                return autonomyLevelFilter;
        }
    }, [intl, autonomyLevelFilter]);

    const customPlanFilterLabel = useMemo<string>(() => {
        switch (customPlanFilter) {
            case all:
                return intl.formatMessage(SreAgentResources.all);
            default:
                return customPlanFilter;
        }
    }, [intl, customPlanFilter]);

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
                        /* TODO: Open 'Response plans' tab */
                    }}
                >
                    {item.responsePlanName || intl.formatMessage(SreAgentResources.default)}
                </Link>
            );
        },
        [intl]
    );

    const onRenderAutonomyLevel = useCallback(
        (item: IncidentHandlerItem) => {
            return (
                <Badge appearance="tint" color="informative">
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

    const onRenderIncidentsReviewed = useCallback((_item: IncidentHandlerItem) => {
        return <Sparkline data={sparklineDummyData} />;
    }, []);

    const onRenderMitigatedByAgent = useCallback((_item: IncidentHandlerItem) => {
        return <Sparkline data={sparklineDummyData} />;
    }, []);

    const onRenderMitigatedByUser = useCallback((_item: IncidentHandlerItem) => {
        return <Sparkline data={sparklineDummyData} />;
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
                isMultiline: true,
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
                isMultiline: true,
                minWidth: 150,
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
                isMultiline: true,
                minWidth: 130,
                maxWidth: 130,
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
                isMultiline: true,
                minWidth: 150,
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
                key: IncidentResponsePlanGridColumnKey.mitigatedByAgent,
                name: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                isResizable: true,
                isMultiline: true,
                minWidth: 150,
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
                minWidth: 150,
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
                minWidth: 150,
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
                customPlanFilter === 'yes' ? item.planType === 'Default' : item.planType !== 'Default'
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
        <Card style={{ width: '100%', height: '100%' }}>
            <Subtitle2>{intl.formatMessage(IncidentManagementResources.incidentResponsePlan)}</Subtitle2>

            <div style={{ width: '100%' }}>
                <div className={styles.incidentFiltersContainer}>
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(SreAgentResources.search)}
                        value={searchText}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                        disabled={disabled}
                    />
                    <Dropdown
                        onOptionSelect={(_e, data) => setAutonomyLevelFilter(data.optionValue ?? all)}
                        value={autonomyLevelFilter}
                        selectedOptions={[autonomyLevelFilter]}
                        button={autonomyLevelFilterLabel}
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

                <div data-is-scrollable="true" user-select="text">
                    <ShimmeredDetailsList
                        columns={columns}
                        items={sortedItems ?? []}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        layoutMode={DetailsListLayoutMode.justified}
                        selectionMode={SelectionMode.none}
                        enableShimmer={isLoading}
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
            </div>
        </Card>
    );
};

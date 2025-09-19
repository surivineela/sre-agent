import {
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
import { formatDateTimeWithShortYear } from '../../../../Common/Helpers/Date';
import { getLocalizedMitigatedBy } from '../../../../Common/Helpers/IncidentManagement';
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentItem } from '../ResponsePlanView';

// TODO: Pagination

const all = 'all';

enum ResponsePlanIncidentsGridColumnKey {
    id = 'id',
    title = 'title',
    severity = 'severity',
    createdOn = 'createdOn',
    assistedByAgent = 'assistedByAgent',
    mitigatedBy = 'mitigatedBy',
    // meanTimeToMitigate = 'meanTimeToMitigate',
}

interface ResponsePlanIncidentsGridProps {
    incidents: IncidentItem[];
    disabled?: boolean;
    isLoading?: boolean;
}

export const ResponsePlanIncidentsGrid = ({ incidents, disabled, isLoading }: ResponsePlanIncidentsGridProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();

    // TODO: Incident chat panel
    const [_isIncidentChatPanelOpen, setIsIncidentChatPanelOpen] = useState(false);

    const [searchText, setSearchText] = useState<string>('');
    const [severityLevelFilter, setSeverityLevelFilter] = useState<string>(all);
    const [mitigatedByFilter, setMitigatedByFilter] = useState<string>(all);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentItem | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    // Filter by used values as more convenient, but also not sure if different platforms have different values
    const severityLevelFilterOptions = useMemo<{ label: string; value: string }[]>(() => {
        return [
            { label: intl.formatMessage(SreAgentResources.all), value: all },
            ...Array.from(new Set(incidents.map(incident => incident.severity))).map(severity => ({
                label: severity,
                value: severity,
            })),
        ];
    }, [intl, incidents]);

    const mitigatedByFilterOptions = useMemo<{ label: string; value: string }[]>(() => {
        return [
            { label: intl.formatMessage(SreAgentResources.all), value: all },
            { label: intl.formatMessage(SreAgentResources.agent), value: 'agent' },
            { label: intl.formatMessage(SreAgentResources.user), value: 'user' },
            { label: intl.formatMessage(SreAgentResources.inProgress), value: 'inProgress' },
        ];
    }, [intl]);

    const severityLevelFilterLabel = useMemo<string>(() => {
        switch (severityLevelFilter) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allSeverityLevels);
            default:
                return severityLevelFilterOptions.find(option => option.value === severityLevelFilter)?.label || severityLevelFilter;
        }
    }, [intl, severityLevelFilter, severityLevelFilterOptions]);

    const mitigatedByFilterLabel = useMemo<string>(() => {
        switch (mitigatedByFilter) {
            case all:
                return intl.formatMessage(IncidentManagementResources.allMitigatedBy);
            default:
                return mitigatedByFilterOptions.find(option => option.value === mitigatedByFilter)?.label || mitigatedByFilter;
        }
    }, [intl, mitigatedByFilter, mitigatedByFilterOptions]);

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as keyof IncidentItem);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending]
    );

    const onRenderIncidentId = useCallback((item: IncidentItem) => {
        return <Text>{item.incidentId}</Text>;
    }, []);

    const onRenderIncidentTitle = useCallback((item: IncidentItem) => {
        return (
            <Link
                onClick={() => {
                    setIsIncidentChatPanelOpen(true);
                }}
            >
                {item.incidentTitle}
            </Link>
        );
    }, []);

    const onRenderSeverityLevel = useCallback((item: IncidentItem) => {
        return <Text>{item.severity}</Text>;
    }, []);

    const onRenderIncidentCreated = useCallback((item: IncidentItem) => {
        return <Text>{formatDateTimeWithShortYear(item.createdOn)}</Text>;
    }, []);

    const onRenderAssistedByAgent = useCallback(
        (item: IncidentItem) => {
            return (
                <Text>{item.assistedByAgent ? intl.formatMessage(SreAgentResources.yes) : intl.formatMessage(SreAgentResources.no)}</Text>
            );
        },
        [intl]
    );

    const onRenderMitigatedBy = useCallback(
        (item: IncidentItem) => {
            return <Text>{getLocalizedMitigatedBy(item.mitigatedBy, intl)}</Text>;
        },
        [intl]
    );

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columns: ISortedDetailsListColumn[] = [
            {
                key: ResponsePlanIncidentsGridColumnKey.id,
                name: intl.formatMessage(IncidentManagementResources.incidentId),
                isResizable: true,
                minWidth: 125,
                maxWidth: 125,
                onRender: onRenderIncidentId,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.id as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.id as keyof IncidentItem) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.title,
                name: intl.formatMessage(IncidentManagementResources.incidentTitle),
                isResizable: true,
                minWidth: 200,
                onRender: onRenderIncidentTitle,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.title as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.title as keyof IncidentItem) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.severity,
                name: intl.formatMessage(IncidentManagementResources.severityLevel),
                isResizable: true,
                minWidth: 125,
                maxWidth: 125,
                onRender: onRenderSeverityLevel,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.severity as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.severity as keyof IncidentItem) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.createdOn,
                name: intl.formatMessage(IncidentManagementResources.incidentCreated),
                isResizable: true,
                minWidth: 150,
                maxWidth: 150,
                onRender: onRenderIncidentCreated,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.createdOn as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.createdOn as keyof IncidentItem) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.assistedByAgent,
                name: intl.formatMessage(IncidentManagementResources.assistedByAgent),
                isResizable: true,
                minWidth: 150,
                maxWidth: 150,
                onRender: onRenderAssistedByAgent,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.assistedByAgent as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.assistedByAgent as keyof IncidentItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.mitigatedBy,
                name: intl.formatMessage(IncidentManagementResources.mitigatedBy),
                isResizable: true,
                minWidth: 150,
                maxWidth: 150,
                onRender: onRenderMitigatedBy,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.mitigatedBy as keyof IncidentItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.mitigatedBy as keyof IncidentItem)
                        ? isSortedDescending
                        : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
        ];

        return columns;
    }, [
        intl,
        onRenderIncidentId,
        onRenderIncidentTitle,
        onRenderSeverityLevel,
        onRenderIncidentCreated,
        onRenderAssistedByAgent,
        onRenderMitigatedBy,
        sortColumnKey,
        isSortedDescending,
        handleColumnClick,
    ]);

    const filteredGridItems = useMemo(() => {
        let filteredGridItems = incidents;

        if (searchText.trim() !== '') {
            filteredGridItems = filteredGridItems.filter(
                item => item.incidentId.includes(searchText.trim()) || item.incidentTitle.includes(searchText.trim())
            );
        }

        if (severityLevelFilter !== all) {
            filteredGridItems = filteredGridItems.filter(item => item.severity === severityLevelFilter);
        }

        if (mitigatedByFilter !== all) {
            filteredGridItems = filteredGridItems.filter(item =>
                mitigatedByFilter === 'yes' ? item.mitigatedBy === 'agent' : item.mitigatedBy === 'user'
            );
        }

        return filteredGridItems;
    }, [incidents, severityLevelFilter, mitigatedByFilter, searchText]);

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
        <Card style={{ width: '100%', height: '100%', overflowY: 'auto' }}>
            <Subtitle2>{intl.formatMessage(IncidentManagementResources.incidents)}</Subtitle2>

            <div style={{ width: '100%' }}>
                <div className={styles.incidentFiltersContainer} style={{ marginBottom: 0 }}>
                    <SearchBox
                        className={styles.searchBox}
                        placeholder={intl.formatMessage(IncidentManagementResources.searchIncidents)}
                        value={searchText}
                        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                        disabled={disabled}
                    />
                    <Dropdown
                        onOptionSelect={(_e, data) => setSeverityLevelFilter(data.optionValue ?? all)}
                        value={severityLevelFilter}
                        selectedOptions={[severityLevelFilter]}
                        button={severityLevelFilterLabel}
                        className={styles.searchBox}
                        disabled={disabled}
                    >
                        {severityLevelFilterOptions.map(option => (
                            <Option value={option.value} text={option.label}>
                                {option.label}
                            </Option>
                        ))}
                    </Dropdown>
                    <Dropdown
                        onOptionSelect={(_e, data) => setMitigatedByFilter(data.optionValue ?? all)}
                        value={mitigatedByFilter}
                        selectedOptions={[mitigatedByFilter]}
                        button={mitigatedByFilterLabel}
                        className={styles.searchBox}
                        disabled={disabled}
                    >
                        {mitigatedByFilterOptions.map(option => (
                            <Option value={option.value} text={option.label}>
                                {option.label}
                            </Option>
                        ))}
                    </Dropdown>
                </div>

                <div data-is-scrollable="true" user-select="text">
                    <ShimmeredDetailsList
                        columns={columns}
                        items={sortedItems}
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

                    {incidents.length === 0 && !isLoading && (
                        <Text align="center" block>
                            {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
                        </Text>
                    )}
                </div>
            </div>
        </Card>
    );
};

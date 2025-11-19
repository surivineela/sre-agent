import {
    Card,
    InputOnChangeData,
    Link,
    mergeClasses,
    SearchBox,
    SearchBoxChangeEvent,
    Subtitle2,
    Text,
} from '@fluentui/react-components';
import { PillFilter } from '../../../../Common/Components/PillFilter/PillFilter';
import { LabelKeyPair } from '../../../../Common/Components/PillFilter/ListWithSearch';
import { ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../../../Common/Clients/ThreadClient';
import { ISortedDetailsListColumn } from '../../../../Common/Components/DetailsList/Constants';
import { Thread } from '../../../../Common/Contracts/DataPlane/Thread';
import { formatDateTimeWithShortYear } from '../../../../Common/Helpers/Date';
import { getLocalizedMitigatedBy } from '../../../../Common/Helpers/IncidentManagement';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentItem } from '../ResponsePlanView';

// TODO: Pagination

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
    onOpenThread: (thread: Thread) => void;
}

export const ResponsePlanIncidentsGrid = ({ incidents, disabled, isLoading, onOpenThread }: ResponsePlanIncidentsGridProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const isDarkMode = useIsDarkMode();
    const { sreAgentEndpoint, resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    const [searchText, setSearchText] = useState<string>('');
    const [severityLevelFilters, setSeverityLevelFilters] = useState<string[]>([]);
    const [mitigatedByFilters, setMitigatedByFilters] = useState<string[]>([]);
    const [sortColumnKey, setSortColumnKey] = useState<keyof IncidentItem | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    // Filter by used values as more convenient, but also not sure if different platforms have different values
    const severityLevelFilterOptions = useMemo<LabelKeyPair[]>(() => {
        return Array.from(new Set(incidents.map(incident => incident.severity))).map(severity => ({
            label: severity,
            key: severity,
        }));
    }, [incidents]);

    const mitigatedByFilterOptions = useMemo<LabelKeyPair[]>(() => {
        return [
            { label: intl.formatMessage(SreAgentResources.agent), key: 'agent' },
            { label: intl.formatMessage(SreAgentResources.user), key: 'user' },
            { label: intl.formatMessage(SreAgentResources.inProgress), key: 'inProgress' },
        ];
    }, [intl]);



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

    const handleIncidentTitleClick = useCallback(
        async (incidentId: string) => {
            const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
            const response = await threadClient.getIncidentThreads({
                skip: 0,
                top: 1,
                filter: `incidentId eq '${incidentId}'`,
            });

            if (response.isSuccessful && response.content && response.content.length > 0) {
                onOpenThread(response.content[0]);
            } else {
                log({
                    action: 'handleIncidentTitleClick',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        incidentId,
                        error: response.error,
                    },
                });
            }
        },
        [sreAgentEndpoint, onOpenThread, log, resourceId]
    );

    const onRenderIncidentTitle = useCallback(
        (item: IncidentItem) => {
            return (
                <Link
                    onClick={() => {
                        handleIncidentTitleClick(item.incidentId);
                    }}
                >
                    {item.incidentTitle}
                </Link>
            );
        },
        [handleIncidentTitleClick]
    );

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

        if (severityLevelFilters.length > 0) {
            filteredGridItems = filteredGridItems.filter(item => severityLevelFilters.includes(item.severity));
        }

        if (mitigatedByFilters.length > 0) {
            filteredGridItems = filteredGridItems.filter(item => mitigatedByFilters.includes(item.mitigatedBy));
        }

        return filteredGridItems;
    }, [incidents, severityLevelFilters, mitigatedByFilters, searchText]);

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
            <Subtitle2 as="h3" style={{ margin: 0 }}>
                {intl.formatMessage(IncidentManagementResources.incidents)}
            </Subtitle2>

            <div className={styles.incidentFiltersContainer} style={{ marginBottom: 0 }}>
                <SearchBox
                    className={styles.searchBox}
                    placeholder={intl.formatMessage(IncidentManagementResources.filterByIncidentIdOrTitle)}
                    value={searchText}
                    onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchText(data.value ?? ''))}
                    disabled={disabled}
                />
                <PillFilter
                    filterType="combobox"
                    label={intl.formatMessage(IncidentManagementResources.severityLevel)}
                    options={severityLevelFilterOptions}
                    selectedKeys={severityLevelFilters}
                    onApply={(keys) => setSeverityLevelFilters(keys)}
                    disabled={disabled}
                    multiSelect
                    addAllOption
                />
                <PillFilter
                    filterType="combobox"
                    label={intl.formatMessage(IncidentManagementResources.mitigatedBy)}
                    options={mitigatedByFilterOptions}
                    selectedKeys={mitigatedByFilters}
                    onApply={(keys) => setMitigatedByFilters(keys)}
                    disabled={disabled}
                    multiSelect
                    addAllOption
                />
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

                {incidents.length === 0 && !isLoading && (
                    <Text align="center" block>
                        {intl.formatMessage(IncidentManagementResources.noIncidentsFound)}
                    </Text>
                )}
            </div>
        </Card>
    );
};

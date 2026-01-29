import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Dropdown,
    Input,
    Label,
    makeStyles,
    MessageBar,
    MessageBarBody,
    Option,
    Radio,
    RadioGroup,
    Skeleton,
    SkeletonItem,
    TableCellLayout,
    TableColumnDefinition,
    Text,
} from '@fluentui/react-components';
import { useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { TimeRangeKeyLabelPair, TimeRangeValue, TimespanKeys } from '../../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import {
    IncidentFilterDocumentPayload,
    IncidentQueryRequest,
    TestHandlerPayload,
    TestHandlerResponse,
} from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementResources, SreAgentResources, TriggerIncidentManagementResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { useIncidentFilterFields } from '../../Hooks/useIncidentFilterFields';
import { IncidentsListColumnKey } from '../CreateIncidentHandler/Contracts';
import { IcmOwningTeamSearch } from '../IcmOwningTeamSearch';
import { getPlatformSpecificStrings } from '../Utilities';

enum SearchType {
    IncidentId = 'incidentId',
    IncidentProperties = 'incidentProperties',
}

interface IncidentTableItem {
    id: string;
    incidentId: string;
    title: string;
    priority: string;
    createdDate: string;
}

export interface TriggerAgentDrawerProps {
    isOpen: boolean;
    onClose: (needRefresh: boolean) => void;
}

const TriggerAgentDrawer = ({ isOpen, onClose }: TriggerAgentDrawerProps) => {
    const queryIncidentsCount = 50;
    const intl = useIntl();
    const styles = useTriggerAgentDrawerStyles();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    const [searchType, setSearchType] = useState<SearchType>(SearchType.IncidentId);
    const [incidentId, setIncidentId] = useState<string>('');
    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>({ key: TimespanKeys.TwentyFourHours });
    const [queryIncidentsFilter, setQueryIncidentsFilter] = useState<IncidentFilterDocumentPayload | undefined>(undefined);
    const [selectedIncidents, setSelectedIncidents] = useState<Set<string>>(new Set());
    const [incidents, setIncidents] = useState<IncidentTableItem[]>([]);
    const [isSearching, setIsSearching] = useState<boolean>(false);
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [processingResults, setProcessingResults] = useState<TestHandlerResponse[]>([]);
    const contentScrollRef = useRef<HTMLDivElement>(null);
    const needThreadRefreshRef = useRef<boolean>(false);

    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint, log), [sreAgentEndpoint, log]);
    const { incidentTypeOptions, impactedServiceOptions, priorityOptions } = useIncidentFilterFields();

    useEffect(() => {
        if (processingResults.length > 0) {
            setTimeout(() => {
                contentScrollRef.current?.scrollTo({ top: contentScrollRef.current.scrollHeight, behavior: 'smooth' });
            }, 100);
        }
    }, [processingResults]);

    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const resetAllStates = () => {
        setSearchType(SearchType.IncidentId);
        setIncidentId('');
        setSelectedTimeRange({ key: TimespanKeys.TwentyFourHours });
        setQueryIncidentsFilter(undefined);
        setSelectedIncidents(new Set());
        setIncidents([]);
        setIsSearching(false);
        setIsSubmitting(false);
        setProcessingResults([]);
    };

    const impactedServiceOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allImpactedServices) }];
        impactedServiceOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [impactedServiceOptions, intl]);

    const priorityOptionsExtended = useMemo(() => {
        const options = [{ key: 'ALL', display: intl.formatMessage(platformSpecificStrings.severityOrPriorityAllOptionLabel) }];
        priorityOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [priorityOptions, intl, platformSpecificStrings]);

    const incidentTypeOptionsExtended = useMemo(() => {
        const options = [];
        if (incidentPlatformType !== IncidentManagementType.Icm) {
            options.push({ key: 'ALL', display: intl.formatMessage(IncidentManagementResources.allIncidentTypes) });
        }
        incidentTypeOptions.forEach(option => options.push({ key: option, display: option }));
        return options;
    }, [incidentTypeOptions, intl, incidentPlatformType]);

    const timeRangeOptions: TimeRangeKeyLabelPair[] = useMemo(
        () => [
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
            {
                key: TimespanKeys.FourteenDays,
                label: intl.formatMessage(IncidentManagementResources.last14Days),
            },
        ],
        [intl]
    );

    const disableSearchBtnForIncidentProperties = useMemo(() => {
        if (isSearching) {
            return true;
        } else if (searchType === SearchType.IncidentId) {
            return !incidentId.trim();
        } else if (searchType === SearchType.IncidentProperties) {
            switch (incidentPlatformType) {
                case IncidentManagementType.Icm:
                    return (
                        !queryIncidentsFilter?.owningTeamId ||
                        !queryIncidentsFilter?.incidentType ||
                        !queryIncidentsFilter?.impactedService ||
                        !queryIncidentsFilter?.priorities?.length
                    );
                case IncidentManagementType.PagerDuty:
                case IncidentManagementType.ServiceNow:
                    return (
                        !queryIncidentsFilter?.incidentType ||
                        !queryIncidentsFilter?.impactedService ||
                        !queryIncidentsFilter?.priorities?.length
                    );
                case IncidentManagementType.AzMonitor:
                    return !queryIncidentsFilter?.priorities?.length;
                default:
                    return false;
            }
        } else {
            return false;
        }
    }, [isSearching, searchType, incidentId, queryIncidentsFilter, incidentPlatformType]);
    const handleSubmit = async () => {
        if (selectedIncidents.size === 0) return;

        setIsSubmitting(true);
        setProcessingResults([]);
        const selectedIncidentIds = Array.from(selectedIncidents);
        const payload: TestHandlerPayload[] = selectedIncidentIds.map(id => ({
            incidentId: id,
        }));
        const response = await incidentHandlerClient.processIncidentsHandler(payload);

        if (response.isSuccessful && response.content) {
            setProcessingResults(response.content);
            setSelectedIncidents(new Set());
        }
        setIsSubmitting(false);
        // Notify parent to refresh thread list
        needThreadRefreshRef.current = true;
    };

    const handleSearch = async () => {
        setIsSearching(true);
        setProcessingResults([]);
        try {
            if (incidentId && incidentId.trim() !== '') {
                // If incident ID is provided, call getIncident
                const response = await incidentHandlerClient.getIncident(incidentId.trim());

                if (response.isSuccessful && response.content) {
                    const incident = response.content;
                    const incidentItem: IncidentTableItem = {
                        id: incident.id || '1',
                        incidentId: incident.id || '',
                        title: incident.title || '',
                        priority: incident.priority || '',
                        createdDate: incident.createdAt || '',
                    };
                    setIncidents([incidentItem]);
                } else {
                    setIncidents([]);
                }
            } else {
                // If no incident ID, use time range and call queryIncidents
                let durationInDays = 1;

                // Convert TimespanKeys to days
                switch (selectedTimeRange.key) {
                    case TimespanKeys.TwentyFourHours:
                        durationInDays = 1;
                        break;
                    case TimespanKeys.ThreeDays:
                        durationInDays = 3;
                        break;
                    case TimespanKeys.SevenDays:
                        durationInDays = 7;
                        break;
                    case TimespanKeys.FourteenDays:
                        durationInDays = 14;
                        break;
                    default:
                        durationInDays = 1;
                }

                const queryRequest: IncidentQueryRequest = {
                    durationInDays: durationInDays,
                    pageSize: queryIncidentsCount,
                    pageNumber: 1,
                    filter: {
                        ...queryIncidentsFilter,
                        impactedService:
                            queryIncidentsFilter?.impactedService === 'ALL' ? undefined : queryIncidentsFilter?.impactedService,
                        priorities:
                            queryIncidentsFilter?.priorities?.length === 0 || queryIncidentsFilter?.priorities?.includes('ALL')
                                ? undefined
                                : queryIncidentsFilter?.priorities,
                        incidentType: queryIncidentsFilter?.incidentType === 'ALL' ? undefined : queryIncidentsFilter?.incidentType,
                    },
                    statuses: [IncidentStatus.active, IncidentStatus.new],
                };

                const response = await incidentHandlerClient.queryIncidents(queryRequest);

                if (response.isSuccessful && response.content) {
                    const incidentItems: IncidentTableItem[] = response.content.items.map((incident, index) => ({
                        id: incident.id || index.toString(),
                        incidentId: incident.id || '',
                        title: incident.title || '',
                        priority: incident.priority || '',
                        createdDate: incident.createdAt || '',
                    }));
                    setIncidents(incidentItems);
                } else {
                    setIncidents([]);
                }
            }
        } catch (error) {
            setIncidents([]);
        } finally {
            setIsSearching(false);
        }
    };

    const columns = useMemo<TableColumnDefinition<IncidentTableItem>[]>(() => {
        return [
            createTableColumn<IncidentTableItem>({
                columnId: IncidentsListColumnKey.incidentId,
                compare: (a, b) => a.incidentId.localeCompare(b.incidentId),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertIdLabel)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <span>{item.incidentId}</span>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<IncidentTableItem>({
                columnId: IncidentsListColumnKey.title,
                compare: (a, b) => a.title.localeCompare(b.title),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertTitleLabel)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <span>{item.title}</span>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<IncidentTableItem>({
                columnId: IncidentsListColumnKey.priority,
                compare: (a, b) => a.priority.localeCompare(b.priority),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <span>{item.priority}</span>
                    </TableCellLayout>
                ),
            }),
            createTableColumn<IncidentTableItem>({
                columnId: IncidentsListColumnKey.createdTimestamp,
                compare: (a, b) => a.createdDate.localeCompare(b.createdDate),
                renderHeaderCell: () => (
                    <span style={{ fontWeight: 600 }}>{intl.formatMessage(platformSpecificStrings.incidentOrAlertCreatedLabel)}</span>
                ),
                renderCell: item => (
                    <TableCellLayout truncate>
                        <span>{item.createdDate}</span>
                    </TableCellLayout>
                ),
            }),
        ];
    }, [platformSpecificStrings, intl]);

    return (
        <Dialog
            open={isOpen}
            onOpenChange={(_, data) => {
                if (!data.open) {
                    resetAllStates();
                    onClose(needThreadRefreshRef.current);
                }
            }}
        >
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle>{intl.formatMessage(TriggerIncidentManagementResources.triggerAgent)}</DialogTitle>
                    <DialogContent className={styles.dialogContent} ref={contentScrollRef}>
                        <div className={styles.content}>
                            <div className={styles.fieldContainer}>
                                <Label>{intl.formatMessage(TriggerIncidentManagementResources.searchBy)}</Label>
                                <RadioGroup
                                    value={searchType}
                                    onChange={(_, data) => {
                                        setSearchType(data.value as SearchType);
                                        setIncidents([]);
                                        setSelectedIncidents(new Set());
                                        setProcessingResults([]);
                                        setIncidentId('');
                                        setQueryIncidentsFilter(undefined);
                                        setSelectedTimeRange({ key: TimespanKeys.TwentyFourHours });
                                    }}
                                    defaultValue={SearchType.IncidentId}
                                    layout="horizontal"
                                >
                                    <Radio
                                        value={SearchType.IncidentId}
                                        label={intl.formatMessage(TriggerIncidentManagementResources.incidentId)}
                                    />
                                    <Radio
                                        value={SearchType.IncidentProperties}
                                        label={intl.formatMessage(TriggerIncidentManagementResources.incidentProperties)}
                                    />
                                </RadioGroup>
                            </div>
                            {searchType === SearchType.IncidentId && (
                                <div className={styles.fieldContainer}>
                                    <Label htmlFor="incident-id">{intl.formatMessage(TriggerIncidentManagementResources.incidentId)}</Label>
                                    <Input
                                        id="incident-id"
                                        placeholder={intl.formatMessage(TriggerIncidentManagementResources.enterIncidentId)}
                                        value={incidentId}
                                        onChange={(_, data) => setIncidentId(data.value)}
                                        className={styles.incidentIdInput}
                                    />
                                </div>
                            )}
                            {searchType === SearchType.IncidentProperties && (
                                <>
                                    {incidentPlatformType === IncidentManagementType.Icm && (
                                        <IcmOwningTeamSearch
                                            defaultTeamId={queryIncidentsFilter?.owningTeamId}
                                            onFieldTouched={() => {}}
                                            onUpdateOwningTeam={team =>
                                                setQueryIncidentsFilter(prev => ({ ...prev, owningTeamId: `${team.id}` }))
                                            }
                                            comboboxClassName={styles.incidentIdInput}
                                        />
                                    )}
                                    {incidentPlatformType !== IncidentManagementType.AzMonitor && (
                                        <>
                                            <div className={styles.fieldContainerHorizontal}>
                                                <Label htmlFor="incident-type" required className={styles.fieldLabel}>
                                                    {intl.formatMessage(IncidentManagementResources.incidentType)}
                                                </Label>
                                                <Dropdown
                                                    id="incident-type"
                                                    placeholder={intl.formatMessage(IncidentManagementResources.chooseIncidentType)}
                                                    value={queryIncidentsFilter?.incidentType || ''}
                                                    selectedOptions={
                                                        queryIncidentsFilter?.incidentType ? [queryIncidentsFilter?.incidentType] : []
                                                    }
                                                    onOptionSelect={(_, data) =>
                                                        setQueryIncidentsFilter(prev => ({ ...prev, incidentType: data.optionValue }))
                                                    }
                                                    className={styles.fieldInput}
                                                >
                                                    {incidentTypeOptionsExtended.map(option => (
                                                        <Option key={option.key} value={option.key}>
                                                            {option.display}
                                                        </Option>
                                                    ))}
                                                </Dropdown>
                                            </div>
                                            <div className={styles.fieldContainerHorizontal}>
                                                <Label htmlFor="incident-impactedService" required className={styles.fieldLabel}>
                                                    {intl.formatMessage(IncidentManagementResources.impactedService)}
                                                </Label>
                                                <Dropdown
                                                    id="incident-impactedService"
                                                    placeholder={intl.formatMessage(IncidentManagementResources.chooseImpactedService)}
                                                    value={queryIncidentsFilter?.impactedService || ''}
                                                    selectedOptions={
                                                        queryIncidentsFilter?.impactedService ? [queryIncidentsFilter?.impactedService] : []
                                                    }
                                                    onOptionSelect={(_, data) =>
                                                        setQueryIncidentsFilter(prev => ({ ...prev, impactedService: data.optionValue }))
                                                    }
                                                    className={styles.fieldInput}
                                                >
                                                    {impactedServiceOptionsExtended.map(option => (
                                                        <Option key={option.key} value={option.key}>
                                                            {option.display}
                                                        </Option>
                                                    ))}
                                                </Dropdown>
                                            </div>
                                        </>
                                    )}

                                    <div className={styles.fieldContainerHorizontal}>
                                        <Label htmlFor="incident-priority" required className={styles.fieldLabel}>
                                            {intl.formatMessage(platformSpecificStrings.severityOrPriorityLabel)}
                                        </Label>
                                        <Dropdown
                                            id="incident-priority"
                                            placeholder={intl.formatMessage(platformSpecificStrings.severityOrPriorityPlaceholder)}
                                            selectedOptions={queryIncidentsFilter?.priorities ?? []}
                                            onOptionSelect={(_, data) =>
                                                setQueryIncidentsFilter(prev => ({ ...prev, priorities: data.selectedOptions }))
                                            }
                                            className={styles.fieldInput}
                                        >
                                            {priorityOptionsExtended.map(option => (
                                                <Option key={option.key} value={option.key}>
                                                    {option.display}
                                                </Option>
                                            ))}
                                        </Dropdown>
                                    </div>

                                    <div className={styles.fieldContainerHorizontal}>
                                        <Label className={styles.fieldLabel}>
                                            {intl.formatMessage(TriggerIncidentManagementResources.incidentCreateTimeRange)}
                                        </Label>
                                        <div className={styles.fieldInput}>
                                            <PillFilter
                                                filterType="timeRange"
                                                label={intl.formatMessage(SreAgentResources.timeRange)}
                                                labelDelimiter={intl.formatMessage(SreAgentResources.equals)}
                                                options={timeRangeOptions}
                                                onApply={setSelectedTimeRange}
                                                selectedValue={selectedTimeRange}
                                                useInDialog={true}
                                            />
                                        </div>
                                    </div>
                                </>
                            )}
                            <div className={styles.searchActions}>
                                <Button appearance="primary" onClick={handleSearch} disabled={disableSearchBtnForIncidentProperties}>
                                    {intl.formatMessage(TriggerIncidentManagementResources.search)}
                                </Button>
                            </div>
                            {isSearching && (
                                <Skeleton>
                                    <SkeletonItem size={32} />
                                </Skeleton>
                            )}
                            {!isSearching && incidents.length > 0 && (
                                <>
                                    {searchType === SearchType.IncidentProperties && (
                                        <Text size={300}>
                                            {intl.formatMessage(TriggerIncidentManagementResources.showingTopResults, {
                                                count: queryIncidentsCount,
                                            })}
                                        </Text>
                                    )}
                                    <div className={styles.tableContainer} style={{ minHeight: 40 + Math.min(incidents.length, 2) * 32 }}>
                                        <div className={styles.scrollableList}>
                                            <DataGrid
                                                items={incidents}
                                                columns={columns}
                                                sortable
                                                selectionMode="multiselect"
                                                selectedItems={selectedIncidents}
                                                onSelectionChange={(_, data) => setSelectedIncidents(data.selectedItems as Set<string>)}
                                                getRowId={item => item.id}
                                                resizableColumns
                                                columnSizingOptions={{
                                                    incidentId: {
                                                        minWidth: 100,
                                                        defaultWidth: 120,
                                                    },
                                                    title: {
                                                        minWidth: 180,
                                                        defaultWidth: 220,
                                                    },
                                                    priority: {
                                                        minWidth: 50,
                                                        defaultWidth: 80,
                                                    },
                                                }}
                                                size="small"
                                            >
                                                <DataGridHeader>
                                                    <DataGridRow>
                                                        {({ renderHeaderCell }) => (
                                                            <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>
                                                        )}
                                                    </DataGridRow>
                                                </DataGridHeader>
                                                <DataGridBody<IncidentTableItem>>
                                                    {({ item, rowId }) => (
                                                        <DataGridRow<IncidentTableItem> key={rowId}>
                                                            {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                                                        </DataGridRow>
                                                    )}
                                                </DataGridBody>
                                            </DataGrid>
                                        </div>
                                    </div>
                                </>
                            )}
                            {processingResults.length > 0 && (
                                <div className={styles.resultsContainer}>
                                    {processingResults.map((result, index) => {
                                        const isSuccess = result.statusCode === 200;
                                        const hasThreadId = result.threadId != null;
                                        let messageResource;

                                        if (isSuccess) {
                                            messageResource = hasThreadId
                                                ? TriggerIncidentManagementResources.incidentProcessSuccessWithThread
                                                : TriggerIncidentManagementResources.incidentProcessSuccess;
                                        } else {
                                            messageResource = hasThreadId
                                                ? TriggerIncidentManagementResources.incidentProcessFailureWithThread
                                                : TriggerIncidentManagementResources.incidentProcessFailure;
                                        }

                                        return (
                                            <MessageBar key={index} intent={isSuccess ? 'success' : 'error'}>
                                                <MessageBarBody>
                                                    {intl.formatMessage(messageResource, {
                                                        incidentId: result.incidentId,
                                                        threadId: result.threadId,
                                                        message: result.message ?? '',
                                                    })}
                                                </MessageBarBody>
                                            </MessageBar>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" onClick={handleSubmit} disabled={selectedIncidents.size === 0 || isSubmitting}>
                            {intl.formatMessage(TriggerIncidentManagementResources.submit)}
                        </Button>
                        <Button
                            appearance="secondary"
                            onClick={() => {
                                resetAllStates();
                                onClose(needThreadRefreshRef.current);
                            }}
                        >
                            {intl.formatMessage(SreAgentResources.close)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};

const useTriggerAgentDrawerStyles = makeStyles({
    dialogSurface: {
        maxWidth: '900px',
        maxHeight: '90vh',
        height: '90vh',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        flex: '1 1 auto',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        position: 'relative',
        overflowY: 'auto',
        flex: '1 1 auto',
        height: '0px',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
        flex: '1 1 0%',
        height: '100%',
    },
    fieldContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    fieldContainerHorizontal: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '16px',
    },
    fieldLabel: {
        minWidth: '150px',
        flexShrink: 0,
    },
    fieldInput: {
        flex: 1,
        maxWidth: '300px',
    },
    incidentIdInput: {
        maxWidth: '300px',
    },
    searchActions: {
        display: 'flex',
        gap: '8px',
    },
    resultsContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        paddingBottom: '24px',
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
});

export default TriggerAgentDrawer;

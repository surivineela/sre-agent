import {
    Button,
    createTableColumn,
    DataGrid,
    DataGridBody,
    DataGridCell,
    DataGridHeader,
    DataGridHeaderCell,
    DataGridRow,
    Drawer,
    DrawerBody,
    DrawerFooter,
    DrawerHeader,
    DrawerHeaderTitle,
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
    tokens,
    Toolbar,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { TimeRangeKeyLabelPair, TimeRangeValue, TimespanKeys } from '../../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { IncidentQueryRequest, TestHandlerPayload, TestHandlerResponse } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
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
    onClose: () => void;
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
    const [owningTeamId, setOwningTeamId] = useState<string | undefined>(undefined);
    const [incidentType, setIncidentType] = useState<string | undefined>(undefined);
    const [selectedIncidents, setSelectedIncidents] = useState<Set<string>>(new Set());
    const [incidents, setIncidents] = useState<IncidentTableItem[]>([]);
    const [isSearching, setIsSearching] = useState<boolean>(false);
    const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
    const [processingResults, setProcessingResults] = useState<TestHandlerResponse[]>([]);

    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint, log), [sreAgentEndpoint, log]);
    const { incidentTypeOptions } = useIncidentFilterFields();

    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const platformSpecificStrings = useMemo(() => getPlatformSpecificStrings(incidentPlatformType), [incidentPlatformType]);

    const resetAllStates = () => {
        setSearchType(SearchType.IncidentId);
        setIncidentId('');
        setSelectedTimeRange({ key: TimespanKeys.TwentyFourHours });
        setOwningTeamId(undefined);
        setIncidentType(undefined);
        setSelectedIncidents(new Set());
        setIncidents([]);
        setIsSearching(false);
        setIsSubmitting(false);
        setProcessingResults([]);
    };

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
                        ...(owningTeamId && { owningTeamId }),
                        ...(incidentType && { incidentType }),
                    },
                    statuses: [IncidentStatus.active],
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
    }, []);

    return (
        <Drawer
            modalType="non-modal"
            open={isOpen}
            position="end"
            size="large"
            className={styles.drawerRoot}
            onOpenChange={(_, data) => {
                if (!data.open) onClose();
            }}
        >
            <DrawerHeader className={styles.header}>
                <DrawerHeaderTitle
                    heading={{
                        className: styles.headingContainer,
                    }}
                    action={
                        <Toolbar>
                            <ToolbarButton
                                aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                                appearance="transparent"
                                icon={<Dismiss24Regular />}
                                onClick={() => {
                                    resetAllStates();
                                    onClose();
                                }}
                            />
                        </Toolbar>
                    }
                >
                    <div className={styles.titleText}>{intl.formatMessage(TriggerIncidentManagementResources.triggerAgent)}</div>
                </DrawerHeaderTitle>
            </DrawerHeader>
            <DrawerBody className={styles.body}>
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
                                setOwningTeamId(undefined);
                                setIncidentType(undefined);
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
                            <IcmOwningTeamSearch
                                defaultTeamId={owningTeamId}
                                onFieldTouched={() => {}}
                                onUpdateOwningTeam={team => setOwningTeamId(`${team.id}`)}
                                comboboxClassName={styles.incidentIdInput}
                            />
                            <div className={styles.fieldContainer}>
                                <Label htmlFor="incident-type" required>
                                    {intl.formatMessage(IncidentManagementResources.incidentType)}
                                </Label>
                                <Dropdown
                                    id="incident-type"
                                    placeholder={intl.formatMessage(IncidentManagementResources.chooseIncidentType)}
                                    value={incidentType || ''}
                                    selectedOptions={incidentType ? [incidentType] : []}
                                    onOptionSelect={(_, data) => setIncidentType(data.optionValue)}
                                    className={styles.incidentIdInput}
                                >
                                    {incidentTypeOptions.map(option => (
                                        <Option key={option} value={option}>
                                            {option}
                                        </Option>
                                    ))}
                                </Dropdown>
                            </div>
                            <div className={styles.fieldContainer}>
                                <Label>{intl.formatMessage(TriggerIncidentManagementResources.incidentCreateTimeRange)}</Label>
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
                        </>
                    )}
                    <div className={styles.searchActions}>
                        <Button
                            appearance="primary"
                            onClick={handleSearch}
                            disabled={
                                isSearching ||
                                (searchType === SearchType.IncidentId && !incidentId.trim()) ||
                                (searchType === SearchType.IncidentProperties && (!owningTeamId || !incidentType))
                            }
                        >
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
                            <div className={styles.tableContainer}>
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
                                    className={styles.dataGrid}
                                >
                                    <DataGridHeader>
                                        <DataGridRow>
                                            {({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}
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
            </DrawerBody>
            <DrawerFooter className={styles.footer}>
                <div className={styles.footerActions}>
                    <Button appearance="primary" onClick={handleSubmit} disabled={selectedIncidents.size === 0 || isSubmitting}>
                        {intl.formatMessage(TriggerIncidentManagementResources.submit)}
                    </Button>
                    <Button
                        appearance="secondary"
                        onClick={() => {
                            resetAllStates();
                            onClose();
                        }}
                    >
                        {intl.formatMessage(SreAgentResources.close)}
                    </Button>
                </div>
            </DrawerFooter>
        </Drawer>
    );
};

const useTriggerAgentDrawerStyles = makeStyles({
    drawerRoot: {
        marginTop: '50px',
        marginBottom: '8px',
        borderRadius: '12px',
        paddingRight: '15px',
        paddingLeft: '15px',
    },
    header: {
        padding: '16px 16px 7px 16px',
    },
    headingContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '8px',
        alignItems: 'center',
        justifyContent: 'start',
        overflow: 'hidden',
    },
    titleText: {
        fontSize: '20px',
        fontWeight: 600,
        lineHeight: '28px',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    body: {
        padding: '16px',
        overflowY: 'auto',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '24px',
    },
    fieldContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
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
    },
    tableContainer: {
        flex: '1',
        display: 'flex',
        flexDirection: 'column',
        minHeight: '0',
        overflow: 'auto',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '4px',
    },
    dataGrid: {
        width: '100%',
        maxWidth: '100%',
    },
    footer: {
        padding: '16px',
    },
    footerActions: {
        display: 'flex',
        gap: '8px',
        justifyContent: 'flex-end',
    },
});

export default TriggerAgentDrawer;

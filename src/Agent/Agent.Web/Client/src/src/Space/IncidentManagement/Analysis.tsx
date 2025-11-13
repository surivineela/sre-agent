import { DataVizPalette, getColorFromToken, IChartProps } from '@fluentui/react-charting';
import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-components';
import { WrenchScrewdriverColor } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { getDataPlaneErrorMessage } from '../../Common/Clients/DataPlaneClient';
import { TimeRangeValue, TimespanKeys } from '../../Common/Components/PillFilter/Contracts';
import { getDefaultTimeRangeOptions } from '../../Common/Components/PillFilter/Hooks/useTimeRangePillFilter';
import { PillFilter } from '../../Common/Components/PillFilter/PillFilter';
import { TextWithLink } from '../../Common/Components/TextWithLink';
import { IncidentFilter } from '../../Common/Contracts/Azure/IncidentHandler';
import { fillMissingDatesInTimeSeries } from '../../Common/Helpers/Date';
import { getLocalizedIncidentPlatformName } from '../../Common/Helpers/IncidentManagement';
import { getPercentChangeInArray } from '../../Common/Helpers/Math';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import CreateIncidentHandlerConsolidated from './CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import { ChartCard } from './Watchtower/Components/ChartCard';
import { IncidentResponsePlanGrid } from './Watchtower/Components/IncidentResponsePlanGrid';
import { StatCard, StatCardData } from './Watchtower/Components/StatCard';
import { getHandlersIncidentSummaryTrendQuery, getHandlersOverviewQuery } from './Watchtower/Queries';
import { ResponsePlanView } from './Watchtower/ResponsePlanView';

// NOTE: RCA impacted service(s) not hooked up

export interface IncidentSummaryItem {
    handledAt: Date;
    distinctIncidentCount: number;
    agentAssisted: number;
    userMitigated: number;
    agentMitigated: number;
    pendingUserAction: number;
}

export interface IncidentHandlerItem {
    responsePlanName: string;
    autonomyLevel: string;
    /** `"Default"` for default handler */
    planType: string; // False for Default, True for Custom
    distinctIncidentCount: number;
    agentAssisted: number;
    userMitigated: number;
    agentMitigated: number;
    pendingUserAction: number;
}

interface AnalysisProps {
    agentAppInsightsAppId: string;
}

const Analysis = ({ agentAppInsightsAppId }: AnalysisProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { token: appInsightsToken, isLoading: isAppInsightsTokenLoading } = useAuthToken('applicationinsightapi');
    const { resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);

    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>({ key: TimespanKeys.SevenDays });
    const [openedResponsePlan, setOpenedResponsePlan] = useState<IncidentHandlerItem | undefined>(undefined);
    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo | undefined>();
    const [handlerOperationStatus, setHandlerOperationStatus] = useState<OperationStatus | undefined>(undefined);

    const [isIncidentSummaryLoading, setIsIncidentSummaryLoading] = useState(true);
    const [isIncidentHandlersLoading, setIsIncidentHandlersLoading] = useState(true);
    const [incidentSummaryResponse, setIncidentSummaryResponse] = useState<IncidentSummaryItem[]>();
    const [incidentHandlersResponse, setIncidentHandlersResponse] = useState<IncidentHandlerItem[]>();
    const [queryErrorMessage, setQueryErrorMessage] = useState<string>();

    const timeRangeOptions = useMemo(() => getDefaultTimeRangeOptions(intl), [intl]);

    const numIncidentsReviewed = useMemo(
        () => incidentSummaryResponse?.reduce((sum, item) => sum + item.distinctIncidentCount, 0) ?? 0,
        [incidentSummaryResponse]
    );

    const filledIncidentSummaryData = useMemo(
        () =>
            fillMissingDatesInTimeSeries(incidentSummaryResponse ?? [], selectedTimeRange, {
                distinctIncidentCount: 0,
                agentAssisted: 0,
                userMitigated: 0,
                agentMitigated: 0,
                pendingUserAction: 0,
            }),
        [incidentSummaryResponse, selectedTimeRange]
    );

    const incidentsReviewedStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(filledIncidentSummaryData, 'distinctIncidentCount');

        return {
            currentValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                    },
                ],
            },
        };
    }, [intl, filledIncidentSummaryData, numIncidentsReviewed]);

    const assistedByAgentStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(filledIncidentSummaryData, 'agentAssisted');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.agentAssisted, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.assistedByAgent),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.agentAssisted })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed, filledIncidentSummaryData]);

    const mitigatedByAgentStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(filledIncidentSummaryData, 'agentMitigated');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.agentMitigated, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.agentMitigated })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed, filledIncidentSummaryData]);

    const mitigatedByUserStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(filledIncidentSummaryData, 'userMitigated');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.userMitigated, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.userMitigated })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed, filledIncidentSummaryData]);

    const pendingUserActionStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(filledIncidentSummaryData, 'pendingUserAction');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.pendingUserAction, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.pendingUserAction })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed, filledIncidentSummaryData]);

    const incidentSummaryChartData = useMemo<IChartProps>(() => {
        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.assistedByAgent),
                    color: getColorFromToken(DataVizPalette.color16),
                    data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.agentAssisted })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                    color: getColorFromToken(DataVizPalette.color8),
                    data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.agentMitigated })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.userMitigated })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                    color: getColorFromToken(DataVizPalette.color10),
                    data: filledIncidentSummaryData.map(row => ({ x: row.handledAt, y: row.pendingUserAction })) ?? [],
                    legendShape: 'circle',
                },
            ],
        };

        return data;
    }, [intl, filledIncidentSummaryData]);

    const fetchIncidentSummaryData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(agentAppInsightsAppId, appInsightsToken, {
            query: getHandlersIncidentSummaryTrendQuery(selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentSummaryItem[] = queryResultRows.map(row => ({
                handledAt: new Date(row[0] ?? Date.now()),
                distinctIncidentCount: row[1] as number,
                agentAssisted: row[2] as number,
                userMitigated: row[3] as number,
                agentMitigated: row[4] as number,
                pendingUserAction: row[5] as number,
            }));
            setIncidentSummaryResponse(data);
            setIsIncidentSummaryLoading(false);
        } else {
            const errorMessage = getDataPlaneErrorMessage(response.error);
            setQueryErrorMessage(errorMessage);
            log({
                action: 'fetchIncidentSummaryData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
    }, [agentAppInsightsAppId, resourceId, log, appInsightsToken, selectedTimeRange]);

    const fetchIncidentHandlersData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(agentAppInsightsAppId, appInsightsToken, {
            query: getHandlersOverviewQuery(selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentHandlerItem[] = queryResultRows.map(row => ({
                responsePlanName: row[0] as string,
                autonomyLevel: row[1] as string,
                planType: row[2] as string,
                distinctIncidentCount: row[3] as number,
                agentAssisted: row[4] as number,
                userMitigated: row[5] as number,
                agentMitigated: row[6] as number,
                pendingUserAction: row[7] as number,
            }));
            setIncidentHandlersResponse(data);
            setIsIncidentHandlersLoading(false);
        } else {
            const errorMessage = getDataPlaneErrorMessage(response.error);
            setQueryErrorMessage(errorMessage);
            log({
                action: 'fetchIncidentHandlersData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
    }, [agentAppInsightsAppId, resourceId, log, appInsightsToken, selectedTimeRange]);

    useEffect(() => {
        fetchIncidentSummaryData();
        fetchIncidentHandlersData();
    }, [fetchIncidentSummaryData, fetchIncidentHandlersData]);

    const handleEditHandler = useCallback((filter: IncidentFilter | undefined) => {
        setHandlerCreateOrEditInfo({ filter });
        setOpenedResponsePlan(undefined);
    }, []);

    useEffect(() => {
        if (handlerOperationStatus) {
            const timer = setTimeout(() => {
                setHandlerOperationStatus(undefined);
            }, 5000);
            return () => clearTimeout(timer);
        }
    }, [handlerOperationStatus]);

    if (handlerCreateOrEditInfo) {
        return (
            <CreateIncidentHandlerConsolidated
                exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                setHandlerOperationStatus={setHandlerOperationStatus}
                handlerCreateOrEditInfo={handlerCreateOrEditInfo}
            />
        );
    }

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    {handlerOperationStatus === 'succeeded' && (
                        <div style={{ marginBottom: 20 }}>
                            <MessageBar intent="success">
                                <MessageBarBody>
                                    <MessageBarTitle>{intl.formatMessage(IncidentManagementResources.responsePlanSaved)}</MessageBarTitle>
                                </MessageBarBody>
                            </MessageBar>
                        </div>
                    )}

                    {handlerOperationStatus === 'failed' && (
                        <div style={{ marginBottom: 20 }}>
                            <MessageBar intent="error">
                                <MessageBarBody>
                                    <MessageBarTitle>
                                        {intl.formatMessage(IncidentManagementResources.responsePlanSaveFailed)}
                                    </MessageBarTitle>
                                </MessageBarBody>
                            </MessageBar>
                        </div>
                    )}

                    {!isAppInsightsTokenLoading && !appInsightsToken ? (
                        <div
                            style={{
                                height: '100%',
                                display: 'flex',
                                flexDirection: 'column',
                                gap: 20,
                                justifyContent: 'center',
                                alignItems: 'center',
                            }}
                        >
                            <WrenchScrewdriverColor fontSize={96} />
                            <TextWithLink
                                text={intl.formatMessage(SreAgentResources.thisFeatureIsntAvailableInThisPortalYet)}
                                linkText={intl.formatMessage(SreAgentResources.openInAzurePortal)}
                                linkUrl={`https://portal.azure.com#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/${encodeURIComponent(resourceId)}/sreLink/views%2Fincidentmanagement%2FMetrics`}
                            />
                        </div>
                    ) : !openedResponsePlan ? (
                        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', gap: 20 }}>
                            <PillFilter
                                label={intl.formatMessage(SreAgentResources.timeRange)}
                                labelDelimiter={intl.formatMessage(SreAgentResources.equals)}
                                filterType="timeRange"
                                options={timeRangeOptions}
                                selectedValue={selectedTimeRange}
                                onApply={value => setSelectedTimeRange(value)}
                                customTimeRangeProps={{
                                    addCustomOption: true,
                                }}
                            />

                            {queryErrorMessage && (
                                <div style={{ maxWidth: 1000 }}>
                                    <MessageBar intent="error">
                                        <MessageBarBody>
                                            <MessageBarTitle>{intl.formatMessage(SreAgentResources.requestError)}</MessageBarTitle>
                                            {queryErrorMessage}
                                        </MessageBarBody>
                                    </MessageBar>
                                </div>
                            )}

                            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                                <StatCard
                                    title={intl.formatMessage(IncidentManagementResources.incidentsReviewed)}
                                    subtitle={intl.formatMessage(IncidentManagementResources.acrossAllIncidentsInPeriod, {
                                        platform: getLocalizedIncidentPlatformName(incidentPlatformType ?? '', intl),
                                    })}
                                    data={incidentsReviewedStatCardData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                                <StatCard
                                    title={intl.formatMessage(IncidentManagementResources.assistedByAgent)}
                                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsAssistedByAgent)}
                                    data={assistedByAgentStatCardData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                                <StatCard
                                    title={intl.formatMessage(IncidentManagementResources.mitigatedByAgent)}
                                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByAgent)}
                                    data={mitigatedByAgentStatCardData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                                <StatCard
                                    title={intl.formatMessage(IncidentManagementResources.mitigatedByUser)}
                                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByUser)}
                                    data={mitigatedByUserStatCardData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                                <StatCard
                                    title={intl.formatMessage(IncidentManagementResources.pendingUserAction)}
                                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsThatRequireAttention)}
                                    data={pendingUserActionStatCardData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                            </div>

                            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                                <ChartCard
                                    title={intl.formatMessage(IncidentManagementResources.incidentSummary)}
                                    data={incidentSummaryChartData}
                                    isLoading={isAppInsightsTokenLoading || isIncidentSummaryLoading}
                                />
                            </div>

                            <div style={{ display: 'flex', flex: '1 1 0', minHeight: 400, marginBottom: 20 }}>
                                <IncidentResponsePlanGrid
                                    responsePlans={incidentHandlersResponse ?? []}
                                    isLoading={isAppInsightsTokenLoading || isIncidentHandlersLoading}
                                    setOpenedResponsePlan={setOpenedResponsePlan}
                                />
                            </div>
                        </div>
                    ) : (
                        <ResponsePlanView
                            openedResponsePlan={openedResponsePlan}
                            setOpenedResponsePlan={setOpenedResponsePlan}
                            timeRangeOptions={timeRangeOptions}
                            selectedTimeRange={selectedTimeRange}
                            setSelectedTimeRange={setSelectedTimeRange}
                            appInsightsId={agentAppInsightsAppId}
                            appInsightsToken={appInsightsToken}
                            onEditHandler={handleEditHandler}
                        />
                    )}
                </div>
            </div>
        </div>
    );
};

export default Analysis;

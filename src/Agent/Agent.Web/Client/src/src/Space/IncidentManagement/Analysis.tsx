import { DataVizPalette, getColorFromToken, IChartProps } from '@fluentui/react-charting';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { TimeRangeKeyLabelPair, TimeRangePillFilter, TimeRangeValue } from '../../Common/Components/PillFilter/TimeRangePillFilter';
import { TimespanKeys } from '../../Common/Helpers/Date';
import { getLocalizedIncidentPlatformName } from '../../Common/Helpers/IncidentManagement';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { ChartCard } from './Watchtower/Components/ChartCard';
import { IncidentResponsePlanGrid } from './Watchtower/Components/IncidentResponsePlanGrid';
import { StatCard } from './Watchtower/Components/StatCard';
import {
    getHandlersIncidentCoverageTrendQuery,
    getHandlersIncidentSummaryTrendQuery,
    getHandlersOverviewQuery,
    watchtowerTempAppInsightsAppId,
} from './Watchtower/Queries';

// TODO: Hook up analysis stat cards + incident coverage lines
// TODO: IncidentResponsePlanGrid response plan link + sparklines data
// TODO: Hook up actual app insights (agent.logConfiguration.applicationInsightsConfiguration.<appId|connectionString>) (-> remove watchtowerTempAppInsightsAppId)

interface IncidentCoverageItem {
    handledAt: Date;
    distinctIncidentCount: number;
}

interface IncidentSummaryItem {
    handledAt: Date;
    distinctIncidentCount: number;
    userMitigated: number;
    agentMitigated: number;
    pendingUserAction: number;
}

export interface IncidentHandlerItem {
    responsePlanName: string;
    autonomyLevel: string;
    /** `"Default"` for default handler */
    planType: string;
    distinctIncidentCount: number;
    userMitigated: number;
    agentMitigated: number;
    pendingUserAction: number;
}

const Analysis = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const appInsightsToken = useAuthToken('applicationinsightapi');
    const { resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();
    const { agentObj } = useContext(SreAgentContext);

    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>({ key: TimespanKeys.SevenDays });

    const [isIncidentCoverageLoading, setIsIncidentCoverageLoading] = useState(true);
    const [isIncidentSummaryLoading, setIsIncidentSummaryLoading] = useState(true);
    const [isIncidentHandlersLoading, setIsIncidentHandlersLoading] = useState(true);
    const [incidentCoverageResponse, setIncidentCoverageResponse] = useState<IncidentCoverageItem[]>();
    const [incidentSummaryResponse, setIncidentSummaryResponse] = useState<IncidentSummaryItem[]>();
    const [incidentHandlersResponse, setIncidentHandlersResponse] = useState<IncidentHandlerItem[]>();

    const incidentManagementPlatform = useMemo(
        () => agentObj?.properties.incidentManagementConfiguration?.type,
        [agentObj?.properties.incidentManagementConfiguration?.type]
    );

    const timeRangeOptions: TimeRangeKeyLabelPair[] = useMemo(
        () => [
            {
                key: TimespanKeys.OneHour,
                label: intl.formatMessage(IncidentManagementResources.lastHour),
            },
            {
                key: TimespanKeys.SixHours,
                label: intl.formatMessage(IncidentManagementResources.last6Hours),
            },
            {
                key: TimespanKeys.TwelveHours,
                label: intl.formatMessage(IncidentManagementResources.last12Hours),
            },
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
        ],
        [intl]
    );

    const incidentCoverageChartData = useMemo<IChartProps>(() => {
        const chartData = incidentCoverageResponse ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.totalIncidents),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                },
                // TODO: This data ???
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsNotHandledByResponsePlanCriteria),
                    color: getColorFromToken(DataVizPalette.color3),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                },
            ],
        };

        return data;
    }, [intl, incidentCoverageResponse]);

    const incidentSummaryChartData = useMemo<IChartProps>(() => {
        const chartData = incidentSummaryResponse ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.agentMitigated })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                    color: getColorFromToken(DataVizPalette.color3),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.userMitigated })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                    color: getColorFromToken(DataVizPalette.color4),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.pendingUserAction })) ?? [],
                },
            ],
        };

        return data;
    }, [intl, incidentSummaryResponse]);

    const fetchIncidentCoverageData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(watchtowerTempAppInsightsAppId, appInsightsToken, {
            query: getHandlersIncidentCoverageTrendQuery(selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentCoverageItem[] = queryResultRows.map(row => ({
                handledAt: new Date(row[0] ?? Date.now()),
                distinctIncidentCount: row[1] as number,
            }));
            setIncidentCoverageResponse(data);
            setIsIncidentCoverageLoading(false);
        } else {
            log({
                action: 'fetchIncidentCoverageData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: 'TODO',
                },
            });
        }
    }, [resourceId, log, appInsightsToken, selectedTimeRange]);

    const fetchIncidentSummaryData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(watchtowerTempAppInsightsAppId, appInsightsToken, {
            query: getHandlersIncidentSummaryTrendQuery(selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentSummaryItem[] = queryResultRows.map(row => ({
                handledAt: new Date(row[0] ?? Date.now()),
                distinctIncidentCount: row[1] as number,
                agentMitigated: row[2] as number,
                userMitigated: row[3] as number,
                pendingUserAction: row[4] as number,
            }));
            setIncidentSummaryResponse(data);
            setIsIncidentSummaryLoading(false);
        } else {
            log({
                action: 'fetchIncidentSummaryData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: 'TODO',
                },
            });
        }
    }, [resourceId, log, appInsightsToken, selectedTimeRange]);

    const fetchIncidentHandlersData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(watchtowerTempAppInsightsAppId, appInsightsToken, {
            query: getHandlersOverviewQuery(selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentHandlerItem[] = queryResultRows.map(row => ({
                responsePlanName: row[0] as string,
                autonomyLevel: row[1] as string,
                planType: row[2] as string,
                distinctIncidentCount: row[3] as number,
                userMitigated: row[4] as number,
                agentMitigated: row[5] as number,
                pendingUserAction: row[6] as number,
            }));
            setIncidentHandlersResponse(data);
            setIsIncidentHandlersLoading(false);
        } else {
            log({
                action: 'fetchIncidentHandlersData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: 'TODO',
                },
            });
        }
    }, [resourceId, log, appInsightsToken, selectedTimeRange]);

    useEffect(() => {
        fetchIncidentCoverageData();
        fetchIncidentSummaryData();
        fetchIncidentHandlersData();
    }, [fetchIncidentCoverageData, fetchIncidentSummaryData, fetchIncidentHandlersData]);

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <div style={{ height: '100%', display: 'flex', flexDirection: 'column', gap: 20 }}>
                        <TimeRangePillFilter
                            label={intl.formatMessage(SreAgentResources.timeRange)}
                            options={timeRangeOptions}
                            selectedValue={selectedTimeRange}
                            onApply={value => setSelectedTimeRange(value)}
                            customTimeRangeProps={{
                                addCustomOption: true,
                            }}
                        />

                        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.incidentsReviewed)}
                                subtitle={intl.formatMessage(IncidentManagementResources.acrossAllIncidentsInPeriod, {
                                    platform: getLocalizedIncidentPlatformName(incidentManagementPlatform ?? '', intl),
                                })}
                                isLoading={isIncidentSummaryLoading}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.mitigatedByAgent)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByAgent)}
                                isLoading={isIncidentSummaryLoading}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.mitigatedByUser)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByUser)}
                                isLoading={isIncidentSummaryLoading}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.pendingUserAction)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsThatRequireAttention)}
                                isLoading={isIncidentSummaryLoading}
                            />
                        </div>

                        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                            <ChartCard
                                title={intl.formatMessage(IncidentManagementResources.incidentCoverage)}
                                data={incidentCoverageChartData}
                                isLoading={isIncidentCoverageLoading}
                            />
                            <ChartCard
                                title={intl.formatMessage(IncidentManagementResources.incidentSummary)}
                                data={incidentSummaryChartData}
                                isLoading={isIncidentSummaryLoading}
                            />
                        </div>

                        <div style={{ display: 'flex', flex: '1 1 0', minHeight: 150 }}>
                            <IncidentResponsePlanGrid
                                responsePlans={incidentHandlersResponse ?? []}
                                isLoading={isIncidentHandlersLoading}
                            />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Analysis;

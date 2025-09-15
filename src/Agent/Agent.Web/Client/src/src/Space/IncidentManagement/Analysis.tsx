import { DataVizPalette, getColorFromToken, IChartProps } from '@fluentui/react-charting';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { TimeRangeKeyLabelPair, TimeRangePillFilter, TimeRangeValue } from '../../Common/Components/PillFilter/TimeRangePillFilter';
import { AppInsightsQueryResult } from '../../Common/Contracts/Azure/AppInsights';
import { TimespanKeys } from '../../Common/Helpers/Date';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { IncidentManagementResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { ChartCard } from './Watchtower/Components/ChartCard';
import { IncidentResponsePlanGrid } from './Watchtower/Components/IncidentResponsePlanGrid';
import { StatCard } from './Watchtower/Components/StatCard';
import { getHandlersIncidentIntakeTrendQuery, watchtowerTempAppInsightsAppId } from './Watchtower/Queries';

const Analysis = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const appInsightsToken = useAuthToken('applicationinsightapi');

    const [selectedTimeRange, setSelectedTimeRange] = useState<TimeRangeValue>({ key: TimespanKeys.SevenDays });
    const [response, setResponse] = useState<AppInsightsQueryResult>();

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
        const queryResultRows = response?.tables[0]?.rows ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.totalIncidents),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsNotHandledByResponsePlanCriteria),
                    color: getColorFromToken(DataVizPalette.color3),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
            ],
        };

        return data;
    }, [intl, response]);

    const incidentSummaryChartData = useMemo<IChartProps>(() => {
        const queryResultRows = response?.tables[0]?.rows ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                    color: getColorFromToken(DataVizPalette.color3),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                    color: getColorFromToken(DataVizPalette.color4),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
            ],
        };

        return data;
    }, [intl, response]);

    const fetchQueryResults = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(watchtowerTempAppInsightsAppId, appInsightsToken, {
            query: getHandlersIncidentIntakeTrendQuery(),
        });

        console.log('Response: ', response);
        if (response.isSuccessful) {
            setResponse(response.content);
        } else {
            // TODO: logs
        }
    }, [appInsightsToken]);

    useEffect(() => {
        fetchQueryResults();
    }, [fetchQueryResults]);

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
                                    platform: '[incident platform name]',
                                })}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.mitigatedByAgent)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByAgent)}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.mitigatedByUser)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByUser)}
                            />
                            <StatCard
                                title={intl.formatMessage(IncidentManagementResources.pendingUserAction)}
                                subtitle={intl.formatMessage(IncidentManagementResources.incidentsThatRequireAttention)}
                            />
                        </div>

                        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                            <ChartCard
                                title={intl.formatMessage(IncidentManagementResources.incidentCoverage)}
                                data={incidentCoverageChartData}
                            />
                            <ChartCard
                                title={intl.formatMessage(IncidentManagementResources.incidentSummary)}
                                data={incidentSummaryChartData}
                            />
                        </div>

                        <div style={{ display: 'flex', flex: '1 1 0', minHeight: 150 }}>
                            <IncidentResponsePlanGrid responsePlans={[]} />
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Analysis;

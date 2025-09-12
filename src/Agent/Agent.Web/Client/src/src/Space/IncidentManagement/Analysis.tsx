import { DataVizPalette, getColorFromToken, IChartProps } from '@fluentui/react-charting';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { DateRange } from '../../Common/Components/DateRange';
import { AppInsightsQueryResult } from '../../Common/Contracts/Azure/AppInsights';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import { ChartCard } from './Watchtower/Components/ChartCard';
import { IncidentResponsePlanGrid } from './Watchtower/Components/IncidentResponsePlanGrid';
import { StatCard } from './Watchtower/Components/StatCard';
import { getHandlersIncidentIntakeTrendQuery, watchtowerTempAppInsightsAppId } from './Watchtower/Queries';

const Analysis = () => {
    const styles = useIncidentManagementStyles();
    const appInsightsToken = useAuthToken('applicationinsightapi');

    const [response, setResponse] = useState<AppInsightsQueryResult>();

    const incidentCoverageChartData = useMemo<IChartProps>(() => {
        const queryResultRows = response?.tables[0]?.rows ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: 'Total incidents',
                    color: getColorFromToken(DataVizPalette.color1),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: 'Incidents reviewed',
                    color: getColorFromToken(DataVizPalette.color2),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: 'Incidents not handled by response plan criteria',
                    color: getColorFromToken(DataVizPalette.color3),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
            ],
        };

        return data;
    }, [response]);

    const incidentSummaryChartData = useMemo<IChartProps>(() => {
        const queryResultRows = response?.tables[0]?.rows ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: 'Incidents reviewed',
                    color: getColorFromToken(DataVizPalette.color1),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: '[Placeholder] by agent',
                    color: getColorFromToken(DataVizPalette.color2),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: '[Placeholder] by user',
                    color: getColorFromToken(DataVizPalette.color3),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
                {
                    legend: 'Pending user action',
                    color: getColorFromToken(DataVizPalette.color4),
                    data: queryResultRows.map(row => ({ x: new Date(row[0] ?? Date.now()), y: row[1] as number })) ?? [],
                },
            ],
        };

        return data;
    }, [response]);

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

    // TODO: check flex wrap

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <div style={{ height: '100%', display: 'flex', flexDirection: 'column', gap: 20 }}>
                        <DateRange />

                        <div style={{ display: 'flex', gap: 20 }}>
                            <StatCard title="Incidents reviewed" subtitle="Across all incidents in [incident platform name]" />
                            <StatCard title="[Placeholder] by agent" subtitle="Incidents [placeholder] by agent" />
                            <StatCard title="[Placeholder] by user" subtitle="Incidents [placeholder] by user" />
                            <StatCard title="Pending user action" subtitle="Incidents that require attention" />
                        </div>

                        <div style={{ display: 'flex', gap: 20 }}>
                            <ChartCard title="Incident coverage" data={incidentCoverageChartData} />
                            <ChartCard title="Incident summary" data={incidentSummaryChartData} />
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

import { DataVizPalette, getColorFromToken, IChartProps, LineChart, Sparkline } from '@fluentui/react-charting';
import { Button } from '@fluentui/react-components';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { DateRange } from '../../Common/Components/DateRange';
import { AppInsightsQueryResult } from '../../Common/Contracts/Azure/AppInsights';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

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
                    x: 2,
                    y: 140.98,
                },
                {
                    x: 3,
                    y: 20,
                },
                {
                    x: 4,
                    y: 89.7,
                },
                {
                    x: 5,
                    y: 99,
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

const tempAppInsightsAppId = 'bc8d1232-d691-428e-a29f-7e785bf2d016';

const getHandlersIncidentIntakeTrendQuery = `let formattedStartTime = ago(30d);
let formattedEndTime = now();
let timeGrain = 1d;
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt= todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| project IncidentId, IncidentHandledAt , UpdatedOn
| summarize arg_max(UpdatedOn, IncidentHandledAt ) by IncidentId
| summarize DistinctIncidentIds = dcount(IncidentId) by bin(IncidentHandledAt , timeGrain)   `;

const Analysis = () => {
    const styles = useIncidentManagementStyles();
    const appInsightsToken = useAuthToken('applicationinsightapi');

    const [response, setResponse] = useState<AppInsightsQueryResult>();

    const lineChartDummyData = useMemo(() => {
        console.log(response);
        const data: IChartProps = {
            chartTitle: 'Incident Intake Trend',
            lineChartData: [
                {
                    legend: 'Incidents',
                    color: getColorFromToken(DataVizPalette.color1),
                    data: response?.tables[0]?.rows.map(row => ({ x: new Date(row[0]), y: row[1] as number })) ?? [],
                },
            ],
        };

        return data;
    }, [response]);

    const fetchQueryResults = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(tempAppInsightsAppId, appInsightsToken, {
            query: getHandlersIncidentIntakeTrendQuery,
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
                    <DateRange />

                    <div style={{ marginTop: 20 }}>
                        <Sparkline data={sparklineDummyData} showLegend />
                    </div>

                    <Button onClick={fetchQueryResults} style={{ marginTop: 20 }}>
                        Refresh
                    </Button>

                    <div>{JSON.stringify(response)}</div>

                    <div style={{ height: 500, width: 800, marginTop: 20 }}>
                        <LineChart data={lineChartDummyData} />
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Analysis;

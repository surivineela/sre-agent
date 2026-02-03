import { FC, memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AppInsightsClient } from '../../Common/Clients/AppInsightsClient';
import { useAuthToken } from '../../Common/Hooks/useAuthToken';
import { OverviewResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { getHandledIncidentsCountQuery, getHandledIncidentsCountTrendQuery } from '../IncidentManagement/Watchtower/Queries';
import MetricsCard from './MetricsCard';

const ReviewedIncidentsCard: FC = () => {
    const intl = useIntl();

    const [count, setCount] = useState<string>('');
    const [trend, setTrend] = useState<{ x: number; y: number }[]>([]);
    const [isFetching, setIsFetching] = useState<boolean>(false);

    const { agentObj, agentLoading } = useContext(SreAgentContext);
    const { token: appInsightsToken, isLoading: appInsightsLoading } = useAuthToken('applicationinsightapi');

    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );

    const fetchIncidentSummaryData = useCallback(
        async (signal?: { cancelled: boolean }): Promise<void> => {
            if (!agentAppInsightsAppId || !appInsightsToken) {
                return;
            }

            setIsFetching(true);
            const [totalResponse, trendResponse] = await Promise.all([
                AppInsightsClient.getLogQueryResults(agentAppInsightsAppId, appInsightsToken, {
                    query: getHandledIncidentsCountQuery(),
                }),
                AppInsightsClient.getLogQueryResults(agentAppInsightsAppId, appInsightsToken, {
                    query: getHandledIncidentsCountTrendQuery(),
                }),
            ]);

            // Discard results if the effect was cleaned up
            if (signal?.cancelled) {
                return;
            }

            setIsFetching(false);

            if (totalResponse.isSuccessful && trendResponse.isSuccessful) {
                const count = totalResponse.content?.tables[0]?.rows[0]?.[0] as number | undefined;
                const trend = trendResponse.content?.tables[0]?.rows || [];

                if (count === undefined || isNaN(count)) {
                    setCount('-');
                } else {
                    setCount(`${count.toString()}`);
                }

                if (trend && trend.length > 0) {
                    const trendData = trend.map((item, index) => ({
                        x: index,
                        y: item[1] as number,
                    }));

                    setTrend(trendData);
                } else {
                    setTrend([]);
                }
            } else {
                setCount('-');
                setTrend([]);
            }
        },

        [agentAppInsightsAppId, appInsightsToken]
    );

    useEffect(() => {
        const signal = { cancelled: false };
        fetchIncidentSummaryData(signal);

        return () => {
            signal.cancelled = true;
            setCount('');
            setIsFetching(false);
        };
    }, [fetchIncidentSummaryData]);

    return (
        <MetricsCard
            title={intl.formatMessage(OverviewResources.reviewedIncidents)}
            chartData={trend}
            score={count}
            refresh={() => fetchIncidentSummaryData()}
            isFetching={isFetching || agentLoading || appInsightsLoading}
        />
    );
};

export default memo(ReviewedIncidentsCard);

import { DataVizPalette, getColorFromToken, IChartProps } from '@fluentui/react-charting';
import { Body1, Button, Divider, Subtitle1, tokens } from '@fluentui/react-components';
import { ArrowLeft20Regular } from '@fluentui/react-icons';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../../Common/Clients/AppInsightsClient';
import { TimeRangeKeyLabelPair, TimeRangeValue } from '../../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { getLocalizedAgentMode } from '../../../Common/Helpers/AgentMode';
import { getPercentChangeInArray } from '../../../Common/Helpers/Math';
import { IncidentManagementResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { IncidentHandlerItem, IncidentSummaryItem } from '../Analysis';
import { ChartCard } from './Components/ChartCard';
import { RcaCard } from './Components/RcaCard';
import { ResponsePlanIncidentsGrid } from './Components/ResponsePlanIncidentsGrid';
import { StatCard, StatCardData } from './Components/StatCard';
import { getHandlerIncidentOverviewQuery, getHandlerIncidentSummaryTrendQuery } from './Queries';

export interface IncidentItem {
    incidentId: string;
    incidentTitle: string;
    severity: string;
    createdOn: Date;
    mitigatedBy: 'user' | 'agent' | 'inProgress';
    // meantTimeToMitigate: number; // No data for this yet
}

interface ResponsePlanViewProps {
    openedResponsePlan: IncidentHandlerItem;
    setOpenedResponsePlan: (plan: IncidentHandlerItem | undefined) => void;
    timeRangeOptions: TimeRangeKeyLabelPair[];
    selectedTimeRange: TimeRangeValue;
    setSelectedTimeRange: (value: TimeRangeValue) => void;
    appInsightsId: string;
    appInsightsToken: string | null;
}

export const ResponsePlanView = ({
    openedResponsePlan,
    setOpenedResponsePlan,
    timeRangeOptions,
    selectedTimeRange,
    setSelectedTimeRange,
    appInsightsId,
    appInsightsToken,
}: ResponsePlanViewProps) => {
    const intl = useIntl();
    const { resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    // TODO: View response plan panel
    const [_isViewResponsePlanPanelOpen, setIsViewResponsePlanPanelOpen] = useState(false);

    const [isIncidentSummaryLoading, setIsIncidentSummaryLoading] = useState(true);
    const [isIncidentsLoading, setIsIncidentsLoading] = useState(true);

    const [incidentSummaryResponse, setIncidentSummaryResponse] = useState<IncidentSummaryItem[]>();
    const [incidentsResponse, setIncidentsResponse] = useState<IncidentItem[]>();

    const numIncidentsReviewed = useMemo(
        () => incidentSummaryResponse?.reduce((sum, item) => sum + item.distinctIncidentCount, 0) ?? 0,
        [incidentSummaryResponse]
    );

    const incidentsReviewedStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(incidentSummaryResponse ?? [], 'distinctIncidentCount');

        return {
            currentValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: incidentSummaryResponse?.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed]);

    const mitigatedByAgentStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(incidentSummaryResponse ?? [], 'agentMitigated');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.agentMitigated, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: incidentSummaryResponse?.map(row => ({ x: row.handledAt, y: row.agentMitigated })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed]);

    const mitigatedByUserStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(incidentSummaryResponse ?? [], 'userMitigated');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.userMitigated, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: incidentSummaryResponse?.map(row => ({ x: row.handledAt, y: row.userMitigated })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed]);

    const pendingUserActionStatCardData = useMemo<StatCardData>(() => {
        const percentChange = getPercentChangeInArray(incidentSummaryResponse ?? [], 'pendingUserAction');

        return {
            currentValue: incidentSummaryResponse?.reduce((sum, item) => sum + item.pendingUserAction, 0) ?? 0,
            maxValue: numIncidentsReviewed,
            percentChange,
            sparklineData: {
                lineChartData: [
                    {
                        legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                        color: getColorFromToken(DataVizPalette.color16),
                        data: incidentSummaryResponse?.map(row => ({ x: row.handledAt, y: row.pendingUserAction })) ?? [],
                    },
                ],
            },
        };
    }, [intl, incidentSummaryResponse, numIncidentsReviewed]);

    const incidentSummaryChartData = useMemo<IChartProps>(() => {
        const chartData = incidentSummaryResponse ?? [];

        const data: IChartProps = {
            lineChartData: [
                {
                    legend: intl.formatMessage(IncidentManagementResources.incidentsReviewed),
                    color: getColorFromToken(DataVizPalette.color1),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.distinctIncidentCount })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByAgent),
                    color: getColorFromToken(DataVizPalette.color8),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.agentMitigated })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.mitigatedByUser),
                    color: getColorFromToken(DataVizPalette.color2),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.userMitigated })) ?? [],
                    legendShape: 'circle',
                },
                {
                    legend: intl.formatMessage(IncidentManagementResources.pendingUserAction),
                    color: getColorFromToken(DataVizPalette.color10),
                    data: chartData.map(row => ({ x: row.handledAt, y: row.pendingUserAction })) ?? [],
                    legendShape: 'circle',
                },
            ],
        };

        return data;
    }, [intl, incidentSummaryResponse]);

    const fetchResponsePlanIncidentSummaryData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(appInsightsId, appInsightsToken, {
            query: getHandlerIncidentSummaryTrendQuery(openedResponsePlan.responsePlanName, selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentSummaryItem[] = queryResultRows.map(row => ({
                handledAt: new Date(row[0] ?? Date.now()),
                distinctIncidentCount: row[1] as number,
                userMitigated: row[2] as number,
                agentMitigated: row[3] as number,
                pendingUserAction: row[4] as number,
            }));
            setIncidentSummaryResponse(data);
            setIsIncidentSummaryLoading(false);
        } else {
            log({
                action: 'fetchResponsePlanIncidentSummaryData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
    }, [resourceId, log, openedResponsePlan, appInsightsId, appInsightsToken, selectedTimeRange]);

    const fetchIncidentsData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(appInsightsId, appInsightsToken, {
            query: getHandlerIncidentOverviewQuery(openedResponsePlan.responsePlanName, selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: IncidentItem[] = queryResultRows.map(row => ({
                incidentId: row[0] as string,
                incidentTitle: row[1] as string,
                severity: row[2] as string,
                createdOn: new Date(row[3] ?? Date.now()),
                mitigatedBy: row[5] === 'active' ? 'inProgress' : row[4] === 'True' ? 'agent' : 'user',
            }));
            setIncidentsResponse(data);
            setIsIncidentsLoading(false);
        } else {
            log({
                action: 'fetchIncidentsData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
    }, [resourceId, log, openedResponsePlan, appInsightsId, appInsightsToken, selectedTimeRange]);

    useEffect(() => {
        fetchResponsePlanIncidentSummaryData();
        fetchIncidentsData();
    }, [fetchResponsePlanIncidentSummaryData, fetchIncidentsData]);

    return (
        <div style={{ height: '100%', display: 'flex', flexDirection: 'column', gap: 20 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <Button appearance="transparent" icon={<ArrowLeft20Regular />} onClick={() => setOpenedResponsePlan(undefined)} />
                    <div>
                        <Subtitle1 block>{openedResponsePlan.responsePlanName || intl.formatMessage(SreAgentResources.default)}</Subtitle1>
                        <Body1 block style={{ color: tokens.colorNeutralForeground4 }}>
                            {intl.formatMessage(IncidentManagementResources.autonomyLevel)}:{' '}
                            {getLocalizedAgentMode(openedResponsePlan.autonomyLevel, intl)}
                        </Body1>
                    </div>
                </div>

                <Button onClick={() => setIsViewResponsePlanPanelOpen(true)}>
                    {intl.formatMessage(IncidentManagementResources.viewPlan)}
                </Button>
            </div>

            <Divider style={{ marginTop: -4, flexGrow: 0 }} />

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

            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                <StatCard
                    title={intl.formatMessage(IncidentManagementResources.incidentsReviewed)}
                    subtitle={intl.formatMessage(IncidentManagementResources.usingThisResponsePlan)}
                    data={incidentsReviewedStatCardData}
                    isLoading={isIncidentSummaryLoading}
                />
                <StatCard
                    title={intl.formatMessage(IncidentManagementResources.mitigatedByAgent)}
                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByAgent)}
                    data={mitigatedByAgentStatCardData}
                    isLoading={isIncidentSummaryLoading}
                />
                <StatCard
                    title={intl.formatMessage(IncidentManagementResources.mitigatedByUser)}
                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsMitigatedByUser)}
                    data={mitigatedByUserStatCardData}
                    isLoading={isIncidentSummaryLoading}
                />
                <StatCard
                    title={intl.formatMessage(IncidentManagementResources.pendingUserAction)}
                    subtitle={intl.formatMessage(IncidentManagementResources.incidentsThatRequireAttention)}
                    data={pendingUserActionStatCardData}
                    isLoading={isIncidentSummaryLoading}
                />
            </div>

            <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap' }}>
                <ChartCard
                    title={intl.formatMessage(IncidentManagementResources.incidentSummary)}
                    data={incidentSummaryChartData}
                    isLoading={isIncidentSummaryLoading}
                />
                <RcaCard
                    openedResponsePlan={openedResponsePlan}
                    selectedTimeRange={selectedTimeRange}
                    appInsightsId={appInsightsId}
                    appInsightsToken={appInsightsToken}
                />
            </div>

            <div style={{ display: 'flex', flex: '1 1 0', minHeight: 200 }}>
                <ResponsePlanIncidentsGrid incidents={incidentsResponse ?? []} isLoading={isIncidentsLoading} />
            </div>
        </div>
    );
};

import { Card, Link, mergeClasses, Subtitle2, Text, tokens } from '@fluentui/react-components';
import { ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../../../../Common/Clients/AppInsightsClient';
import { AiGeneratedBadge } from '../../../../Common/Components/AiGeneratedBadge';
import { ISortedDetailsListColumn } from '../../../../Common/Components/DetailsList/Constants';
import { TimeRangeValue } from '../../../../Common/Components/PillFilter/Contracts';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { IncidentManagementResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { IncidentHandlerItem } from '../../Analysis';
import { getIncidentRootCauseOverviewQuery } from '../Queries';

enum ResponsePlanIncidentsGridColumnKey {
    category = 'category',
    incidentCount = 'incidentCount',
    impactedServices = 'impactedServices',
}

interface RcaItem {
    category: string;
    incidentCount: number;
    // impactedServices: string[]; // No data yet
}

interface RcaCardProps {
    openedResponsePlan: IncidentHandlerItem;
    selectedTimeRange: TimeRangeValue;
    appInsightsId: string;
    appInsightsToken: string | null;
}

export const RcaCard = ({ openedResponsePlan, selectedTimeRange, appInsightsId, appInsightsToken }: RcaCardProps) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const isDarkMode = useIsDarkMode();
    const { resourceId } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    // TODO: RCA category panel
    const [_isRcaCategoryPanelOpen, setIsRcaCategoryPanelOpen] = useState(false);

    const [sortColumnKey, setSortColumnKey] = useState<keyof RcaItem | undefined>();
    const [isSortedDescending, setIsSortedDescending] = useState<boolean>(false);

    const [isRcaOverviewLoading, setIsRcaOverviewLoading] = useState(true);
    const [rcaOverviewItems, setRcaOverviewItems] = useState<RcaItem[]>();

    const fetchResponsePlanRcaData = useCallback(async () => {
        if (!appInsightsToken) return;

        const response = await AppInsightsClient.getLogQueryResults(appInsightsId, appInsightsToken, {
            query: getIncidentRootCauseOverviewQuery(openedResponsePlan.responsePlanName, selectedTimeRange),
        });

        if (response.isSuccessful) {
            const queryResultRows = response.content?.tables[0]?.rows ?? [];
            const data: RcaItem[] = queryResultRows.map(row => ({
                category: row[0] as string,
                // impactedServices: [row[1] as string],
                incidentCount: row[1] as number,
            }));
            setRcaOverviewItems(data);
            setIsRcaOverviewLoading(false);
        } else {
            log({
                action: 'fetchResponsePlanRcaData',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    error: response.error?.response?.data?.error,
                },
            });
        }
    }, [resourceId, log, openedResponsePlan, appInsightsId, appInsightsToken, selectedTimeRange]);

    const handleColumnClick = useCallback(
        (column: IColumn) => {
            const isSameColumn = column.key === sortColumnKey;
            setSortColumnKey(column.key as keyof RcaItem);
            setIsSortedDescending(isSameColumn ? !isSortedDescending : false);
        },
        [sortColumnKey, isSortedDescending]
    );

    const onRenderCategory = useCallback(
        (item: RcaItem) => {
            if (!item.category) return intl.formatMessage(SreAgentResources.other);

            const tempDisabled = true; // Disabled until context pane implemented
            if (tempDisabled) return item.category;

            return (
                <Link
                    onClick={() => {
                        setIsRcaCategoryPanelOpen(true);
                    }}
                >
                    {item.category}
                </Link>
            );
        },
        [intl]
    );

    const onRenderIncidentCount = useCallback((item: RcaItem) => {
        return <Text>{item.incidentCount}</Text>;
    }, []);

    /*const onRenderImpactedServices = useCallback((item: RcaItem) => {
        return (
            <div style={{ display: 'flex', gap: 4 }}>
                {item.impactedServices.map((service, index) => {
                    let text = service;

                    if (index === 2) {
                        text = `+${item.impactedServices.length - 2}`;
                    } else if (index > 2) {
                        return null;
                    } else if (!service) {
                        return '-';
                    }

                    return (
                        <Badge appearance="tint" color="subtle" shape="rounded">
                            {text}
                        </Badge>
                    );
                })}
            </div>
        );
    }, []);*/

    const columns = useMemo<ISortedDetailsListColumn[]>(() => {
        const columns: ISortedDetailsListColumn[] = [
            {
                key: ResponsePlanIncidentsGridColumnKey.category,
                name: intl.formatMessage(IncidentManagementResources.topCategories),
                isResizable: true,
                minWidth: 200,
                onRender: onRenderCategory,
            },
            {
                key: ResponsePlanIncidentsGridColumnKey.incidentCount,
                name: intl.formatMessage(IncidentManagementResources.incidents),
                isResizable: true,
                minWidth: 125,
                maxWidth: 125,
                onRender: onRenderIncidentCount,
                isSorted: sortColumnKey === (ResponsePlanIncidentsGridColumnKey.incidentCount as keyof RcaItem),
                isSortedDescending:
                    sortColumnKey === (ResponsePlanIncidentsGridColumnKey.incidentCount as keyof RcaItem) ? isSortedDescending : undefined,
                onColumnClick: (_, col) => handleColumnClick(col),
            },
        ];

        return columns;
    }, [intl, onRenderCategory, onRenderIncidentCount, sortColumnKey, isSortedDescending, handleColumnClick]);

    const sortedItems = useMemo(() => {
        if (!rcaOverviewItems) return [];
        if (!sortColumnKey) return rcaOverviewItems;

        return [...rcaOverviewItems].sort((a, b) => {
            const valA = a[sortColumnKey] ?? '';
            const valB = b[sortColumnKey] ?? '';

            if (valA === valB) return 0;
            return (valA > valB ? 1 : -1) * (isSortedDescending ? -1 : 1);
        });
    }, [rcaOverviewItems, sortColumnKey, isSortedDescending]);

    useEffect(() => {
        fetchResponsePlanRcaData();
    }, [fetchResponsePlanRcaData]);

    return (
        <Card style={{ flex: '1 1 650px', minWidth: 650, height: 310 }} appearance={isDarkMode ? 'filled-alternative' : undefined}>
            <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                    <Subtitle2 as='h3' style={{ margin: 0 }}>{intl.formatMessage(IncidentManagementResources.rootCauseAnalysis)}</Subtitle2>
                    <AiGeneratedBadge />
                </div>
                <Text style={{ color: tokens.colorNeutralForeground4 }}>
                    {intl.formatMessage(IncidentManagementResources.rootCauseAnalysisDescription)}
                </Text>
            </div>

            <div data-is-scrollable="true" user-select="text" style={{ overflowY: 'auto' }}>
                <ShimmeredDetailsList
                    columns={columns}
                    items={sortedItems}
                    constrainMode={ConstrainMode.horizontalConstrained}
                    layoutMode={DetailsListLayoutMode.justified}
                    selectionMode={SelectionMode.none}
                    enableShimmer={isRcaOverviewLoading}
                    className={mergeClasses(styles.detailsListBase, isDarkMode ? styles.detailsListDarkModeBackground : undefined)}
                    styles={{
                        root: {
                            width: '100%',
                            userSelect: 'text',
                        },
                    }}
                    compact
                />

                {rcaOverviewItems?.length === 0 && !isRcaOverviewLoading && (
                    <Text align="center" block>
                        {intl.formatMessage(IncidentManagementResources.noRcaCategoriesFound)}
                    </Text>
                )}
            </div>
        </Card>
    );
};

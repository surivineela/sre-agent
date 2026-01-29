import { GroupedVerticalBarChart, IChartProps, ILegendsStyles, LineChart } from '@fluentui/react-charting';
import { Button, makeStyles, Skeleton, SkeletonItem, Subtitle2, Text, tokens, Tooltip } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { getLocaleDateTimeHHMM } from '../../../../Common/Helpers/Date';
import { convertLineChartToAdaptiveGroupedRanges } from '../../../../Common/Helpers/Graph';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';

const sentenceCaseLegendStyles: Partial<ILegendsStyles> = {
    text: {
        textTransform: 'none',
    },
};

const useChartCardStyles = makeStyles({
    card: {
        flex: '1 1 550px',
        minWidth: '550px',
        height: '310px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '16px',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
    },
    buttonBar: {
        display: 'flex',
        justifyContent: 'end',
        alignItems: 'center',
        gap: '6px',
    },
    chartTypeButtonSelected: {
        backgroundColor: tokens.colorSubtleBackgroundPressed,
    },
    chartContainer: {
        height: '275px',
        width: 'calc(100% - 16px)',
    },
    skeletonItem: {
        height: '210px',
    },
    calloutContainer: {
        padding: '11px 16px',
        minWidth: '150px',
    },
    calloutDate: {
        fontSize: '12px',
        lineHeight: '16px',
        marginBottom: '8px',
        color: tokens.colorNeutralForeground2,
    },
    calloutContent: {
        display: 'flex',
        alignItems: 'flex-start',
        gap: '8px',
    },
    calloutColorBar: {
        width: '3px',
        height: '42px',
        flexShrink: 0,
    },
    calloutLabel: {
        fontSize: '12px',
        lineHeight: '16px',
        marginBottom: '4px',
    },
    calloutValue: {
        fontSize: '18px',
        fontWeight: 600,
        lineHeight: '24px',
    },
});

interface ChartCardProps {
    title: string;
    data: IChartProps;
    isLoading?: boolean;
}

export const ChartCard = ({ title, data, isLoading }: ChartCardProps) => {
    const intl = useIntl();
    const styles = useChartCardStyles();
    const [chartType, setChartType] = useState<'line' | 'bar'>('line');

    const hasData = useMemo(() => {
        return data?.lineChartData?.some(series => series.data && series.data.length > 0);
    }, [data]);

    const groupedBarData = useMemo(() => {
        const converted = convertLineChartToAdaptiveGroupedRanges(data);
        if (converted) {
            return converted.map(group => ({
                ...group,
                xAxisCalloutData: group.name,
            }));
        }
        return converted;
    }, [data]);

    const onRenderCalloutPerDataPoint = useCallback(
        (props?: any) => {
            if (!props) return null;

            let xAxisLabel = '';
            if (props.key) {
                const parts = props.key.split('-');
                if (parts.length >= 3) {
                    const startMs = parseInt(parts[parts.length - 2], 10);
                    const endMs = parseInt(parts[parts.length - 1], 10);

                    if (!isNaN(startMs) && !isNaN(endMs)) {
                        const startDate = getLocaleDateTimeHHMM(new Date(startMs));
                        const endDate = getLocaleDateTimeHHMM(new Date(endMs));

                        xAxisLabel = startMs === endMs ? `${startDate} UTC` : `${startDate} UTC - ${endDate} UTC`;
                    }
                }
            }

            return (
                <div className={styles.calloutContainer}>
                    {xAxisLabel && <div className={styles.calloutDate}>{xAxisLabel}</div>}
                    <div className={styles.calloutContent}>
                        <div className={styles.calloutColorBar} style={{ backgroundColor: props.color }} />
                        <div>
                            <div className={styles.calloutLabel}>{props.legend}</div>
                            <div className={styles.calloutValue}>{props.data}</div>
                        </div>
                    </div>
                </div>
            );
        },
        [styles]
    );

    const maxValue = useMemo(() => {
        if (!data?.lineChartData?.length) return 0;

        let max = 0;
        data.lineChartData.forEach(series => {
            series.data?.forEach(point => {
                if (point.y > max) {
                    max = point.y;
                }
            });
        });
        return Math.ceil(max);
    }, [data]);

    const yAxisTickCount = useMemo(() => {
        if (maxValue === 0) return 1;
        if (maxValue === 1) return 1;
        return Math.min(4, maxValue + 1);
    }, [maxValue]);

    return (
        <div className={styles.card}>
            <div>
                <Subtitle2 as="h3">{title}</Subtitle2>
            </div>

            <div className={styles.buttonBar}>
                <Tooltip content={intl.formatMessage(SreAgentResources.lineChart)} relationship="label">
                    <Button
                        icon={<DataArea20Regular />}
                        onClick={() => {
                            setChartType('line');
                        }}
                        className={chartType === 'line' ? styles.chartTypeButtonSelected : undefined}
                        aria-label={`${intl.formatMessage(SreAgentResources.lineChart)} - ${chartType === 'line' ? intl.formatMessage(SreAgentResources.selected) : intl.formatMessage(SreAgentResources.unselected)}`}
                        disabled={!hasData}
                    />
                </Tooltip>
                <Tooltip content={intl.formatMessage(SreAgentResources.barChart)} relationship="label">
                    <Button
                        icon={<DataBarVerticalAscending16Regular />}
                        onClick={() => {
                            setChartType('bar');
                        }}
                        className={chartType === 'bar' ? styles.chartTypeButtonSelected : undefined}
                        aria-label={`${intl.formatMessage(SreAgentResources.barChart)} - ${chartType === 'bar' ? intl.formatMessage(SreAgentResources.selected) : intl.formatMessage(SreAgentResources.unselected)}`}
                        disabled={!hasData}
                    />
                </Tooltip>
            </div>

            {isLoading ? (
                <Skeleton>
                    <SkeletonItem size={32} className={styles.skeletonItem} />
                </Skeleton>
            ) : (
                <div className={styles.chartContainer}>
                    {!hasData && (
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                height: '100%',
                                justifyContent: 'center',
                                alignItems: 'center',
                                gap: 8,
                            }}
                        >
                            <Text size={400} weight="semibold" block>
                                {intl.formatMessage(SreAgentResources.noIncidentMetricsToReport)}
                            </Text>
                            <Text block>{intl.formatMessage(SreAgentResources.trySelectingADifferentDateRange)}</Text>
                        </div>
                    )}

                    {chartType === 'line' && hasData && (
                        <LineChart
                            data={data}
                            yAxisTickFormat={(value: number) => Math.round(value).toString()}
                            yMinValue={0}
                            yMaxValue={maxValue > 0 ? maxValue : undefined}
                            yAxisTickCount={yAxisTickCount}
                            legendProps={{ styles: sentenceCaseLegendStyles }}
                            styles={{ root: { backgroundColor: 'transparent' } }}
                        />
                    )}

                    {chartType === 'bar' && hasData && groupedBarData && (
                        <GroupedVerticalBarChart
                            data={groupedBarData}
                            xAxisOuterPadding={2 / 3}
                            yAxisTickFormat={(value: number) => Math.round(value).toString()}
                            yMinValue={0}
                            yMaxValue={maxValue > 0 ? maxValue : undefined}
                            yAxisTickCount={yAxisTickCount}
                            onRenderCalloutPerDataPoint={onRenderCalloutPerDataPoint}
                            legendProps={{ styles: sentenceCaseLegendStyles }}
                            styles={{ root: { backgroundColor: 'transparent' } }}
                        />
                    )}
                </div>
            )}
        </div>
    );
};

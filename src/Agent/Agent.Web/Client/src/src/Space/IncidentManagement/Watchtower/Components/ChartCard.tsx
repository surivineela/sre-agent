import { GroupedVerticalBarChart, IChartProps, LineChart } from '@fluentui/react-charting';
import { Button, Card, makeStyles, Skeleton, SkeletonItem, Subtitle2, tokens, Tooltip } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { getLocaleDateTimeHHMM } from '../../../../Common/Helpers/Date';
import { convertLineChartToAdaptiveGroupedRanges } from '../../../../Common/Helpers/Graph';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';

const useChartCardStyles = makeStyles({
    card: {
        flex: '1 1 550px',
        minWidth: '550px',
        height: '310px',
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
    const isDarkMode = useIsDarkMode();
    const styles = useChartCardStyles();
    const [chartType, setChartType] = useState<'line' | 'bar'>('line');

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
        <Card className={styles.card} appearance={isDarkMode ? 'filled-alternative' : undefined}>
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
                    />
                </Tooltip>
            </div>

            {isLoading ? (
                <Skeleton>
                    <SkeletonItem size={32} className={styles.skeletonItem} />
                </Skeleton>
            ) : (
                <div className={styles.chartContainer}>
                    {chartType === 'line' && (
                        <LineChart
                            data={data}
                            yAxisTickFormat={(value: number) => Math.round(value).toString()}
                            yMinValue={0}
                            yMaxValue={maxValue > 0 ? maxValue : undefined}
                            yAxisTickCount={yAxisTickCount}
                        />
                    )}

                    {chartType === 'bar' && groupedBarData && (
                        <GroupedVerticalBarChart
                            data={groupedBarData}
                            xAxisOuterPadding={2 / 3}
                            yAxisTickFormat={(value: number) => Math.round(value).toString()}
                            yMinValue={0}
                            yMaxValue={maxValue > 0 ? maxValue : undefined}
                            yAxisTickCount={yAxisTickCount}
                            onRenderCalloutPerDataPoint={onRenderCalloutPerDataPoint}
                        />
                    )}
                </div>
            )}
        </Card>
    );
};

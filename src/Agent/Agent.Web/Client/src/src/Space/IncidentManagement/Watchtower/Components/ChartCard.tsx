import { IChartProps, IVerticalBarChartDataPoint, LineChart, VerticalBarChart } from '@fluentui/react-charting';
import { Button, Card, Skeleton, SkeletonItem, Subtitle2, tokens } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { CSSProperties, useMemo, useState } from 'react';

const chartTypeButtonSelectedStyle: CSSProperties = {
    backgroundColor: tokens.colorSubtleBackgroundPressed,
};

interface ChartCardProps {
    title: string;
    data: IChartProps;
    isLoading?: boolean;
}

export const ChartCard = ({ title, data, isLoading }: ChartCardProps) => {
    const [chartType, setChartType] = useState<'line' | 'bar'>('line');

    // TODO: Need to more carefully explore this, and likely use either the GroupedVerticalBarChart or VerticalStackedBarChart
    const barChartData = useMemo<IVerticalBarChartDataPoint[]>(() => {
        return (
            data?.lineChartData?.map(series => ({
                legend: series.legend,
                // NOTE: BarCharts don't have a configurable `legendShape`, so squares to match the bars it is
                color: series.color,
                x: series.data[0]?.x ?? Date.now(),
                y: series.data[0]?.y ?? 50,
            })) ?? []
        );
    }, [data]);

    return (
        <Card style={{ flex: '1 1 550px', minWidth: 550, height: 310 }}>
            <div>
                <Subtitle2>{title}</Subtitle2>
            </div>

            <div style={{ display: 'flex', justifyContent: 'end', alignItems: 'center', gap: 6 }}>
                <Button
                    icon={<DataArea20Regular />}
                    onClick={() => {
                        setChartType('line');
                    }}
                    style={chartType === 'line' ? chartTypeButtonSelectedStyle : {}}
                />
                <Button
                    icon={<DataBarVerticalAscending16Regular />}
                    onClick={() => {
                        setChartType('bar');
                    }}
                    style={chartType === 'bar' ? chartTypeButtonSelectedStyle : {}}
                    disabled={true}
                />
            </div>

            {isLoading ? (
                <Skeleton>
                    <SkeletonItem size={32} style={{ height: 210 }} />
                </Skeleton>
            ) : (
                <div style={{ height: 275, width: 'calc(100% - 16px)' }}>
                    {chartType === 'line' && <LineChart data={data} />}

                    {chartType === 'bar' && <VerticalBarChart data={barChartData} />}
                </div>
            )}
        </Card>
    );
};

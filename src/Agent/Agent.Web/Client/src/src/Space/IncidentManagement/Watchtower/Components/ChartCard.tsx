import { GroupedVerticalBarChart, IChartProps, LineChart } from '@fluentui/react-charting';
import { Button, Card, Skeleton, SkeletonItem, Subtitle2, tokens } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { CSSProperties, useMemo, useState } from 'react';
import { convertLineChartToAdaptiveGroupedRanges } from '../../../../Common/Helpers/Graph';

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

    const groupedBarData = useMemo(() => convertLineChartToAdaptiveGroupedRanges(data), [data]);

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
                />
            </div>

            {isLoading ? (
                <Skeleton>
                    <SkeletonItem size={32} style={{ height: 210 }} />
                </Skeleton>
            ) : (
                <div style={{ height: 275, width: 'calc(100% - 16px)' }}>
                    {chartType === 'line' && <LineChart data={data} />}

                    {chartType === 'bar' && groupedBarData && <GroupedVerticalBarChart data={groupedBarData} xAxisOuterPadding={2 / 3} />}
                </div>
            )}
        </Card>
    );
};

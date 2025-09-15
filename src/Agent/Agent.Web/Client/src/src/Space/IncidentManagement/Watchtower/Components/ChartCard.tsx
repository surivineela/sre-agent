import { IChartProps, IVerticalBarChartDataPoint, LineChart, VerticalBarChart } from '@fluentui/react-charting';
import { Button, Card, Subtitle2, tokens } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { CSSProperties, useMemo, useState } from 'react';

const chartTypeButtonSelectedStyle: CSSProperties = {
    backgroundColor: tokens.colorSubtleBackgroundPressed,
};

interface ChartCardProps {
    title: string;
    data: IChartProps;
}

export const ChartCard = ({ title, data }: ChartCardProps) => {
    const [chartType, setChartType] = useState<'line' | 'bar'>('line');

    const barChartData = useMemo<IVerticalBarChartDataPoint[]>(() => {
        return (
            data?.lineChartData?.map(item => ({
                legend: item.legend,
                color: item.color,
                x: item.data[0]?.x ?? Date.now(),
                y: item.data[0]?.y ?? 50,
            })) ?? []
        );
    }, [data]);

    return (
        <Card style={{ flexGrow: 1, minWidth: 650, height: 310 }}>
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

            <div style={{ height: 275, width: '100%' }}>
                {chartType === 'line' && <LineChart data={data} />}

                {chartType === 'bar' && <VerticalBarChart data={barChartData} />}
            </div>
        </Card>
    );
};

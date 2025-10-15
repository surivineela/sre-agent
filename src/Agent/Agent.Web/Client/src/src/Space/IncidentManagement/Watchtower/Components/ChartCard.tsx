import { GroupedVerticalBarChart, IChartProps, LineChart } from '@fluentui/react-charting';
import { Button, Card, Skeleton, SkeletonItem, Subtitle2, tokens, Tooltip } from '@fluentui/react-components';
import { DataArea20Regular, DataBarVerticalAscending16Regular } from '@fluentui/react-icons';
import { CSSProperties, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { convertLineChartToAdaptiveGroupedRanges } from '../../../../Common/Helpers/Graph';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';

const chartTypeButtonSelectedStyle: CSSProperties = {
    backgroundColor: tokens.colorSubtleBackgroundPressed,
};

interface ChartCardProps {
    title: string;
    data: IChartProps;
    isLoading?: boolean;
}

export const ChartCard = ({ title, data, isLoading }: ChartCardProps) => {
    const intl = useIntl();
    const isDarkMode = useIsDarkMode();
    const [chartType, setChartType] = useState<'line' | 'bar'>('line');

    const groupedBarData = useMemo(() => convertLineChartToAdaptiveGroupedRanges(data), [data]);

    return (
        <Card style={{ flex: '1 1 550px', minWidth: 550, height: 310 }} appearance={isDarkMode ? 'filled-alternative' : undefined}>
            <div>
                <Subtitle2>{title}</Subtitle2>
            </div>

            <div style={{ display: 'flex', justifyContent: 'end', alignItems: 'center', gap: 6 }}>
                <Tooltip content={intl.formatMessage(SreAgentResources.lineChart)} relationship="label">
                    <Button
                        icon={<DataArea20Regular />}
                        onClick={() => {
                            setChartType('line');
                        }}
                        style={chartType === 'line' ? chartTypeButtonSelectedStyle : {}}
                    />
                </Tooltip>
                <Tooltip content={intl.formatMessage(SreAgentResources.barChart)} relationship="label">
                    <Button
                        icon={<DataBarVerticalAscending16Regular />}
                        onClick={() => {
                            setChartType('bar');
                        }}
                        style={chartType === 'bar' ? chartTypeButtonSelectedStyle : {}}
                    />
                </Tooltip>
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

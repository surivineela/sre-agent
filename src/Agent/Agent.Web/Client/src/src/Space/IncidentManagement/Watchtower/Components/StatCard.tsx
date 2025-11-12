import { IChartProps, Sparkline } from '@fluentui/react-charting';
import { Badge, Body1Strong, Caption1, Card, Skeleton, SkeletonItem, Subtitle2, Title2, tokens } from '@fluentui/react-components';
import { ArrowDown16Regular, ArrowRight16Regular, ArrowUp16Regular } from '@fluentui/react-icons';
import { useIsDarkMode } from '../../../../Common/Hooks/useIsDarkMode';

export interface StatCardData {
    percentChange?: number;
    currentValue: number;
    maxValue?: number;
    /** NOTE: Sparklines WILL NOT RENDER ANYTHING with <6 data points */
    sparklineData: IChartProps;
}

interface StatCardProps {
    title: string;
    subtitle: string;
    data: StatCardData;
    isLoading?: boolean;
}

export const StatCard = ({ title, subtitle, data, isLoading }: StatCardProps) => {
    const isDarkMode = useIsDarkMode();

    return (
        <Card style={{ flexGrow: 1, minWidth: 225, maxWidth: 400, height: 120 }} appearance={isDarkMode ? 'filled-alternative' : undefined}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start' }}>
                <div>
                    <Body1Strong block>{title}</Body1Strong>
                    <Caption1 block style={{ color: tokens.colorNeutralForeground3 }}>
                        {subtitle}
                    </Caption1>
                </div>

                {!isLoading && data.percentChange !== undefined && (
                    <Badge appearance="tint">
                        {data.percentChange < 0 ? (
                            <ArrowDown16Regular />
                        ) : data.percentChange === 0 ? (
                            <ArrowRight16Regular />
                        ) : (
                            <ArrowUp16Regular />
                        )}{' '}
                        {data.percentChange}%
                    </Badge>
                )}
            </div>

            {isLoading ? (
                <Skeleton>
                    <SkeletonItem size={16} style={{ height: 50 }} />
                </Skeleton>
            ) : (
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', height: '150px' }}>
                    <div>
                        <Title2>{data.currentValue}</Title2>
                        {data.maxValue !== undefined && <Subtitle2>/{data.maxValue}</Subtitle2>}
                    </div>

                    <div>
                        <Sparkline data={data.sparklineData} />
                    </div>
                </div>
            )}
        </Card>
    );
};

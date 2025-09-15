import { DataVizPalette, getColorFromToken, Sparkline } from '@fluentui/react-charting';
import { Badge, Body1Strong, Caption1, Card, Subtitle2, Title2, tokens } from '@fluentui/react-components';
import { ArrowUp16Regular } from '@fluentui/react-icons';

const sparklineDummyData = {
    chartTitle: '10.21',
    lineChartData: [
        {
            legend: '19.64',
            color: getColorFromToken(DataVizPalette.color1),
            data: [
                {
                    x: 1,
                    y: 58.13,
                },
                {
                    x: 3,
                    y: 20,
                },
                {
                    x: 6,
                    y: 13.28,
                },
                {
                    x: 7,
                    y: 31.32,
                },
                {
                    x: 8,
                    y: 10.21,
                },
            ],
        },
    ],
};

interface StatCardProps {
    title: string;
    subtitle: string;
}

export const StatCard = ({ title, subtitle }: StatCardProps) => {
    return (
        <Card style={{ flexGrow: 1, minWidth: 315, height: 120 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start' }}>
                <div>
                    <Body1Strong block>{title}</Body1Strong>
                    <Caption1 block style={{ color: tokens.colorNeutralForeground3 }}>
                        {subtitle}
                    </Caption1>
                </div>

                <Badge appearance="tint">
                    <ArrowUp16Regular /> X%
                </Badge>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', height: '150px' }}>
                <div>
                    <Title2>80</Title2>
                    <Subtitle2>/100</Subtitle2>
                </div>

                <div>
                    <Sparkline data={sparklineDummyData} />
                </div>
            </div>
        </Card>
    );
};

import { Caption1, Subtitle1, tokens } from '@fluentui-copilot/react-copilot';
import { DataVizPalette, getColorFromToken, Sparkline } from '@fluentui/react-charts';
import { Badge, Caption1Strong, Card, CardFooter, makeStyles } from '@fluentui/react-components';
import { FC, memo, ReactNode, useMemo } from 'react';
import { MetricsCardHeader } from './MetricsCardHeader';

interface IMetricsCardProps {
    title: string;
    subtitle: string;
    percentageChange: number;
    score: string;
    refresh: () => Promise<unknown>;
    footer: {
        icon: ReactNode;
        text: string;
        result: string;
    };
    className?: string;
}

const useStyles = makeStyles({
    content: {
        borderBottom: `1px ${tokens.colorNeutralStroke2} solid`,
        padding: `${tokens.spacingVerticalS} 0`,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    footer: {
        padding: `${tokens.spacingVerticalXXS} 0`,
    },
});

const MetricsCard: FC<IMetricsCardProps> = ({ title, subtitle, percentageChange, score, footer, refresh }) => {
    const { sparkLineColor, percentageSign } = useMemo(() => {
        let sparkLineColor = DataVizPalette.color1;
        let percentageSign = '';

        if (percentageChange > 0) {
            sparkLineColor = DataVizPalette.color5;
            percentageSign = '+';
        } else if (percentageChange < 0) {
            sparkLineColor = DataVizPalette.color2;
        }

        return {
            sparkLineColor,
            percentageSign,
        };
    }, [percentageChange]);

    const styles = useStyles();

    return (
        <Card size={'small'}>
            <MetricsCardHeader
                title={title}
                subtitle={subtitle}
                refresh={refresh}>
                <Badge appearance={'tint'} color={percentageChange === 0 ? undefined : percentageChange > 0 ? 'success' : 'severe'}>
                    {`${percentageSign}${percentageChange}%`}
                </Badge>
            </MetricsCardHeader>
            <div className={styles.content}>
                <Subtitle1>{score}</Subtitle1>
                <Sparkline
                    showLegend={false}
                    data={{
                        lineChartData: [
                            {
                                color: getColorFromToken(sparkLineColor),
                                legend: score,
                                data: [
                                    {
                                        x: 1,
                                        y: 29.13,
                                    },
                                    {
                                        x: 2,
                                        y: 70.98,
                                    },
                                    {
                                        x: 3,
                                        y: 60,
                                    },
                                    {
                                        x: 4,
                                        y: 89.7,
                                    },
                                    {
                                        x: 5,
                                        y: 19,
                                    },
                                    {
                                        x: 6,
                                        y: 49.44,
                                    },
                                ],
                            },
                        ],
                    }}
                />
            </div>
            <CardFooter className={styles.footer}>
                {footer.icon}
                <Caption1>
                    {footer.text}
                    {':'} <Caption1Strong>{footer.result}</Caption1Strong>
                </Caption1>
            </CardFooter>
        </Card>
    );
};

export default memo(MetricsCard);

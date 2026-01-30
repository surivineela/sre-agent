import { Caption1, Subtitle1, tokens } from '@fluentui-copilot/react-copilot';
import { DataVizPalette, getColorFromToken, Sparkline } from '@fluentui/react-charts';
import { Badge, Caption1Strong, Card, CardFooter, makeStyles, Skeleton, SkeletonItem } from '@fluentui/react-components';
import { FC, memo, ReactNode, useMemo } from 'react';
import { MetricsCardHeader } from './MetricsCardHeader';

interface IMetricsCardProps {
    title: string;
    subtitle: string;
    percentageChange?: number;
    score: string;
    chartData?: Array<{ x: number; y: number }>;
    refresh: () => Promise<unknown>;
    footer?: {
        icon: ReactNode;
        text: string;
        result: string;
    };
    className?: string;
    isFetching?: boolean;
}

const useStyles = makeStyles({
    root: {
        height: '100%',
    },
    content: {
        padding: `${tokens.spacingVerticalS} 0`,
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        height: '100px',
    },
    footer: {
        padding: `${tokens.spacingVerticalXXS} 0`,
        borderTop: `1px ${tokens.colorNeutralStroke2} solid`,
    },
    skeletonContainer: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
        gap: tokens.spacingHorizontalS,
    },
    skeletonChart: {
        flexGrow: 1,
        maxWidth: '120px',
    },
});

const MetricsCard: FC<IMetricsCardProps> = ({ title, subtitle, percentageChange, score, chartData, footer, refresh, isFetching }) => {
    const { sparkLineColor, percentageSign } = useMemo(() => {
        let sparkLineColor = DataVizPalette.color1;
        let percentageSign = '';

        if (percentageChange !== undefined) {
            if (percentageChange > 0) {
                sparkLineColor = DataVizPalette.color5;
                percentageSign = '+';
            } else if (percentageChange < 0) {
                sparkLineColor = DataVizPalette.color2;
            }
        }

        return {
            sparkLineColor,
            percentageSign,
        };
    }, [percentageChange]);

    const styles = useStyles();

    return (
        <Card className={styles.root}>
            <MetricsCardHeader title={title} subtitle={subtitle} refresh={refresh}>
                {percentageChange !== undefined && (
                    <Badge appearance={'tint'} color={percentageChange === 0 ? undefined : percentageChange > 0 ? 'success' : 'severe'}>
                        {`${percentageSign}${percentageChange}%`}
                    </Badge>
                )}
            </MetricsCardHeader>
            <div className={styles.content}>
                {isFetching ? (
                    <Skeleton className={styles.skeletonContainer}>
                        <SkeletonItem size={24} style={{ width: '60px' }} />
                        <SkeletonItem size={48} className={styles.skeletonChart} />
                    </Skeleton>
                ) : (
                    <>
                        <Subtitle1>{score}</Subtitle1>
                        {chartData && (
                            <Sparkline
                                showLegend={false}
                                data={{
                                    lineChartData: [
                                        {
                                            color: getColorFromToken(sparkLineColor),
                                            legend: score,
                                            data: chartData,
                                        },
                                    ],
                                }}
                            />
                        )}
                    </>
                )}
            </div>
            {footer && (
                <CardFooter className={styles.footer}>
                    {footer.icon}
                    <Caption1>
                        {footer.text}
                        {':'} <Caption1Strong>{footer.result}</Caption1Strong>
                    </Caption1>
                </CardFooter>
            )}
        </Card>
    );
};

export default memo(MetricsCard);

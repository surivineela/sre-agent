import { Body1Strong, Caption1 } from '@fluentui-copilot/react-copilot';
import { Button, Card, CardFooter, CardHeader, makeStyles, Skeleton, SkeletonItem, tokens } from '@fluentui/react-components';
import { FC, memo, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SessionInsight, SessionInsightClient } from '../../Common/Clients/SessionInsightClient';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { OverviewResources } from '../../Strings/SREAgentResources';
import EmptyBody from './EmptyBody';
import MetricsCardHeader from './MetricsCardHeader';

const useStyles = makeStyles({
    root: {
        height: '100%',
    },
    loader: {
        height: '120px',
        marginBottom: tokens.spacingVerticalM,
    },
    insightCard: {
        margin: `${tokens.spacingVerticalM} 0px`,
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke1}`,
    },
});

const RecentInsightsCard: FC = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const sessionInsightClient = SessionInsightClient.getInstance(sreAgentEndpoint);

    const [sessionInsights, setSessionInsights] = useState<SessionInsight[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    const { scrollable } = useScrollableComponentStyles();
    const styles = useStyles();

    const intl = useIntl();

    useEffect(() => {
        let isSubscribed = true;

        const fetchInsights = async () => {
            setLoading(true);
            const response = await sessionInsightClient.getInsights();

            if (isSubscribed) {
                if (response.isSuccessful) {
                    setSessionInsights(response.content || []);
                    setError(null);
                } else {
                    setSessionInsights([]);
                    setError(intl.formatMessage(OverviewResources.failedToLoadInsights));
                }
                setLoading(false);
            }
        };

        fetchInsights();

        return () => {
            isSubscribed = false;
        };
    }, []);

    return (
        <Card size={'small'} className={styles.root}>
            <MetricsCardHeader title={intl.formatMessage(OverviewResources.recentInsights)} refresh={async () => { }} />
            <div className={scrollable} style={{ overflow: 'auto' }}>
                {loading ? (
                    <Skeleton>
                        {Array.from({ length: 5 }).map((_, index) => (
                            <SkeletonItem key={index} className={styles.loader} />
                        ))}
                    </Skeleton>
                ) : (
                    <>
                        {error ? (
                            <EmptyBody imageSrc={'WarningSpotIllustration.svg'} message={error} />
                        ) : (
                            <>
                                {sessionInsights.length === 0 ? (
                                    <EmptyBody message={intl.formatMessage(OverviewResources.noRecentInsights)} />
                                ) : (
                                    sessionInsights.map((insight, index) => {
                                        return (
                                            <Card size={'small'} key={index} className={styles.insightCard}>
                                                <CardHeader
                                                    header={
                                                        <>
                                                            <Body1Strong>{insight.title}</Body1Strong>
                                                        </>
                                                    }
                                                />

                                                {insight.generatedTimestamp && (
                                                    <Caption1>{getSafeDateTime(insight.generatedTimestamp).toLocaleString()}</Caption1>
                                                )}
                                                <CardFooter>
                                                    <Button size={'small'}>{intl.formatMessage(OverviewResources.viewLogs)}</Button>
                                                    <Button size={'small'}>{intl.formatMessage(OverviewResources.rootCause)}</Button>
                                                    <Button size={'small'}>{intl.formatMessage(OverviewResources.talkToAgent)}</Button>
                                                </CardFooter>
                                            </Card>
                                        );
                                    })
                                )}
                            </>
                        )}
                    </>
                )}
            </div>
        </Card>
    );
};

export default memo(RecentInsightsCard);

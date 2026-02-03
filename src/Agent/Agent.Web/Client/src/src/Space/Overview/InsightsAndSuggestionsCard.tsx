import { Body1, Body1Strong, Caption1 } from '@fluentui-copilot/react-copilot';
import {
    Badge,
    BadgeProps,
    Button,
    Card,
    CardFooter,
    CardHeader,
    Divider,
    Link,
    makeStyles,
    Skeleton,
    SkeletonItem,
    tokens,
} from '@fluentui/react-components';
import { FC, memo, useCallback, useContext, useEffect, useLayoutEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { InsightClient } from '../../Common/Clients/InsightClient';
import { Insight, InsightKind, InsightPriority } from '../../Common/Contracts/DataPlane/Insight';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable';
import { OverviewResources, SreAgentResources } from '../../Strings/SREAgentResources';
import EmptyBody from './EmptyBody';
import MetricsCardHeader from './MetricsCardHeader';

const MAX_LINES = 5;

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
    cardHeader: {
        marginBottom: tokens.spacingVerticalS,
    },
    cardTitle: {
        marginLeft: tokens.spacingHorizontalS,
    },
    subtitle: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    subtitleDivider: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        maxHeight: '5px',
        minHeight: '5px',
    },
    cardContent: {
        padding: `${tokens.spacingVerticalS} 0px`,
    },
    cardContentCollapsed: {
        display: '-webkit-box',
        WebkitLineClamp: MAX_LINES,
        WebkitBoxOrient: 'vertical',
        overflow: 'hidden',
    },
    showMoreButton: {
        marginTop: tokens.spacingVerticalXS,
        padding: 0,
        minWidth: 'auto',
    },
});

const InsightsAndSuggestionsCard: FC = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const insightClient = InsightClient.getInstance(sreAgentEndpoint);

    const [sessionInsights, setSessionInsights] = useState<Insight[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const [hasMore, setHasMore] = useState<boolean>(true);
    const [isLoadingMore, setIsLoadingMore] = useState<boolean>(false);

    const insightsRef = useRef<Insight[]>([]);
    insightsRef.current = sessionInsights;
    const hasMoreRef = useRef<boolean>(hasMore);
    hasMoreRef.current = hasMore;

    const { scrollable } = useScrollableComponentStyles();
    const styles = useStyles();

    const intl = useIntl();

    useEffect(() => {
        let isSubscribed = true;

        const fetchInsights = async () => {
            setLoading(true);
            const response = await insightClient.getInsights({ skip: 0, top: 20 });

            if (isSubscribed) {
                if (response.isSuccessful) {
                    const content = response.content;
                    setSessionInsights(content?.insights || []);
                    setHasMore(content?.hasMore || false);
                    setError(null);
                } else {
                    setSessionInsights([]);
                    setHasMore(false);
                    setError(intl.formatMessage(OverviewResources.failedToLoadInsightsAndSuggestions));
                }
                setLoading(false);
            }
        };

        fetchInsights();

        return () => {
            isSubscribed = false;
        };
    }, []);

    const fetchMoreInsights = useCallback(async () => {
        if (!hasMoreRef.current) {
            setIsLoadingMore(true);
            const response = await insightClient.getInsights({ skip: insightsRef.current.length, top: 20 });

            if (response.isSuccessful) {
                const content = response.content;
                setSessionInsights(prev => [...prev, ...(content?.insights || [])]);
                setHasMore(content?.hasMore || false);
                setError(null);
            }

            setIsLoadingMore(false);
        }
    }, []);

    return (
        <Card className={styles.root}>
            <MetricsCardHeader title={intl.formatMessage(OverviewResources.insightsAndSuggestions)} refresh={async () => {}} />
            <div className={scrollable}>
                {loading ? (
                    <Loader length={5} />
                ) : (
                    <>
                        {error ? (
                            <EmptyBody imageSrc={'WarningSpotIllustration.svg'} message={error} />
                        ) : (
                            <>
                                {sessionInsights.length === 0 ? (
                                    <EmptyBody message={intl.formatMessage(OverviewResources.noInsightsOrSuggestions)} />
                                ) : (
                                    <>
                                        {sessionInsights.map(insight => (
                                            <InsightCardItem key={insight.id} insight={insight} />
                                        ))}
                                        {hasMore && (
                                            <>
                                                {isLoadingMore ? (
                                                    <Loader length={1} />
                                                ) : (
                                                    <Button onClick={fetchMoreInsights}>
                                                        {intl.formatMessage(OverviewResources.viewMoreInsights)}
                                                    </Button>
                                                )}
                                            </>
                                        )}
                                    </>
                                )}
                            </>
                        )}
                    </>
                )}
            </div>
        </Card>
    );
};

const Loader = memo(function ({ length }: { length: number }) {
    const styles = useStyles();

    return (
        <Skeleton>
            {Array.from({ length }).map((_, index) => (
                <SkeletonItem key={index} className={styles.loader} />
            ))}
        </Skeleton>
    );
});

const InsightCardItem: FC<{ insight: Insight }> = memo(function ({ insight }) {
    const styles = useStyles();
    const intl = useIntl();
    const [isExpanded, setIsExpanded] = useState(false);
    const [hasOverflow, setHasOverflow] = useState(false);
    const contentRef = useRef<HTMLDivElement>(null);

    useLayoutEffect(() => {
        const element = contentRef.current;
        if (element) {
            // Check if the content overflows (scrollHeight > clientHeight means content is clamped)
            setHasOverflow(element.scrollHeight > element.clientHeight);
        }
    }, [insight.content.message]);

    const toggleExpand = useCallback(() => {
        setIsExpanded(prev => !prev);
    }, []);

    return (
        <Card className={styles.insightCard} size={'small'}>
            <CardHeader
                header={
                    <div className={styles.cardHeader}>
                        <PriorityBadge priority={insight.priority} />
                        <Body1Strong className={styles.cardTitle}>{insight.content.title}</Body1Strong>
                    </div>
                }
                description={<Subtitle kind={insight.kind} date={insight.updatedAt} />}
            />
            <div className={styles.cardContent}>
                <div ref={contentRef} className={!isExpanded ? styles.cardContentCollapsed : undefined}>
                    <Body1>{insight.content.message}</Body1>
                </div>
                {(hasOverflow || isExpanded) && (
                    <Link as="button" className={styles.showMoreButton} onClick={toggleExpand}>
                        <Body1>
                            {isExpanded ? intl.formatMessage(SreAgentResources.showLess) : intl.formatMessage(SreAgentResources.showMore)}
                        </Body1>
                    </Link>
                )}
            </div>

            <CardFooter>
                {[
                    intl.formatMessage(OverviewResources.viewLogs),
                    intl.formatMessage(OverviewResources.rootCause),
                    intl.formatMessage(OverviewResources.talkToAgent),
                ].map((actionText, index) => (
                    <Button key={index} size={'small'}>
                        {actionText}
                    </Button>
                ))}
            </CardFooter>
        </Card>
    );
});

const PriorityBadge = memo(function ({ priority }: { priority: InsightPriority }) {
    const intl = useIntl();

    let showBadge = true;
    let priorityText = '';
    let color: BadgeProps['color'] = 'important';
    let appearance: BadgeProps['appearance'] = 'outline';

    switch (priority) {
        case InsightPriority.High:
            priorityText = intl.formatMessage(OverviewResources.highPriority);
            color = 'danger';
            appearance = 'filled';
            break;
        case InsightPriority.Medium:
            priorityText = intl.formatMessage(OverviewResources.mediumPriority);
            color = 'severe';
            appearance = 'outline';
            break;
        case InsightPriority.Low:
            priorityText = intl.formatMessage(OverviewResources.lowPriority);
            color = 'important';
            appearance = 'outline';
            break;
        default:
            showBadge = false;
    }

    return (
        showBadge && (
            <Badge color={color} appearance={appearance} size={'large'}>
                {priorityText}
            </Badge>
        )
    );
});

const Subtitle = memo(function ({ kind, date }: { kind: InsightKind; date: Date | string }) {
    const styles = useStyles();
    const intl = useIntl();

    let kindString = intl.formatMessage(OverviewResources.incidentInsight);

    switch (kind) {
        case InsightKind.Incident:
            kindString = intl.formatMessage(OverviewResources.incidentInsight);
            break;
        case InsightKind.Configuration:
            kindString = intl.formatMessage(OverviewResources.configurationInsight);
            break;
        case InsightKind.Repository:
            kindString = intl.formatMessage(OverviewResources.repositoryInsight);
            break;
        case InsightKind.UsagePattern:
            kindString = intl.formatMessage(OverviewResources.usagePatternInsight);
            break;
    }

    return (
        <Caption1 className={styles.subtitle}>
            <span>{kindString}</span>
            <Divider vertical className={styles.subtitleDivider} appearance={'strong'} />
            <span>{getSafeDateTime(date).toLocaleString()}</span>
        </Caption1>
    );
});

export default memo(InsightsAndSuggestionsCard);

import { Button, makeStyles, mergeClasses, shorthands, Spinner, Text, tokens } from '@fluentui/react-components';
import { ArrowSyncRegular } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SessionInsight, SessionInsightClient } from '../../Common/Clients/SessionInsightClient';
import { SreAgentResources, SreAgentTabResources } from '../../Strings/SREAgentResources';
import { useCommonStyles } from '../Styles/Common.styles';
import InsightsDetailPanel from './InsightsDetailPanel';
import InsightsList from './InsightsList';

const useStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        minHeight: 0,
        backgroundColor: tokens.colorNeutralBackground1,
        padding: `${tokens.spacingVerticalXL} ${tokens.spacingHorizontalXXL}`,
        ...shorthands.overflow('hidden'),
        // Remove any margins that might affect positioning
        ...shorthands.margin(0),
    },
    header: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        ...shorthands.padding('0px', '0px', '16px', '0px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke2),
        flexShrink: 0,
    },
    title: {
        fontSize: tokens.fontSizeBase500,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    content: {
        display: 'flex',
        flexDirection: 'row',
        flex: 1,
        minHeight: 0,
        ...shorthands.overflow('hidden'),
        ...shorthands.gap('16px'),
        ...shorthands.padding('16px', '0px', '0px', '0px'),
    },
    threadsPanel: {
        width: '280px',
        flexShrink: 0,
        ...shorthands.borderRight('1px', 'solid', tokens.colorNeutralStroke2),
        backgroundColor: tokens.colorNeutralBackground2,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        ...shorthands.overflow('hidden'),
        ...shorthands.borderRadius(tokens.borderRadiusMedium),
    },
    insightsPanel: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        minHeight: 0,
        ...shorthands.overflow('hidden'),
        backgroundColor: tokens.colorNeutralBackground1,
    },
    loadingContainer: {
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        height: '100%',
        ...shorthands.gap('12px'),
    },
});

const SessionInsights: FC = () => {
    const styles = useStyles();
    const commonStyles = useCommonStyles();

    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const sessionInsightClient = SessionInsightClient.getInstance(sreAgentEndpoint);

    const [insights, setInsights] = useState<SessionInsight[]>([]);
    const [selectedInsightId, setSelectedInsightId] = useState<string | null>(null);
    const [loading, setLoading] = useState(true);
    const [refreshing, setRefreshing] = useState(false);

    const loadInsights = useCallback(
        async (showSpinner = true) => {
            try {
                if (showSpinner) {
                    setLoading(true);
                } else {
                    setRefreshing(true);
                }

                // Fetch insights from Cosmos DB - now returning full SessionInsight objects
                const response = await sessionInsightClient.getInsights();

                if (!response.isSuccessful) {
                    setInsights([]);
                    return;
                }

                const mappedInsights = response.content || [];

                // Sort by generated timestamp
                mappedInsights.sort((a, b) => {
                    const dateA = new Date(a.generatedTimestamp).getTime();
                    const dateB = new Date(b.generatedTimestamp).getTime();
                    return dateB - dateA;
                });

                setInsights(mappedInsights);

                // Auto-select first insight if none selected
                if (!selectedInsightId && mappedInsights.length > 0) {
                    setSelectedInsightId(mappedInsights[0].threadId);
                }
            } catch (error) {
                console.error('Failed to load session insights:', error);
                setInsights([]);
            } finally {
                setLoading(false);
                setRefreshing(false);
            }
        },
        [resourceId, selectedInsightId]
    );

    useEffect(() => {
        loadInsights();
    }, []);

    const handleRefresh = useCallback(() => {
        loadInsights(false);
    }, [loadInsights]);

    const handleInsightSelect = useCallback((threadId: string) => {
        setSelectedInsightId(threadId);
    }, []);

    const selectedInsight = useMemo(() => insights.find(i => i.threadId === selectedInsightId), [insights, selectedInsightId]);

    return (
        <div className={mergeClasses(commonStyles.contentRootBorderAndBackground, styles.container)}>
            {loading ? (
                <div className={styles.loadingContainer}>
                    <Spinner label="Loading session insights..." />
                </div>
            ) : (
                <>
                    <div className={styles.header}>
                        <Text className={styles.title}>
                            <FormattedMessage {...SreAgentTabResources.sessionInsights} />
                        </Text>
                        <Button appearance="subtle" icon={<ArrowSyncRegular />} onClick={handleRefresh} disabled={refreshing}>
                            {refreshing ? 'Refreshing...' : 'Refresh'}
                        </Button>
                    </div>
                    <div className={styles.content}>
                        <div className={styles.threadsPanel}>
                            <InsightsList insights={insights} selectedInsightId={selectedInsightId} onInsightSelect={handleInsightSelect} />
                        </div>
                        <div className={styles.insightsPanel}>
                            {selectedInsight ? (
                                <InsightsDetailPanel insight={selectedInsight} onInsightsGenerated={() => loadInsights(false)} />
                            ) : (
                                <div className={styles.emptyState}>
                                    <Text size={400} weight="semibold">
                                        <FormattedMessage {...SreAgentResources.noInsightSelected} />
                                    </Text>
                                    <Text size={300}>
                                        {insights.length === 0
                                            ? 'No session insights found. Generate insights for a thread by clicking the chart icon (📊) in the chat footer.'
                                            : 'Select an insight from the list to view details'}
                                    </Text>
                                </div>
                            )}
                        </div>
                    </div>
                </>
            )}
        </div>
    );
};

export default SessionInsights;

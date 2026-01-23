import { tokens } from '@fluentui-copilot/react-copilot';
import { makeStyles } from '@fluentui/react-components';
import { FC, memo } from 'react';
import AnalyzedIncidentsCard from './AnalyzedIncidentsCard.tsx';
import IncidentManagementCard from './IncidentManagementCard.tsx';
import IntentMetScoreCard from './IntentMetScoreCard.tsx';
import MitigationMeanTimeCard from './MitigationMeanTimeCard.tsx';
import RecentInsightsCard from './RecentInsightsCard.tsx';
import SuggestedActions from './SuggestedActions.tsx';

const useStyles = makeStyles({
    overview: {
        marginTop: `${tokens.spacingVerticalXL}`,
        padding: '50px',
        height: 'auto',
        alignSelf: 'stretch',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadius3XL,
    },
    overviewInner: {
        display: 'grid',
        gridTemplateColumns: 'repeat(4, 1fr)',
        gridTemplateRows: 'auto 150px 150px 150px 250px',
        gap: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    suggestionActions: {
        gridColumn: '1 / -1',
        gridRow: '1',
    },
    intentMetScoreCard: {
        gridColumn: '1 / span 2',
        gridRow: '2',
    },
    mitigationMeanTimeCard: {
        gridColumn: '1',
        gridRow: '3',
    },
    analyzedIncidentsCard: {
        gridColumn: '2',
        gridRow: '3',
    },
    incidentManagementCard: {
        gridColumn: '1 / span 2',
        gridRow: '4 / span 2',
        minHeight: 0,
        overflow: 'auto',
    },
    recentInsightsCard: {
        gridColumn: '3 / span 2',
        gridRow: '2 / -1',
        minHeight: 0,
    },
});

const Overview: FC = () => {
    const styles = useStyles();

    return (
        <div className={styles.overview}>
            <div className={styles.overviewInner}>
                <div className={styles.suggestionActions}>
                    <SuggestedActions />
                </div>
                <div className={styles.intentMetScoreCard}>
                    <IntentMetScoreCard />
                </div>
                <div className={styles.mitigationMeanTimeCard}>
                    <MitigationMeanTimeCard />
                </div>
                <div className={styles.analyzedIncidentsCard}>
                    <AnalyzedIncidentsCard />
                </div>
                <div className={styles.incidentManagementCard}>
                    <IncidentManagementCard />
                </div>
                <div className={styles.recentInsightsCard}>
                    <RecentInsightsCard />
                </div>
            </div>
        </div>
    );
};

export default memo(Overview);

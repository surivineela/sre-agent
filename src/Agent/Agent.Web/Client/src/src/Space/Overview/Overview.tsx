import { tokens } from '@fluentui-copilot/react-copilot';
import { makeStyles, mergeClasses } from '@fluentui/react-components';
import { FC, memo } from 'react';
import { useScrollableComponentStyles } from '../../Common/Styles/Scrollable.ts';
import IncidentManagementCard from './IncidentManagementCard.tsx';
import InsightsAndSuggestionsCard from './InsightsAndSuggestionsCard.tsx';
import IntentMetScoreCard from './IntentMetScoreCard.tsx';
import MitigationMeanTimeCard from './MitigationMeanTimeCard.tsx';
import ReviewedIncidentsCard from './ReviewedIncidentsCard.tsx';
import SuggestedActionsCard from './SuggestedActionsCard';
import TimeSavingCard from './TimeSavingCard.tsx';

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
        gridTemplateRows: 'auto 450px 150px 150px',
        gap: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
        minWidth: '800px',
        '@media (max-width: 1000px)': {
            gridTemplateColumns: 'repeat(4, 1fr)',
            gridTemplateRows: 'auto 450px 450px 150px',
        },
    },
    suggestionActions: {
        gridColumn: '1 / -1',
    },
    incidentManagementCard: {
        gridColumn: '1 / span 2',
        gridRow: '2',
        overflow: 'hidden',
        '@media (max-width: 1000px)': {
            gridColumn: '1 / -1',
            gridRow: '2',
        },
    },
    insightsAndSuggestionsCard: {
        gridColumn: '3 / span 2',
        gridRow: '2 / span 3',
        overflow: 'hidden',
        '@media (max-width: 1000px)': {
            gridColumn: '1 / -1',
            gridRow: '3',
        },
    },
    intentMetScoreCard: {
        gridColumn: '1',
        gridRow: '3',
        '@media (max-width: 1000px)': {
            gridColumn: '1',
            gridRow: '4',
        },
    },
    reviewedIncidentsCard: {
        gridColumn: '2',
        gridRow: '3',
        '@media (max-width: 1000px)': {
            gridColumn: '2',
            gridRow: '4',
        },
    },
    mitigationMeanTimeCard: {
        gridColumn: '1',
        gridRow: '4',
        '@media (max-width: 1000px)': {
            gridColumn: '3',
            gridRow: '4',
        },
    },
    timeSavingCard: {
        gridColumn: '2',
        gridRow: '4',
        '@media (max-width: 1000px)': {
            gridColumn: '4',
            gridRow: '4',
        },
    },
});

const Overview: FC = () => {
    const styles = useStyles();

    const { scrollable } = useScrollableComponentStyles(true);

    return (
        <div className={mergeClasses(styles.overview, scrollable)}>
            <div className={styles.overviewInner}>
                <div className={styles.suggestionActions}>
                    <SuggestedActionsCard />
                </div>
                <div className={styles.incidentManagementCard}>
                    <IncidentManagementCard />
                </div>
                <div className={styles.insightsAndSuggestionsCard}>
                    <InsightsAndSuggestionsCard />
                </div>
                <div className={styles.intentMetScoreCard}>
                    <IntentMetScoreCard />
                </div>
                <div className={styles.reviewedIncidentsCard}>
                    <ReviewedIncidentsCard />
                </div>
                <div className={styles.mitigationMeanTimeCard}>
                    <MitigationMeanTimeCard />
                </div>
                <div className={styles.timeSavingCard}>
                    <TimeSavingCard />
                </div>
            </div>
        </div>
    );
};

export default memo(Overview);

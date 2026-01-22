import { tokens } from '@fluentui-copilot/react-copilot';
import { makeStyles } from '@fluentui/react-components';
import { FC, memo } from 'react';
import AnalyzedIncidentsCard from './AnalyzedIncidentsCard.tsx';
import IntentMetScoreCard from './IntentMetScoreCard.tsx';
import MitigationMeanTimeCard from './MitigationMeanTimeCard.tsx';
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
        gridTemplateRows: 'auto',
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
            </div>
        </div>
    );
};

export default memo(Overview);

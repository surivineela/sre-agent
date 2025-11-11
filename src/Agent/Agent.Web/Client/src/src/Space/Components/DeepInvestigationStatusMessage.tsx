import { Body1, createMotionComponent, makeStyles, tokens } from '@fluentui/react-components';
import { SearchSparkle24Regular } from '@fluentui/react-icons';
import { memo } from 'react';
import { FormattedMessage } from 'react-intl';
import { AgentTaskResources } from '../../Strings/SREAgentResources';

const useStyles = makeStyles({
    root: {
        margin: '10px 0px 5px 30px',
    },
    item: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
});

const DropIn = createMotionComponent({
    keyframes: [
        { transform: 'translateY(-50%)', opacity: 0 },
        { transform: 'translateY(0%)', opacity: 1 },
    ],
    duration: 500,
});

const DeepInvestigationStatusMessage = ({ isDeepInvestigationTurnedOn }: { isDeepInvestigationTurnedOn: boolean }) => {
    const styles = useStyles();

    return (
        <div className={styles.root}>
            <DropIn>
                <div className={styles.item}>
                    <SearchSparkle24Regular />
                    <Body1>
                        {isDeepInvestigationTurnedOn ? (
                            <FormattedMessage {...AgentTaskResources.deepInvestigationTurnedOnMessage} />
                        ) : (
                            <FormattedMessage {...AgentTaskResources.deepInvestigationTurnedOffMessage} />
                        )}
                    </Body1>
                </div>
            </DropIn>
        </div>
    );
};

export default memo(DeepInvestigationStatusMessage);

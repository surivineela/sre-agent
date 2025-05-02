import { Button, Text } from '@fluentui/react-components';
import { CheckmarkCircle16Regular, DismissCircle24Filled, Warning24Filled } from '@fluentui/react-icons';
import { Dispatch, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { ActionSeverityMetrics, ActionStatusMetrics } from '../Hooks/useMetrics';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import { ThreadActionFilter } from './ThreadsMenu';
import TimeDropdown, { SelectedTimes } from './TimeDropdown';

interface ActivitiesStatusBarProps {
    selectedTime: SelectedTimes;
    setSelectedTime: Dispatch<React.SetStateAction<SelectedTimes>>;
    setThreadActionFilter: Dispatch<React.SetStateAction<ThreadActionFilter>>;
    onCriticalClick: (clicked: boolean) => void;
    onWarningClick: (clicked: boolean) => void;
    actionSeverityMetrics?: ActionSeverityMetrics;
    actionStatusMetrics?: ActionStatusMetrics;
}

const ActivitiesStatusBar = (props: ActivitiesStatusBarProps) => {
    const { selectedTime, setSelectedTime, actionSeverityMetrics, actionStatusMetrics, onCriticalClick, onWarningClick } = props;
    const styles = useActionsStatusBarStyles();
    const [isCriticalClicked, setIsCriticalClicked] = useState<boolean>(false);
    const [isWarningClicked, setIsWarningClicked] = useState<boolean>(false);
    const intl = useIntl();

    const handleCriticalClick = useCallback(() => {
        setIsCriticalClicked(prev => {
            const next = !prev;
            setIsWarningClicked(false);
            onCriticalClick(next);
            return next;
        });
    }, [setIsWarningClicked, onCriticalClick]);

    const handleWarningClick = useCallback(() => {
        setIsWarningClicked(prev => {
            const next = !prev;
            setIsCriticalClicked(false);
            onWarningClick(next);
            return next;
        });
    }, [setIsCriticalClicked, onWarningClick]);

    return (
        <>
            <div className={styles.containerNoBorder}>
                <TimeDropdown selectedTime={selectedTime} setSelectedTime={setSelectedTime} />
                <Button
                    onClick={handleCriticalClick}
                    appearance={isCriticalClicked ? 'secondary' : 'transparent'}
                    className={isCriticalClicked ? styles.buttonClicked : styles.buttonUnclicked}
                >
                    <div className={styles.statusGroup}>
                        <DismissCircle24Filled className={styles.error} />
                        <Text>{actionSeverityMetrics?.criticalActionsCount ?? 0}</Text>
                    </div>
                </Button>
                <Button
                    onClick={handleWarningClick}
                    appearance={isWarningClicked ? 'secondary' : 'transparent'}
                    className={isWarningClicked ? styles.buttonClicked : styles.buttonUnclicked}
                >
                    <div className={styles.statusGroup}>
                        <Warning24Filled className={styles.warning} />
                        <Text>{actionSeverityMetrics?.warningActionsCount ?? 0}</Text>
                    </div>
                </Button>
            </div>
            <div className={styles.completedActionGroup}>
                <CheckmarkCircle16Regular className={styles.completedActionText} />
                <Text className={styles.completedActionText}>
                    {intl.formatMessage(SreAgentResources.actionsCompleted, {
                        numOfActions: actionStatusMetrics?.completedActionsCount ?? 0,
                    })}
                </Text>
            </div>
        </>
    );
};

export default ActivitiesStatusBar;

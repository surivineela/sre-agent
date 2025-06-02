import { Text } from '@fluentui/react-components';
import { CheckmarkCircle16Regular } from '@fluentui/react-icons';
import { useIntl } from 'react-intl';
import { ThreadSeverity } from '../../Common/Clients/ThreadClient';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { ActionSeverityMetrics, ActionStatusMetrics } from '../Hooks/useMetrics';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import TimeDropdown, { SelectedTimes } from './TimeDropdown';

interface ActivitiesStatusBarProps {
    selectedTime: SelectedTimes;
    setSelectedTime: (selectedTime: SelectedTimes) => void;
    setThreadSeverity: (severity: ThreadSeverity | undefined) => void;
    onCriticalClick: () => void;
    onWarningClick: () => void;
    actionSeverityMetrics?: ActionSeverityMetrics;
    actionStatusMetrics?: ActionStatusMetrics;
    isCriticalClicked: boolean;
    isWarningClicked: boolean;
}

const ActivitiesStatusBar = (props: ActivitiesStatusBarProps) => {
    const { selectedTime, setSelectedTime, actionStatusMetrics } = props;
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();

    return (
        <>
            <div className={styles.containerNoBorder}>
                <TimeDropdown selectedTime={selectedTime} setSelectedTime={setSelectedTime} />
            </div>
            {(actionStatusMetrics?.completedActionsCount ?? 0) > 0 && (
                <div className={styles.completedActionGroup}>
                    <CheckmarkCircle16Regular className={styles.completedActionText} />
                    <Text className={styles.completedActionText}>
                        {intl.formatMessage(SreAgentResources.actionsCompleted, {
                            numOfActions: actionStatusMetrics?.completedActionsCount ?? 0,
                        })}
                    </Text>
                </div>
            )}
        </>
    );
};

export default ActivitiesStatusBar;

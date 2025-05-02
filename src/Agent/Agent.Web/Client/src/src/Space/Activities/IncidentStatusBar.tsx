import { Text } from '@fluentui/react-components';
import { Dispatch } from 'react';
import { useIntl } from 'react-intl';
import { MetricsResources } from '../../Strings/SREAgentResources';
import { IncidentMetrics } from '../Hooks/useMetrics';
import { useActionsStatusBarStyles, useIncidentStyles } from '../Styles/Incident.styles';
import TimeDropdown, { SelectedTimes } from './TimeDropdown';

interface IncidentStatusBarProps {
    selectedTime: SelectedTimes;
    setSelectedTime: Dispatch<React.SetStateAction<SelectedTimes>>;
    incidentMetrics?: IncidentMetrics;
}

const IncidentStatusBar = (props: IncidentStatusBarProps) => {
    const { selectedTime, setSelectedTime, incidentMetrics } = props;
    const styles = useActionsStatusBarStyles();
    const intl = useIntl();

    return (
        <div className={styles.container}>
            <TimeDropdown selectedTime={selectedTime} setSelectedTime={setSelectedTime} />
            <StatusItem active={true} count={incidentMetrics?.activeCount ?? 0} label={intl.formatMessage(MetricsResources.active)} />
            <StatusItem
                active={false}
                count={incidentMetrics?.mitigatedCount ?? 0}
                label={intl.formatMessage(MetricsResources.mitigated)}
            />
            <StatusItem active={false} count={incidentMetrics?.resolvedCount ?? 0} label={intl.formatMessage(MetricsResources.resolved)} />
        </div>
    );
};

type StatusItemProps = {
    count: number;
    label: string;
    active: boolean;
};

const StatusItem: React.FC<StatusItemProps> = ({ count, label, active }) => {
    const styles = useIncidentStyles();
    return (
        <div className={styles.statusItem}>
            <div className={active ? styles.verticalBar : styles.verticalBarGray} />
            <Text className={styles.count}>{count}</Text>
            <Text className={styles.label}>{label}</Text>
        </div>
    );
};

export default IncidentStatusBar;

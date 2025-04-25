import { Dropdown, Option, Text } from '@fluentui/react-components';
import { CheckmarkCircle24Filled, ErrorCircle24Filled, Warning24Filled } from '@fluentui/react-icons';
import { Dispatch, useMemo } from 'react';
import { IncidentStatus, Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useIncidentStatusBarStyles } from '../Styles/Incident.styles';

export enum SelectedTimes {
    OneDay = '24hrs',
    SevenDays = '7d',
    ThirtyDays = '30d',
}

interface IncidentStatusBarProps {
    threads: Thread[];
    selectedTime: string;
    setSelectedTime: Dispatch<React.SetStateAction<string>>;
}

const IncidentStatusBar = (props: IncidentStatusBarProps) => {
    const { threads, selectedTime, setSelectedTime } = props;
    const styles = useIncidentStatusBarStyles();

    const errorCount = useMemo(() => {
        return threads?.filter(item => item.incidentStatus === IncidentStatus.error)?.length ?? 0;
    }, [threads]);

    const warningCount = useMemo(() => {
        return threads?.filter(item => item.incidentStatus === IncidentStatus.warning)?.length ?? 0;
    }, [threads]);

    const successCount = useMemo(() => {
        return threads?.filter(item => item.incidentStatus === IncidentStatus.success)?.length ?? 0;
    }, [threads]);

    return (
        <div className={styles.container}>
            <Dropdown
                onOptionSelect={(_e, data) => setSelectedTime(data.optionValue ?? SelectedTimes.OneDay)}
                value={selectedTime}
                className={styles.dropdown}
            >
                <Option value={SelectedTimes.OneDay}>{SelectedTimes.OneDay}</Option>
                <Option value={SelectedTimes.SevenDays}>{SelectedTimes.SevenDays}</Option>
                <Option value={SelectedTimes.ThirtyDays}>{SelectedTimes.ThirtyDays}</Option>
            </Dropdown>

            <div className={styles.statusGroup}>
                <ErrorCircle24Filled className={styles.error} />
                <Text>{errorCount}</Text>
            </div>

            <div className={styles.statusGroup}>
                <Warning24Filled className={styles.warning} />
                <Text>{warningCount}</Text>
            </div>

            <div className={styles.statusGroup}>
                <CheckmarkCircle24Filled className={styles.success} />
                <Text>{successCount}</Text>
            </div>
        </div>
    );
};

export default IncidentStatusBar;

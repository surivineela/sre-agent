import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import TimeDropdown, { SelectedTimes } from './TimeDropdown';

interface ActivitiesStatusBarProps {
    selectedTime: SelectedTimes;
    setSelectedTime: (selectedTime: SelectedTimes) => void;
}

const ActivitiesStatusBar = (props: ActivitiesStatusBarProps) => {
    const { selectedTime, setSelectedTime } = props;
    const styles = useActionsStatusBarStyles();

    return (
        <div className={styles.containerNoBorder}>
            <TimeDropdown selectedTime={selectedTime} setSelectedTime={setSelectedTime} />
        </div>
    );
};

export default ActivitiesStatusBar;

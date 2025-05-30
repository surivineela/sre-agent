import { Dropdown, Option } from '@fluentui/react-components';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';

export enum SelectedTimes {
    OneDay = '24hrs',
    SevenDays = '7d',
    ThirtyDays = '30d',
}

interface TimeDropdownProps {
    selectedTime: SelectedTimes;
    setSelectedTime: (selectedTime: SelectedTimes) => void;
}

const TimeDropdown = (props: TimeDropdownProps) => {
    const { selectedTime, setSelectedTime } = props;
    const styles = useActionsStatusBarStyles();

    return (
        <Dropdown
            onOptionSelect={(_e, data) => setSelectedTime((data.optionValue as SelectedTimes) ?? SelectedTimes.OneDay)}
            value={selectedTime}
            className={styles.dropdown}
        >
            <Option value={SelectedTimes.OneDay}>{SelectedTimes.OneDay}</Option>
            <Option value={SelectedTimes.SevenDays}>{SelectedTimes.SevenDays}</Option>
            <Option value={SelectedTimes.ThirtyDays}>{SelectedTimes.ThirtyDays}</Option>
        </Dropdown>
    );
};

export default TimeDropdown;

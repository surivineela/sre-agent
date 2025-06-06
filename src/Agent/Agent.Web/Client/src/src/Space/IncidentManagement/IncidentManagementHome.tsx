import { FC } from 'react';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import IncidentHandlerTab from './IncidentHandlerTab';

const IncidentManagementHome: FC = () => {
    const styles = useIncidentManagementStyles();

    //NOTE(stpelleg): TODO - tabs for incident handlers etc.
    return (
        <div className={styles.root}>
            <div className={styles.container}>
                <IncidentHandlerTab />
            </div>
        </div>
    );
};

export default IncidentManagementHome;

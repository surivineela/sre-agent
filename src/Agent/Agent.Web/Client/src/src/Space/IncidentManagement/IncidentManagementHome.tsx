import { FC } from 'react';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';
import IncidentHandlerTab from './IncidentHandlerTab';

interface IncidentManagementHomeProps {
    openHandlerCreate?: () => void;
}

const IncidentManagementHome: FC<IncidentManagementHomeProps> = ({ openHandlerCreate }) => {
    const styles = useIncidentManagementStyles();

    //NOTE(stpelleg): TODO - tabs for incident handlers etc.
    return (
        <div className={styles.root}>
            <div className={styles.container}>
                <IncidentHandlerTab openHandlerCreate={openHandlerCreate} />
            </div>
        </div>
    );
};

export default IncidentManagementHome;

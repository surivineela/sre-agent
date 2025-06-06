import { FC, useState } from 'react';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import { IncidentManagementView } from './IncidentManagement.contracts';
import IncidentManagementHome from './IncidentManagementHome';

const IncidentManagement: FC = () => {
    const [currentView, setCurrentView] = useState<IncidentManagementView>(IncidentManagementView.Home);
    return (
        <div>
            {currentView === IncidentManagementView.Create ? (
                <CreateIncidentHandler exitToHome={() => setCurrentView(IncidentManagementView.Home)} />
            ) : (
                <IncidentManagementHome openHandlerCreate={() => setCurrentView(IncidentManagementView.Create)} />
            )}
        </div>
    );
};

export default IncidentManagement;

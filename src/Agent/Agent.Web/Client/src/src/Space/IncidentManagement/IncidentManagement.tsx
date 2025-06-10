import { initializeIcons } from '@fluentui/react';
import { FC, useEffect, useState } from 'react';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import { IncidentManagementView } from './IncidentManagement.contracts';
import IncidentManagementHome from './IncidentManagementHome';

const IncidentManagement: FC = () => {
    const [currentView, setCurrentView] = useState<IncidentManagementView>(IncidentManagementView.Home);
    const [iconsInitialized, setIconsInitialized] = useState(false);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div>
                {currentView === IncidentManagementView.Create ? (
                    <CreateIncidentHandler exitToHome={() => setCurrentView(IncidentManagementView.Home)} />
                ) : (
                    <IncidentManagementHome openHandlerCreate={() => setCurrentView(IncidentManagementView.Create)} />
                )}
            </div>
        )
    );
};

export default IncidentManagement;

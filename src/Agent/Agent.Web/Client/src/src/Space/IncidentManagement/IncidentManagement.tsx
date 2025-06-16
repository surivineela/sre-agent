import { initializeIcons } from '@fluentui/react';
import { FC, useEffect, useState } from 'react';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import { OperationStatus } from './CreateIncidentHandler/IncidentHandlerCreateContext';
import IncidentManagementHome from './IncidentManagementHome';

interface HandlerCreateOrEditInfo {
    filterId: string;
    handlerId?: string;
}

const IncidentManagement: FC = () => {
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [handlerOperationStatus, setHandlerOperationStatus] = useState<OperationStatus | undefined>(undefined);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div>
                {handlerCreateOrEditInfo ? (
                    <CreateIncidentHandler
                        exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                        setHandlerOperationStatus={setHandlerOperationStatus}
                        handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                    />
                ) : (
                    <IncidentManagementHome
                        handlerOperationStatus={handlerOperationStatus}
                        openHandlerCreate={setHandlerCreateOrEditInfo}
                    />
                )}
            </div>
        )
    );
};

export default IncidentManagement;

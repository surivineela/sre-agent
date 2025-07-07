import { initializeIcons } from '@fluentui/react';
import { FC, useEffect, useMemo, useState } from 'react';
import Url from '../../Common/Helpers/Url';
import { HandlerCreateOrEditInfo, OperationStatus } from './CreateIncidentHandler/Contracts';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import CreateIncidentHandlerConsolidated from './CreateIncidentHandler/CreateIncidentHandlerConsolidated';
import IncidentManagementHome from './IncidentManagementHome';

const IncidentManagement: FC = () => {
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [handlerCreateOrEditInfo, setHandlerCreateOrEditInfo] = useState<HandlerCreateOrEditInfo>();
    const [handlerOperationStatus, setHandlerOperationStatus] = useState<OperationStatus | undefined>(undefined);

    const useConsolidatedCreate = useMemo(() => Url.getFeatureValue('consolidatedcreate') === 'true', []);

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div>
                {handlerCreateOrEditInfo ? (
                    useConsolidatedCreate ? (
                        <CreateIncidentHandlerConsolidated
                            exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                            setHandlerOperationStatus={setHandlerOperationStatus}
                            handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                        />
                    ) : (
                        <CreateIncidentHandler
                            exitToHome={() => setHandlerCreateOrEditInfo(undefined)}
                            setHandlerOperationStatus={setHandlerOperationStatus}
                            handlerCreateOrEditInfo={handlerCreateOrEditInfo}
                        />
                    )
                ) : (
                    <IncidentManagementHome
                        handlerOperationStatus={handlerOperationStatus}
                        openHandlerCreate={setHandlerCreateOrEditInfo}
                        useConsolidatedCreate={useConsolidatedCreate}
                    />
                )}
            </div>
        )
    );
};

export default IncidentManagement;

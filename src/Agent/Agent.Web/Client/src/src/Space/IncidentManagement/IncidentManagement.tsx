import { initializeIcons } from '@fluentui/react';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../Common/Clients/ArmClient';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentHandler } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../Strings/SREAgentResources';
import CreateIncidentHandler from './CreateIncidentHandler/CreateIncidentHandler';
import IncidentManagementHome from './IncidentManagementHome';

const IncidentManagement: FC = () => {
    const [iconsInitialized, setIconsInitialized] = useState(false);
    const [incidentFilterIdForHandlerCreate, setIncidentFilterIdForHandlerCreate] = useState<string>();
    const [creatingHandler, setCreatingHandler] = useState<boolean>(false);

    const azPortalContext = useContext(AzPortalContext);
    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);
    const intl = useIntl();

    const createHandler = (handler: IncidentHandler) => {
        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(IncidentHandlerCreateResources.handlerAddNotificationTitle),
            intl.formatMessage(IncidentHandlerCreateResources.handlerAddNotificationDescription)
        );

        setIncidentFilterIdForHandlerCreate(undefined);
        setCreatingHandler(true);

        const additionalInfo = {
            handlerName: handler.name,
            incidentFilterId: handler.incidentFilterId,
        };

        azPortalContext.log({
            action: 'create-incidentHandler',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId: resourceId,
            data: additionalInfo,
        });

        incidentHandlerClient.createHandler(handler).then(createResult => {
            if (!createResult.isSuccessful) {
                azPortalContext.log({
                    action: 'create-incidentHandler',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    resourceId: resourceId,
                    data: { ...additionalInfo, error: createResult.error },
                });
                setCreatingHandler(false);
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(IncidentHandlerCreateResources.handlerAddNotificationError, {
                        errorMessage: getErrorMessage(createResult.error),
                    })
                );
            } else {
                azPortalContext.log({
                    action: 'create-incidentHandler',
                    actionModifier: 'success',
                    logLevel: 'info',
                    resourceId: resourceId,
                    data: additionalInfo,
                });
                setCreatingHandler(false);
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(IncidentHandlerCreateResources.handlerAddNotificationSuccess)
                );
            }
        });
    };

    useEffect(() => {
        initializeIcons();
        setIconsInitialized(true);
    }, []);

    return (
        iconsInitialized && (
            <div>
                {incidentFilterIdForHandlerCreate ? (
                    <CreateIncidentHandler
                        createHandler={createHandler}
                        exitToHome={() => setIncidentFilterIdForHandlerCreate(undefined)}
                        incidentFilterId={incidentFilterIdForHandlerCreate}
                    />
                ) : (
                    <IncidentManagementHome creatingHandler={creatingHandler} openHandlerCreate={setIncidentFilterIdForHandlerCreate} />
                )}
            </div>
        )
    );
};

export default IncidentManagement;

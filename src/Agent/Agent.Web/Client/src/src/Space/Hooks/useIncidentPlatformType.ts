import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getDataPlaneErrorMessage, Response } from '../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentPlatformTypeResponse } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';

export const useIncidentPlatformType = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [incidentPlatformType, setIncidentPlatformType] = useState<IncidentManagementType>();
    const [incidentPlatformTypeLoading, setIncidentPlatformTypeLoading] = useState<boolean>(false);
    const [incidentPlatformTypeLoaded, setIncidentPlatformTypeLoaded] = useState(false);
    const [incidentPlatformTypeLoadFailure, setIncidentPlatformTypeLoadFailure] = useState('');
    const [refreshCounter, setRefreshCounter] = useState<number>(0);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

    const getIncidentPlatformType = useCallback((): Promise<Response<IncidentPlatformTypeResponse>> => {
        return incidentHandlerClient.getIncidentPlatformType();
    }, [incidentHandlerClient]);

    useEffect(() => {
        let isSubscribed = true;

        azPortalContext.log({
            action: 'fetch-incidentPlatformType',
            actionModifier: 'start',
            logLevel: 'info',
        });
        setIncidentPlatformTypeLoading(true);
        setIncidentPlatformTypeLoaded(false);
        setIncidentPlatformTypeLoadFailure('');

        getIncidentPlatformType().then(incidentPlatformTypeResult => {
            if (isSubscribed) {
                setIncidentPlatformTypeLoading(false);
                if (incidentPlatformTypeResult?.isSuccessful && incidentPlatformTypeResult.content) {
                    azPortalContext.log({
                        action: 'fetch-incidentPlatformType',
                        actionModifier: 'success',
                        logLevel: 'info',
                    });
                    setIncidentPlatformType(incidentPlatformTypeResult.content.incidentPlatformType);
                    setIncidentPlatformTypeLoaded(true);
                } else {
                    const error = getDataPlaneErrorMessage(incidentPlatformTypeResult?.error);
                    azPortalContext.log({
                        action: 'fetch-incidentPlatformType',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        data: { error },
                    });
                    setIncidentPlatformTypeLoadFailure(error || 'Failed to incident platform type');
                }
            }
        });

        return () => {
            isSubscribed = false;
        };
    }, [getIncidentPlatformType, refreshCounter]);

    const refreshIncidentPlatformType = useCallback(() => {
        setRefreshCounter(prev => prev + 1);
    }, []);

    return {
        incidentPlatformType,
        incidentPlatformTypeLoading,
        incidentPlatformTypeLoaded,
        incidentPlatformTypeLoadFailure,
        refreshIncidentPlatformType,
    };
};

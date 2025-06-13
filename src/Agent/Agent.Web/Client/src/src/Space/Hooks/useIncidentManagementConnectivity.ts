import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';

export const useIncidentManagementConnectivity = (shouldPoll: boolean) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const [isIncidentManagementConnected, setIsIncidentManagementConnected] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const pollingTimerRef = useRef<NodeJS.Timeout | null>(null);

    const checkConnectivity = useCallback(async () => {
        const result = await IncidentHandlerClient.getInstance(sreAgentEndpoint).checkConnectivity();
        return result?.content ?? false;
    }, [sreAgentEndpoint]);

    const checkIncidentManagementConnectivity = useCallback(async () => {
        setIsLoading(true);
        const result = await checkConnectivity();
        setIsIncidentManagementConnected(result);
        setIsLoading(false);
        return result;

    }, [checkConnectivity]);

    const stopPolling = useCallback(() => {
        if (pollingTimerRef.current) {
            clearInterval(pollingTimerRef.current);
            pollingTimerRef.current = null;
        }
    }, []);

    const startPolling = useCallback(() => {
        if (pollingTimerRef.current || !shouldPoll) return;

        pollingTimerRef.current = setInterval(async () => {
            const result = await checkConnectivity();
            setIsIncidentManagementConnected(result);

            if (result) {
                stopPolling();
            }
        }, 2000);
    }, [checkConnectivity, shouldPoll, stopPolling]);


    useEffect(() => {
        const runInitialCheck = async () => {
            const result = await checkIncidentManagementConnectivity();
                if (!result && shouldPoll) {
                    startPolling();
                }
            };

        runInitialCheck();
        return stopPolling;
    }, [checkIncidentManagementConnectivity, startPolling, shouldPoll, stopPolling]);

    const refresh = useCallback(() => {
        checkIncidentManagementConnectivity();
    }, [checkIncidentManagementConnectivity]);

    return {
        refresh,
        isIncidentManagementConnected,
        isLoading,
    };
};

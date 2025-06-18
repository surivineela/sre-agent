import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { SreAgentContext } from '../Contracts/Context';

export const useIncidentManagementConnectivity = (shouldPoll: boolean) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const {
        incidentManagement: { isIncidentManagementConnected, setIsIncidentManagementConnected, hasFilters, setHasFilters },
    } = useContext(SreAgentContext);

    const [isLoading, setIsLoading] = useState(true);
    const pollingTimerRef = useRef<NodeJS.Timeout | null>(null);

    const checkConnectivity = useCallback(async () => {
        const result = await IncidentHandlerClient.getInstance(sreAgentEndpoint).checkConnectivity();
        return result?.content ?? false;
    }, [sreAgentEndpoint]);

    const checkHandlers = useCallback(async () => {
        const result = await IncidentHandlerClient.getInstance(sreAgentEndpoint).listIncidentFilters();
        return !!result?.content?.length;
    }, [sreAgentEndpoint]);

    const checkIncidentManagementConnectivity = useCallback(async () => {
        setIsLoading(true);
        const result = await checkConnectivity();
        if (result) {
            const hasIncidentFilters = await checkHandlers();
            setHasFilters(hasIncidentFilters);
        }
        setIsIncidentManagementConnected(result);
        setIsLoading(false);
        return result;
    }, [checkConnectivity, checkHandlers]);

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
            if (result) {
                const hasIncidentFilters = await checkHandlers();
                setHasFilters(hasIncidentFilters);
            }
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
        hasFilters,
        isLoading,
    };
};

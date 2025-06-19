import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { SreAgentContext } from '../Contracts/Context';

export const useIncidentManagementConnectivity = (shouldPoll: boolean) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const {
        incidentManagement: { isIncidentManagementConnected, setIsIncidentManagementConnected, hasFilters, setHasFilters },
    } = useContext(SreAgentContext);

    const [isLoading, setIsLoading] = useState(true);
    const pollingTimerRef = useRef<NodeJS.Timeout | null>(null);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

    const checkConnectivity = useCallback(async () => {
        const result = await incidentHandlerClient.checkConnectivity();
        return result?.content ?? false;
    }, [incidentHandlerClient]);

    const checkHandlers = useCallback(async () => {
        const result = await incidentHandlerClient.listIncidentFilters();
        return !!result?.content?.length;
    }, [incidentHandlerClient]);

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
    }, [checkConnectivity, checkHandlers, setHasFilters, setIsIncidentManagementConnected]);

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
    }, [checkConnectivity, checkHandlers, setHasFilters, setIsIncidentManagementConnected, shouldPoll, stopPolling]);

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

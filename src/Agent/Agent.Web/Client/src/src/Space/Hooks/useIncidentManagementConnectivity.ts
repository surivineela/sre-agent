import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementConnectionState } from '../Contracts/Context';

const POLLING_INTERVAL_LONG = 30000; // 30 seconds
const POLLING_INTERVAL_SHORT = 5000; // 5 seconds
const POLLING_TIMEOUT = 120000; // 2 minutes

const getPollingInterval = (isConnected: boolean) => (isConnected ? POLLING_INTERVAL_LONG : POLLING_INTERVAL_SHORT);

export const useIncidentManagementConnectivity = (
    shouldPoll: boolean,
    agentLastUpdatedTime: number | undefined,
    incidentPlatformType: IncidentManagementType | undefined
) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [isLoading, setIsLoading] = useState(true);
    const [isIncidentManagementConnected, setIsIncidentManagementConnected] = useState(false);
    const [incidentManagementConnectedCounter, setIncidentManagementConnectedCounter] = useState(0);
    const isIncidentManagementConnectedRef = useRef(isIncidentManagementConnected);
    const [hasFilters, setHasFilters] = useState(false);
    const pollingTimerRef = useRef<NodeJS.Timeout | null>(null);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

    const checkConnectivity = useCallback(async () => {
        const result = await incidentHandlerClient.checkConnectivity();
        return result?.content;
    }, [incidentHandlerClient]);

    const checkHandlers = useCallback(async () => {
        const result = await incidentHandlerClient.listIncidentFilters();
        return !!result?.content?.length;
    }, [incidentHandlerClient]);

    const checkIncidentManagementConnectivity = useCallback(async () => {
        setIsLoading(true);
        const result = await checkConnectivity();
        if (result !== undefined) {
            if (result) {
                const hasIncidentFilters = await checkHandlers();
                setHasFilters(hasIncidentFilters);
            }
            setIsIncidentManagementConnected(result);
            setIncidentManagementConnectedCounter(c => c + 1);
        }
        setIsLoading(false);
        return result;
    }, [checkConnectivity, checkHandlers, setHasFilters, setIsIncidentManagementConnected]);

    const stopPolling = useCallback(() => {
        if (pollingTimerRef.current) {
            clearTimeout(pollingTimerRef.current);
            pollingTimerRef.current = null;
        }
    }, []);

    const startPolling = useCallback(() => {
        if (pollingTimerRef.current || !shouldPoll) return;

        const poll = async () => {
            const result = await checkConnectivity();
            let interval = POLLING_INTERVAL_LONG;

            if (result !== undefined) {
                if (result) {
                    const hasIncidentFilters = await checkHandlers();
                    setHasFilters(hasIncidentFilters);
                }
                setIsIncidentManagementConnected(result);
                setIncidentManagementConnectedCounter(c => c + 1);
                interval = getPollingInterval(result);
            } else {
                interval = getPollingInterval(isIncidentManagementConnectedRef.current);
            }

            pollingTimerRef.current = setTimeout(poll, interval);
        };

        poll();
    }, [checkConnectivity, checkHandlers, setHasFilters, setIsIncidentManagementConnected, shouldPoll, stopPolling]);

    useEffect(() => {
        isIncidentManagementConnectedRef.current = isIncidentManagementConnected;
    }, [isIncidentManagementConnected]);

    useEffect(() => {
        const runInitialCheck = async () => {
            await checkIncidentManagementConnectivity();
            if (shouldPoll) {
                startPolling();
            }
        };

        runInitialCheck();
        return stopPolling;
    }, [checkIncidentManagementConnectivity, startPolling, shouldPoll, stopPolling]);

    const refresh = useCallback(() => {
        checkIncidentManagementConnectivity();
    }, [checkIncidentManagementConnectivity]);

    const incidentManagementConnectionState: IncidentManagementConnectionState = useMemo(() => {
        if (isIncidentManagementConnected) {
            return 'connected';
        }
        if (!!agentLastUpdatedTime && Date.now() - agentLastUpdatedTime < POLLING_TIMEOUT) {
            return 'waiting';
        }
        return 'notConnected';
    }, [incidentManagementConnectedCounter, isIncidentManagementConnected, agentLastUpdatedTime]);

    useEffect(() => {
        if (incidentManagementConnectionState === 'notConnected') {
            azPortalContext.log({
                action: 'poll-incidentManagement-connectivity-postSetup',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { incidentPlatformType },
            });
        }
    }, [incidentPlatformType, incidentManagementConnectionState, azPortalContext]);

    return {
        refresh,
        incidentManagementConnectionState,
        isIncidentManagementConnected,
        setIsIncidentManagementConnected,
        hasFilters,
        setHasFilters,
        isLoading,
    };
};

import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentHandler } from '../../Common/Contracts/Azure/IncidentHandler';

export const useIncidentHandlers = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [incidentHandlers, setIncidentHandlers] = useState<IncidentHandler[]>();
    const [isLoading, setIsLoading] = useState<boolean>(true);

    const getIncidentHandlers = useCallback(async (): Promise<IncidentHandler[]> => {
        const incidentResults = await IncidentHandlerClient.getInstance(
            sreAgentEndpoint,
            azPortalContext.log.bind(azPortalContext)
        ).listHandlers();
        return incidentResults?.content ?? [];
    }, [sreAgentEndpoint, azPortalContext]);

    const refresh = useCallback(async () => {
        setIsLoading(true);
        const results = await getIncidentHandlers();
        setIncidentHandlers(results);
        setIsLoading(false);
    }, [getIncidentHandlers]);

    const filterIdToHandlerMap = useMemo(() => {
        const map: Record<string, IncidentHandler> = {};
        incidentHandlers?.forEach(handler => {
            map[handler.incidentFilterId] = handler;
        });
        return map;
    }, [incidentHandlers]);

    useEffect(() => {
        let isSubscribed = true;

        const fetch = async () => {
            const initialResults = await getIncidentHandlers();
            if (!isSubscribed) return;
            setIncidentHandlers(initialResults);
            setIsLoading(false);
        };

        fetch();

        return () => {
            isSubscribed = false;
        };
    }, [getIncidentHandlers]);

    return {
        filterIdToHandlerMap,
        refresh,
        incidentHandlers,
        incidentHandlersLoading: isLoading,
    };
};

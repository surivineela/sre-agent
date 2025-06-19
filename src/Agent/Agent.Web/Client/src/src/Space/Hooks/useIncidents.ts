import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentFilterDocumentPayload } from '../../Common/Contracts/Azure/IncidentHandler';

export const useIncidents = (durationInDays: number, filter: IncidentFilterDocumentPayload) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [incidents, setIncidents] = useState<IncidentDocument[]>();
    const [isLoading, setIsLoading] = useState<boolean>(false);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

    const getIncidents = useCallback(
        async (durationInDays: number, filter: IncidentFilterDocumentPayload): Promise<IncidentDocument[]> => {
            const incidentResults = await incidentHandlerClient.queryIncidents({
                durationInDays,
                filter,
            });

            return incidentResults?.content ?? [];
        },
        [incidentHandlerClient]
    );

    useEffect(() => {
        let isSubscribed = true;

        const getInitialIncidents = async () => {
            if (durationInDays && filter) {
                setIsLoading(true);
                const incidentResults = await getIncidents(durationInDays, filter);
                if (isSubscribed) {
                    setIncidents(incidentResults);
                    setIsLoading(false);
                }
            }
        };

        getInitialIncidents();

        return () => {
            isSubscribed = false;
        };
    }, [getIncidents, durationInDays, filter]);

    return { incidents, incidentsLoading: isLoading };
};

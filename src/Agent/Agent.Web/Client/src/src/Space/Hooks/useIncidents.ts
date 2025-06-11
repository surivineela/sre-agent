import { useCallback, useContext, useEffect, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IIncidentDocument, IncidentFilterDocumentPayload } from '../../Common/Contracts/Azure/IncidentHandler';

export const useIncidents = (durationInDays: number, filter: IncidentFilterDocumentPayload) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [incidents, setIncidents] = useState<IIncidentDocument[]>();
    const [isLoading, setIsLoading] = useState<boolean>(false);

    const getIncidents = useCallback(
        async (durationInDays: number, filter: IncidentFilterDocumentPayload): Promise<IIncidentDocument[]> => {
            const incidentResults = await IncidentHandlerClient.getInstance(sreAgentEndpoint).queryIncidents({
                durationInDays,
                filter,
            });

            return incidentResults?.content ?? [];
        },
        [sreAgentEndpoint]
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

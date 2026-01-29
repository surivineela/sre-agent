import { useFormikContext } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { IncidentDocument, IncidentQueryRequest } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType, IncidentStatus } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { SreAgentContext } from '../../Contracts/Context';
import { getFilterValues } from '../Utilities';
import { TimeDuration } from './Contracts';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';

const pageSize = 10;

const defaultNumberOfIncidentsToLoad = 10;

/**
 * Return 1.5 times of the number of incidents that can fill the incidents list div to make sure the div is overflowed. Return 5 if the result is less than 5.
 * @param incidentsListContainerHeight
 * @param numberOfincidentsInDiv the existing number of incidents in the div
 * @returns
 */
const getNumberOfincidentsToOverflowincidentsListDiv = (
    incidentsListDivHeightInPx: number | undefined,
    numberOfincidentsInDiv: number
): number => {
    if (incidentsListDivHeightInPx === undefined) return defaultNumberOfIncidentsToLoad;

    const incidentItemHeightInPx = 32;

    const numberOfincidentsToLoad = Math.ceil(1.5 * (incidentsListDivHeightInPx / incidentItemHeightInPx)) - numberOfincidentsInDiv;

    return Math.max(numberOfincidentsToLoad, defaultNumberOfIncidentsToLoad);
};

export const usePreviewIncidents = () => {
    const {
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [azPortalContext, sreAgentEndpoint]
    );

    const { values } = useFormikContext<IncidentHandlerCreateFormValues>();

    const [selectedTimespan, setSelectedTimespan] = useState<TimeDuration>(
        incidentPlatformType === IncidentManagementType.AzMonitor ? TimeDuration.Last30Days : TimeDuration.Last90Days
    );
    const onSelectedTimespanChange = useCallback((value: TimeDuration) => setSelectedTimespan(value), []);

    const [incidents, setIncidents] = useState<IncidentDocument[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);

    const incidentsInitialized = useRef(false);

    const incidentsListDivRef = useRef<HTMLDivElement | null>(null);
    const [isLoadingInitialIncidents, setIsLoadingInitialIncidents] = useState<boolean>(true);
    const [hasMoreOldIncidents, setHasMoreOldIncidents] = useState<boolean>(true);
    const isLoadingOldIncidents = useRef<boolean>(false);
    const loadOldIncidentCallId = useRef<string>(Guid.newShortGuid());
    const incidentsPageNumber = useRef<number>(0);

    const loadMoreOldIncidents = useCallback(
        async (overflowDiv: boolean): Promise<boolean | undefined> => {
            if (!isLoadingInitialIncidents && !isLoadingOldIncidents.current) {
                const callId = loadOldIncidentCallId.current;
                isLoadingOldIncidents.current = true;

                const numberOfIncidentsToLoad = overflowDiv
                    ? getNumberOfincidentsToOverflowincidentsListDiv(incidentsListDivRef.current?.clientHeight, incidents?.length || 0)
                    : defaultNumberOfIncidentsToLoad;

                const filterValues = getFilterValues(values, incidentPlatformType, true, undefined, undefined);
                const oldIncidentsResponse = await incidentHandlerClient.queryIncidents({
                    filter: filterValues,
                    durationInDays: selectedTimespan,
                    pageSize: pageSize,
                    pageNumber: ++incidentsPageNumber.current,
                    statuses: Object.values(IncidentStatus),
                });

                if (callId === loadOldIncidentCallId.current) {
                    const olderIncidents = oldIncidentsResponse.content?.items ?? [];
                    if (oldIncidentsResponse.isSuccessful && olderIncidents.length < numberOfIncidentsToLoad) {
                        setHasMoreOldIncidents(false);
                    }
                    setIncidents(prevIncidents => {
                        if (!prevIncidents) {
                            return olderIncidents;
                        }

                        const olderIncidentsToAdd = olderIncidents.filter(
                            oldIncident => !prevIncidents.some(prevIncident => prevIncident.id === oldIncident.id)
                        );

                        const newIncidents = [...prevIncidents, ...olderIncidentsToAdd].sort((a, b) =>
                            b.createdAt.localeCompare(a.createdAt)
                        );
                        return newIncidents;
                    });

                    isLoadingOldIncidents.current = false;

                    return oldIncidentsResponse.isSuccessful;
                } else {
                    isLoadingOldIncidents.current = false;
                    return undefined;
                }
            }
        },
        [
            isLoadingInitialIncidents,
            incidentHandlerClient.queryIncidents,
            selectedTimespan,
            incidentPlatformType,
            values.impactedService,
            values.priorities,
            values.incidentType,
            values.titleContains,
            values.owningTeamId,
            values.createdBy,
            values.monitorId,
            incidents?.length,
        ]
    );

    useEffect(() => {
        let subscribed = true;

        loadOldIncidentCallId.current = Guid.newShortGuid();
        setHasMoreOldIncidents(true);
        setIsLoadingInitialIncidents(true);
        setLoadingIncidents(true);
        incidentsPageNumber.current = 0;

        const filterValues = getFilterValues(values, incidentPlatformType, true, undefined, undefined);
        const queryPayload: IncidentQueryRequest = {
            filter: filterValues,
            durationInDays: selectedTimespan,
            pageSize: pageSize,
            pageNumber: ++incidentsPageNumber.current,
            statuses: Object.values(IncidentStatus),
        };

        incidentHandlerClient.queryIncidents(queryPayload).then(filteredIncidentsResponse => {
            if (!subscribed) {
                return;
            }

            if (filteredIncidentsResponse.isSuccessful && filteredIncidentsResponse.content) {
                const filteredIncidents = filteredIncidentsResponse.content.items.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
                setIncidents(filteredIncidents);
            } else {
                setIncidents([]);
            }

            setIsLoadingInitialIncidents(false);
            setLoadingIncidents(false);
            incidentsInitialized.current = true;
        });

        return () => {
            subscribed = false;
        };
    }, [
        incidentHandlerClient,
        selectedTimespan,
        incidentPlatformType,
        values.impactedService,
        values.priorities,
        values.incidentType,
        values.titleContains,
        values.owningTeamId,
        values.createdBy,
        values.monitorId,
    ]);

    return {
        loadingIncidents,

        // For paginating incidents on scroll
        incidentsListDivRef,
        isLoadingInitialIncidents,
        hasMoreOldIncidents,
        loadMoreOldIncidents,

        selectedTimespan,
        onSelectedTimespanChange,
        incidents,
    };
};

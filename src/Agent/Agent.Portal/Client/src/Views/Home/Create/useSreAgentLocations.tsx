import { useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { LocationClient } from '../../../Common/Clients/LocationClient';
import { ResourceTypes } from '../../../Common/Constants/Arm';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { PortalResources } from '../../../Strings/Resources';

export const useSreAgentLocations = (subscriptionId: string, telemetrySource: TelemetrySource) => {
    const intl = useIntl();

    const [locationsList, setLocationsList] = useState<string[]>([]);
    const [locationsLoading, setLocationsLoading] = useState<boolean>(false);
    const [locationsLoadFailure, setLocationLoadFailure] = useState<string>('');
    const [containsNoLocations, setContainsNoLocations] = useState<boolean>(false);

    const locationClient = useMemo(() => LocationClient.getInstance(telemetrySource), [telemetrySource]);

    const fetchLocationData = useCallback(
        async (subscriptionId: string) => {
            setLocationsLoading(true);
            const supportedAgentLocations = await locationClient.getLocationsFromArmManifest(
                subscriptionId,
                ResourceTypes.AppProvider,
                ResourceTypes.SreAgent,
                telemetrySource
            );
            const agentLocationsLoadFailure =
                supportedAgentLocations.length > 0 ? '' : intl.formatMessage(PortalResources.sreAgentLocationsLoadFailure);
            setContainsNoLocations(supportedAgentLocations.length === 0);
            setLocationsList(supportedAgentLocations);
            setLocationLoadFailure(agentLocationsLoadFailure);
            setLocationsLoading(false);
        },
        [telemetrySource, intl, locationClient]
    );

    useEffect(() => {
        if (subscriptionId) {
            fetchLocationData(subscriptionId);
        }
    }, [fetchLocationData, subscriptionId]);

    return {
        locationsList,
        locationsLoading,
        locationsLoadFailure,
        containsNoLocations,
    };
};

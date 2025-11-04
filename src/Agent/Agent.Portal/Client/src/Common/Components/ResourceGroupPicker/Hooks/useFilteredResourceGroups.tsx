import { useEffect, useMemo, useState } from 'react';
import { ResourceGroupClient } from '../../../Clients/ResourceGroupClient';
import { TelemetrySource } from '../../../Constants/Telemetry';
import { useAuth } from '../../../Contexts/AuthContext';
import { LogLevel } from '../../../Contracts/Telemetry';
import { useTelemetry } from '../../../Hooks/useTelemetry';

interface UseFilteredResourceGroupsParams {
    subscriptionIds: string[];
    telemetrySource: TelemetrySource;
}

interface UseFilteredResourceGroupsResult {
    filteredResourceGroupSet: Set<string> | undefined;
    resourceGroupsLoading: boolean;
    resourceGroupsLoadFailure: string;
}

export const useFilteredResourceGroups = (params: UseFilteredResourceGroupsParams): UseFilteredResourceGroupsResult => {
    const { subscriptionIds, telemetrySource } = params;
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [filteredResourceGroupSet, setFilteredResourceGroupsSet] = useState<Set<string>>();
    const [resourceGroupsLoading, setResourceGroupsLoading] = useState<boolean>(false);
    const [resourceGroupsLoadFailure, setResourceGroupsLoadFailure] = useState<string>('');

    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        if (!isAuthenticated || !subscriptionIds || subscriptionIds.length === 0) {
            setFilteredResourceGroupsSet(undefined);
            setResourceGroupsLoading(false);
            setResourceGroupsLoadFailure('');
            return;
        }

        const fetchFilteredResourceGroups = async () => {
            setResourceGroupsLoading(true);
            setResourceGroupsLoadFailure('');

            const response = await resourceGroupClient.getResourceGroupsInSubscriptionWithSreAgentKinds(subscriptionIds);

            if (!response.isSuccessful) {
                const errorMessage = response.error instanceof Error ? response.error.message : String(response.error);
                setResourceGroupsLoadFailure('Failed to load filtered resource groups.');
                logEvent({
                    action: 'fetch-filtered-resource-groups',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: errorMessage,
                        subscriptionIds: subscriptionIds.join(','),
                    },
                });
                setFilteredResourceGroupsSet(undefined);
            } else {
                setFilteredResourceGroupsSet(response.content);
            }

            setResourceGroupsLoading(false);
        };

        fetchFilteredResourceGroups();
    }, [resourceGroupClient, isAuthenticated, subscriptionIds, logEvent]);

    return {
        filteredResourceGroupSet,
        resourceGroupsLoading,
        resourceGroupsLoadFailure,
    };
};

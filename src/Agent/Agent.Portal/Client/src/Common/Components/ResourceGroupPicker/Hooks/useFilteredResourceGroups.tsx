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

            try {
                const resourceGroupSet = await resourceGroupClient.getResourceGroupsInSubscriptionWithSreAgentKinds(subscriptionIds);
                setFilteredResourceGroupsSet(resourceGroupSet);
            } catch (error) {
                const errorMessage = error instanceof Error ? error.message : String(error);
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

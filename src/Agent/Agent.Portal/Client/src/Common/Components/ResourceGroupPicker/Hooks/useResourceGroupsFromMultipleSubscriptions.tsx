import { useEffect, useMemo, useState } from 'react';
import { ResourceGroupClient } from '../../../Clients/ResourceGroupClient';
import { TelemetrySource } from '../../../Constants/Telemetry';
import { useAuth } from '../../../Contexts/AuthContext';
import { ResourceGroup } from '../../../Contracts/Arm';
import { LogLevel } from '../../../Contracts/Telemetry';
import { useTelemetry } from '../../../Hooks/useTelemetry';

export interface ResourceGroupWithSelection extends ResourceGroup {
    selected: boolean;
    recommended: boolean;
}

interface UseResourceGroupsFromMultipleSubscriptionsParams {
    subscriptionIds: string[];
    telemetrySource: TelemetrySource;
}

interface UseResourceGroupsFromMultipleSubscriptionsResult {
    resourceGroupsList: ResourceGroupWithSelection[] | undefined;
    resourceGroupsLoading: boolean;
    resourceGroupsLoadFailure: string;
}

export const useResourceGroupsFromMultipleSubscriptions = (
    params: UseResourceGroupsFromMultipleSubscriptionsParams
): UseResourceGroupsFromMultipleSubscriptionsResult => {
    const { subscriptionIds, telemetrySource } = params;
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [resourceGroupsList, setResourceGroupsList] = useState<ResourceGroupWithSelection[]>();
    const [resourceGroupsLoading, setResourceGroupsLoading] = useState<boolean>(false);
    const [resourceGroupsLoadFailure, setResourceGroupsLoadFailure] = useState<string>('');

    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        if (!isAuthenticated || !subscriptionIds || subscriptionIds.length === 0) {
            setResourceGroupsList(undefined);
            setResourceGroupsLoading(false);
            setResourceGroupsLoadFailure('');
            return;
        }

        const fetchResourceGroups = async () => {
            setResourceGroupsLoading(true);
            setResourceGroupsLoadFailure('');

            const response = await resourceGroupClient.getAllResourceGroupsFromSubscriptions(subscriptionIds);

            if (response.isSuccessful && response.content) {
                const mappedRgs: ResourceGroupWithSelection[] = response.content.map(item => ({
                    ...item,
                    selected: false,
                    recommended: false,
                }));
                setResourceGroupsList(mappedRgs);
            } else {
                const errorMessage = response.error instanceof Error ? response.error.message : String(response.error);
                setResourceGroupsLoadFailure('Failed to load resource groups.');
                logEvent({
                    action: 'fetch-resource-groups-multiple-subscriptions',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: errorMessage,
                        subscriptionIds: subscriptionIds.join(','),
                    },
                });
                setResourceGroupsList(undefined);
            }

            setResourceGroupsLoading(false);
        };

        fetchResourceGroups();
    }, [resourceGroupClient, isAuthenticated, subscriptionIds, logEvent]);

    return {
        resourceGroupsList,
        resourceGroupsLoading,
        resourceGroupsLoadFailure,
    };
};

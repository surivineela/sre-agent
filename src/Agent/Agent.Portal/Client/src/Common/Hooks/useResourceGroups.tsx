import { useEffect, useMemo, useState } from 'react';
import { ResourceGroupClient } from '../Clients/ResourceGroupClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { useAuth } from '../Contexts/AuthContext';
import { ResourceGroup } from '../Contracts/Arm';
import { LogLevel } from '../Contracts/Telemetry';
import { getArmErrorMessage } from '../Utilities/Client';
import { useTelemetry } from './useTelemetry';

interface UseResourceGroupsParams {
    disabled?: boolean;
    subscriptionId?: string;
    telemetrySource: TelemetrySource;
}

interface UseResourceGroupsResult {
    resourceGroups: ResourceGroup[] | undefined;
    error: Error | undefined;
    isLoading: boolean;
}

export const useResourceGroups = (params: UseResourceGroupsParams): UseResourceGroupsResult => {
    const { disabled = false, subscriptionId, telemetrySource } = params || {};
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [resourceGroups, setResourceGroups] = useState<ResourceGroup[]>();
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<Error>();

    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        if (!isAuthenticated || disabled || !subscriptionId) {
            setResourceGroups(undefined);
            setIsLoading(false);
            setError(undefined);
            return;
        }

        const fetchResourceGroups = async () => {
            setIsLoading(true);
            setError(undefined);

            const response = await resourceGroupClient.getResourceGroups(subscriptionId);

            if (response.isSuccessful && response.content) {
                setResourceGroups(response.content);
            } else {
                const errorMessage = getArmErrorMessage(response.error);
                const err = new Error(errorMessage);
                setError(err);
                logEvent({
                    action: 'fetch-resource-groups',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: errorMessage,
                        subscriptionId,
                    },
                });
                setResourceGroups(undefined);
            }

            setIsLoading(false);
        };

        fetchResourceGroups();
    }, [resourceGroupClient, isAuthenticated, disabled, subscriptionId, logEvent]);

    return { resourceGroups, error, isLoading };
};

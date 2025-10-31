import { useEffect, useMemo, useState } from 'react';
import { SubscriptionClient } from '../Clients/SubscriptionClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { useAuth } from '../Contexts/AuthContext';
import { Subscription } from '../Contracts/Arm';
import { LogLevel } from '../Contracts/Telemetry';
import { getArmErrorMessage } from '../Utilities/Client';
import { useTelemetry } from './useTelemetry';

interface UseSubscriptionsParams {
    disabled?: boolean;
    telemetrySource: TelemetrySource;
}

interface UseSubscriptionsResult {
    subscriptions: Subscription[] | undefined;
    error: Error | undefined;
    isLoading: boolean;
}

export const useSubscriptions = (params: UseSubscriptionsParams): UseSubscriptionsResult => {
    const { disabled = false, telemetrySource } = params || {};
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [subscriptions, setSubscriptions] = useState<Subscription[]>();
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<Error>();

    const subscriptionClient = useMemo(() => SubscriptionClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        if (!isAuthenticated || disabled) {
            setSubscriptions(undefined);
            setIsLoading(false);
            setError(undefined);
            return;
        }

        const fetchSubscriptions = async () => {
            setIsLoading(true);
            setError(undefined);

            const response = await subscriptionClient.getSubscriptions();

            if (response.isSuccessful && response.content) {
                setSubscriptions(response.content);
            } else {
                const errorMessage = getArmErrorMessage(response.error);
                const err = new Error(errorMessage);
                setError(err);
                logEvent({
                    action: 'fetch-subscriptions',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: errorMessage,
                    },
                });
                setSubscriptions(undefined);
            }

            setIsLoading(false);
        };

        fetchSubscriptions();
    }, [subscriptionClient, isAuthenticated, disabled, logEvent]);

    return { subscriptions, error, isLoading };
};

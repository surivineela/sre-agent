import { useEffect, useMemo, useState } from 'react';
import { ArmClient } from '../Clients/ArmClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { useAuth } from '../Contexts/AuthContext';
import { Tenant } from '../Contracts/Arm';
import { LogLevel } from '../Contracts/Telemetry';
import { useTelemetry } from './useTelemetry';

export const useTenants = (telemetrySource: TelemetrySource) => {
    const { isAuthenticated } = useAuth();
    const { logEvent } = useTelemetry(telemetrySource, undefined);

    const [tenants, setTenants] = useState<Tenant[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string>();

    const armClient = useMemo(() => ArmClient.getInstance(telemetrySource), [telemetrySource]);

    useEffect(() => {
        if (!isAuthenticated) {
            setTenants([]);
            return;
        }

        const fetchTenants = async () => {
            setIsLoading(true);
            setError(undefined);

            const response = await armClient.getTenants();

            if (response.isSuccessful && response.content) {
                setTenants(response.content);
            } else {
                const errorMessage = response.error instanceof Error ? response.error.message : String(response.error);
                setError(errorMessage);
                logEvent({
                    action: 'fetch-tenants',
                    actionModifier: 'error',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        error: errorMessage,
                    },
                });
                setTenants([]);
            }

            setIsLoading(false);
        };

        fetchTenants();
    }, [armClient, isAuthenticated, logEvent]);

    return { tenants, isLoading, error };
};

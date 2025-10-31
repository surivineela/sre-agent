import { useCallback, useEffect, useMemo, useState } from 'react';
import { PermissionsClient } from '../Clients/PermissionsClient';
import { TelemetrySource } from '../Constants/Telemetry';

export interface UsePermissionsOptions {
    entityId: string;
    actions: string[];
    telemetrySource: TelemetrySource;
}

export const usePermissions = (options: UsePermissionsOptions) => {
    const { entityId, actions, telemetrySource } = options;
    const [hasPermissions, setPermissions] = useState(false);
    const [isLoadingPermissions, setIsLoadingPermissions] = useState<boolean>(true);

    const permissionsClient = useMemo(() => PermissionsClient.getInstance(telemetrySource), [telemetrySource]);

    const fetchPermissions = useCallback(
        async () => {
            setIsLoadingPermissions(true);
            const permissions = await permissionsClient.hasPermission(entityId, actions);
            setPermissions(permissions);
            setIsLoadingPermissions(false);
        },
        /*
         * Many callers of this hook are passing in an in-place actions array,
         * which will cause a render loop. Make the dependency a stringified array to fix this.
         */
        // eslint-disable-next-line react-hooks/exhaustive-deps
        [entityId, JSON.stringify(actions)]
    );

    const refreshPermissions = useCallback((): Promise<void> => {
        return fetchPermissions();
    }, [fetchPermissions]);

    useEffect(() => {
        fetchPermissions();
    }, [fetchPermissions]);

    return { hasPermissions, refreshPermissions, isLoadingPermissions };
};

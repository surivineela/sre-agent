import { useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { PermissionsClient } from '../../../Common/Clients/PermissionsClient';
import { PermissionsCheckResponse } from '../../../Common/Contracts/Azure/Permission';

export function usePermissions(resourceId: string) {
    const [permissions, setPermissions] = useState<PermissionsCheckResponse>();
    const [permissionsLoaded, setPermissionsLoaded] = useState(false);
    const azPortalContext = useContext(AzPortalContext);

    const getPermissions = useCallback(async () => {
        azPortalContext.log({
            action: 'fetch-permissions',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });
        setPermissions(undefined);
        setPermissionsLoaded(false);

        const response = await PermissionsClient.getPermissions(resourceId);

        if (response.metadata.success) {
            azPortalContext.log({
                action: 'fetch-permissions',
                actionModifier: 'success',
                logLevel: 'info',
                resourceId,
            });
            setPermissions(response.data);
            setPermissionsLoaded(true);
        } else {
            azPortalContext.log({
                action: 'fetch-permissions',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId,
                data: { error: response.metadata.error },
            });
        }
    }, [azPortalContext, resourceId]);

    useEffect(() => {
        if (resourceId) {
            getPermissions();
        }
    }, [resourceId, getPermissions]);

    return {
        permissions,
        permissionsLoaded,
        refresh: getPermissions,
    };
}

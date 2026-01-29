import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { PermissionClient } from '../Clients/PermissionsClient';
import { PermissionActions } from '../Contracts/Azure/Permission';

interface UserPermissionsState {
    canReadAgent: boolean;
    canWriteAgent: boolean;
    canDeleteAgent: boolean;
    canReadThreads: boolean;
    canWriteThreads: boolean;
    canDeleteThreads: boolean;
    canApproveThreads: boolean;
    canReadIncidentManagement: boolean;
    canWriteIncidentManagement: boolean;
    canDeleteIncidentManagement: boolean;
    canReadGraph: boolean;
    canWriteGraph: boolean;
    canDeleteGraph: boolean;
    loading: boolean;
    error: boolean;
}

interface UserPermissions extends UserPermissionsState {
    refresh: () => void;
}

// Single-entry in-memory cache of permission evaluation to avoid duplicate network calls for current resource.
interface CachedPermissionState {
    fetched: boolean;
    promise?: Promise<void>;
    data?: {
        canReadAgent: boolean;
        canWriteAgent: boolean;
        canDeleteAgent: boolean;
        canReadThreads: boolean;
        canWriteThreads: boolean;
        canDeleteThreads: boolean;
        canApproveThreads: boolean;
        canReadIncidentManagement: boolean;
        canWriteIncidentManagement: boolean;
        canDeleteIncidentManagement: boolean;
        canReadGraph: boolean;
        canWriteGraph: boolean;
        canDeleteGraph: boolean;
    };
    error?: boolean;
    fetchedAt?: number; // epoch ms when permissions were fetched
}
let permissionCache: CachedPermissionState | undefined;

// Default TTL for permission cache (minutes).
const PERMISSIONS_TTL_MINUTES = 5;
const ttlExpired = (entry?: CachedPermissionState) => {
    if (!entry?.fetchedAt) return true;
    const ttlMs = PERMISSIONS_TTL_MINUTES * 60 * 1000;
    return Date.now() - entry.fetchedAt > ttlMs;
};

const createPermissionObject = (value: boolean) => ({
    canReadAgent: value,
    canWriteAgent: value,
    canDeleteAgent: value,
    canReadThreads: value,
    canWriteThreads: value,
    canDeleteThreads: value,
    canApproveThreads: value,
    canReadIncidentManagement: value,
    canWriteIncidentManagement: value,
    canDeleteIncidentManagement: value,
    canReadGraph: value,
    canWriteGraph: value,
    canDeleteGraph: value,
});

export const useUserPermissions = (): UserPermissions => {
    const { resourceId: agentResourceId, isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const allPermissions = useMemo(() => createPermissionObject(true), []);

    const [state, setState] = useState<UserPermissionsState>({
        ...allPermissions,
        loading: true,
        error: false,
    });
    const [refreshToken, setRefreshToken] = useState(0);

    const emptyPermissions = useMemo(() => createPermissionObject(false), []);

    useEffect(() => {
        if (isCrossTenantPortalMode || AzPortalProxy.inStandaloneMode) {
            setState({
                ...allPermissions,
                loading: false,
                error: false,
            });
            return;
        }

        if (!agentResourceId) {
            permissionCache = undefined;
            setState({
                ...emptyPermissions,
                loading: false,
                error: true,
            });
            return;
        }

        if (permissionCache?.fetched && permissionCache.data && !ttlExpired(permissionCache)) {
            setState({ ...permissionCache.data, loading: false, error: !!permissionCache.error });
            return;
        }

        if (!permissionCache || !permissionCache.promise || ttlExpired(permissionCache)) {
            const client = PermissionClient.getInstance();
            permissionCache = {
                fetched: false,
                promise: client
                    .getPermissions(agentResourceId)
                    .then(response => {
                        const raw = response.data?.value;
                        if (!raw) throw new Error('No permissions data received');

                        // Checking just data plane permissions
                        const dataPlanePermissionSet = raw.map(p => ({
                            actions: (p as any).dataActions || [],
                            notActions: (p as any).notDataActions || [],
                        }));

                        // Checking both control plane and data plane permissions
                        const permissionSet = raw.map(p => ({
                            actions: [...(p.actions || []), ...(dataPlanePermissionSet.map(ps => ps.actions).flat() || [])],
                            notActions: [...(p.notActions || []), ...(dataPlanePermissionSet.map(ps => ps.notActions).flat() || [])],
                        }));

                        const canReadAgent = client.canPerformActions([PermissionActions.AgentRead], permissionSet, agentResourceId);
                        const canWriteAgent = client.canPerformActions([PermissionActions.AgentWrite], permissionSet, agentResourceId);
                        const canDeleteAgent = client.canPerformActions([PermissionActions.AgentDelete], permissionSet, agentResourceId);
                        const canWriteThreads = client.canPerformActions(
                            [PermissionActions.AgentThreadsWrite],
                            permissionSet,
                            agentResourceId
                        );

                        // TODO (yoonaoh): Data plane thread/read permissions are needed for functionality,
                        // will be adding more data plane checks per feature in the future
                        const canReadThreads = client.canPerformActions(
                            [PermissionActions.AgentThreadsRead],
                            dataPlanePermissionSet,
                            agentResourceId
                        );
                        const canDeleteThreads = client.canPerformActions(
                            [PermissionActions.AgentThreadsDelete],
                            permissionSet,
                            agentResourceId
                        );
                        const canApproveThreads = client.canPerformActions(
                            [PermissionActions.AgentThreadsApprove],
                            permissionSet,
                            agentResourceId
                        );
                        const canReadIncidentManagement = client.canPerformActions(
                            [PermissionActions.AgentIncidentManagementRead],
                            permissionSet,
                            agentResourceId
                        );
                        const canWriteIncidentManagement = client.canPerformActions(
                            [PermissionActions.AgentIncidentManagementWrite],
                            permissionSet,
                            agentResourceId
                        );
                        const canDeleteIncidentManagement = client.canPerformActions(
                            [PermissionActions.AgentIncidentManagementDelete],
                            permissionSet,
                            agentResourceId
                        );
                        const canReadGraph = client.canPerformActions([PermissionActions.AgentGraphRead], permissionSet, agentResourceId);
                        const canWriteGraph = client.canPerformActions([PermissionActions.AgentGraphWrite], permissionSet, agentResourceId);
                        const canDeleteGraph = client.canPerformActions(
                            [PermissionActions.AgentGraphDelete],
                            permissionSet,
                            agentResourceId
                        );

                        permissionCache!.data = {
                            canReadAgent,
                            canWriteAgent,
                            canDeleteAgent,
                            canReadThreads,
                            canWriteThreads,
                            canDeleteThreads,
                            canApproveThreads,
                            canReadIncidentManagement,
                            canWriteIncidentManagement,
                            canDeleteIncidentManagement,
                            canReadGraph,
                            canWriteGraph,
                            canDeleteGraph,
                        };
                        permissionCache!.error = false;
                        permissionCache!.fetched = true;
                        permissionCache!.fetchedAt = Date.now();
                    })
                    .catch(() => {
                        if (permissionCache) {
                            permissionCache.data = { ...emptyPermissions };
                            permissionCache.error = true;
                            permissionCache.fetched = true;
                            permissionCache.fetchedAt = Date.now();
                        }
                    }),
            };
        }

        let isSubscribed = true;
        permissionCache.promise?.then(() => {
            if (!isSubscribed) return;
            const data = permissionCache?.data || emptyPermissions;
            setState({ ...data, loading: false, error: !!permissionCache?.error });
        });

        return () => {
            isSubscribed = false;
        };
    }, [agentResourceId, refreshToken, emptyPermissions, allPermissions, isCrossTenantPortalMode]);

    const refresh = useCallback(() => {
        if (agentResourceId) {
            permissionCache = undefined;
            setState(prev => ({ ...prev, loading: true }));
            setRefreshToken(t => t + 1);
        }
    }, [agentResourceId]);

    return { ...state, refresh };
};

export default useUserPermissions;

// Test-only utility to clear cached permissions between tests so tests can be independent
export const __resetUserPermissionsCacheForTests = () => {
    permissionCache = undefined;
};

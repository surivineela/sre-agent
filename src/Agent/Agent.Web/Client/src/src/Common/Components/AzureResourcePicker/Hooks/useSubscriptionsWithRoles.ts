import { useEffect, useState } from 'react';
import { Subscription } from '../../../../Space/Settings/Hooks/useSubscriptions';
import AzPortalProxy from '../../../AzPortalProxy/AzPortalProxy';
import { IdentityClient } from '../../../Clients/IdentityClient';
import { SubscriptionsClient } from '../../../Clients/SubscriptionsClient';
import { RoleDefinitionIds, SubscriptionWithPermission, canRoleAssignRoles, getRoleDisplayName } from '../Contracts';

/**
 * Priority of roles for determining the "highest privilege" role.
 * Lower index = higher privilege.
 */
const rolePriority: string[] = [
    RoleDefinitionIds.owner,
    RoleDefinitionIds.userAccessAdministrator,
    RoleDefinitionIds.contributor,
    RoleDefinitionIds.reader,
];

/**
 * Extracts the role definition ID (GUID) from a full role definition resource ID.
 * e.g., "/subscriptions/.../providers/Microsoft.Authorization/roleDefinitions/8e3af657-a8ff-443c-a75c-2fe8c4bcb635"
 *       => "8e3af657-a8ff-443c-a75c-2fe8c4bcb635"
 */
const extractRoleDefinitionId = (roleDefinitionResourceId: string): string => {
    const parts = roleDefinitionResourceId.split('/');
    return parts[parts.length - 1];
};

const getHighestPrivilegeRole = (roleDefinitionIds: string[]): string | null => {
    for (const priorityRoleId of rolePriority) {
        if (roleDefinitionIds.includes(priorityRoleId)) {
            return priorityRoleId;
        }
    }
    return roleDefinitionIds.length > 0 ? roleDefinitionIds[0] : null;
};

export interface UseSubscriptionsWithRolesResult {
    selectableSubscriptions: SubscriptionWithPermission[];
    disabledSubscriptions: SubscriptionWithPermission[];
    isLoading: boolean;
    error: string | null;
}

export const useSubscriptionsWithRoles = (portalContext: AzPortalProxy): UseSubscriptionsWithRolesResult => {
    const [selectableSubscriptions, setSelectableSubscriptions] = useState<SubscriptionWithPermission[]>([]);
    const [disabledSubscriptions, setDisabledSubscriptions] = useState<SubscriptionWithPermission[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchSubscriptionsWithRoles = async () => {
            try {
                const userInfo = AzPortalProxy.envInfo?.userInfo;
                const principalId = userInfo?.objectId || userInfo?.principalId;

                if (!principalId) {
                    throw new Error('Unable to determine user principal ID');
                }

                const subscriptionsResponse = await SubscriptionsClient.getSubscriptions();
                const subscriptions: Subscription[] = subscriptionsResponse?.data?.value || [];

                if (subscriptions.length === 0) {
                    setIsLoading(false);
                    return;
                }

                const subscriptionIds = subscriptions.map(sub => sub.subscriptionId);
                const recommendedSubscriptions = await SubscriptionsClient.getSubscriptionsWithSreAgentResources(subscriptionIds);
                const roleAssignmentPromises = subscriptions.map(async subscription => {
                    const scope = `/subscriptions/${subscription.subscriptionId}`;
                    const roleAssignmentsResponse = await IdentityClient.getRoleAssignmentsWithScope(scope, principalId);
                    const roleDefinitionIds: string[] = [];

                    if (roleAssignmentsResponse?.data) {
                        const data = roleAssignmentsResponse.data as any;
                        if (Array.isArray(data.value)) {
                            data.value.forEach((assignment: any) => {
                                if (assignment.properties?.roleDefinitionId) {
                                    roleDefinitionIds.push(extractRoleDefinitionId(assignment.properties.roleDefinitionId));
                                }
                            });
                        } else if (data.properties?.roleDefinitionId) {
                            roleDefinitionIds.push(extractRoleDefinitionId(data.properties.roleDefinitionId));
                        }
                    }

                    return {
                        subscription,
                        roleDefinitionIds,
                    };
                });

                const roleAssignmentResults = await Promise.all(roleAssignmentPromises);

                const selectable: SubscriptionWithPermission[] = [];
                const disabled: SubscriptionWithPermission[] = [];

                roleAssignmentResults.forEach(({ subscription, roleDefinitionIds }) => {
                    const highestRoleId = getHighestPrivilegeRole(roleDefinitionIds);
                    const hasRoleAssignPermission = roleDefinitionIds.some(canRoleAssignRoles);

                    const subscriptionWithPermission: SubscriptionWithPermission = {
                        id: `/subscriptions/${subscription.subscriptionId}`,
                        name: subscription.displayName,
                        displayName: subscription.displayName,
                        subscriptionId: subscription.subscriptionId,
                        state: subscription.state,
                        tenantId: subscription.tenantId,
                        myRole: highestRoleId ? getRoleDisplayName(highestRoleId) : null,
                        canAssignRoles: hasRoleAssignPermission,
                        recommended: recommendedSubscriptions.has(subscription.subscriptionId),
                        selected: false,
                    };

                    if (hasRoleAssignPermission) {
                        selectable.push(subscriptionWithPermission);
                    } else {
                        disabled.push(subscriptionWithPermission);
                    }
                });

                setSelectableSubscriptions(selectable);
                setDisabledSubscriptions(disabled);
            } catch (err) {
                const errorMessage = err instanceof Error ? err.message : 'Failed to fetch subscriptions with roles';
                portalContext.log({
                    action: 'useSubscriptionsWithRoles',
                    actionModifier: 'error',
                    logLevel: 'error',
                    data: { error: errorMessage },
                });
                setError(errorMessage);
            } finally {
                setIsLoading(false);
            }
        };

        fetchSubscriptionsWithRoles();
    }, [portalContext]);

    return {
        selectableSubscriptions,
        disabledSubscriptions,
        isLoading,
        error,
    };
};

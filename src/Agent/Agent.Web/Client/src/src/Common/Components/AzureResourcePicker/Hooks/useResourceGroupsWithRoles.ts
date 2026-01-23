import { useEffect, useState } from 'react';
import AzPortalProxy from '../../../AzPortalProxy/AzPortalProxy';
import { IdentityClient, RoleAssignmentsByScope } from '../../../Clients/IdentityClient';
import { ResourceGroupClient } from '../../../Clients/ResourceGroupClient';
import { ResourceGroupWithPermission, RoleDefinitionIds, canRoleAssignRoles, getRoleDisplayName } from '../Contracts';

const rolePriority: string[] = [
    RoleDefinitionIds.owner,
    RoleDefinitionIds.userAccessAdministrator,
    RoleDefinitionIds.contributor,
    RoleDefinitionIds.reader,
];

const getHighestPrivilegeRole = (roleDefinitionIds: string[]): string | null => {
    for (const priorityRoleId of rolePriority) {
        if (roleDefinitionIds.includes(priorityRoleId)) {
            return priorityRoleId;
        }
    }
    return roleDefinitionIds.length > 0 ? roleDefinitionIds[0] : null;
};

const getRolesForResourceGroup = (resourceGroupId: string, subscriptionId: string, roleAssignments: RoleAssignmentsByScope): string[] => {
    const roleDefinitionIds: string[] = [];
    const rgIdLower = resourceGroupId.toLowerCase();
    const subScope = `/subscriptions/${subscriptionId}`.toLowerCase();

    const rgRoles = roleAssignments.rolesByScope.get(rgIdLower);
    if (rgRoles) {
        roleDefinitionIds.push(...rgRoles);
    }

    const subRoles = roleAssignments.rolesByScope.get(subScope);
    if (subRoles) {
        roleDefinitionIds.push(...subRoles);
    }

    return roleDefinitionIds;
};

export interface UseResourceGroupsWithRolesResult {
    selectableResourceGroups: ResourceGroupWithPermission[];
    disabledResourceGroups: ResourceGroupWithPermission[];
    isLoading: boolean;
    error: string | null;
}

export const useResourceGroupsWithRoles = (subscriptionIds: string[], portalContext: AzPortalProxy): UseResourceGroupsWithRolesResult => {
    const [selectableResourceGroups, setSelectableResourceGroups] = useState<ResourceGroupWithPermission[]>([]);
    const [disabledResourceGroups, setDisabledResourceGroups] = useState<ResourceGroupWithPermission[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (subscriptionIds.length === 0) {
            setSelectableResourceGroups([]);
            setDisabledResourceGroups([]);
            setIsLoading(false);
            return;
        }

        const fetchResourceGroupsWithRoles = async () => {
            setIsLoading(true);
            setError(null);

            try {
                const userInfo = AzPortalProxy.envInfo?.userInfo;
                const principalId = userInfo?.objectId || userInfo?.principalId;

                if (!principalId) {
                    throw new Error('Unable to determine user principal ID');
                }

                const resourceGroups = await ResourceGroupClient.getResourcesGroupsFromArg(subscriptionIds, portalContext);

                if (resourceGroups.length === 0) {
                    setIsLoading(false);
                    return;
                }

                const recommendedResourceGroupIds = await ResourceGroupClient.getResourceGroupIdsWithSreAgentResources(subscriptionIds);

                const roleAssignments = await IdentityClient.getRoleAssignmentsFromArg(subscriptionIds, principalId);

                // Build resource group with permission objects
                const selectable: ResourceGroupWithPermission[] = [];
                const disabled: ResourceGroupWithPermission[] = [];

                resourceGroups.forEach(resourceGroup => {
                    const roleDefinitionIds = getRolesForResourceGroup(resourceGroup.id, resourceGroup.subscriptionId, roleAssignments);
                    const highestRoleId = getHighestPrivilegeRole(roleDefinitionIds);
                    const hasRoleAssignPermission = roleDefinitionIds.some(canRoleAssignRoles);

                    const resourceGroupWithPermission: ResourceGroupWithPermission = {
                        id: resourceGroup.id,
                        name: resourceGroup.name,
                        location: resourceGroup.location,
                        subscriptionId: resourceGroup.subscriptionId,
                        properties: resourceGroup.properties,
                        myRole: highestRoleId ? getRoleDisplayName(highestRoleId) : null,
                        canAssignRoles: hasRoleAssignPermission,
                        recommended: recommendedResourceGroupIds.has(resourceGroup.id.toLowerCase()),
                        selected: false,
                    };

                    if (hasRoleAssignPermission) {
                        selectable.push(resourceGroupWithPermission);
                    } else {
                        disabled.push(resourceGroupWithPermission);
                    }
                });

                selectable.sort((a, b) => a.name.localeCompare(b.name));
                disabled.sort((a, b) => a.name.localeCompare(b.name));

                setSelectableResourceGroups(selectable);
                setDisabledResourceGroups(disabled);
            } catch (err) {
                const errorMessage = err instanceof Error ? err.message : 'Failed to fetch resource groups with roles';
                portalContext.log({
                    action: 'useResourceGroupsWithRoles',
                    actionModifier: 'error',
                    logLevel: 'error',
                    data: { error: errorMessage },
                });
                setError(errorMessage);
            } finally {
                setIsLoading(false);
            }
        };

        fetchResourceGroupsWithRoles();
    }, [subscriptionIds, portalContext]);

    return {
        selectableResourceGroups,
        disabledResourceGroups,
        isLoading,
        error,
    };
};

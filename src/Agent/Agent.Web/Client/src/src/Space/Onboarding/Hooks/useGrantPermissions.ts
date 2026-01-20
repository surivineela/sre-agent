import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { ArmTemplateBuilder } from '../../../Common/ArmTemplateBuilder/ArmTemplateBuilder';
import { ARM_DEPLOYMENT_NAME_LIMIT, ArmTemplateParameterName } from '../../../Common/ArmTemplateBuilder/ArmTemplateTypes';
import { RoleAssignmentTemplateResource } from '../../../Common/ArmTemplateFragments/RoleAssignmentTemplateResource';
import { SubscriptionRoleAssignmentTemplateResource } from '../../../Common/ArmTemplateFragments/SubscriptionRoleAssignmentTemplateResource';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { DeploymentClient } from '../../../Common/Clients/DeploymentClient';
import { IdentityClient } from '../../../Common/Clients/IdentityClient';
import { PermissionClient } from '../../../Common/Clients/PermissionsClient';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { ProvisioningStates } from '../../../Common/Constants/Arm';
import { CoreRBACRoleIds, getRoleIdsForResourceGroup, RBACRoleIds, RBACRoleIdToNameMap } from '../../../Common/Contracts/Azure/Permission';
import { AgentAccessLevel } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';

export type ScopeType = 'subscription' | 'resourceGroup';

export interface UseGrantPermissionsParams {
    scopeType: ScopeType;
    subscriptionId: string;
    resourceGroupId: string;
    permissionsLevel: AgentAccessLevel;
    agentIdentityResourceId: string;
    agentResourceId: string;
    /** Location for the deployment (e.g., 'eastus') */
    location: string;
}

export interface UseGrantPermissionsResult {
    requiredRoleIds: string[];
    existingRoleIds: string[];
    missingRoleIds: string[];
    isLoading: boolean;
    isGranting: boolean;
    error: string | undefined;
    grantSuccess: boolean;
    checkExistingRoles: () => Promise<void>;
    grantPermissions: () => Promise<boolean>;
}

export const useGrantPermissions = (params: UseGrantPermissionsParams): UseGrantPermissionsResult => {
    const { scopeType, subscriptionId, resourceGroupId, permissionsLevel, agentIdentityResourceId, agentResourceId, location } = params;

    const azPortalContext = useContext(AzPortalContext);

    const [requiredRoleIds, setRequiredRoleIds] = useState<string[]>([]);
    const [existingRoleIds, setExistingRoleIds] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isGranting, setIsGranting] = useState<boolean>(false);
    const [error, setError] = useState<string | undefined>(undefined);
    const [grantSuccess, setGrantSuccess] = useState<boolean>(false);

    const missingRoleIds = useMemo(() => {
        return requiredRoleIds.filter(roleId => !existingRoleIds.includes(roleId));
    }, [requiredRoleIds, existingRoleIds]);

    const scope = useMemo(() => {
        if (scopeType === 'subscription') {
            return `/subscriptions/${subscriptionId}`;
        }
        return resourceGroupId;
    }, [scopeType, subscriptionId, resourceGroupId]);

    const calculateRequiredRoles = useCallback(async (): Promise<string[]> => {
        if (scopeType === 'subscription') {
            return [RBACRoleIds.reader];
        }

        try {
            const resourceTypesByRg = await ResourceGroupClient.listResourceKindsInResourceGroups([resourceGroupId]);
            const resourceTypes = resourceTypesByRg[resourceGroupId] ?? [];
            return getRoleIdsForResourceGroup(resourceTypes, permissionsLevel);
        } catch (err) {
            azPortalContext.log({
                action: 'calculateRequiredRoles',
                actionModifier: 'failed',
                logLevel: 'error',
                data: {
                    message: `Failed to calculate required roles: ${getErrorMessage(err)}`,
                    resourceGroupId,
                },
            });
            return [...CoreRBACRoleIds];
        }
    }, [scopeType, resourceGroupId, permissionsLevel, azPortalContext]);

    const checkExistingRoles = useCallback(async (): Promise<void> => {
        if (!agentIdentityResourceId || !scope) {
            return;
        }

        setIsLoading(true);
        setError(undefined);

        try {
            const identity = await IdentityClient.getManagedUserIdentity(agentIdentityResourceId);
            if (!identity) {
                throw new Error('Failed to retrieve managed identity');
            }

            const principalId = identity.properties?.principalId;
            if (!principalId) {
                throw new Error('Managed identity does not have a principal ID');
            }

            const required = await calculateRequiredRoles();
            setRequiredRoleIds(required);

            const permissionClient = PermissionClient.getInstance();
            const existingRoles: string[] = [];

            await Promise.all(
                required.map(async roleId => {
                    const hasRole = await permissionClient.hasRoleAssignment(scope, roleId, principalId);
                    if (hasRole) {
                        existingRoles.push(roleId);
                    }
                })
            );

            setExistingRoleIds(existingRoles);

            azPortalContext.log({
                action: 'checkExistingRoles',
                actionModifier: 'success',
                logLevel: 'info',
                data: {
                    scopeType,
                    scope,
                    requiredCount: required.length,
                    existingCount: existingRoles.length,
                    missingCount: required.length - existingRoles.length,
                },
            });
        } catch (err) {
            const errorMessage = getErrorMessage(err);
            setError(errorMessage);

            azPortalContext.log({
                action: 'checkExistingRoles',
                actionModifier: 'failed',
                logLevel: 'error',
                data: {
                    message: errorMessage,
                    scopeType,
                    scope,
                },
            });
        } finally {
            setIsLoading(false);
        }
    }, [agentIdentityResourceId, scope, scopeType, calculateRequiredRoles, azPortalContext]);

    const getParameters = useCallback((): Record<string, any> => {
        const { subscription, resourceGroup } = new ArmResourceDescriptor(agentResourceId);

        const baseParams: Record<string, any> = {
            [ArmTemplateParameterName.SubscriptionId]: {
                value: `/subscriptions/${subscription}`,
            },
            [ArmTemplateParameterName.Location]: {
                value: '',
            },
            [ArmTemplateParameterName.ResourceGroupName]: {
                value: resourceGroup,
            },
        };

        return baseParams;
    }, [agentResourceId]);

    const buildTemplate = useCallback(
        (roleIds: string[], deploymentGuid: string, principalId?: string) => {
            const builder = new ArmTemplateBuilder();

            if (scopeType === 'subscription') {
                if (!principalId) {
                    throw new Error('Principal ID is required for subscription-level role assignments');
                }
                const subscriptionRoleAssignment = new SubscriptionRoleAssignmentTemplateResource(builder, {
                    roleDefinitionIds: roleIds,
                    subscriptionId,
                    deploymentGuid,
                    principalId,
                    location,
                });
                builder.addResource(subscriptionRoleAssignment);
            } else {
                const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceGroupId);
                if (subscription && resourceGroup) {
                    const roleAssignment = new RoleAssignmentTemplateResource(builder, {
                        roleDefinitionIds: roleIds,
                        resourceGroupName: resourceGroup,
                        subscriptionId: subscription,
                        deploymentGuid,
                    });
                    builder.addResource(roleAssignment);
                }
            }

            return builder.getTemplate();
        },
        [scopeType, subscriptionId, resourceGroupId, location]
    );

    const getDeploymentResourceId = useCallback(
        (deploymentGuid: string): string => {
            const { subscription, resourceGroup } = new ArmResourceDescriptor(agentResourceId);
            const agentName = agentResourceId.split('/').pop() ?? 'agent';

            const maxNameLength = ARM_DEPLOYMENT_NAME_LIMIT - 30;
            const safeName = agentName.length > maxNameLength ? agentName.substring(0, maxNameLength) : agentName;
            const deploymentName = `${safeName}-roleAssignments-${deploymentGuid}`;

            return `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.Resources/deployments/${deploymentName}`;
        },
        [agentResourceId]
    );

    const pollForDeploymentCompletion = useCallback(
        async (deploymentId: string, notificationId: string): Promise<boolean> => {
            const maxAttempts = 60; // 5 minutes with 5 second intervals
            let attempts = 0;

            while (attempts < maxAttempts) {
                try {
                    const response = await DeploymentClient.getDeployment(deploymentId);
                    const provisioningState = response?.data?.properties?.provisioningState ?? '';

                    if (provisioningState === ProvisioningStates.succeeded) {
                        azPortalContext.stopNotification(notificationId, true, 'Role assignments granted successfully');
                        return true;
                    }

                    if (provisioningState === ProvisioningStates.Failed) {
                        const deploymentError = getErrorMessage(response?.data?.properties?.error);
                        azPortalContext.stopNotification(notificationId, false, `Failed to grant role assignments: ${deploymentError}`);
                        return false;
                    }

                    await new Promise(resolve => setTimeout(resolve, 5000));
                    attempts++;
                } catch (err) {
                    const errorMessage = getErrorMessage(err);
                    azPortalContext.log({
                        action: 'pollForDeploymentCompletion',
                        actionModifier: 'error',
                        logLevel: 'error',
                        data: {
                            message: errorMessage,
                            deploymentId,
                            attempt: attempts,
                        },
                    });
                    attempts++;
                }
            }

            azPortalContext.stopNotification(notificationId, false, 'Role assignment deployment timed out');
            return false;
        },
        [azPortalContext]
    );

    const grantPermissions = useCallback(async (): Promise<boolean> => {
        if (missingRoleIds.length === 0) {
            setGrantSuccess(true);
            return true;
        }

        setIsGranting(true);
        setError(undefined);
        setGrantSuccess(false);

        const notification = azPortalContext.startNotification(
            'Granting permissions',
            `Assigning ${missingRoleIds.length} role(s) to the agent's managed identity...`
        );

        try {
            const identity = await IdentityClient.getManagedUserIdentity(agentIdentityResourceId);
            if (!identity) {
                throw new Error('Failed to retrieve managed identity');
            }

            const principalId = identity.properties?.principalId;
            if (!principalId) {
                throw new Error('Failed to retrieve principal ID from managed identity');
            }

            const deploymentGuid = `${new Date().getTime()}`;
            const template = buildTemplate(missingRoleIds, deploymentGuid, principalId);
            const parameters = getParameters();
            const deploymentResourceId = getDeploymentResourceId(deploymentGuid);

            azPortalContext.log({
                action: 'grantPermissions',
                actionModifier: 'startDeployment',
                logLevel: 'info',
                data: {
                    scopeType,
                    scope,
                    roleCount: missingRoleIds.length,
                    roleNames: missingRoleIds.map(id => RBACRoleIdToNameMap[id] ?? id),
                    deploymentResourceId,
                },
            });

            const deploymentResponse = await DeploymentClient.createNewDeployment(deploymentResourceId, template, parameters, true);

            if (deploymentResponse.metadata.error) {
                throw new Error(getErrorMessage(deploymentResponse.metadata.error));
            }

            const success = await pollForDeploymentCompletion(deploymentResourceId, notification);

            if (success) {
                setGrantSuccess(true);
                await checkExistingRoles();
            }

            azPortalContext.log({
                action: 'grantPermissions',
                actionModifier: success ? 'success' : 'failed',
                logLevel: success ? 'info' : 'error',
                data: {
                    scopeType,
                    scope,
                    roleCount: missingRoleIds.length,
                    success,
                },
            });

            return success;
        } catch (err) {
            const errorMessage = getErrorMessage(err);
            setError(errorMessage);

            azPortalContext.stopNotification(notification, false, `Failed to grant permissions: ${errorMessage}`);

            azPortalContext.log({
                action: 'grantPermissions',
                actionModifier: 'failed',
                logLevel: 'error',
                data: {
                    message: errorMessage,
                    scopeType,
                    scope,
                },
            });

            return false;
        } finally {
            setIsGranting(false);
        }
    }, [
        missingRoleIds,
        agentIdentityResourceId,
        scopeType,
        scope,
        azPortalContext,
        buildTemplate,
        getParameters,
        getDeploymentResourceId,
        pollForDeploymentCompletion,
        checkExistingRoles,
    ]);

    useEffect(() => {
        if (agentIdentityResourceId && scope) {
            checkExistingRoles();
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [agentIdentityResourceId, scope, scopeType, permissionsLevel]);

    return {
        requiredRoleIds,
        existingRoleIds,
        missingRoleIds,
        isLoading,
        isGranting,
        error,
        grantSuccess,
        checkExistingRoles,
        grantPermissions,
    };
};

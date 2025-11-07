import { useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import * as Yup from 'yup';
import { DeploymentClient } from '../../../Common/Clients/DeploymentClient';
import { PermissionsClient } from '../../../Common/Clients/PermissionsClient';
import { ResourceClient } from '../../../Common/Clients/ResourceClient';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { SreAgentClient } from '../../../Common/Clients/SreAgentClient';
import { DeploymentProvisioningStates, ResourceTypes } from '../../../Common/Constants/Arm';
import { TelemetrySource } from '../../../Common/Constants/Telemetry';
import { useAuth } from '../../../Common/Contexts/AuthContext';
import { useNotifications } from '../../../Common/Contexts/NotificationContext';
import { ArmObj } from '../../../Common/Contracts/Arm';
import { CoreRBACRoleIds, getRoleIdsForResourceGroup, RBACRoleIds } from '../../../Common/Contracts/Permissions';
import { Response } from '../../../Common/Contracts/Response';
import { Agent, AgentAccessLevel, AgentMode } from '../../../Common/Contracts/SreAgent';
import { LogLevel } from '../../../Common/Contracts/Telemetry';
import { useTelemetry } from '../../../Common/Hooks/useTelemetry';
import { parseArmId } from '../../../Common/Utilities/ArmId';
import { ArmTemplateBuilder } from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateBuilder';
import {
    AppInsightsParameterName,
    ArmTemplate,
    ArmTemplateParameterName,
    SreAgentParameterName,
} from '../../../Common/Utilities/ArmTemplateBuilder/ArmTemplateTypes';
import { AppInsightsTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/AppInsightsTemplateResource';
import { AppInsightsDependencyResolver } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/DependencyResolvers/AppInsightsDependencyResolver';
import { RoleAssignmentTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/RoleAssignmentTemplateResource';
import { SreAgentTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/SreAgentTemplateResource';
import { UserIdentityTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/UserIdentityTemplateResource';
import { UserRoleAssignmentTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/UserRoleAssignmentTemplateResource';
import { WorkspaceTemplateResource } from '../../../Common/Utilities/ArmTemplateBuilder/SreAgent/WorkspaceTemplateResource';
import { getArmErrorMessage } from '../../../Common/Utilities/Client';
import { getCanonicalLocation, getResourceLocation } from '../../../Common/Utilities/Location';
import { AgentCreateDeploymentNotificationResources, PortalResources } from '../../../Strings/Resources';
import { SreAgentCreateFormProps } from './CreateAgentDialog';

const ARM_DEPLOYMENT_NAME_LIMIT = 64;

export enum SreAgentPermissions {
    roleAssignmentWrite = 'Microsoft.Authorization/roleAssignments/write',
    roleAssignmentRead = 'Microsoft.Authorization/roleAssignments/read',
    roleAssignmentDelete = 'Microsoft.Authorization/roleAssignments/delete',
    identityWrite = 'Microsoft.ManagedIdentity/userAssignedIdentities/write',
    roleAssignmentAll = 'Microsoft.Authorization/roleAssignments/*',
    authWrite = 'Microsoft.Authorization/*/Write',
    authAll = 'Microsoft.Authorization/*',
    deployWrite = 'Microsoft.Resources/deployments/write',
}

export const useSreAgentCreate = ({
    showRegistrationDialog,
    agentSpaceId,
    agentSpaceLocation,
    onDeploymentStarted,
}: {
    showRegistrationDialog: () => Promise<boolean>;
    agentSpaceId: string;
    agentSpaceLocation?: string;
    onDeploymentStarted?: () => void;
}) => {
    const { user } = useAuth();
    const { logEvent } = useTelemetry(TelemetrySource.SreAgentCreate, undefined);
    const notifications = useNotifications();
    const intl = useIntl();

    const [isDeploying, setIsDeploying] = useState<boolean>(false);
    const [permissionsLoading, setPermissionsLoading] = useState<boolean>(false);
    const [deploymentId, setDeploymentId] = useState<string>('');
    const [agentResourceId, setAgentResourceId] = useState<string>('');
    const [agentName, setAgentName] = useState<string>('');

    const permissionsClient = useMemo(() => PermissionsClient.getInstance(TelemetrySource.SreAgentCreate), []);
    const resourceClient = useMemo(() => ResourceClient.getInstance(TelemetrySource.SreAgentCreate), []);
    const deploymentClient = useMemo(() => DeploymentClient.getInstance(TelemetrySource.SreAgentCreate), []);
    const resourceGroupClient = useMemo(() => ResourceGroupClient.getInstance(TelemetrySource.SreAgentCreate), []);
    const agentClient = useMemo(() => SreAgentClient.getInstance(TelemetrySource.SreAgentCreate), []);

    const handleFailedDeployment = useCallback(
        (deploymentName: string, error?: unknown, notificationId?: string, title?: string, description?: string) => {
            setIsDeploying(false);
            const errorMessage = getArmErrorMessage(error);

            logEvent({
                action: DeploymentProvisioningStates.deploymentFailed,
                actionModifier: 'failed',
                additionalData: {
                    deploymentName,
                    status: DeploymentProvisioningStates.deploymentFailed,
                    error: errorMessage,
                },
            });

            if (notificationId) {
                const failureTitle = title ?? intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationTitle);
                const failureDescription =
                    description ??
                    intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationFailedDescription, {
                        agentName,
                        error: errorMessage,
                    });

                notifications.fail(notificationId, failureTitle, failureDescription);
                // Note: Previously linked to Azure Portal ArmErrorsBlade for error details
            }

            if (error) {
                logEvent({
                    action: 'Failed to create SRE Agent',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        agentResourceId,
                    },
                });
            }
        },
        [agentName, agentResourceId, logEvent, notifications, intl]
    );

    const waitForAgentSiteLoad = useCallback(
        async (agentResourceId: string) => {
            let agentArmResponse: Response<ArmObj<Agent>> | undefined = undefined;
            const armPingLimit = 150;
            const armPingSleep = 2000;
            let armPingIndex = 0;

            for (armPingIndex; armPingIndex < armPingLimit; armPingIndex++) {
                agentArmResponse = await agentClient.getAgent(agentResourceId);

                if (agentArmResponse.isSuccessful && agentArmResponse.content?.properties?.provisioningState === 'Succeeded') {
                    break;
                }

                await new Promise(r => setTimeout(r, armPingSleep));
            }

            if (armPingIndex >= armPingLimit) {
                logEvent({
                    action: 'Polling on agent ARM object failed',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        agentResourceId,
                    },
                });

                return;
            }

            const agentStaticUrl = `${agentArmResponse?.content?.properties?.agentEndpoint}/static/`;
            const agentSitePingLimit = 120;
            const agentSitePingSleep = 1000;
            let agentSitePingIndex = 0;
            for (agentSitePingIndex = 0; agentSitePingIndex < agentSitePingLimit; agentSitePingIndex++) {
                try {
                    const response = await fetch(agentStaticUrl as string);
                    if (response.ok) {
                        break;
                    }
                } catch (_e) {
                    //Nothing to do
                }

                await new Promise(r => setTimeout(r, agentSitePingSleep));
            }

            if (agentSitePingIndex >= agentSitePingLimit) {
                logEvent({
                    action: 'Polling on agent static site failed',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    additionalData: {
                        agentResourceId,
                    },
                });
            }
        },
        [agentClient, logEvent]
    );

    const deployAndWaitForSiteLoad = useCallback(
        async (
            deploymentResourceId: string,
            validationNotificationId: string,
            template: ArmTemplate,
            parameters: Record<string, unknown>,
            agentResourceId: string
        ) => {
            const localDeploymentClient = DeploymentClient.getInstance(TelemetrySource.SreAgentCreate);
            const agentName = (parameters[SreAgentParameterName.AgentName] as { value: string }).value;

            const response = await localDeploymentClient.createNewDeployment(
                deploymentResourceId,
                template,
                parameters,
                agentResourceId,
                true
            );

            if (response.isSuccessful) {
                // Validation succeeded
                notifications.succeed(
                    validationNotificationId,
                    intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationTitle),
                    intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationSuccessDescription, {
                        agentName,
                    })
                );

                // Trigger step transition to deployment tracking
                if (onDeploymentStarted) {
                    onDeploymentStarted();
                }

                // Start deployment polling notification
                notifications.startWithPolling(intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationTitle), {
                    pollFn: async () => {
                        const deploymentResponse = await deploymentClient.getDeployment(deploymentResourceId);
                        const provisioningState = deploymentResponse?.content?.properties?.provisioningState;

                        if (provisioningState === DeploymentProvisioningStates.succeeded) {
                            setIsDeploying(false);
                            return {
                                complete: true,
                                success: true,
                                title: intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationTitle),
                                description: intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreatedSuccessDescription, {
                                    agentName,
                                }),
                            };
                        }

                        if (provisioningState === DeploymentProvisioningStates.Failed) {
                            setIsDeploying(false);
                            const errorMessage = getArmErrorMessage(deploymentResponse?.content?.properties?.error);
                            return {
                                complete: true,
                                success: false,
                                title: intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationTitle),
                                description: intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationFailedDescription, {
                                    agentName,
                                    error: errorMessage,
                                }),
                                // Note: Previously linked to Azure Portal AgentFrameBlade.ReactView and ArmErrorsBlade
                            };
                        }

                        // Still in progress
                        return { complete: false };
                    },
                    interval: 5000,
                    maxAttempts: 120, // 10 minutes max
                });

                waitForAgentSiteLoad(agentResourceId);
            } else {
                setIsDeploying(false);
                const errorMessage = getArmErrorMessage(response.error);
                handleFailedDeployment(
                    deploymentResourceId,
                    response.error,
                    validationNotificationId,
                    intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationTitle),
                    intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationFailedDescription, {
                        agentName,
                        error: errorMessage,
                    })
                );
            }
        },
        [handleFailedDeployment, waitForAgentSiteLoad, deploymentClient, notifications, intl, onDeploymentStarted]
    );

    const deployTemplate = useCallback(
        (
            deploymentResourceId: string,
            subscriptionId: string,
            validationNotificationId: string,
            template: ArmTemplate,
            parameters: Record<string, unknown>,
            agentResourceId: string
        ) => {
            const registerRpPromises = [
                resourceClient.registerProvider(subscriptionId, ResourceTypes.WorkspaceProvider),
                resourceClient.registerProvider(subscriptionId, ResourceTypes.AppProvider),
                resourceClient.registerProvider(subscriptionId, ResourceTypes.ManagedIdentityProvider),
            ];
            return Promise.all(registerRpPromises).then(async results => {
                if (results.some(result => !result.isSuccessful)) {
                    const errors = results.map(result => result.error).filter(error => !!error);
                    logEvent({
                        action: 'Failed to register at least one provider',
                        actionModifier: 'failed',
                        logLevel: LogLevel.Error,
                        additionalData: {
                            errors,
                        },
                    });

                    const continueDeployment = await showRegistrationDialog();
                    if (continueDeployment) {
                        deployAndWaitForSiteLoad(deploymentResourceId, validationNotificationId, template, parameters, agentResourceId);
                    } else {
                        setIsDeploying(false);
                        const agentName = (parameters[SreAgentParameterName.AgentName] as { value: string }).value;
                        notifications.fail(
                            validationNotificationId,
                            intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationTitle),
                            intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationRPFailureDescription, {
                                agentName,
                            })
                        );
                        logEvent({
                            action: 'User cancelled deployment due to failed registration',
                            actionModifier: 'failed',
                            logLevel: LogLevel.Error,
                            additionalData: {
                                errors,
                            },
                        });
                    }
                } else {
                    deployAndWaitForSiteLoad(deploymentResourceId, validationNotificationId, template, parameters, agentResourceId);
                }
            });
        },
        [resourceClient, deployAndWaitForSiteLoad, showRegistrationDialog, logEvent, notifications, intl]
    );

    const getRoleIdsForManagedResourceGroups = useCallback(
        async (resourceGroupIds: string[], permissionsLevel: AgentAccessLevel) => {
            const resourceGroupIdToRoleIds: Record<string, string[]> = {};

            const response = await resourceGroupClient.listResourceKindsInResourceGroups(resourceGroupIds);

            if (!response.isSuccessful) {
                logEvent({
                    action: `Failed to get resource types for resource groups: ${resourceGroupIds.join(', ')}`,
                    actionModifier: 'failed',
                    logLevel: LogLevel.Warning,
                    additionalData: {
                        resourceGroupIds,
                        error: response.error,
                    },
                });

                for (const resourceGroupId of resourceGroupIds) {
                    resourceGroupIdToRoleIds[resourceGroupId] = [...CoreRBACRoleIds];
                }
                return resourceGroupIdToRoleIds;
            }

            const resourceGroupIdToResourceTypes = response.content ?? {};
            for (const resourceGroupId of resourceGroupIds) {
                const resourceTypes = resourceGroupIdToResourceTypes[resourceGroupId] ?? [];
                const roleIds = getRoleIdsForResourceGroup(resourceTypes, permissionsLevel);
                resourceGroupIdToRoleIds[resourceGroupId] = roleIds;
            }

            return resourceGroupIdToRoleIds;
        },
        [logEvent, resourceGroupClient]
    );

    const generateTemplate = useCallback(
        async (values: SreAgentCreateFormProps, dateTime: string) => {
            const builder = new ArmTemplateBuilder();

            const userIdentityTemplate = new UserIdentityTemplateResource(builder, {
                location: getCanonicalLocation(values.location),
            });
            const workspaceTemplateResource = new WorkspaceTemplateResource(builder, getCanonicalLocation(values.location));
            const sreAgentTemplateResource = new SreAgentTemplateResource(builder, {
                mode: values.mode,
                managedResourceIds: values.managedResourceGroups.map(resourceGroup => {
                    return resourceGroup.id;
                }),
                managedResourceNames: values.managedResourceGroups.map(resourceGroup => {
                    return resourceGroup.name;
                }),
                deploymentGuid: dateTime,
                agentSpaceId: agentSpaceId || undefined,
            });

            const resourceGroupIdToRoleIds = await getRoleIdsForManagedResourceGroups(
                values.managedResourceGroups.map(rg => rg.id),
                values.permissionsLevel
            );
            const userRoleAssignmentTemplate = new UserRoleAssignmentTemplateResource(builder, {
                roleDefinitionId: RBACRoleIds.sreAgentAdministrator,
                deploymentGuid: dateTime,
            });

            // Create one RoleAssignmentTemplateResource for each resource group with its specific roles
            Object.entries(resourceGroupIdToRoleIds).forEach(([resourceGroupId, roleIds]) => {
                if (roleIds.length > 0) {
                    const parsedId = parseArmId(resourceGroupId);
                    const resourceGroupName = parsedId?.resourceGroup;
                    const subscriptionId = parsedId?.subscription;

                    if (resourceGroupName && subscriptionId) {
                        const roleAssignmentTemplateResource = new RoleAssignmentTemplateResource(builder, {
                            roleDefinitionIds: roleIds,
                            resourceGroupName: resourceGroupName,
                            subscriptionId: subscriptionId,
                            deploymentGuid: dateTime,
                        });
                        builder.addResource(roleAssignmentTemplateResource);
                    }
                }
            });

            const resourceGroupName = parseArmId(values.resourceGroupId)?.resourceGroup;
            const applicationInsightsTemplateResource = new AppInsightsTemplateResource(builder, {
                subscription: values.subscriptionId,
                resourceGroup: resourceGroupName ?? '',
                dependencyResolvers: [new AppInsightsDependencyResolver(builder, true)],
            });

            builder.addResource(applicationInsightsTemplateResource);
            builder.addResource(userIdentityTemplate);
            builder.addResource(workspaceTemplateResource);
            builder.addResource(sreAgentTemplateResource);
            builder.addResource(userRoleAssignmentTemplate);
            return builder.getTemplate();
        },
        [getRoleIdsForManagedResourceGroups, agentSpaceId]
    );

    const generateParameters = useCallback(
        (values: SreAgentCreateFormProps) => {
            const parameters: Record<string, unknown> = {};
            const resourceGroupName = parseArmId(values.resourceGroupId)?.resourceGroup;

            parameters[ArmTemplateParameterName.SubscriptionId] = {
                value: values.subscriptionId,
            };
            parameters[ArmTemplateParameterName.Location] = {
                value: getCanonicalLocation(values.location),
            };
            parameters[ArmTemplateParameterName.ResourceGroupName] = {
                value: resourceGroupName,
            };
            parameters[SreAgentParameterName.AgentName] = {
                value: values.name,
            };
            parameters[SreAgentParameterName.AccessLevel] = {
                value: values.permissionsLevel,
            };
            parameters[AppInsightsParameterName.AppInsightsRequestSource] = {
                value: 'SreAgent',
            };
            parameters[SreAgentParameterName.UserObjectId] = {
                value: user?.objectId ?? '',
            };

            return parameters;
        },
        [user?.objectId]
    );

    const createResourceGroup = useCallback(
        async (subscriptionId: string, resourceGroupName: string, location: string, resourceGroupTags: Record<string, string>) => {
            const supportedLocation = await getResourceLocation(
                TelemetrySource.SreAgentCreate,
                ResourceTypes.ResourcesProvider,
                'resourceGroups',
                location,
                subscriptionId
            );

            return resourceGroupClient.createResourceGroup(subscriptionId, resourceGroupName, supportedLocation, resourceGroupTags);
        },
        [resourceGroupClient]
    );

    const getAndCreateResourceGroup = useCallback(
        (subscriptionId: string, resourceGroupName: string, location: string) => {
            return resourceGroupClient.getResourceGroup(subscriptionId, resourceGroupName).then(resourceGroupResponse => {
                if (resourceGroupResponse.isSuccessful && resourceGroupResponse.content) {
                    // NOTE: Don't create a new RG if one already exists in the location, but update the tags
                    const updatedResourceGroup = {
                        ...resourceGroupResponse.content,
                        properties: {
                            ...resourceGroupResponse.content.properties,
                            provisioningState: undefined,
                        },
                    };

                    return resourceGroupClient.updateResourceGroup(
                        `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroupName}`,
                        updatedResourceGroup
                    );
                } else {
                    return createResourceGroup(subscriptionId, resourceGroupName, location, {});
                }
            });
        },
        [resourceGroupClient, createResourceGroup]
    );

    const onSubmit = useCallback(
        (values: SreAgentCreateFormProps) => {
            const dateTime = `${new Date().getTime()}`;
            const maxNameLength = ARM_DEPLOYMENT_NAME_LIMIT - 24;
            const safeName = values.name.length > maxNameLength ? values.name.substring(0, maxNameLength) : values.name;
            const deploymentName = `SRE-Agent-${safeName}-${dateTime}`;
            const deploymentResourceId = `${values.resourceGroupId}/providers/Microsoft.Resources/deployments/${deploymentName}`;
            const agentResourceId = `${values.resourceGroupId}/providers/Microsoft.App/agents/${values.name}`;

            logEvent({
                action: DeploymentProvisioningStates.deploymentSubmitted,
                actionModifier: 'start',
                additionalData: {
                    subscriptionId: values.subscriptionId,
                    deploymentName: deploymentName,
                    deploymentId,
                    deploymentResourceId,
                    resourceGroupId: values.resourceGroupId,
                    location: values.location,
                    isResourceGroupNew: values.isResourceGroupNew,
                    name: values.name,
                },
            });

            const notificationId = notifications.start(
                intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationTitle),
                intl.formatMessage(AgentCreateDeploymentNotificationResources.agentCreationValidationInProgress, {
                    agentName: values.name,
                })
            );

            setAgentName(values.name);
            setDeploymentId(deploymentResourceId);
            setAgentResourceId(agentResourceId);
            setIsDeploying(true);

            generateTemplate(values, dateTime).then((template: ArmTemplate) => {
                if (template) {
                    const parameters = generateParameters(values);
                    if (values.isResourceGroupNew) {
                        const resourceGroupName = parseArmId(values.resourceGroupId)?.resourceGroup ?? '';
                        getAndCreateResourceGroup(values.subscriptionId, resourceGroupName, getCanonicalLocation(values.location)).then(
                            resourceGroupResponse => {
                                if (resourceGroupResponse.isSuccessful) {
                                    return deployTemplate(
                                        deploymentResourceId,
                                        values.subscriptionId,
                                        notificationId,
                                        template,
                                        parameters,
                                        agentResourceId
                                    );
                                } else {
                                    handleFailedDeployment(
                                        deploymentName,
                                        resourceGroupResponse.error,
                                        notificationId,
                                        intl.formatMessage(AgentCreateDeploymentNotificationResources.resourceGroupFailedTitle),
                                        intl.formatMessage(AgentCreateDeploymentNotificationResources.resourceGroupFailedDescription)
                                    );
                                }
                            }
                        );
                    } else {
                        return deployTemplate(
                            deploymentResourceId,
                            values.subscriptionId,
                            notificationId,
                            template,
                            parameters,
                            agentResourceId
                        );
                    }
                } else {
                    handleFailedDeployment(
                        deploymentName,
                        {
                            code: intl.formatMessage(AgentCreateDeploymentNotificationResources.templateBuilderFailedTitle),
                            message: intl.formatMessage(AgentCreateDeploymentNotificationResources.templateBuilderFailedDescription),
                        },
                        notificationId,
                        intl.formatMessage(AgentCreateDeploymentNotificationResources.templateBuilderFailedTitle),
                        intl.formatMessage(AgentCreateDeploymentNotificationResources.templateBuilderFailedDescription)
                    );
                }
            });
        },
        [
            deployTemplate,
            deploymentId,
            generateParameters,
            generateTemplate,
            getAndCreateResourceGroup,
            handleFailedDeployment,
            logEvent,
            intl,
            notifications,
        ]
    );

    const validationSchema = useMemo(() => {
        return Yup.object().shape({
            subscriptionId: Yup.string().ensure().required(intl.formatMessage(PortalResources.fieldRequired)),
            resourceGroupId: Yup.string()
                .ensure()
                .test(
                    'deny-assignments',
                    intl.formatMessage(PortalResources.resourceGroupDenyAssignmentError),
                    async function (resourceGroupId) {
                        const permissionsResponse = await permissionsClient.getDenyAssignments(resourceGroupId);
                        const hasDenyAssignments = permissionsResponse?.content?.value?.some(
                            denyAssignment =>
                                denyAssignment.properties.scope === resourceGroupId &&
                                denyAssignment.properties.permissions.notActions.findIndex(
                                    action => action === SreAgentPermissions.deployWrite
                                ) !== -1
                        );
                        return resourceGroupId !== '' ? !hasDenyAssignments : true;
                    }
                ),
            isResourceGroupNew: Yup.boolean().notRequired(),
            name: Yup.string()
                .ensure()
                .required(intl.formatMessage(PortalResources.fieldRequired))
                .min(2, intl.formatMessage(PortalResources.sreAgentNameMinMax))
                .max(32, intl.formatMessage(PortalResources.sreAgentNameMinMax))
                .test('no-spaces', intl.formatMessage(PortalResources.sreAgentNameValidation), value => {
                    return value ? /^[a-z](?!.*--)[a-z0-9-]*[a-z0-9]$/.test(value) : true;
                })
                .test('name-unique', intl.formatMessage(PortalResources.sreAgentNameAlreadyExists), async function (value) {
                    const { resourceGroupId } = this.parent;
                    if (!value || !resourceGroupId) {
                        return true;
                    }

                    // Call getAgent to check if it exists
                    // If we get a 404 (or other error), the agent doesn't exist, so validation passes
                    const agentResourceId = `${resourceGroupId}/providers/Microsoft.App/agents/${value}`;
                    const response = await agentClient.getAgent(agentResourceId);

                    if (response.isSuccessful) {
                        return false;
                    }
                    return true;
                }),
            location: Yup.string().required(intl.formatMessage(PortalResources.fieldRequired)),
            managedResourceGroups: Yup.array().notRequired(),
            managedResourceGroupsPermissionError: Yup.boolean().notRequired(),
            managedResourceGroupsLockError: Yup.boolean().notRequired(),
            managedResourceGroupsDenyAssignmentError: Yup.boolean().notRequired(),
            managedResourceGroupsPolicyError: Yup.boolean().notRequired(),
            maxResourceGroupsError: Yup.boolean().notRequired(),
            mode: Yup.string().oneOf(Object.values(AgentMode)).notRequired(),
            permissionsLevel: Yup.string().oneOf(Object.values(AgentAccessLevel)).notRequired(),
        });
    }, [intl, permissionsClient, agentClient]);

    const initialValues = useMemo(() => {
        return {
            subscriptionId: '',
            resourceGroupId: '',
            isResourceGroupNew: false,
            name: '',
            location: agentSpaceLocation || '',
            managedResourceGroups: [],
            managedResourceGroupsPermissionError: false,
            managedResourceGroupsLockError: false,
            maxResourceGroupsError: false,
            managedResourceGroupsDenyAssignmentError: false,
            managedResourceGroupsPolicyError: false,
            mode: AgentMode.Review,
            permissionsLevel: AgentAccessLevel.low,
            agentSpaceId: agentSpaceId || '',
        };
    }, [agentSpaceId, agentSpaceLocation]);

    return {
        initialValues,
        validationSchema,
        isDeploying,
        onSubmit,
        permissionsLoading,
        setPermissionsLoading,
        deploymentResourceId: deploymentId,
        agentResourceId,
    };
};

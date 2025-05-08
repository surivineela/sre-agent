import { IColumn } from '@fluentui/react';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ArmTemplateBuilder } from '../../Common/ArmTemplateBuilder/ArmTemplateBuilder';
import {
    ArmServiceType,
    ArmTemplateParameterName,
    AzureMonitorWorkspaceParameterName,
    DataCollectionRuleParameterName,
    GrafanaParameterName,
} from '../../Common/ArmTemplateBuilder/ArmTemplateTypes';
import { AzureMonitorWorkspaceTemplateResource } from '../../Common/ArmTemplateFragments/AzureMonitorWorkspaceTemplateResource';
import { DataCollectionRuleTemplateResource } from '../../Common/ArmTemplateFragments/DataCollectionRuleTemplateResource';
import { GrafanaTemplateResource } from '../../Common/ArmTemplateFragments/GrafanaTemplateResource';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../Common/Clients/ArmClient';
import { AzureMonitorWorkspaceClient } from '../../Common/Clients/AzureMonitorWorkspaceClient';
import { DataCollectionEndpointClient } from '../../Common/Clients/DataCollectionEndpointClient';
import { DataCollectionRuleClient } from '../../Common/Clients/DataCollectionRuleClient';
import { DeploymentClient } from '../../Common/Clients/DeploymentClient';
import { GrafanaClient } from '../../Common/Clients/GrafanaClient';
import { IdentityClient } from '../../Common/Clients/IdentityClient';
import { PermissionClient } from '../../Common/Clients/PermissionsClient';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import { ProvisioningStates } from '../../Common/Constants/Arm';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { Grafana } from '../../Common/Contracts/Azure/Grafana';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { Guid } from '../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { equals } from '../../Common/Helpers/Strings';
import { SreAgentContext } from '../../Space/Contracts/Context';
import { useSreAgent } from '../../Space/Settings/Hooks/useSreAgent';
import { GrafanaDashboardResources } from '../../Strings/SREAgentResources';

const grafanaRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/22926164-76b3-42b3-bc55-97df8dab3e41';
const monitoringReaderRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/43d0d8ad-25c7-4714-9337-8ba259a9fe05';
const monitoringDataReaderRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/b0d8363b-8ddd-447d-831f-62ca05bff136';
const monitoringMetricsPublisherRole = '/providers/Microsoft.Authorization/roleDefinitions/3913510d-42f4-4e42-8a64-420c390055eb';

export function useGrafanaDashboard(resourceId: string, userPrincipalId?: string) {
    const azPortalContext = useContext(AzPortalContext);

    const {
        grafana: { isGrafanaUpdating, deploymentId, notificationId, setNotificationId, setIsGrafanaUpdating, setDeploymentId },
    } = useContext(SreAgentContext);

    const intl = useIntl();

    const { agent, agentLoaded, refresh } = useSreAgent(resourceId);

    const [isUpdating, setIsUpdating] = useState<boolean>(false);
    const [progress, setProgress] = useState(false);
    const [existingGrafanaResourceNames, setExistingGrafanaResourceNames] = useState<string[]>([]);
    const [isDirty, setIsDirty] = useState(false);
    const [newGrafanaResourceName, setNewGrafanaResourceName] = useState<string>();
    const [permissionsLoaded, setPermissionsLoaded] = useState(false);
    const [hasRbacWritePermission, setHasRbacWritePermission] = useState<boolean>(false);

    const grafanaRbacRoles = useMemo(
        () => [
            {
                role: intl.formatMessage(GrafanaDashboardResources.monitoringMetricsPublisher),
                scope: intl.formatMessage(GrafanaDashboardResources.dataCollectionRule),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.userAssignedManagedIdentity),
            },
            {
                role: intl.formatMessage(GrafanaDashboardResources.grafanaAdmin),
                scope: intl.formatMessage(GrafanaDashboardResources.azureManagedGrafana),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.user),
            },
            {
                role: intl.formatMessage(GrafanaDashboardResources.grafanaAdmin),
                scope: intl.formatMessage(GrafanaDashboardResources.azureManagedGrafana),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.userAssignedManagedIdentity),
            },
            {
                role: intl.formatMessage(GrafanaDashboardResources.monitoringReaderRole),
                scope: intl.formatMessage(GrafanaDashboardResources.subscription),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.azureManagedGrafana),
            },
            {
                role: intl.formatMessage(GrafanaDashboardResources.monitoringReaderRole),
                scope: intl.formatMessage(GrafanaDashboardResources.azureMonitorWorkspace),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.azureManagedGrafana),
            },
            {
                role: intl.formatMessage(GrafanaDashboardResources.monitoringDataReaderRole),
                scope: intl.formatMessage(GrafanaDashboardResources.azureMonitorWorkspace),
                assignedTo: intl.formatMessage(GrafanaDashboardResources.azureManagedGrafana),
            },
        ],
        []
    );

    const grafanaEndpoint = useMemo(
        () => agent?.properties?.dashboardConfiguration?.grafanaUrl,
        [agent?.properties?.dashboardConfiguration?.grafanaUrl]
    );

    const { resourceGroup, subscription } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const subscriptionId = useMemo(() => `/subscriptions/${subscription}`, [subscription]);

    const resourceGroupId = useMemo(() => `${subscriptionId}/resourceGroups/${resourceGroup}`, [subscriptionId, resourceGroup]);

    const existingManagedUserIdentityResourceId = useMemo(
        () => Object.keys(agent?.identity?.userAssignedIdentities ?? {})[0],
        [agent?.identity?.userAssignedIdentities]
    );

    const newGrafanaResourceNameErrorMessage = useMemo(() => {
        if (isDirty) {
            if (existingGrafanaResourceNames.includes(newGrafanaResourceName ?? '')) {
                return intl.formatMessage(GrafanaDashboardResources.uniqueGrafanaResourceNameError);
            }

            const name = newGrafanaResourceName ?? '';
            const isValid = name.length >= 2 && name.length <= 23 && /^[A-Za-z][A-Za-z0-9-]*[A-Za-z0-9]$/.test(name);

            if (!isValid) {
                return intl.formatMessage(GrafanaDashboardResources.invalidGrafanaResourceNameError);
            }
        }

        return undefined;
    }, [existingGrafanaResourceNames, intl, isDirty, newGrafanaResourceName]);

    const grafanaRbacColumns: IColumn[] = useMemo(
        () => [
            {
                key: 'role',
                fieldName: 'role',
                name: intl.formatMessage(GrafanaDashboardResources.role),
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
            },
            {
                key: 'scope',
                fieldName: 'scope',
                name: intl.formatMessage(GrafanaDashboardResources.scope),
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
            },
            {
                key: 'assignedTo',
                fieldName: 'assignedTo',
                name: intl.formatMessage(GrafanaDashboardResources.assignedTo),
                minWidth: 200,
                maxWidth: 300,
                isResizable: true,
            },
        ],
        [intl]
    );

    const fetchDataCollectionRuleResource = useCallback(
        async (resourceId: string) => {
            const response = await DataCollectionRuleClient.getDataCollectionRule(resourceId);
            if (response.metadata.success) {
                return response.data;
            } else {
                azPortalContext.log({
                    action: 'GetDataCollectionRuleResource',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const fetchDataCollectionEndpointResource = useCallback(
        async (resourceId: string) => {
            const response = await DataCollectionEndpointClient.getDataCollectionEndpoint(resourceId);
            if (response.metadata.success) {
                return response.data;
            } else {
                azPortalContext.log({
                    action: 'GetDataCollectionEndpointResource',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const fetchAzureMonitorWorkspaceResource = useCallback(
        async (resourceId: string) => {
            const response = await AzureMonitorWorkspaceClient.getAzureMonitorWorkspace(resourceId);

            if (response.metadata.success) {
                return response.data;
            } else {
                azPortalContext.log({
                    action: 'GetAzureMonitorWorkspaceResource',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const fetchGrafanaResource = useCallback(
        async (resourceId: string) => {
            const response = await GrafanaClient.getGrafana(resourceId);
            if (response.metadata.success) {
                return response.data;
            } else {
                azPortalContext.log({
                    action: 'GetGrafanaResource',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const fetchManagedUserIdentityResource = useCallback(async () => {
        const response = await IdentityClient.getManagedUserIdentity(existingManagedUserIdentityResourceId);
        if (response.metadata.success) {
            return response.data;
        } else {
            azPortalContext.log({
                action: 'GetManagedUserIdentityResource',
                actionModifier: 'failed',
                resourceId: existingManagedUserIdentityResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [azPortalContext, existingManagedUserIdentityResourceId]);

    const assignMonitoringMetricsPublisherRoleToDataCollectionRule = useCallback(
        async (scope: string, umiPrincipalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope,
                    principalId: umiPrincipalId,
                    roleDefinitionId: monitoringMetricsPublisherRole,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringMetricsPublisherRoleToDataCollectionRule',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext, resourceId]
    );

    const assignMonitoringReaderRoleToSubscription = useCallback(
        async (principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: subscriptionId,
                    principalId,
                    roleDefinitionId: monitoringReaderRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringReaderRoleToSubscription',
                    actionModifier: 'failed',
                    resourceId: subscriptionId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext, subscriptionId]
    );

    const assignMonitoringReaderRoleToAzureMonitorWorkspace = useCallback(
        async (scope: string, principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: scope,
                    principalId,
                    roleDefinitionId: monitoringReaderRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringReaderRoleToCurrentUser',
                    actionModifier: 'failed',
                    resourceId: scope,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const assignMonitoringDataReaderRoleToAzureMonitorWorkspace = useCallback(
        async (scope: string, principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope,
                    principalId,
                    roleDefinitionId: monitoringDataReaderRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'assignMonitoringDataReaderRoleToAzureMonitorWorkspace',
                    actionModifier: 'failed',
                    resourceId: scope,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const assignGrafanaRoleToManagedIdentity = useCallback(
        async (scope: string, principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope,
                    principalId,
                    roleDefinitionId: grafanaRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignGrafanaRoleToManagedIdentity',
                    actionModifier: 'failed',
                    resourceId: scope,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext]
    );

    const assignGrafanaRoleToCurrentUser = useCallback(
        async (scope: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope,
                    principalId: userPrincipalId,
                    roleDefinitionId: grafanaRoleDefinition,
                    principalType: 'User',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignGrafanaRoleToCurrentUser',
                    actionModifier: 'failed',
                    resourceId: scope,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azPortalContext, userPrincipalId]
    );

    const generateParameters = useCallback(() => {
        const parameters: Record<string, any> = {};

        const dateSuffix = new Date().getTime();
        const azureMonitorWorkspaceResourceName = `azure-monitor-workspace-${dateSuffix}`;

        parameters[ArmTemplateParameterName.SubscriptionId] = {
            value: subscription,
        };
        parameters[ArmTemplateParameterName.Location] = {
            value: agent?.location,
        };
        parameters[ArmTemplateParameterName.ResourceGroupName] = {
            value: resourceGroup,
        };

        parameters[GrafanaParameterName.GrafanaName] = {
            value: newGrafanaResourceName,
        };

        parameters[AzureMonitorWorkspaceParameterName.WorkspaceName] = {
            value: azureMonitorWorkspaceResourceName,
        };

        parameters[DataCollectionRuleParameterName.DataCollectionRuleName] = {
            value: azureMonitorWorkspaceResourceName,
        };

        parameters[DataCollectionRuleParameterName.AzureMonitorWorkspaceId] = {
            value: `${resourceGroupId}/providers/${ArmServiceType.AzureMonitorWorkspace}/${azureMonitorWorkspaceResourceName}`,
        };

        return parameters;
    }, [subscription, agent?.location, resourceGroup, newGrafanaResourceName, resourceGroupId]);

    const generateTemplate = useCallback(async () => {
        const builder = new ArmTemplateBuilder();

        const grafanaResource = new GrafanaTemplateResource(builder);

        const azureMonitorWorkspaceResource = new AzureMonitorWorkspaceTemplateResource(builder);

        const dataCollectionRuleResource = new DataCollectionRuleTemplateResource(builder);

        builder.addResource(azureMonitorWorkspaceResource);
        builder.addResource(dataCollectionRuleResource);
        builder.addResource(grafanaResource);

        return builder.getTemplate();
    }, []);

    const handleFailedDeployment = useCallback(
        (notificationId: string, deploymentName: string, error?: any) => {
            setProgress(false);

            const errorMessage = getErrorMessage(error);

            azPortalContext.log({
                action: 'CreateGrafanaDashboard',
                actionModifier: 'failed',
                resourceId: deploymentId,
                logLevel: 'error',
                data: {
                    error: errorMessage,
                    deploymentName,
                },
            });

            azPortalContext.stopNotification(
                notificationId,
                false,
                intl.formatMessage(GrafanaDashboardResources.grafanaCreationFailed, { errorMessage: errorMessage })
            );

            setIsUpdating(false);
            setIsGrafanaUpdating(false);
        },
        [azPortalContext, deploymentId, intl, setIsGrafanaUpdating, setIsUpdating]
    );

    const deployTemplate = useCallback(
        async (
            deploymentResourceId: string,
            deploymentName: string,
            notificationId: string,
            template: any,
            parameters: Record<string, any>
        ) => {
            const deploymentResponse = await DeploymentClient.createNewDeployment(deploymentResourceId, template, parameters, true);
            if (deploymentResponse.metadata.success) {
                if (equals(deploymentResponse.data.properties?.provisioningState ?? '', 'Failed')) {
                    handleFailedDeployment(notificationId, deploymentName, deploymentResponse.data.properties?.error);
                }
            } else {
                handleFailedDeployment(notificationId, deploymentName, deploymentResponse.metadata.error);
            }
        },
        [handleFailedDeployment]
    );

    const onLinkGrafanaDashboard = useCallback(
        async (
            notificationId: string,
            dataCollectionRuleResourceId: string,
            dataCollectionEndpointResourceId: string,
            parameters?: Record<string, any>,
            grafanaResource?: ArmObj<Grafana>
        ) => {
            const azureMonitorWorkspaceResource = await fetchAzureMonitorWorkspaceResource(parameters?.azureMonitorWorkspaceId.value);
            const dataCollectionRuleResource = await fetchDataCollectionRuleResource(dataCollectionRuleResourceId);
            const dataCollectionEndpointResource = await fetchDataCollectionEndpointResource(dataCollectionEndpointResourceId);

            const updatedAgentInfo = {
                properties: {
                    dashboardConfiguration: {
                        grafanaUrl: grafanaResource?.properties.endpoint ?? '',
                        azureMonitorWorkspaceQueryEndpoint: azureMonitorWorkspaceResource?.properties.metrics.prometheusQueryEndpoint,
                        identity: existingManagedUserIdentityResourceId,
                        azureMonitorWorkspaceMetricsIngestionEndpoint: `${dataCollectionEndpointResource?.properties.metricsIngestion.endpoint}/dataCollectionRules/${dataCollectionRuleResource?.properties.immutableId}/streams/Microsoft-PrometheusMetrics/api/v1/write?api-version=2023-04-24`,
                    },
                },
            };

            const response = await SreAgentClient.patchAgent(resourceId, updatedAgentInfo);

            if (response.metadata.success) {
                azPortalContext.stopNotification(
                    notificationId,
                    true,
                    intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardSuccess)
                );
                refresh();
            } else {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardFailed)
                );
                azPortalContext.log({
                    action: 'LinkGrafanaDashboard',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        error: response?.metadata?.error,
                    },
                });
            }
            setIsUpdating(false);
            setIsGrafanaUpdating(false);
        },
        [
            fetchAzureMonitorWorkspaceResource,
            fetchDataCollectionRuleResource,
            fetchDataCollectionEndpointResource,
            existingManagedUserIdentityResourceId,
            resourceId,
            setIsGrafanaUpdating,
            azPortalContext,
            intl,
            refresh,
            setIsUpdating,
        ]
    );

    const onCreateGrafanaDashboard = useCallback(async () => {
        setIsUpdating(true);
        setIsGrafanaUpdating(true);

        const notificationId = azPortalContext.startNotification(
            intl.formatMessage(GrafanaDashboardResources.grafanaCreationTitle),
            intl.formatMessage(GrafanaDashboardResources.grafanaCreationInProgress)
        );

        const deploymentName = `Grafana-Dashboard-${new Date().getTime()}`;
        const deploymentResourceId = `${resourceGroupId}/providers/Microsoft.Resources/deployments/${deploymentName}`;

        azPortalContext.log({
            action: 'CreateGrafanaDashboard',
            actionModifier: 'started',
            resourceId: deploymentResourceId,
            logLevel: 'info',
            data: {
                newGrafanaResourceName,
                resourceGroupId,
                subscriptionId,
                location: agent?.location,
                namePrefix: agent?.name,
            },
        });

        setNotificationId(notificationId);
        setDeploymentId(deploymentResourceId);
        setProgress(true);

        const template = await generateTemplate();

        if (template) {
            const parameters = generateParameters();
            return deployTemplate(deploymentResourceId, deploymentName, notificationId, template, parameters);
        }
    }, [
        setIsUpdating,
        setIsGrafanaUpdating,
        azPortalContext,
        intl,
        resourceGroupId,
        newGrafanaResourceName,
        subscriptionId,
        agent?.location,
        agent?.name,
        setDeploymentId,
        setNotificationId,
        generateTemplate,
        generateParameters,
        deployTemplate,
    ]);

    const fetchPermissions = useCallback(async () => {
        const response = await PermissionClient.getInstance().hasPermission(resourceGroupId, [PermissionActions.RbacWrite]);
        setHasRbacWritePermission(response);
        setPermissionsLoaded(true);
    }, [resourceGroupId]);

    useEffect(() => {
        if ((progress || isGrafanaUpdating) && deploymentId && notificationId) {
            const fetchDeploymentStatus = async () => {
                const response = await DeploymentClient.getDeployment(deploymentId);
                const provisioningState = response?.data?.properties?.provisioningState || '';
                if (provisioningState === ProvisioningStates.succeeded) {
                    clearInterval(intervalId);
                    setProgress(false);
                    setIsGrafanaUpdating(false);

                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(GrafanaDashboardResources.grafanaCreationSuccess)
                    );
                    const linkingNotificationId = azPortalContext.startNotification(
                        intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardTitle),
                        intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardInProgress)
                    );

                    const grafanaResourceId = `${resourceGroupId}/providers/${ArmServiceType.DashboardGrafana}/${response.data.properties?.parameters?.grafanaName.value}`;

                    const grafanaResource = await fetchGrafanaResource(grafanaResourceId);
                    const umiResource = await fetchManagedUserIdentityResource();

                    const grafanaPrincipalId = grafanaResource?.identity?.principalId ?? '';
                    const azureMonitorWorkspaceName = response.data.properties?.parameters?.workspaceName.value;
                    const azureMonitorWorkspaceId = response.data.properties?.parameters?.azureMonitorWorkspaceId.value;
                    const umiPrincipalId = umiResource?.properties?.principalId ?? '';
                    const dataCollectionResourceGroupId = `${subscriptionId}/resourceGroups/MA_${azureMonitorWorkspaceName}_${agent?.location}_managed`;
                    const dataCollectionRuleResourceId = `${dataCollectionResourceGroupId}/providers/${ArmServiceType.DataCollectionRule}/${response.data.properties?.parameters?.dataCollectionRuleName.value}`;
                    const dataCollectionEndpointResourceId = `${dataCollectionResourceGroupId}/providers/${ArmServiceType.DataCollectionEndpoint}/${response.data.properties?.parameters?.dataCollectionRuleName.value}`;

                    await Promise.all([
                        assignMonitoringReaderRoleToSubscription(grafanaPrincipalId),
                        assignGrafanaRoleToCurrentUser(grafanaResourceId),
                        assignGrafanaRoleToManagedIdentity(grafanaResourceId, umiPrincipalId),
                        assignMonitoringReaderRoleToAzureMonitorWorkspace(azureMonitorWorkspaceId, grafanaPrincipalId),
                        assignMonitoringDataReaderRoleToAzureMonitorWorkspace(azureMonitorWorkspaceId, grafanaPrincipalId),
                        assignMonitoringMetricsPublisherRoleToDataCollectionRule(dataCollectionRuleResourceId, umiPrincipalId),
                        onLinkGrafanaDashboard(
                            linkingNotificationId,
                            dataCollectionRuleResourceId,
                            dataCollectionEndpointResourceId,
                            response.data.properties?.parameters,
                            grafanaResource
                        ),
                    ]);
                } else if (provisioningState === ProvisioningStates.Failed) {
                    clearInterval(intervalId);
                    setProgress(false);
                    handleFailedDeployment(notificationId, deploymentId, response?.data?.properties?.error);
                }
            };

            fetchDeploymentStatus();
            const intervalId: NodeJS.Timeout = setInterval(fetchDeploymentStatus, 5000);
            return () => clearInterval(intervalId);
        }
    }, [
        agent?.location,
        subscriptionId,
        progress,
        notificationId,
        resourceGroupId,
        isGrafanaUpdating,
        isUpdating,
        setIsGrafanaUpdating,
        onLinkGrafanaDashboard,
        handleFailedDeployment,
        assignGrafanaRoleToCurrentUser,
        fetchGrafanaResource,
        assignMonitoringMetricsPublisherRoleToDataCollectionRule,
        assignMonitoringDataReaderRoleToAzureMonitorWorkspace,
        fetchManagedUserIdentityResource,
        assignMonitoringReaderRoleToAzureMonitorWorkspace,
        assignMonitoringReaderRoleToSubscription,
        assignGrafanaRoleToManagedIdentity,
        azPortalContext,
        intl,
        deploymentId,
    ]);

    useEffect(() => {
        const fetchGrafanaResources = async () => {
            const response = await GrafanaClient.getGrafanaResourcesFromArg([subscription], azPortalContext);
            if (!response?.length) {
                return;
            }
            const grafanaResourceNames: string[] = [];
            response.map(grafanaResource => {
                if (grafanaResource?.name && grafanaResource?.resourceGroupName === resourceGroup) {
                    grafanaResourceNames.push(grafanaResource.name);
                }
            });
            setExistingGrafanaResourceNames(grafanaResourceNames);
        };
        if (agentLoaded && !grafanaEndpoint) {
            fetchGrafanaResources();
        }
    }, [agentLoaded, grafanaEndpoint, azPortalContext, subscription, resourceGroup]);

    useEffect(() => {
        if (!permissionsLoaded) {
            fetchPermissions();
        }
    }, [permissionsLoaded, fetchPermissions]);

    return {
        grafanaEndpoint,
        newGrafanaResourceNameErrorMessage,
        agentLoaded,
        newGrafanaResourceName,
        hasRbacWritePermission,
        permissionsLoaded,
        grafanaRbacColumns,
        grafanaRbacRoles,
        isGrafanaUpdating,
        isUpdating,
        setNewGrafanaResourceName,
        setIsDirty,
        onCreateGrafanaDashboard,
    };
}

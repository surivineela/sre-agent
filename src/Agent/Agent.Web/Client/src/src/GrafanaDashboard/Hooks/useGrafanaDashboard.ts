import { format } from '@fluentui/react';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
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
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import { ProvisioningStates } from '../../Common/Constants/Arm';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { Grafana } from '../../Common/Contracts/Azure/Grafana';
import { Guid } from '../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { equals } from '../../Common/Helpers/Strings';
import { useSreAgent } from '../../Space/Settings/Hooks/useSreAgent';
import { GrafanaDashboardResources } from '../../Strings/SREResources.resjson';

const grafanaRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/22926164-76b3-42b3-bc55-97df8dab3e41';
const monitoringReaderRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/43d0d8ad-25c7-4714-9337-8ba259a9fe05';
const monitoringDataReaderRoleDefinition = '/providers/Microsoft.Authorization/roleDefinitions/b0d8363b-8ddd-447d-831f-62ca05bff136';
const monitoringMetricsPublisherRole = '/providers/Microsoft.Authorization/roleDefinitions/3913510d-42f4-4e42-8a64-420c390055eb';

export function useGrafanaDashboard(resourceId: string, userPrincipalId?: string) {
    const azPortalContext = useContext(AzPortalContext);

    const { agent, agentLoaded, refresh } = useSreAgent(resourceId);

    const [grafanaResourceName, setGrafanaResourceName] = useState<string>();
    const [isUpdating, setIsUpdating] = useState<boolean>(false);
    const [progress, setProgress] = useState(false);
    const [deploymentId, setDeploymentId] = useState<string>('');
    const [notificationId, setNotificationId] = useState<string>();
    const [existingGrafanaResourceNames, setExistingGrafanaResourceNames] = useState<string[]>([]);

    const grafanaEndpoint = useMemo(
        () => agent?.properties?.dashboardConfiguration?.grafanaUrl,
        [agent?.properties?.dashboardConfiguration?.grafanaUrl]
    );

    const { resourceGroup, subscription } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const subscriptionId = useMemo(() => `/subscriptions/${subscription}`, [subscription]);

    const resourceGroupId = useMemo(() => `${subscriptionId}/resourceGroups/${resourceGroup}`, [subscriptionId, resourceGroup]);

    const azureMonitorWorkspaceResourceId = useMemo(
        () => `${resourceGroupId}/providers/Microsoft.Monitor/accounts/${grafanaResourceName}`,
        [grafanaResourceName, resourceGroupId]
    );

    const existingManagedUserIdentityResourceId = useMemo(
        () => Object.keys(agent?.identity?.userAssignedIdentities ?? {})[0],
        [agent?.identity?.userAssignedIdentities]
    );

    const grafanaResourceId = useMemo(
        () => `${resourceGroupId}/providers/${ArmServiceType.DashboardGrafana}/${grafanaResourceName}`,
        [grafanaResourceName, resourceGroupId]
    );

    const dataCollectionEndpointResourceId = useMemo(() => {
        return `/subscriptions/${subscription}/resourceGroups/MA_${grafanaResourceName}_${agent?.location}_managed/providers/Microsoft.Insights/dataCollectionEndpoints/${grafanaResourceName}`;
    }, [agent?.location, subscription, grafanaResourceName]);

    const dataCollectionRuleResourceId = useMemo(() => {
        return `/subscriptions/${subscription}/resourceGroups/MA_${grafanaResourceName}_${agent?.location}_managed/providers/Microsoft.Insights/dataCollectionRules/${grafanaResourceName}`;
    }, [agent?.location, subscription, grafanaResourceName]);

    const newGrafanaResourceNameErrorMessage = useMemo(() => {
        if (existingGrafanaResourceNames.includes(grafanaResourceName ?? '')) {
            return GrafanaDashboardResources.uniqueGrafanaResourceNameError;
        }

        const name = grafanaResourceName ?? '';
        const isValid = name.length >= 2 && name.length <= 23 && /^[A-Za-z][A-Za-z0-9-]*[A-Za-z0-9]$/.test(name);

        if (!isValid) {
            return GrafanaDashboardResources.invalidGrafanaResourceNameError;
        }

        return undefined;
    }, [existingGrafanaResourceNames, grafanaResourceName]);

    const fetchDataCollectionRuleResource = useCallback(async () => {
        const response = await DataCollectionRuleClient.getDataCollectionRule(dataCollectionRuleResourceId);
        if (response.metadata.success) {
            return response.data;
        } else {
            azPortalContext.log({
                action: 'GetDataCollectionRuleResource',
                actionModifier: 'failed',
                resourceId: dataCollectionRuleResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [dataCollectionRuleResourceId]);

    const fetchDataCollectionEndpointResource = useCallback(async () => {
        const response = await DataCollectionEndpointClient.getDataCollectionEndpoint(dataCollectionEndpointResourceId);
        if (response.metadata.success) {
            return response.data;
        } else {
            azPortalContext.log({
                action: 'GetDataCollectionEndpointResource',
                actionModifier: 'failed',
                resourceId: dataCollectionEndpointResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [dataCollectionEndpointResourceId]);

    const fetchAzureMonitorWorkspaceResource = useCallback(async () => {
        const response = await AzureMonitorWorkspaceClient.getAzureMonitorWorkspace(azureMonitorWorkspaceResourceId);

        if (response.metadata.success) {
            return response.data;
        } else {
            azPortalContext.log({
                action: 'GetAzureMonitorWorkspaceResource',
                actionModifier: 'failed',
                resourceId: azureMonitorWorkspaceResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [azureMonitorWorkspaceResourceId]);

    const fetchGrafanaResource = useCallback(async () => {
        const response = await GrafanaClient.getGrafana(grafanaResourceId);
        if (response.metadata.success) {
            return response.data;
        } else {
            azPortalContext.log({
                action: 'GetGrafanaResource',
                actionModifier: 'failed',
                resourceId: grafanaResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [grafanaResourceName, grafanaResourceId]);

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
    }, [existingManagedUserIdentityResourceId]);

    const assignMonitoringMetricsPublisherRoleToCurrentUser = useCallback(
        async (umiPrincipalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: dataCollectionRuleResourceId,
                    principalId: umiPrincipalId,
                    roleDefinitionId: monitoringMetricsPublisherRole,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringMetricsPublisherRoleToCurrentUser',
                    actionModifier: 'failed',
                    resourceId: dataCollectionRuleResourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [dataCollectionRuleResourceId]
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
        [subscription]
    );

    const assignMonitoringReaderRoleToCurrentUser = useCallback(
        async (principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: `${resourceGroupId}/providers/${ArmServiceType.AzureMonitorWorkspace}/${grafanaResourceName}`,
                    principalId,
                    roleDefinitionId: monitoringReaderRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringReaderRoleToCurrentUser',
                    actionModifier: 'failed',
                    resourceId: `${resourceGroupId}/providers/${ArmServiceType.AzureMonitorWorkspace}/${grafanaResourceName}`,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [grafanaResourceName, resourceGroupId]
    );

    const assignMonitoringDataReaderRoleToCurrentUser = useCallback(
        async (principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: azureMonitorWorkspaceResourceId,
                    principalId,
                    roleDefinitionId: monitoringDataReaderRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignMonitoringDataReaderRoleToCurrentUser',
                    actionModifier: 'failed',
                    resourceId: azureMonitorWorkspaceResourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [azureMonitorWorkspaceResourceId]
    );

    const assignGrafanaRoleToManagedIdentity = useCallback(
        async (principalId: string) => {
            const response = await IdentityClient.putRoleAssignmentWithScope({
                name: Guid.newGuid(),
                properties: {
                    scope: grafanaResourceId,
                    principalId,
                    roleDefinitionId: grafanaRoleDefinition,
                    principalType: 'ServicePrincipal',
                },
            });

            if (response?.metadata.error) {
                azPortalContext.log({
                    action: 'AssignGrafanaRoleToManagedIdentity',
                    actionModifier: 'failed',
                    resourceId: grafanaResourceId,
                    logLevel: 'error',
                    data: response.metadata.error,
                });
            }
        },
        [grafanaResourceId]
    );

    const assignGrafanaRoleToCurrentUser = useCallback(async () => {
        const response = await IdentityClient.putRoleAssignmentWithScope({
            name: Guid.newGuid(),
            properties: {
                scope: grafanaResourceId,
                principalId: userPrincipalId,
                roleDefinitionId: grafanaRoleDefinition,
                principalType: 'User',
            },
        });

        if (response?.metadata.error) {
            azPortalContext.log({
                action: 'AssignGrafanaRoleToCurrentUser',
                actionModifier: 'failed',
                resourceId: grafanaResourceId,
                logLevel: 'error',
                data: response.metadata.error,
            });
        }
    }, [grafanaResourceId, userPrincipalId]);

    const generateParameters = useCallback(() => {
        const parameters: Record<string, any> = {};

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
            value: grafanaResourceName,
        };

        parameters[AzureMonitorWorkspaceParameterName.WorkspaceName] = {
            value: grafanaResourceName,
        };

        parameters[DataCollectionRuleParameterName.DataCollectionRuleName] = {
            value: grafanaResourceName,
        };

        parameters[DataCollectionRuleParameterName.AzureMonitorWorkspaceId] = {
            value: azureMonitorWorkspaceResourceId,
        };

        return parameters;
    }, [subscription, resourceGroup, agent?.location, grafanaResourceName, grafanaResourceName, azureMonitorWorkspaceResourceId]);

    const generateTemplate = useCallback(async () => {
        const builder = new ArmTemplateBuilder();

        const grafanaResource = new GrafanaTemplateResource(builder);

        const azureMonitorWorkspaceResource = new AzureMonitorWorkspaceTemplateResource(builder);

        const dataCollectionRuleResource = new DataCollectionRuleTemplateResource(builder);

        builder.addResource(grafanaResource);
        builder.addResource(azureMonitorWorkspaceResource);
        builder.addResource(dataCollectionRuleResource);

        return builder.getTemplate();
    }, []);

    const handleFailedDeployment = useCallback((notificationId: string, deploymentName: string, error?: any) => {
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

        azPortalContext.stopNotification(notificationId, false, format(GrafanaDashboardResources.grafanaCreationFailed, errorMessage));

        setIsUpdating(false);
    }, []);

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
        async (notificationId: string, grafanaResource?: ArmObj<Grafana>) => {
            const azureMonitorWorkspaceResource = await fetchAzureMonitorWorkspaceResource();
            const dataCollectionRuleResource = await fetchDataCollectionRuleResource();
            const dataCollectionEndpointResource = await fetchDataCollectionEndpointResource();

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
                azPortalContext.stopNotification(notificationId, true, GrafanaDashboardResources.grafanaCreationSuccess);
                refresh();
                setIsUpdating(false);
            } else {
                handleFailedDeployment(notificationId, deploymentId, response?.metadata?.error);
            }
        },
        [
            existingManagedUserIdentityResourceId,
            fetchAzureMonitorWorkspaceResource,
            fetchDataCollectionEndpointResource,
            fetchDataCollectionRuleResource,
            resourceId,
        ]
    );

    const onCreateGrafanaDashboard = useCallback(async () => {
        setIsUpdating(true);

        const notificationId = azPortalContext.startNotification(
            GrafanaDashboardResources.grafanaCreationTitle,
            GrafanaDashboardResources.grafanaCreationInProgress
        );

        const deploymentName = `Grafana-Dashboard-${new Date().getTime()}`;
        const deploymentResourceId = `${resourceGroupId}/providers/Microsoft.Resources/deployments/${deploymentName}`;

        azPortalContext.log({
            action: 'CreateGrafanaDashboard',
            actionModifier: 'started',
            resourceId: deploymentResourceId,
            logLevel: 'info',
            data: {
                grafanaResourceName,
                resourceGroupId,
                subscriptionId,
                location: agent?.location,
                namePrefix: agent?.name,
            },
        });

        setDeploymentId(deploymentResourceId);
        setNotificationId(notificationId);
        setProgress(true);

        const template = await generateTemplate();

        if (template) {
            const parameters = generateParameters();
            return deployTemplate(deploymentResourceId, deploymentName, notificationId, template, parameters);
        }
    }, [agent?.location, agent?.name, resourceGroup, subscription, deployTemplate, generateParameters, generateTemplate, resourceGroupId]);

    useEffect(() => {
        if (progress && deploymentId && notificationId) {
            const fetchDeploymentStatus = async () => {
                const response = await DeploymentClient.getDeployment(deploymentId);
                const provisioningState = response?.data?.properties?.provisioningState || '';
                if (provisioningState === ProvisioningStates.succeeded) {
                    clearInterval(intervalId);
                    setProgress(false);
                    const grafanaResource = await fetchGrafanaResource();
                    const umiResource = await fetchManagedUserIdentityResource();
                    await Promise.all([
                        assignMonitoringReaderRoleToSubscription(grafanaResource?.identity?.principalId ?? ''),
                        assignMonitoringReaderRoleToCurrentUser(grafanaResource?.identity?.principalId ?? ''),
                        assignMonitoringDataReaderRoleToCurrentUser(grafanaResource?.identity?.principalId ?? ''),
                        assignMonitoringMetricsPublisherRoleToCurrentUser(umiResource?.properties?.principalId ?? ''),
                        assignGrafanaRoleToManagedIdentity(umiResource?.properties?.principalId ?? ''),
                        assignGrafanaRoleToCurrentUser(),
                        onLinkGrafanaDashboard(notificationId, grafanaResource),
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
        progress,
        deploymentId,
        notificationId,
        handleFailedDeployment,
        assignGrafanaRoleToCurrentUser,
        fetchGrafanaResource,
        assignMonitoringReaderRoleToCurrentUser,
        assignMonitoringDataReaderRoleToCurrentUser,
        fetchManagedUserIdentityResource,
        assignMonitoringMetricsPublisherRoleToCurrentUser,
        assignMonitoringReaderRoleToSubscription,
        assignGrafanaRoleToManagedIdentity,
    ]);

    useEffect(() => {
        const fetchGrafanaResources = async () => {
            const response = await GrafanaClient.getGrafanaResourcesFromArg([subscription], azPortalContext);
            if (!response?.length) {
                return;
            }
            let grafanaResourceNames: string[] = [];
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
    }, [agentLoaded, grafanaEndpoint, azPortalContext, subscription]);

    return {
        grafanaEndpoint,
        grafanaResourceName,
        isUpdating,
        newGrafanaResourceNameErrorMessage,
        agentLoaded,
        onCreateGrafanaDashboard,
        setGrafanaResourceName,
    };
}

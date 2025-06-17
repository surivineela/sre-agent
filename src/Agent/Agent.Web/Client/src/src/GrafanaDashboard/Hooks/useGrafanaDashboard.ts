import { IColumn } from '@fluentui/react';
import { OptionOnSelectData, SelectionEvents } from '@fluentui/react-components';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { ArmTemplateBuilder } from '../../Common/ArmTemplateBuilder/ArmTemplateBuilder';
import {
    ArmServiceType,
    ArmTemplateParameterName,
    AzureMonitorWorkspaceParameterName,
    GrafanaParameterName,
} from '../../Common/ArmTemplateBuilder/ArmTemplateTypes';
import { AzureMonitorWorkspaceTemplateResource } from '../../Common/ArmTemplateFragments/AzureMonitorWorkspaceTemplateResource';
import { GrafanaTemplateResource } from '../../Common/ArmTemplateFragments/GrafanaTemplateResource';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../Common/Clients/ArmClient';
import { AzureMonitorWorkspaceClient } from '../../Common/Clients/AzureMonitorWorkspaceClient';
import { DataCollectionEndpointClient } from '../../Common/Clients/DataCollectionEndpointClient';
import { DataCollectionRuleClient } from '../../Common/Clients/DataCollectionRuleClient';
import { DeploymentClient } from '../../Common/Clients/DeploymentClient';
import { GrafanaClient } from '../../Common/Clients/GrafanaClient';
import { IdentityClient } from '../../Common/Clients/IdentityClient';
import { LocationClient } from '../../Common/Clients/LocationClient';
import { PermissionClient } from '../../Common/Clients/PermissionsClient';
import SreAgentClient from '../../Common/Clients/SreAgentClient';
import { ProvisioningStates } from '../../Common/Constants/Arm';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { AzureMonitorWorkspace } from '../../Common/Contracts/Azure/AzureMonitorWorkspace';
import { Grafana } from '../../Common/Contracts/Azure/Grafana';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import { Guid } from '../../Common/Helpers/Guid';
import { getCanonicalLocation, getUserFriendlyLocation, isSameLocation } from '../../Common/Helpers/LocationHelper';
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
    const [existingGrafanaResources, setExistingGrafanaResources] = useState<{ id: string; name: string }[]>([]);
    const [existingAzureMonitorWorkspaceResources, setExistingAzureMonitorWorkspaceResources] = useState<{ id: string; name: string }[]>(
        []
    );
    const [isGrafanaDirty, setIsGrafanaDirty] = useState(false);
    const [isAzureMonitorWorkspaceDirty, setIsAzureMonitorWorkspaceDirty] = useState(false);
    const [newGrafanaResourceName, setNewGrafanaResourceName] = useState<string>('');
    const [newAzureMonitorWorkspaceResourceName, setNewAzureMonitorWorkspaceResourceName] = useState<string>('');
    const [selectedGrafanaResource, setSelectedGrafanaResource] = useState<{ id: string; name: string }>({
        id: '',
        name: '',
    });
    const [selectedAzureMonitorWorkspaceResource, setSelectedAzureMonitorWorkspaceResource] = useState<{ id: string; name: string }>({
        id: '',
        name: '',
    });
    const [permissionsLoaded, setPermissionsLoaded] = useState(false);
    const [hasRbacWritePermission, setHasRbacWritePermission] = useState<boolean>(false);
    const [isGrafanaPopoverOpen, setIsGrafanaPopoverOpen] = useState<boolean>(false);
    const [isAzureMonitorWorkspacePopoverOpen, setIsAzureMonitorWorkspacePopoverOpen] = useState<boolean>(false);
    const [locationOptions, setLocationOptions] = useState<string[]>([]);
    const [selectedLocation, setSelectedLocation] = useState<string>('');

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
        [intl]
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
        if (isGrafanaDirty) {
            const filteredGrafanaResources = existingGrafanaResources.filter(resource => resource.id !== 'new');

            const doesResourceNameExist = filteredGrafanaResources.some(
                resource =>
                    resource.name === newGrafanaResourceName && new ArmResourceDescriptor(resource.id).resourceGroup === resourceGroup
            );

            if (doesResourceNameExist) {
                return intl.formatMessage(GrafanaDashboardResources.uniqueGrafanaResourceNameError);
            }

            const name = newGrafanaResourceName ?? '';
            const isValid = name.length >= 2 && name.length <= 23 && /^[A-Za-z][A-Za-z0-9-]*[A-Za-z0-9]$/.test(name);

            if (!isValid) {
                return intl.formatMessage(GrafanaDashboardResources.invalidGrafanaResourceNameError);
            }
        }

        return undefined;
    }, [existingGrafanaResources, intl, isGrafanaDirty, newGrafanaResourceName, resourceGroup]);

    const newAzureMonitorWorkspaceResourceNameErrorMessage = useMemo(() => {
        if (isAzureMonitorWorkspaceDirty) {
            const filteredAzureMonitorWorkspaceResources = existingAzureMonitorWorkspaceResources.filter(resource => resource.id !== 'new');

            const doesResourceNameExist = filteredAzureMonitorWorkspaceResources.some(
                resource =>
                    resource.name === newAzureMonitorWorkspaceResourceName &&
                    new ArmResourceDescriptor(resource.id).resourceGroup === resourceGroup
            );

            if (doesResourceNameExist) {
                return intl.formatMessage(GrafanaDashboardResources.uniqueAmwResourceNameError);
            }

            const name = newAzureMonitorWorkspaceResourceName ?? '';
            const isValid = /^[A-Za-z0-9-]{3,44}$/.test(name);

            if (!isValid) {
                return intl.formatMessage(GrafanaDashboardResources.invalidAmwResourceNameError);
            }
        }

        return undefined;
    }, [existingAzureMonitorWorkspaceResources, intl, isAzureMonitorWorkspaceDirty, newAzureMonitorWorkspaceResourceName, resourceGroup]);

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

        parameters[ArmTemplateParameterName.SubscriptionId] = {
            value: subscription,
        };
        parameters[ArmTemplateParameterName.Location] = {
            value: getCanonicalLocation(selectedLocation),
        };
        parameters[ArmTemplateParameterName.ResourceGroupName] = {
            value: resourceGroup,
        };

        if (selectedGrafanaResource.id === 'new') {
            parameters[GrafanaParameterName.GrafanaName] = {
                value: selectedGrafanaResource.name,
            };
        }

        if (selectedAzureMonitorWorkspaceResource.id === 'new') {
            parameters[AzureMonitorWorkspaceParameterName.WorkspaceName] = {
                value: selectedAzureMonitorWorkspaceResource.name,
            };
        }

        return parameters;
    }, [
        subscription,
        selectedLocation,
        resourceGroup,
        selectedGrafanaResource.id,
        selectedGrafanaResource.name,
        selectedAzureMonitorWorkspaceResource.id,
        selectedAzureMonitorWorkspaceResource.name,
    ]);

    const generateTemplate = useCallback(async () => {
        const builder = new ArmTemplateBuilder();

        if (selectedGrafanaResource.id === 'new') {
            const grafanaResource = new GrafanaTemplateResource(builder);
            builder.addResource(grafanaResource);
        }

        if (selectedAzureMonitorWorkspaceResource.id === 'new') {
            const azureMonitorWorkspaceResource = new AzureMonitorWorkspaceTemplateResource(builder);
            builder.addResource(azureMonitorWorkspaceResource);
        }

        return builder.getTemplate();
    }, [selectedAzureMonitorWorkspaceResource.id, selectedGrafanaResource.id]);

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
                intl.formatMessage(GrafanaDashboardResources.grafanaCreationFailed, { errorMessage })
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
            azureMonitorWorkspaceResource?: ArmObj<AzureMonitorWorkspace>,
            grafanaResource?: ArmObj<Grafana>
        ) => {
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

    const handlePostDeployment = useCallback(async () => {
        const linkingNotificationId = azPortalContext.startNotification(
            intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardTitle),
            intl.formatMessage(GrafanaDashboardResources.linkGrafanaDashboardInProgress)
        );

        const grafanaResourceId =
            selectedGrafanaResource.id === 'new'
                ? `${resourceGroupId}/providers/${ArmServiceType.DashboardGrafana}/${selectedGrafanaResource.name}`
                : selectedGrafanaResource.id;
        const grafanaResource = await fetchGrafanaResource(grafanaResourceId);
        const grafanaPrincipalId = grafanaResource?.identity?.principalId ?? '';

        const umiResource = await fetchManagedUserIdentityResource();
        const umiPrincipalId = umiResource?.properties?.principalId ?? '';

        const azureMonitorWorkspaceId =
            selectedAzureMonitorWorkspaceResource.id === 'new'
                ? `${resourceGroupId}/providers/${ArmServiceType.AzureMonitorWorkspace}/${selectedAzureMonitorWorkspaceResource.name}`
                : selectedAzureMonitorWorkspaceResource.id;
        const azureMonitorWorkspaceResource = await fetchAzureMonitorWorkspaceResource(azureMonitorWorkspaceId);
        const dataCollectionRuleResourceId =
            azureMonitorWorkspaceResource?.properties.defaultIngestionSettings.dataCollectionRuleResourceId || '';
        const dataCollectionEndpointResourceId =
            azureMonitorWorkspaceResource?.properties.defaultIngestionSettings.dataCollectionEndpointResourceId || '';

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
                azureMonitorWorkspaceResource,
                grafanaResource
            ),
        ]);
    }, [
        assignGrafanaRoleToCurrentUser,
        assignGrafanaRoleToManagedIdentity,
        assignMonitoringDataReaderRoleToAzureMonitorWorkspace,
        assignMonitoringMetricsPublisherRoleToDataCollectionRule,
        assignMonitoringReaderRoleToAzureMonitorWorkspace,
        assignMonitoringReaderRoleToSubscription,
        azPortalContext,
        fetchAzureMonitorWorkspaceResource,
        fetchGrafanaResource,
        fetchManagedUserIdentityResource,
        intl,
        onLinkGrafanaDashboard,
        resourceGroupId,
        selectedAzureMonitorWorkspaceResource.id,
        selectedAzureMonitorWorkspaceResource.name,
        selectedGrafanaResource.id,
        selectedGrafanaResource.name,
    ]);

    const onCreateGrafanaDashboard = useCallback(async () => {
        setIsUpdating(true);
        setIsGrafanaUpdating(true);

        if (selectedAzureMonitorWorkspaceResource.id === 'new' || selectedGrafanaResource.id === 'new') {
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
                    resourceName: selectedGrafanaResource.name,
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
        } else {
            handlePostDeployment();
        }
    }, [
        setIsGrafanaUpdating,
        selectedAzureMonitorWorkspaceResource.id,
        selectedGrafanaResource.id,
        selectedGrafanaResource.name,
        azPortalContext,
        intl,
        resourceGroupId,
        subscriptionId,
        agent?.location,
        agent?.name,
        setNotificationId,
        setDeploymentId,
        generateTemplate,
        generateParameters,
        deployTemplate,
        handlePostDeployment,
    ]);

    const fetchPermissions = useCallback(async () => {
        const response = await PermissionClient.getInstance().hasPermission(resourceGroupId, [PermissionActions.RbacWrite]);
        setHasRbacWritePermission(response);
        setPermissionsLoaded(true);
    }, [resourceGroupId]);

    const onAddNewGrafanaResourceName = useCallback(() => {
        const editedGrafanaResourceList = existingGrafanaResources.filter(resource => resource.id !== 'new');
        const newGrafanaResource = {
            id: 'new',
            name: newGrafanaResourceName,
        };
        editedGrafanaResourceList.push(newGrafanaResource);
        setSelectedGrafanaResource(newGrafanaResource);
        setExistingGrafanaResources(editedGrafanaResourceList);
        setIsGrafanaDirty(false);
        setNewGrafanaResourceName('');
        setIsGrafanaPopoverOpen(false);
    }, [existingGrafanaResources, newGrafanaResourceName]);

    const onAddNewAzureMonitorWorkspaceName = useCallback(() => {
        const editedAzureMonitorWorkspaceResourceList = existingAzureMonitorWorkspaceResources.filter(resource => resource.id !== 'new');
        const newAzureMonitorWorkspaceResource = {
            id: 'new',
            name: newAzureMonitorWorkspaceResourceName,
        };
        editedAzureMonitorWorkspaceResourceList.push(newAzureMonitorWorkspaceResource);
        setSelectedAzureMonitorWorkspaceResource(newAzureMonitorWorkspaceResource);
        setExistingAzureMonitorWorkspaceResources(editedAzureMonitorWorkspaceResourceList);
        setIsAzureMonitorWorkspaceDirty(false);
        setNewAzureMonitorWorkspaceResourceName('');
        setIsAzureMonitorWorkspacePopoverOpen(false);
    }, [existingAzureMonitorWorkspaceResources, newAzureMonitorWorkspaceResourceName]);

    const onGrafanaOptionSelect = useCallback(
        (_event: SelectionEvents, option: OptionOnSelectData) => {
            const grafanaResource = existingGrafanaResources.find(resource => resource.name === option.optionValue);
            if (grafanaResource) {
                setSelectedGrafanaResource({
                    id: grafanaResource.id ?? '',
                    name: grafanaResource.name ?? '',
                });
            }
        },
        [existingGrafanaResources]
    );

    const onAzureMonitorWorkspaceOptionSelect = useCallback(
        (_event: SelectionEvents, option: OptionOnSelectData) => {
            const azureMonitorWorkspaceResource = existingAzureMonitorWorkspaceResources.find(
                resource => resource.name === option.optionValue
            );
            if (azureMonitorWorkspaceResource) {
                setSelectedAzureMonitorWorkspaceResource({
                    id: azureMonitorWorkspaceResource.id ?? '',
                    name: azureMonitorWorkspaceResource.name ?? '',
                });
            }
        },
        [existingAzureMonitorWorkspaceResources]
    );

    const fetchLocationOptions = useCallback(async () => {
        const monitorResponse = await LocationClient.getLocForResTypes(subscription, 'Microsoft.Monitor');
        const dashboardResponse = await LocationClient.getLocForResTypes(subscription, 'Microsoft.Dashboard');
        if (monitorResponse.metadata.success && dashboardResponse.metadata.success) {
            const accountsResource = monitorResponse.data.resourceTypes.find(resourceType => resourceType.resourceType === 'accounts');
            const dashboardResource = dashboardResponse.data.resourceTypes.find(resourceType => resourceType.resourceType === 'grafana');
            const commonLocations = accountsResource?.locations?.filter(location => dashboardResource?.locations?.includes(location));
            if (commonLocations) {
                setLocationOptions(commonLocations);
                if (commonLocations.find(location => isSameLocation(location, agent?.location))) {
                    setSelectedLocation(getUserFriendlyLocation(agent?.location || ''));
                }
            }
        } else {
            azPortalContext.log({
                action: 'GetLocationOptions',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
            });
        }
    }, [agent?.location, azPortalContext, resourceId, subscription]);

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

                    handlePostDeployment();
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
        selectedGrafanaResource.id,
        selectedGrafanaResource.name,
        selectedAzureMonitorWorkspaceResource.id,
        selectedAzureMonitorWorkspaceResource.name,
        fetchAzureMonitorWorkspaceResource,
        handlePostDeployment,
    ]);

    useEffect(() => {
        const fetchGrafanaResources = async () => {
            const response = await GrafanaClient.getGrafanaResourcesFromArg([subscription], azPortalContext);
            if (!response?.length) {
                return;
            }
            const grafanaResources: { id: string; name: string }[] = [];
            response.map(grafanaResource => {
                if (grafanaResource?.name) {
                    grafanaResources.push({ id: grafanaResource.id, name: grafanaResource.name });
                }
            });
            setExistingGrafanaResources(grafanaResources);
        };
        if (agentLoaded && !grafanaEndpoint) {
            fetchGrafanaResources();
        }
    }, [agentLoaded, grafanaEndpoint, azPortalContext, subscription, resourceGroup]);

    useEffect(() => {
        const fetchAzureMonitorWorkspaceResources = async () => {
            const response = await AzureMonitorWorkspaceClient.getAzureMonitorWorkspaceResourcesFromArg([subscription], azPortalContext);
            if (!response?.length) {
                return;
            }
            const azureMonitorWorkspaceResources: { id: string; name: string }[] = [];
            response.map(azureMonitorWorkspaceResource => {
                if (azureMonitorWorkspaceResource?.name) {
                    azureMonitorWorkspaceResources.push({ id: azureMonitorWorkspaceResource.id, name: azureMonitorWorkspaceResource.name });
                }
            });
            setExistingAzureMonitorWorkspaceResources(azureMonitorWorkspaceResources);
        };
        fetchAzureMonitorWorkspaceResources();
    }, [agentLoaded, grafanaEndpoint, azPortalContext, subscription, resourceGroup]);

    useEffect(() => {
        if (!permissionsLoaded) {
            fetchPermissions();
        }
    }, [permissionsLoaded, fetchPermissions]);

    useEffect(() => {
        fetchLocationOptions();
    }, [fetchLocationOptions]);

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
        existingGrafanaResources,
        isGrafanaPopoverOpen,
        selectedGrafanaResource,
        newAzureMonitorWorkspaceResourceNameErrorMessage,
        isAzureMonitorWorkspacePopoverOpen,
        newAzureMonitorWorkspaceResourceName,
        selectedAzureMonitorWorkspaceResource,
        existingAzureMonitorWorkspaceResources,
        locationOptions,
        selectedLocation,
        setSelectedLocation,
        setSelectedAzureMonitorWorkspaceResource,
        setNewAzureMonitorWorkspaceResourceName,
        setIsAzureMonitorWorkspacePopoverOpen,
        onAzureMonitorWorkspaceOptionSelect,
        onAddNewAzureMonitorWorkspaceName,
        onGrafanaOptionSelect,
        setSelectedGrafanaResource,
        setIsGrafanaPopoverOpen,
        onAddNewGrafanaResourceName,
        setNewGrafanaResourceName,
        setIsGrafanaDirty,
        setIsAzureMonitorWorkspaceDirty,
        onCreateGrafanaDashboard,
    };
}

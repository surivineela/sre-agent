import { IColumn, Link, Selection } from '@fluentui/react';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { ArmTemplateBuilder } from '../../../Common/ArmTemplateBuilder/ArmTemplateBuilder';
import {
    ARM_DEPLOYMENT_NAME_LIMIT,
    ArmTemplateParameterName,
    SreAgentParameterName,
} from '../../../Common/ArmTemplateBuilder/ArmTemplateTypes';
import { RoleAssignmentTemplateResource } from '../../../Common/ArmTemplateFragments/RoleAssignmentTemplateResource';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { DeploymentClient } from '../../../Common/Clients/DeploymentClient';
import { IdentityClient } from '../../../Common/Clients/IdentityClient';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ProvisioningStates } from '../../../Common/Constants/Arm';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { CoreRBACRoleIds, getRoleIdsForResourceGroup } from '../../../Common/Contracts/Azure/Permission';
import { AgentAccessLevel } from '../../../Common/Contracts/Azure/SreAgent';
import { getUserFriendlyLocation } from '../../../Common/Helpers/LocationHelper';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { ManagedResourcesStringResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { Identity } from '../../Contracts/Identity';
import { useManagedResourcesStyles } from '../Styles/ManagedResources.styles';
import { getSubscriptionId, ResourceGroup, useResourceGroups } from './useResourceGroups';
import { useSreAgent } from './useSreAgent';
import { Subscription, useSubscriptions } from './useSubscriptions';

export enum PermissionPrincipalType {
    servicePrincipal = 'ServicePrincipal',
}

export interface Location {
    displayName: string;
    name: string;
    id?: string;
    regionalDisplayName?: string;
    type?: 'EdgeZone' | 'Region';
}

export function useManagedResources(resourceId: string, portalContext: AzPortalProxy) {
    const { agentLoaded, agent, refresh } = useSreAgent(resourceId);
    const { agent: agentContext } = useContext(SreAgentContext);
    const { accessLevel } = agentContext;
    const styles = useManagedResourcesStyles();
    const intl = useIntl();

    const subscriptionId = useMemo(() => getSubscriptionId(resourceId), [resourceId]);

    const subscriptionIds = useMemo(() => {
        const subIds =
            agent?.properties.knowledgeGraphConfiguration?.managedResources?.map(rg => {
                return getSubscriptionId(rg);
            }) || [];
        return Array.from(new Set(subIds));
    }, [agent]);

    const { subscriptionsList, subscriptionsLoading, subscriptionOptions } = useSubscriptions();
    const { resourceGroupsList, resourceGroupsLoading } = useResourceGroups(subscriptionIds, portalContext);

    const [selectedSubscriptions, setSelectedSubscriptions] = useState<Subscription[]>([]);
    const [selectedLocations, setSelectedLocations] = useState<Location[]>([]);
    const [searchText, setSearchText] = useState<string>('');
    const [locationsList, setLocationsList] = useState<string[]>();
    const [selectedResourceGroups, setSelectedResourceGroups] = useState<ResourceGroup[]>([]);
    const [showDeleteConfirmationDialog, setShowDeleteConfirmationDialog] = useState(false);
    const [isUpdating, setIsUpdating] = useState(false);
    const [hideResourceGroupPicker, setHideResourceGroupPicker] = useState(true);
    const [deploymentId, setDeploymentId] = useState<string>('');
    const [notification, setNotification] = useState<string>('');
    const [numberOfRgs, setNumberOfRgs] = useState<number>(0);

    const selection = useRef(
        new Selection({
            onSelectionChanged: () => {
                setSelectedResourceGroups(selection.current.getSelection() as ResourceGroup[]);
            },
        })
    );

    const isDeleteDisabled = useMemo(() => selectedResourceGroups.length === 0 || isUpdating, [isUpdating, selectedResourceGroups.length]);

    const managedResourceGroupIds = useMemo(() => agent?.properties.knowledgeGraphConfiguration?.managedResources || [], [agent]);

    const managedResourceGroups = useMemo(() => {
        return resourceGroupsList?.filter(resourceGroup => {
            const isManagedResource = managedResourceGroupIds.includes(resourceGroup.id);
            const hasSelectedSubscriptions = selectedSubscriptions.length !== 0;
            const hasSelectedLocations = selectedLocations.length !== 0;

            const locationMatches = hasSelectedLocations
                ? selectedLocations.some(location => getUserFriendlyLocation(resourceGroup.location) === location.displayName)
                : true;

            const matchesSearchText = searchText ? resourceGroup.name.toLowerCase().includes(searchText.toLowerCase()) : true;

            if (!hasSelectedSubscriptions || !hasSelectedLocations) {
                return isManagedResource && locationMatches && matchesSearchText;
            }

            return (
                isManagedResource &&
                locationMatches &&
                matchesSearchText &&
                selectedSubscriptions.some(subscription => subscription.subscriptionId === getSubscriptionId(resourceGroup.id))
            );
        });
    }, [managedResourceGroupIds, resourceGroupsList, selectedSubscriptions, selectedLocations, searchText]);

    const isLoading = useMemo(
        () => resourceGroupsLoading || !agentLoaded || subscriptionsLoading || !locationsList || !subscriptionsList,
        [agentLoaded, locationsList, resourceGroupsLoading, subscriptionsList, subscriptionsLoading]
    );

    const agentResourceGroupId = useMemo(() => {
        if (!agent?.id) return '';

        const descriptor = new ArmResourceDescriptor(agent.id);
        return `/subscriptions/${descriptor.subscription}/resourceGroups/${descriptor.resourceGroup}`;
    }, [agent]);

    const agentName = useMemo(() => {
        return agent?.name || '';
    }, [agent]);

    const openResourceOverviewBlade = useCallback(
        (id: string) => {
            if (id) {
                portalContext.openBlade({
                    extension: 'HubsExtension',
                    detailBlade: 'ResourceMenuBlade',
                    detailBladeInputs: {
                        id,
                    },
                });
            }
        },
        [portalContext]
    );

    const columns: IColumn[] = useMemo(
        () => [
            {
                key: 'name',
                name: `${intl.formatMessage(ManagedResourcesStringResources.resourceGroup)}`,
                minWidth: 300,
                maxWidth: 500,
                isResizable: true,
                onRender: (item: ResourceGroup) => (
                    <div className={styles.statusRow}>
                        <img src="./ResourceGroup.svg" alt="ResourceGroup" style={{ height: 16, width: 16 }} />
                        <Link
                            style={{ overflow: 'hidden', textOverflow: 'ellipsis', flexShrink: 5 }}
                            onClick={_e => openResourceOverviewBlade(item.id)}
                        >
                            {item.name}
                        </Link>
                    </div>
                ),
            },
            {
                key: 'subscription',
                name: `${intl.formatMessage(ManagedResourcesStringResources.subscription)}`,
                minWidth: 200,
                maxWidth: 500,
                isResizable: true,
                onRender: (item: ResourceGroup) => {
                    const subscriptionId = getSubscriptionId(item.id);
                    const subscription = subscriptionsList?.find(subscription => subscription.subscriptionId === subscriptionId);
                    return (
                        <Link
                            style={{ overflow: 'hidden', textOverflow: 'ellipsis', flexShrink: 5 }}
                            onClick={_e => openResourceOverviewBlade(item.id.split('/resource')[0])}
                        >
                            {subscription?.displayName ?? ''}
                        </Link>
                    );
                },
            },
            {
                key: 'location',
                name: `${intl.formatMessage(ManagedResourcesStringResources.region)}`,
                minWidth: 200,
                maxWidth: 400,
                isResizable: true,
                onRender: (item: ResourceGroup) => {
                    const itemLocation = getUserFriendlyLocation(item.location);
                    return itemLocation;
                },
            },
        ],
        [subscriptionsList, styles.statusRow, intl]
    );

    useEffect(() => {
        const locations = Array.from(new Set(resourceGroupsList?.map(item => item.location))) ?? [];
        const userFriendlyLocations = locations.map(loc => getUserFriendlyLocation(loc) ?? loc);
        setLocationsList(userFriendlyLocations);
    }, [resourceGroupsList]);

    const getParameters = useCallback(
        (identity: ArmObj<Identity> | undefined) => {
            const { subscription, resourceGroup } = new ArmResourceDescriptor(agent?.id ?? '');

            const parameters: Record<string, any> = {};
            parameters[ArmTemplateParameterName.SubscriptionId] = {
                value: `/subscriptions/${subscription}`,
            };
            parameters[ArmTemplateParameterName.Location] = {
                value: agent?.location || '',
            };
            parameters[ArmTemplateParameterName.ResourceGroupName] = {
                value: resourceGroup,
            };
            parameters[SreAgentParameterName.UserIdentityName] = {
                value: identity?.name || '',
            };
            return parameters;
        },
        [agent?.id, agent?.location]
    );

    const getRoleIdsForManagedResourceGroups = useCallback(
        async (resourceGroupIds: string[], accessLevel: AgentAccessLevel) => {
            const resourceGroupIdToRoleIds: Record<string, string[]> = {};

            try {
                const resourceGroupIdToResourceTypes = await ResourceGroupClient.listResourceKindsInResourceGroups(resourceGroupIds);
                for (const resourceGroupId of resourceGroupIds) {
                    const resourceTypes = resourceGroupIdToResourceTypes[resourceGroupId] ?? [];
                    const roleIds = getRoleIdsForResourceGroup(resourceTypes, accessLevel);
                    resourceGroupIdToRoleIds[resourceGroupId] = roleIds;
                }
            } catch (error) {
                portalContext.log({
                    action: 'getRoleIdsForManagedResourceGroups',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to get role IDs for managed resource groups: ${getErrorMessage(error)}`,
                    },
                });
                for (const resourceGroupId of resourceGroupIds) {
                    resourceGroupIdToRoleIds[resourceGroupId] = [...CoreRBACRoleIds];
                }
            }
            return resourceGroupIdToRoleIds;
        },
        [portalContext, resourceId]
    );

    const getTemplate = useCallback(
        async (selectedResourceGroups: ResourceGroup[], dateTime: string) => {
            const builder = new ArmTemplateBuilder();
            const resourceGroupIdToRoleIds = await getRoleIdsForManagedResourceGroups(
                (selectedResourceGroups ?? []).map(rg => rg.id),
                accessLevel
            );

            // Create one RoleAssignmentTemplateResource for each new resource group with its specific roles
            Object.entries(resourceGroupIdToRoleIds).forEach(([resourceGroupId, roleIds]) => {
                if (roleIds.length > 0) {
                    const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceGroupId);
                    if (resourceGroup && subscription) {
                        const roleAssignmentTemplateResource = new RoleAssignmentTemplateResource(builder, {
                            roleDefinitionIds: roleIds,
                            resourceGroupName: resourceGroup,
                            subscriptionId: subscription,
                            deploymentGuid: dateTime,
                        });
                        builder.addResource(roleAssignmentTemplateResource);
                    }
                }
            });
            const template = builder.getTemplate();

            return template;
        },
        [accessLevel, getRoleIdsForManagedResourceGroups]
    );

    const getDeploymentResourceId = useCallback(
        (dateTime: string) => {
            const maxNameLength = ARM_DEPLOYMENT_NAME_LIMIT - 30;
            const safeName = agentName.length > maxNameLength ? agentName.substring(0, maxNameLength) : agentName;
            const deploymentName = `${safeName}-roleAssignments-${dateTime}`;
            const deploymentResourceId = `${agentResourceGroupId}/providers/Microsoft.Resources/deployments/${deploymentName}`;
            return deploymentResourceId;
        },
        [agentName, agentResourceGroupId]
    );

    const onAddClick = useCallback(
        async (selectedResourceGroups: ResourceGroup[]) => {
            const numberOfRgs = selectedResourceGroups.length;
            setNumberOfRgs(numberOfRgs);
            setIsUpdating(true);
            setHideResourceGroupPicker(false);
            const notification = portalContext.startNotification(
                numberOfRgs > 1
                    ? intl.formatMessage(ManagedResourcesStringResources.addNotificationPluralTitle, { number: numberOfRgs })
                    : intl.formatMessage(ManagedResourcesStringResources.addNotificationTitle, { number: numberOfRgs }),
                numberOfRgs > 1
                    ? intl.formatMessage(ManagedResourcesStringResources.addNotificationPluralDescription)
                    : intl.formatMessage(ManagedResourcesStringResources.addNotificationDescription)
            );
            setNotification(notification);

            const updatedManagedResourceGroupIds = [
                ...managedResourceGroupIds,
                ...selectedResourceGroups.map(selectedResourceGroup => selectedResourceGroup.id),
            ];

            const newAgentInfo = {
                properties: {
                    knowledgeGraphConfiguration: {
                        ...agent!.properties.knowledgeGraphConfiguration,
                        managedResources: updatedManagedResourceGroupIds,
                    },
                },
            };
            const agentPromise = SreAgentClient.patchAgent(resourceId, newAgentInfo);

            const identity = await IdentityClient.getManagedUserIdentity(agent?.properties.knowledgeGraphConfiguration?.identity ?? '');

            const dateTime = `${new Date().getTime()}`;
            const template = await getTemplate(selectedResourceGroups, dateTime);
            const parameters = getParameters(identity);
            const deploymentResourceId = getDeploymentResourceId(dateTime);
            setDeploymentId(deploymentResourceId);
            const roleAssignmentsPromise = DeploymentClient.createNewDeployment(deploymentResourceId, template, parameters, true);

            const updateManagedRgPromises = await Promise.all([agentPromise, roleAssignmentsPromise]);

            const isSuccessful = updateManagedRgPromises.every((promise: any) => !promise.metadata.error);

            if (!isSuccessful) {
                setIsUpdating(false);
                setDeploymentId('');
                let errorMsg = '';
                if (!updateManagedRgPromises[0].metadata.success) {
                    const agentError = updateManagedRgPromises[0].metadata.error;
                    errorMsg = getErrorMessage(agentError);
                    portalContext.log({
                        action: 'addManagedResourceGroups',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            message: `Failed to add managed resources to SRE agent: ${errorMsg}`,
                        },
                    });
                } else {
                    const deploymentError = updateManagedRgPromises[0].metadata.error;
                    errorMsg = getErrorMessage(deploymentError);
                    portalContext.log({
                        action: 'addManagedResourceGroups',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            message: `Failed to deploy role assignments for resource groups: ${errorMsg}`,
                        },
                    });
                }
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(ManagedResourcesStringResources.addNotificationError, {
                        error: errorMsg,
                    })
                );
                refresh();
            }
        },
        [portalContext, intl, managedResourceGroupIds, agent, resourceId, getTemplate, getParameters, getDeploymentResourceId, refresh]
    );

    const onDeleteClick = useCallback(async () => {
        setIsUpdating(true);
        const notification = portalContext.startNotification(
            intl.formatMessage(ManagedResourcesStringResources.deleteNotificationTitle),
            intl.formatMessage(ManagedResourcesStringResources.deleteNotificationDescription)
        );

        const updatedManagedResourceGroupIds = managedResourceGroupIds.filter(
            resourceGroup => !selectedResourceGroups.some(selectedResourceGroup => selectedResourceGroup.id === resourceGroup)
        );

        const newAgentInfo = {
            properties: {
                knowledgeGraphConfiguration: {
                    ...agent!.properties.knowledgeGraphConfiguration,
                    managedResources: updatedManagedResourceGroupIds,
                },
            },
        };

        const response = await SreAgentClient.patchAgent(resourceId, newAgentInfo);

        if (response.metadata.success) {
            portalContext.stopNotification(
                notification,
                true,
                intl.formatMessage(ManagedResourcesStringResources.deleteNotificationSuccess)
            );
            refresh();
        } else {
            portalContext.stopNotification(
                notification,
                false,
                intl.formatMessage(ManagedResourcesStringResources.deleteNotificationError)
            );
            portalContext.log({
                action: 'deleteManagedResourceGroups',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    message: `Failed to delete managed resources: ${response.metadata.error?.Message}`,
                },
            });
        }
        setIsUpdating(false);
    }, [managedResourceGroupIds, refresh, resourceId, selectedResourceGroups, agent, portalContext, intl]);

    const pollForDeploymentCompletion = useCallback(() => {
        if (isUpdating && deploymentId) {
            const fetchDeploymentStatus = async () => {
                const response = await DeploymentClient.getDeployment(deploymentId);
                const provisioningState = response?.data?.properties?.provisioningState || '';
                if (provisioningState === ProvisioningStates.succeeded) {
                    clearInterval(intervalId);
                    setIsUpdating(false);
                    setDeploymentId('');
                    portalContext.stopNotification(
                        notification,
                        true,
                        numberOfRgs > 1
                            ? intl.formatMessage(ManagedResourcesStringResources.addNotificationPluralSuccess)
                            : intl.formatMessage(ManagedResourcesStringResources.addNotificationSuccess)
                    );

                    refresh();
                } else if (provisioningState === ProvisioningStates.Failed) {
                    clearInterval(intervalId);
                    setIsUpdating(false);
                    setDeploymentId('');
                    portalContext.log({
                        action: 'deleteManagedResourceGroups',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            message: `Failed to delete managed resources: ${response.metadata.error?.Message}`,
                        },
                    });
                    portalContext.stopNotification(
                        notification,
                        false,
                        intl.formatMessage(ManagedResourcesStringResources.addNotificationError, {
                            error: getErrorMessage(response?.data?.properties?.error),
                        })
                    );
                }
            };

            fetchDeploymentStatus();
            const intervalId: NodeJS.Timeout = setInterval(fetchDeploymentStatus, 5000);
            return () => clearInterval(intervalId);
        }
    }, [isUpdating, deploymentId, portalContext, notification, numberOfRgs, intl, refresh, resourceId]);

    useEffect(() => {
        pollForDeploymentCompletion();
    }, [pollForDeploymentCompletion]);

    return {
        managedResourceGroups,
        columns,
        isLoading,
        subscriptionsList,
        subscriptionOptions,
        selectedSubscriptions,
        locationsList,
        selectedLocations,
        searchText,
        isDeleteDisabled,
        selection,
        showDeleteConfirmationDialog,
        hideResourceGroupPicker,
        subscriptionId,
        managedResourceGroupIds,
        setHideResourceGroupPicker,
        setShowDeleteConfirmationDialog,
        setSearchText,
        setSelectedLocations,
        setSelectedSubscriptions,
        onAddClick,
        onDeleteClick,
        refresh,
        isUpdating,
    };
}

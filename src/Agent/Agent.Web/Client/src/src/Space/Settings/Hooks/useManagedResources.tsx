import { IColumn, Selection } from '@fluentui/react';
import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { IdentityClient } from '../../../Common/Clients/IdentityClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { Guid } from '../../../Common/Helpers/Guid';
import { getUserFriendlyLocation } from '../../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources } from '../../../Strings/SREAgentResources';
import { useManagedResourcesStyles } from '../Styles/ManagedResources.styles';
import { getSubscriptionId, ResourceGroup, useResourceGroups } from './useResourceGroups';
import { useSreAgent } from './useSreAgent';
import { Subscription, useSubscriptions } from './useSubscriptions';

export enum PermissionIds {
    owner = '8e3af657-a8ff-443c-a75c-2fe8c4bcb635',
    contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c',
    reader = 'acdd72a7-3385-48ef-bd42-f606fba81ae7',
    monitoringContributor = '749f88d5-cbae-40b8-bcfc-e573ddc772fa',
}

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
        () => resourceGroupsLoading || !agentLoaded || subscriptionsLoading || !locationsList || !subscriptionsList || isUpdating,
        [agentLoaded, isUpdating, locationsList, resourceGroupsLoading, subscriptionsList, subscriptionsLoading]
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
                        {item.name}
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
                    return subscription?.displayName ?? '';
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

    const onAddClick = useCallback(
        async (selectedResourceGroups: ResourceGroup[]) => {
            const numberOfRgs = selectedResourceGroups.length;
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

            const identity = await IdentityClient.getManagedUserIdentity(agent?.properties.knowledgeGraphConfiguration?.identity ?? '');

            const agentPromise = SreAgentClient.patchAgent(resourceId, newAgentInfo);

            const resourceGroupContributorPromises = selectedResourceGroups.map(rg => {
                return IdentityClient.putRoleAssignmentWithScope({
                    name: Guid.newGuid(),
                    properties: {
                        scope: rg.id,
                        principalId: identity.data?.properties?.principalId ?? '',
                        roleDefinitionId: `${rg.id}/providers/Microsoft.Authorization/roleDefinitions/${PermissionIds.contributor}`,
                        principalType: PermissionPrincipalType.servicePrincipal,
                    },
                });
            });

            const resourceGroupMontioringContributorPromises = selectedResourceGroups.map(rg => {
                return IdentityClient.putRoleAssignmentWithScope({
                    name: Guid.newGuid(),
                    properties: {
                        scope: rg.id,
                        principalId: identity.data?.properties?.principalId ?? '',
                        roleDefinitionId: `${rg.id}/providers/Microsoft.Authorization/roleDefinitions/${PermissionIds.monitoringContributor}`,
                        principalType: PermissionPrincipalType.servicePrincipal,
                    },
                });
            });

            const updateManagedRgPromises = await Promise.all([
                agentPromise,
                ...resourceGroupContributorPromises,
                ...resourceGroupMontioringContributorPromises,
            ]);

            const isSuccessful = updateManagedRgPromises.every((promise: any) => !promise.metadata.error);

            if (isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    numberOfRgs > 1
                        ? intl.formatMessage(ManagedResourcesStringResources.addNotificationPluralSuccess)
                        : intl.formatMessage(ManagedResourcesStringResources.addNotificationSuccess)
                );
                refresh();
            } else {
                if (!updateManagedRgPromises[0].metadata.success) {
                    const agentError = updateManagedRgPromises[0].metadata.error;
                    const errorMsg = getErrorMessage(agentError);
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
                    const errorPromises = updateManagedRgPromises.filter(promise => !promise.metadata.success);
                    const numberOfErrors = errorPromises.length;
                    const errorMessages = updateManagedRgPromises.map((p, i) => {
                        if (!p.metadata.success) {
                            return `${selectedResourceGroups[i - 1]?.name || ''} - "${getErrorMessage(p.metadata.error)}"`;
                        }
                    });
                    const finalErrorMessage = errorMessages.filter(Boolean).join(', ');
                    portalContext.stopNotification(
                        notification,
                        false,
                        intl.formatMessage(ManagedResourcesStringResources.addNotificationError, {
                            number: numberOfErrors,
                            error: finalErrorMessage,
                        })
                    );

                    errorPromises.forEach(errorPromise => {
                        const errorMessage = getErrorMessage(errorPromise.metadata.error);
                        portalContext.log({
                            action: 'addManagedResourceGroups',
                            actionModifier: 'failed',
                            resourceId,
                            logLevel: 'error',
                            data: {
                                message: `Failed to add role assignment for resource group to managed resources: ${errorMessage}`,
                            },
                        });
                    });
                    refresh();
                }
            }
            setIsUpdating(false);
        },
        [agent, managedResourceGroupIds, refresh, resourceId, portalContext, intl]
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
    };
}

import { useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { ResourcePickerTabResources } from '../../../Strings/SREAgentResources';

const fetchFilteredResourceGroups = (subscriptionIds: string[], portalContext: AzPortalProxy) => {
    return ResourceGroupClient.getResourceGroupsInSubscriptionWithSreAgentKinds(subscriptionIds)
        .then(resourceGroupSet => ({
            isSuccessful: true,
            content: resourceGroupSet,
            error: undefined,
        }))
        .catch(reason => {
            portalContext.log({
                action: 'getFilteredResourceGroups',
                actionModifier: 'Error',
                data: {
                    message: 'Failed to get filtered resource groups from ARG',
                },
            });
            return {
                isSuccessful: false,
                content: undefined,
                error: reason,
            };
        });
};

export const useFilteredResourceGroups = (portalContext: AzPortalProxy, subscriptionIds?: string[]) => {
    const [filteredResourceGroupSet, setFilteredResourceGroupsSet] = useState<Set<string>>();
    const [resourceGroupsLoading, setResourceGroupsLoading] = useState<boolean>(!!subscriptionIds);
    const [resourceGroupsLoadFailure, setResourceGroupsLoadFailure] = useState<string>('');
    const intl = useIntl();

    useEffect(() => {
        if (subscriptionIds && subscriptionIds.length > 0) {
            setResourceGroupsLoading(true);
            fetchFilteredResourceGroups(subscriptionIds, portalContext).then(result => {
                setFilteredResourceGroupsSet(result.content);
                setResourceGroupsLoading(false);
                setResourceGroupsLoadFailure(
                    result.isSuccessful ? '' : intl.formatMessage(ResourcePickerTabResources.failedToLoadResourceGroups)
                );
            });
        }
    }, [intl, portalContext, subscriptionIds]);

    return {
        filteredResourceGroupSet,
        resourceGroupsLoading,
        resourceGroupsLoadFailure,
    };
};

import { IDropdownOption } from '@fluentui/react/lib/Dropdown';
import { useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { ResourceGroupClient } from '../../../Common/Clients/ResourceGroupClient';
import { StringMap } from '../../../Common/Helpers/LocationHelper';
import { ManagedResourcesStringResources } from '../../../Strings/SREAgentResources';
import { ResourceGroupWithSelection } from '../ResourceGroupPicker';

export interface ResourceGroup {
    id: string;
    location: string;
    name: string;
    tags?: StringMap<string>;
    properties?: {
        provisioningState: string;
    };
    managedBy?: string;
}

export const getResourceGroupName = (resourceId: string): string => {
    const resourceGroup = resourceId.match(/\/resourceGroups\/([^/]+)/i);
    return resourceGroup ? resourceGroup[1] : '';
};

export const getSubscriptionId = (resourceId: string): string => {
    const subscription = resourceId.match(/\/subscriptions\/([^/]+)/i);
    return subscription ? subscription[1] : '';
};

export const sortDropdownOptionsFunc = (optionA: IDropdownOption, optionB: IDropdownOption) => {
    return optionA.text?.localeCompare(optionB.text);
};

const fetchResourceGroups = (subscriptions: string[], portalContext: AzPortalProxy) => {
    if (subscriptions.length == 0) {
        return Promise.resolve({
            isSuccessful: true,
            content: [],
            error: undefined,
        });
    }
    return ResourceGroupClient.getResourcesGroupsFromArg(subscriptions, portalContext)
        .then(resourceGroups => ({
            isSuccessful: true,
            content: resourceGroups,
            error: undefined,
        }))
        .catch(reason => {
            return {
                isSuccessful: false,
                content: undefined,
                error: reason,
            };
        });
};

export const useResourceGroups = (subscriptionIds: string[], portalContext: AzPortalProxy) => {
    const [resourceGroupsList, setResourceGroupsList] = useState<ResourceGroupWithSelection[]>();
    const [resourceGroupOptions, setResourceGroupOptions] = useState<IDropdownOption[]>();
    const [resourceGroupsLoading, setResourceGroupsLoading] = useState<boolean>(!!subscriptionIds);
    const [resourceGroupsLoadFailure, setResourceGroupsLoadFailure] = useState<string>('');
    const intl = useIntl();

    useEffect(() => {
        if (subscriptionIds) {
            setResourceGroupsLoading(true);
            fetchResourceGroups(subscriptionIds, portalContext).then(result => {
                const mappedRgs =
                    result.isSuccessful && result.content
                        ? result.content?.map(item => ({
                              ...item,
                              selected: false,
                          }))
                        : [];

                const rgOptions = (mappedRgs || [])
                    .map(rg => ({
                        key: rg.name,
                        text: rg.name,
                        data: rg,
                        selected: false,
                    }))
                    .sort(sortDropdownOptionsFunc);
                setResourceGroupsList(mappedRgs);
                setResourceGroupOptions(rgOptions);
                setResourceGroupsLoading(false);
                setResourceGroupsLoadFailure(
                    result.isSuccessful ? '' : intl.formatMessage(ManagedResourcesStringResources.resourceGroupsLoadFailure)
                );
            });
        }
    }, [subscriptionIds, portalContext, intl]);

    return {
        resourceGroupsList,
        resourceGroupsLoading,
        resourceGroupsLoadFailure,
        resourceGroupOptions,
    };
};

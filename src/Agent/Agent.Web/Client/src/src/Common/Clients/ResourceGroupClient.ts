import { ApiVersions } from '../ApiVersions';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { ArmResourceDescriptor } from '../Helpers/ResourceDescriptors';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class ResourceGroupClient {
    private static extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds: string[]): {
        resourceGroupNames: string[];
        subscriptionIds: string[];
    } {
        const resourceGroupNames: string[] = [];
        const subscriptionIds: string[] = [];

        resourceGroupIds.forEach(resourceGroupId => {
            const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceGroupId);
            if (subscription && resourceGroup) {
                resourceGroupNames.push(resourceGroup);
                if (!subscriptionIds.includes(subscription)) {
                    subscriptionIds.push(subscription);
                }
            }
        });

        return { resourceGroupNames, subscriptionIds };
    }

    public static getResourcesGroupsFromArg(
        subscriptions: string[],
        portalContext: AzPortalProxy,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<any[]> {
        const query = `
            resourcecontainers
            | where type == "microsoft.resources/subscriptions/resourcegroups"
            | project id, name, type, location, subscriptionId, properties
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetResourceGroupsFromArg',
        }).then((response: any) => {
            if (response?.data?.data?.rows[0]) {
                return response.data.data.rows.map((row: any[]) => {
                    return {
                        id: row[0],
                        name: row[1],
                        type: row[2],
                        location: row[3],
                        subscriptionId: row[4],
                        properties: row[5],
                    };
                });
            } else {
                portalContext.log({
                    action: 'GetResourceGroupsFromArg',
                    actionModifier: 'Error',
                });
                return [];
            }
        });
    }

    public static getResourceGroupsInSubscriptionWithSreAgentKinds(
        subscriptionIds: string[],
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ) {
        const cleanedSubscriptionIds = subscriptionIds.filter(str => str !== '');
        const query = `
            where type in~ ('microsoft.web/sites', 'microsoft.app/containerapps', 'microsoft.compute/virtualmachines', 'microsoft.containerservice/managedclusters', 'microsoft.cache/redis', 'microsoft.dbforpostgresql/flexibleservers', 'microsoft.dbforpostgresql/servers', 'microsoft.documentdb/databaseaccounts', 'microsoft.sql/servers', 'microsoft.sql/servers/databases', 'microsoft.storage/storageaccounts')
            | summarize by resourceGroup
          `;
        const content: ARGRequestContent = {
            query,
            subscriptions: cleanedSubscriptionIds,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'listResourceKindsInResourceGroups',
        }).then(response => {
            const resourceGroupsWithApps = new Set<string>();
            if (response?.data?.data?.rows[0]) {
                response.data.data.rows.forEach(row => {
                    resourceGroupsWithApps.add(row[0]);
                });
            }
            return resourceGroupsWithApps;
        });
    }

    public static listResourceKindsInResourceGroups(
        resourceGroupIds: string[],
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ) {
        const { resourceGroupNames, subscriptionIds } = this.extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds);

        const resourceGroupNamesLower = resourceGroupNames.map(name => `'${name.toLowerCase()}'`).join(', ');
        const query = `
            where type != ""
            | where tolower(resourceGroup) in~ (${resourceGroupNamesLower})
            | project type, resourceGroupId = strcat('/subscriptions/', subscriptionId, '/resourceGroups/', resourceGroup)
            | summarize by resourceGroupId, type
          `;

        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'listResourceKindsInResourceGroups',
        }).then(response => {
            const results: Record<string, string[]> = {};
            if (response?.data?.data?.rows[0]) {
                response.data.data.rows.forEach(row => {
                    const resourceGroupId = row[0];
                    const type = row[1];

                    if (!results[resourceGroupId]) {
                        results[resourceGroupId] = [];
                    }
                    results[resourceGroupId].push(type);
                });
            }
            return results;
        });
    }

    public static listAllResourcesInResourceGroups(resourceGroupIds: string[], apiVersion = ApiVersions.argQueryApiVersion20200401Preview) {
        const { resourceGroupNames, subscriptionIds } = this.extractResourceGroupNamesAndSubscriptionIds(resourceGroupIds);

        const resourceGroupNamesLower = resourceGroupNames.map(name => `'${name.toLowerCase()}'`).join(', ');
        const query = `
            where tolower(resourceGroup) in~ (${resourceGroupNamesLower})
            | summarize by type
          `;
        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'listAllResourcesInResourceGroups',
        }).then(response => {
            const resourceTypes: string[] = [];
            if (response?.data?.data?.rows[0]) {
                response.data.data.rows.forEach(row => {
                    resourceTypes.push(row[0]);
                });
            }
            return resourceTypes;
        });
    }
}

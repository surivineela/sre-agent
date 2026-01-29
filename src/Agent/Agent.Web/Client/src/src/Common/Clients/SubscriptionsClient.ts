import { Subscription } from '../../Space/Settings/Hooks/useSubscriptions';
import { ApiVersions } from '../ApiVersions';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { ResponseArray } from '../Contracts/Azure/ArmObj';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class SubscriptionsClient {
    public static getSubscriptionsFromArg(
        portalContext: AzPortalProxy,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<any[]> {
        const query = `
            resourcecontainers
            | where type == "microsoft.resources/subscriptions"
            | project id, name, type, subscriptionId, properties.subscriptionPolicies
        `;

        const content: ARGRequestContent = {
            query,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetSubscriptionsFromArg',
        }).then((response: any) => {
            if (response?.data?.data?.rows[0]) {
                return response.data.data.rows.map((row: any[]) => {
                    return {
                        id: row[0],
                        name: row[1],
                        type: row[2],
                        subscriptionId: row[3],
                        subscriptionPolicies: row[4],
                    };
                });
            } else {
                portalContext.log({
                    action: 'GetSubscriptionsFromArg',
                    actionModifier: 'Error',
                });
                return [];
            }
        });
    }

    public static getSubscriptions = (apiVersion = ApiVersions.armApiVersion20210401) => {
        return MakeArmCall<ResponseArray<Subscription>>({
            url: `/subscriptions?api-version=${apiVersion}`,
            commandName: 'getSubscriptions',
            apiVersion,
        });
    };

    /**
     * Queries Azure Resource Graph to find subscriptions with SRE Agent-compatible resources.
     */
    public static getSubscriptionsWithSreAgentResources = async (
        subscriptionIds: string[],
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<Set<string>> => {
        if (subscriptionIds.length === 0) {
            return new Set<string>();
        }

        const query = `
            where type in~ ('microsoft.web/sites', 'microsoft.app/containerapps', 'microsoft.compute/virtualmachines', 'microsoft.containerservice/managedclusters', 'microsoft.cache/redis', 'microsoft.dbforpostgresql/flexibleservers', 'microsoft.dbforpostgresql/servers', 'microsoft.documentdb/databaseaccounts', 'microsoft.sql/servers', 'microsoft.sql/servers/databases', 'microsoft.storage/storageaccounts')
            | summarize by subscriptionId
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        const response = await MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetSubscriptionsWithSreAgentResources',
        });

        const subscriptionsWithResources = new Set<string>();
        if (response?.data?.data?.rows) {
            response.data.data.rows.forEach((row: any[]) => {
                subscriptionsWithResources.add(row[0]);
            });
        }

        return subscriptionsWithResources;
    };
}

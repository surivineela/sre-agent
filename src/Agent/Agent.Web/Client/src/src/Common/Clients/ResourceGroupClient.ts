import { ApiVersions } from '../ApiVersions';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class ResourceGroupClient {
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
}

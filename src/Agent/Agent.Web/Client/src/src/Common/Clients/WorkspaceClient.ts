import { ApiVersions } from '../ApiVersions';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class WorkspaceClient {
    public static getWorkspaceFromId(
        subscriptions: string[],
        resourceGroupName: string,
        workspaceId: string,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<string | null> {
        const content = {
            query: `where isnotempty(properties) 
                          | where type =~ "Microsoft.OperationalInsights/workspaces"
                          | where resourceGroup == '${resourceGroupName}'
                          | where properties.customerId == '${workspaceId}'`,
            subscriptions: subscriptions,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetWorkspaceFromId',
        }).then((response: any) => {
            if (response && response.data?.count === 1 && response.data?.data?.rows[0]) {
                return response.data.data.rows[0][0];
            }
            return null;
        });
    }
}

import { ApiVersions } from '../ApiVersions';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { AzureMonitorWorkspace } from '../Contracts/Azure/AzureMonitorWorkspace';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class AzureMonitorWorkspaceClient {
    public static getAzureMonitorWorkspace(resourceId: string, apiVersion = ApiVersions.azureMonitorWorkspace20230403) {
        return MakeArmCall<ArmObj<AzureMonitorWorkspace>>({
            resourceId,
            commandName: 'GetAzureMonitorWorkspaceResource',
            apiVersion,
        });
    }

    public static getAzureMonitorWorkspaceResourcesFromArg(
        subscriptions: string[],
        portalContext: AzPortalProxy,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<any[]> {
        const query = `
  resources
  | where type == "microsoft.monitor/accounts"
  | project 
      id,
      name,
      resourceGroup = tostring(split(id, '/')[4]),
      properties,
      location
`;

        const content: ARGRequestContent = {
            query,
            subscriptions,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetAzureMonitorWorkspaceResourcesFromArg',
        }).then((response: any) => {
            if (response?.data?.data?.rows[0]) {
                return response.data.data.rows.map((row: any[]) => {
                    return {
                        id: row[0],
                        name: row[1],
                        resourceGroupName: row[2],
                    };
                });
            } else {
                portalContext.log({
                    action: 'GetAzureMonitorWorkspaceResourcesFromArg',
                    actionModifier: 'Error',
                    data: response?.data,
                });
                return [];
            }
        });
    }
}

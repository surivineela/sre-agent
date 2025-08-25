import { ApiVersions } from '../ApiVersions';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { Grafana } from '../Contracts/Azure/Grafana';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class GrafanaClient {
    public static getGrafana(resourceId: string, apiVersion = ApiVersions.grafana20241001) {
        return MakeArmCall<ArmObj<Grafana>>({
            resourceId,
            commandName: 'GetGrafanaResource',
            apiVersion,
        });
    }

    public static getGrafanaResourcesFromArg(
        subscriptions: string[],
        portalContext: AzPortalProxy,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<any[]> {
        const query = `
  resources
  | where type == "microsoft.dashboard/grafana"
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
            commandName: 'GetGrafanaResourcesFromArg',
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
                    action: 'GetGrafanaResourcesFromArg',
                    actionModifier: 'Error',
                    data: response?.data,
                });
                return [];
            }
        });
    }
}

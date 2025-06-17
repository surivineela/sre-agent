import { ApiVersions } from '../ApiVersions';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export class AppInsightsClient {
    public static getAppInsightsComponentFromAppId(
        subscriptions: string[],
        resourceGroupName: string,
        appId: string,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<string | null> {
        const content = {
            query: `where isnotempty(properties)
                | where type =~ "Microsoft.Insights/components"
                | where resourceGroup == '${resourceGroupName}'
                | where properties.AppId == '${appId}'
                | project id`,
            subscriptions: subscriptions,
        };

        return MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetAppInsightsComponentFromAppId',
        }).then((response: any) => {
            if (response && response.data?.count === 1 && response.data?.data?.rows[0]) {
                return response.data.data.rows[0][0];
            }
            return null;
        });
    }
}

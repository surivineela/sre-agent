import axios from 'axios';
import { ApiVersions } from '../ApiVersions';
import { AppInsightsEndpoints, AppInsightsQueryBody, AppInsightsQueryResult } from '../Contracts/Azure/AppInsights';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

const getAppInsightsQueryUrl = (appInsightsAppId: string): string => {
    return `${AppInsightsEndpoints.public}/${appInsightsAppId}/query?api-version=${ApiVersions.AppInsightsApiVersion20220615}`;
};

const getAppInsightsHeaders = (appInsightsToken: string): { [key: string]: any } => {
    return { Authorization: appInsightsToken, 'Content-Type': 'application/json' };
};

export class AppInsightsClient {
    /** https://learn.microsoft.com/en-us/rest/api/application-insights/query/execute?view=rest-application-insights-v1&tabs=HTTP */
    public static getLogQueryResults = async (appInsightsAppId: string, appInsightsToken: string, body: AppInsightsQueryBody) => {
        const headers = getAppInsightsHeaders(appInsightsToken);
        const uri = getAppInsightsQueryUrl(appInsightsAppId);

        try {
            const response = await axios.post(uri, body, { headers });

            return {
                isSuccessful: true,
                content: response.data as AppInsightsQueryResult,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

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

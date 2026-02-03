import axios from 'axios';
import { ApiVersions } from '../ApiVersions';
import { AppInsightsEndpoints, AppInsightsQueryBody, AppInsightsQueryResult } from '../Contracts/Azure/AppInsights';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

const getAppInsightsQueryUrl = (appInsightsAppId: string): string => {
    return `${AppInsightsEndpoints.public}/${appInsightsAppId}/query?api-version=${ApiVersions.AppInsightsApiVersion20220615}`;
};

const getAppInsightsHeaders = (appInsightsToken: string): { [key: string]: any } => {
    return { Authorization: `Bearer ${appInsightsToken}`, 'Content-Type': 'application/json' };
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
        } catch (e: any) {
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

    public static async getApplicationInsightsBySubscription(
        subscriptionId: string,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<{
        isSuccessful: boolean;
        data?: Array<{ id: string; name: string; resourceGroup: string; location: string }>;
        error?: any;
    }> {
        const content = {
            query: `resources
                | where type == "microsoft.insights/components"
                | where subscriptionId == "${subscriptionId}"
                | project id, name, resourceGroup, location
                | order by name asc`,
            subscriptions: [subscriptionId],
        };

        try {
            const response = await MakeArmCall<ARGResponse, ARGRequestContent>({
                method: 'POST',
                url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
                body: content,
                commandName: 'GetApplicationInsightsBySubscription',
            });

            if (response && response.data?.data?.rows) {
                const data = response.data.data.rows.map((row: any[]) => ({
                    id: row[0],
                    name: row[1],
                    resourceGroup: row[2],
                    location: row[3],
                }));
                return { isSuccessful: true, data };
            }
            return { isSuccessful: false, error: 'No data returned' };
        } catch (error) {
            return { isSuccessful: false, error };
        }
    }

    /**
     * Fetches App Insights component details by resource ID.
     * Returns appId and connectionString needed for logConfiguration.
     * https://learn.microsoft.com/en-us/rest/api/application-insights/components/get
     */
    public static async getAppInsightsComponentById(
        resourceId: string,
        apiVersion = ApiVersions.AppInsightsComponentsApiVersion20200202
    ): Promise<{
        isSuccessful: boolean;
        data?: { appId: string; connectionString: string };
        error?: any;
    }> {
        const response = await MakeArmCall<{ properties: { AppId: string; ConnectionString: string } }>({
            resourceId,
            commandName: 'GetAppInsightsComponentById',
            apiVersion,
        });

        if (response.metadata?.success && response.data?.properties) {
            return {
                isSuccessful: true,
                data: {
                    appId: response.data.properties.AppId,
                    connectionString: response.data.properties.ConnectionString,
                },
            };
        }
        return { isSuccessful: false, error: response.metadata?.error || 'Failed to fetch App Insights component' };
    }
}

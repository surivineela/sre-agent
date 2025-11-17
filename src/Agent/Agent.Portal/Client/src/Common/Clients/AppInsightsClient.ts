import { TelemetrySource } from '../Constants/Telemetry';
import { ARGRequestContent } from '../Contracts/Arg';
import { Response } from '../Contracts/Response';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export interface ApplicationInsights {
    readonly id: string;
    readonly name: string;
    readonly resourceGroup: string;
    readonly location: string;
}

export class AppInsightsClient extends Client {
    private static _instance: AppInsightsClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): AppInsightsClient {
        if (!AppInsightsClient._instance) {
            AppInsightsClient._instance = new AppInsightsClient(telemetrySource);
        }
        return AppInsightsClient._instance;
    }

    public async getApplicationInsightsBySubscription(subscriptionId: string): Promise<Response<ApplicationInsights[]>> {
        const content: ARGRequestContent = {
            query: `resources
                    | where type == 'microsoft.insights/components'
                    | where subscriptionId == '${subscriptionId}'
                    | project id, name, resourceGroup, location
                    | order by name asc`,
            subscriptions: [subscriptionId],
        };

        return this.armClient.executeArg<ApplicationInsights>(content, 'GetApplicationInsights');
    }
}

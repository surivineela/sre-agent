import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ResponseArray, Subscription } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export class SubscriptionClient extends Client {
    private static _instance: SubscriptionClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): SubscriptionClient {
        if (!SubscriptionClient._instance) {
            SubscriptionClient._instance = new SubscriptionClient(telemetrySource);
        }
        return SubscriptionClient._instance;
    }

    public async getSubscriptions(apiVersion = ApiVersions.subscriptionsApiVersion20200101): Promise<Response<Subscription[]>> {
        const response = await this.armClient.makeArmCall<ResponseArray<Subscription>>({
            method: 'GET',
            resourceId: '/subscriptions',
            apiVersion,
            commandName: 'getSubscriptions',
        });

        if (response.isSuccessful && response.content) {
            return {
                isSuccessful: true,
                content: response.content.value,
            };
        }

        return {
            isSuccessful: false,
            error: response.error,
        };
    }
}

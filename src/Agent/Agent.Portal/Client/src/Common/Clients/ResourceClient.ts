import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmObj, ResponseArray } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export interface ResourceProvider {
    id: string;
    namespace: string;
    resourceTypes: Array<{
        resourceType: string;
        locations: string[];
        apiVersions: string[];
    }>;
}

export interface Subscription {
    id: string;
    subscriptionId: string;
    displayName: string;
    state: string;
    tenantId: string;
}

export class ResourceClient extends Client {
    private static _instance: ResourceClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): ResourceClient {
        if (!ResourceClient._instance) {
            ResourceClient._instance = new ResourceClient(telemetrySource);
        }
        return ResourceClient._instance;
    }

    public async getResource<T = any>(resourceId: string, apiVersion: string): Promise<Response<ArmObj<T>>> {
        return this.armClient.makeArmCall<ArmObj<T>>({
            method: 'GET',
            resourceId,
            apiVersion,
            commandName: 'GetResource',
        });
    }

    public async deleteResource(resourceId: string, apiVersion: string): Promise<Response<void>> {
        return this.armClient.makeArmCall<void>({
            method: 'DELETE',
            resourceId,
            apiVersion,
            commandName: 'DeleteResource',
        });
    }

    public async getSubscriptions(apiVersion = ApiVersions.resourceApiVersion20200101): Promise<Response<ResponseArray<Subscription>>> {
        return this.armClient.makeArmCall<ResponseArray<Subscription>>({
            method: 'GET',
            resourceId: '/subscriptions',
            apiVersion,
            commandName: 'GetSubscriptions',
        });
    }

    public async getProvider(
        subscriptionId: string,
        resourceProvider: string,
        apiVersion = ApiVersions.resourceProviderApiVersion20220901
    ): Promise<Response<ResourceProvider>> {
        return this.armClient.makeArmCall<ResourceProvider>({
            method: 'GET',
            resourceId: `/subscriptions/${subscriptionId}/providers/${resourceProvider}`,
            apiVersion,
            commandName: 'getProvider',
        });
    }

    public async registerProvider(
        subscriptionId: string,
        resourceProvider: string,
        apiVersion = ApiVersions.resourceProviderApiVersion20220901
    ): Promise<Response<ResourceProvider>> {
        return this.armClient.makeArmCall<ResourceProvider>({
            method: 'POST',
            resourceId: `/subscriptions/${subscriptionId}/providers/${resourceProvider}/register`,
            apiVersion,
            commandName: 'registerProvider',
        });
    }
}

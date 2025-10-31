import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ResponseArray } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { format } from '../Utilities/String';
import { ArmClient } from './ArmClient';
import { Client } from './Client';
import { ResourceClient, ResourceProvider } from './ResourceClient';

const listLocationsUrl = '/subscriptions/{0}/locations';

export interface AzureLocation {
    id: string;
    name: string;
    displayName: string;
    regionalDisplayName: string;
    metadata?: {
        regionType?: string;
        regionCategory?: string;
        geography?: string;
        geographyGroup?: string;
        physicalLocation?: string;
    };
}

export class LocationClient extends Client {
    private static _instance: LocationClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): LocationClient {
        if (!LocationClient._instance) {
            LocationClient._instance = new LocationClient(telemetrySource);
        }
        return LocationClient._instance;
    }

    public async getLocationsFromArmManifest(
        subscriptionId: string,
        resourceProvider: string,
        resourceType: string,
        telemetrySource: TelemetrySource,
        getProviderArmApiVersion = ApiVersions.providerApiVersion20231101,
        registerProviderArmApiVersion = ApiVersions.providerApiVersion20231101
    ): Promise<string[]> {
        // First try a GET call on the provider.
        const resourceClient = ResourceClient.getInstance(telemetrySource);
        const providerGetResult = await resourceClient.getProvider(subscriptionId, resourceProvider, getProviderArmApiVersion);

        // If we were able to find the locations from the GET call, we return that.
        const locationsFromGetCall = this._extractLocationsFromArmManifestResult(providerGetResult, resourceType);
        if (locationsFromGetCall) {
            return locationsFromGetCall;
        }

        // Otherwise we reregister the provider and try to find the locations in the manifest that we get back.
        const providerRegisterResult = await resourceClient.registerProvider(
            subscriptionId,
            resourceProvider,
            registerProviderArmApiVersion
        );

        // If we were able to find the locations from the register call result, we return that. Otherwise we return an empty array.
        return this._extractLocationsFromArmManifestResult(providerRegisterResult, resourceType) || [];
    }

    public async listLocations(
        subscriptionId: string,
        apiVersion = ApiVersions.listLocationApiVersion20200101
    ): Promise<Response<ResponseArray<AzureLocation>>> {
        return this.armClient.makeArmCall<ResponseArray<AzureLocation>>({
            method: 'GET',
            resourceId: format(listLocationsUrl, subscriptionId),
            apiVersion,
            commandName: 'listLocations',
        });
    }

    private _extractLocationsFromArmManifestResult(manifestResult: Response<ResourceProvider>, resourceType: string): string[] | null {
        if (manifestResult.isSuccessful && manifestResult.content) {
            const providerResourceType = manifestResult.content.resourceTypes.find(
                (val: { resourceType: string; locations: string[]; apiVersions: string[] }) =>
                    val.resourceType.toLowerCase() === resourceType.toLowerCase()
            );
            if (providerResourceType) {
                return providerResourceType.locations;
            }
        }
        return null;
    }
}

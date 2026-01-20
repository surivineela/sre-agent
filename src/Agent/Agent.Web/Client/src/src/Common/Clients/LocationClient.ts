import { ApiVersions } from '../ApiVersions';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { LocForResTypes, SupportedModel } from '../Contracts/Azure/Location';
import MakeArmCall from './ArmClient';

export class LocationClient {
    public static getLocForResTypes(
        subscriptionId: string,
        resourceType: string,
        apiVersion = ApiVersions.resourceLocationApiVersion20140401
    ) {
        const url = `/subscriptions/${subscriptionId}/providers/${resourceType}?api-version=${apiVersion}`;
        return MakeArmCall<LocForResTypes>({
            url,
            commandName: 'GetLocForResTypes',
        });
    }

    public static getSupportedModels(
        subscriptionId: string,
        location: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ) {
        const url = `/subscriptions/${subscriptionId}/providers/Microsoft.App/locations/${location}/supportedModels?api-version=${apiVersion}`;
        return MakeArmCall<{ value: ArmObj<SupportedModel>[] }>({
            url,
            commandName: 'GetSupportedModels',
        });
    }
}

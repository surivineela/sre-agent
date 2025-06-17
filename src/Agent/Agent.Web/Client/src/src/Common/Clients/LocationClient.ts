import { ApiVersions } from '../ApiVersions';
import { LocForResTypes } from '../Contracts/Azure/Location';
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
}

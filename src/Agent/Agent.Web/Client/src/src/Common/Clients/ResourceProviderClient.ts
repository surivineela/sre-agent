import { ApiVersions } from '../ApiVersions';
import { ResourceProvider } from '../Contracts/Azure/ResourceProvider';
import MakeArmCall from './ArmClient';

export default class ResourceProviderClient {
    public static getProvider = (subscriptionId: string, resourceProvider: string, apiVersion = ApiVersions.ArmApiVersion20210401) => {
        return MakeArmCall<ResourceProvider>({
            resourceId: `/subscriptions/${subscriptionId}/providers/${resourceProvider}`,
            commandName: 'getProvider',
            method: 'GET',
            apiVersion,
        });
    };
}

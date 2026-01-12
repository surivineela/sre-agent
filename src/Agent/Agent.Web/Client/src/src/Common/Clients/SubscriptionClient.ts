import { ApiVersions } from '../ApiVersions';
import { Subscription } from '../Contracts/Azure/Subscription';
import MakeArmCall from './ArmClient';

export default class SubscriptionClient {
    public static getSubscription = (resourceId: string, apiVersion = ApiVersions.providerApiVersion20160901) => {
        return MakeArmCall<Subscription>({
            resourceId,
            commandName: 'getSubscription',
            apiVersion,
        });
    };

    public static getSubscriptions = (apiVersion = ApiVersions.providerApiVersion20160901) => {
        return MakeArmCall<{ value: Subscription[] }>({
            resourceId: '/subscriptions',
            commandName: 'getSubscriptions',
            apiVersion,
        });
    };
}

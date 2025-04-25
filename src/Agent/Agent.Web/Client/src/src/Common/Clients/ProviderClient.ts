import { ApiVersions } from '../ApiVersions';
import MakeArmCall from './ArmClient';

export default class Provider {
    public static registerProvider = (
        subscriptionId: string,
        provider: string,
        apiVersion = ApiVersions.providerApiVersion20160901
    ) => {

        return MakeArmCall<any>({
            resourceId: `/subscriptions/${subscriptionId}/providers/${provider}/register`,
            commandName: 'registerProvider',
            method: 'POST',
            apiVersion,
        });
    };
}
import { ApiVersions } from '../ApiVersions';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { DataCollectionRule } from '../Contracts/Azure/DataCollection';
import MakeArmCall from './ArmClient';

export class DataCollectionRuleClient {
    public static getDataCollectionRule(resourceId: string, apiVersion = ApiVersions.dataCollection20230311) {
        return MakeArmCall<ArmObj<DataCollectionRule>>({
            resourceId,
            commandName: 'GetDataCollectionRuleResource',
            apiVersion,
        });
    }
}

import { ApiVersions } from '../ApiVersions';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { DataCollectionEndpoint } from '../Contracts/Azure/DataCollection';
import MakeArmCall from './ArmClient';

export class DataCollectionEndpointClient {
    public static getDataCollectionEndpoint(resourceId: string, apiVersion = ApiVersions.dataCollection20230311) {
        return MakeArmCall<ArmObj<DataCollectionEndpoint>>({
            resourceId,
            commandName: 'GetDataCollectionEndpointResource',
            apiVersion,
        });
    }
}

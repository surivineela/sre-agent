import { ApiVersions } from '../ApiVersions';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { AzureMonitorWorkspace } from '../Contracts/Azure/AzureMonitorWorkspace';
import MakeArmCall from './ArmClient';

export class AzureMonitorWorkspaceClient {
    public static getAzureMonitorWorkspace(resourceId: string, apiVersion = ApiVersions.azureMonitorWorkspace20230403) {
        return MakeArmCall<ArmObj<AzureMonitorWorkspace>>({
            resourceId,
            commandName: 'GetAzureMonitorWorkspaceResource',
            apiVersion,
        });
    }
}

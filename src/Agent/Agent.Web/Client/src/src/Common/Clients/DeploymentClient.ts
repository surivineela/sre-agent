import { ApiVersions } from '../ApiVersions';
import { DeploymentExtended } from '../Contracts/Azure/Deployment';
import MakeArmCall from './ArmClient';

export class DeploymentClient {
    public static getDeployment(resourceId: string, apiVersion = ApiVersions.armApiVersion20210401) {
        return MakeArmCall<DeploymentExtended>({
            commandName: 'getDeployment',
            method: 'GET',
            resourceId,
            apiVersion,
        });
    }

    public static createNewDeployment(
        resourceId: string,
        template: any,
        parameters: Record<string, any>,
        skipPolling: boolean = false,
        apiVersion = ApiVersions.armApiVersion20210401
    ) {
        return MakeArmCall<any>({
            resourceId,
            commandName: 'createNewDeployment',
            method: 'PUT',
            body: {
                properties: {
                    template: template,
                    parameters: parameters,
                    mode: 'Incremental',
                },
            },
            apiVersion,
            skipPolling,
        });
    }
}

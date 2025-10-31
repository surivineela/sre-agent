import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmObj, ResponseArray } from '../Contracts/Arm';
import { ArmDeploymentOperationResponse, DeploymentExtended } from '../Contracts/Deployment';
import { Response } from '../Contracts/Response';
import { isDeploymentStateTerminal } from '../Utilities/Deployment';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export class DeploymentClient extends Client {
    private static _instance: DeploymentClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): DeploymentClient {
        if (!DeploymentClient._instance) {
            DeploymentClient._instance = new DeploymentClient(telemetrySource);
        }
        return DeploymentClient._instance;
    }

    public async createNewDeployment(
        resourceId: string,
        template: any,
        parameters: Record<string, any>,
        primaryResourceId?: string,
        skipPolling = false,
        apiVersion = ApiVersions.armApiVersion20230301
    ): Promise<Response<ArmObj<DeploymentExtended>>> {
        return this.armClient.makeArmCall<ArmObj<DeploymentExtended>>({
            method: 'PUT',
            resourceId,
            body: {
                properties: {
                    template,
                    parameters,
                    mode: 'Incremental',
                },
                tags: primaryResourceId
                    ? {
                          primaryResourceId,
                      }
                    : {},
            } as any,
            apiVersion,
            commandName: 'CreateNewDeployment',
            skipPolling,
        });
    }

    public async getDeployments(
        resourceId: string,
        apiVersion = ApiVersions.armApiVersion20230301
    ): Promise<Response<ResponseArray<ArmObj<DeploymentExtended>>>> {
        return this.armClient.makeArmCall<ResponseArray<ArmObj<DeploymentExtended>>>({
            method: 'GET',
            resourceId: `${resourceId}/deployments`,
            apiVersion,
            commandName: 'getDeployments',
        });
    }

    public async getDeployment(resourceId: string, apiVersion = ApiVersions.armApiVersion20230301): Promise<Response<DeploymentExtended>> {
        return this.armClient.makeArmCall<DeploymentExtended>({
            method: 'GET',
            resourceId,
            apiVersion,
            commandName: 'getDeployment',
        });
    }

    public async getTerminalDeploymentWithPolling(
        resourceId: string,
        apiVersion = ApiVersions.armApiVersion20230301
    ): Promise<Response<DeploymentExtended>> {
        const response = await this.getDeployment(resourceId, apiVersion);

        if (response.isSuccessful && response.content) {
            if (isDeploymentStateTerminal(response.content.properties?.provisioningState)) {
                return response;
            } else {
                await new Promise(resolve => setTimeout(resolve, 2000)); // Wait 2 seconds before polling again
                return this.getTerminalDeploymentWithPolling(resourceId, apiVersion);
            }
        } else {
            return response;
        }
    }

    public async getDeploymentOperations(
        resourceId: string,
        apiVersion = ApiVersions.armApiVersion20230301
    ): Promise<Response<ArmDeploymentOperationResponse>> {
        return this.armClient.makeArmCall<ArmDeploymentOperationResponse>({
            method: 'GET',
            resourceId: `${resourceId}/operations`,
            apiVersion,
            commandName: 'getDeploymentOperations',
        });
    }
}

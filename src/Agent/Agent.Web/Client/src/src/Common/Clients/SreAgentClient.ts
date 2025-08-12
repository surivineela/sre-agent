import { ApiVersions } from '../ApiVersions';
import { HttpResponseObject } from '../ArmHelper.types';
import { ArmArray, ArmObj } from '../Contracts/Azure/ArmObj';
import { Agent, DataConnector, ProvisioningState } from '../Contracts/Azure/SreAgent';
import MakeArmCall from './ArmClient';

export default class SreAgentClient {
    public static getAgent = (resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'getAgent',
            apiVersion,
        });
    };

    public static putAgent = (resourceId: string, agent: ArmObj<Agent>, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        const commandName = 'putAgent';
        return MakeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName,
            method: 'PUT',
            body: agent,
            apiVersion,
        }).then((response: HttpResponseObject<ArmObj<Agent>>) => {
            return this._pollOnProvisioningStateIfNeeded(resourceId, response, commandName, apiVersion);
        });
    };

    public static patchAgent = (
        resourceId: string,
        agent: Partial<ArmObj<Partial<Agent>>>,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ) => {
        const commandName = 'patchAgent';
        return MakeArmCall<ArmObj<Agent>, Partial<ArmObj<Partial<Agent>>>>({
            resourceId,
            commandName,
            method: 'PATCH',
            body: agent,
            apiVersion,
        }).then((response: HttpResponseObject<ArmObj<Agent>>) => {
            return this._pollOnProvisioningStateIfNeeded(resourceId, response, commandName, apiVersion);
        });
    };

    public static deleteAgent = (resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'deleteAgent',
            apiVersion,
            method: 'DELETE',
        });
    };

    /** subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/Microsoft.App/agents/{agentName}/DataConnectors */
    public static listDataConnectors = (agentResourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmArray<DataConnector>>({
            resourceId: `${agentResourceId}/DataConnectors`,
            commandName: 'listDataConnectors',
            apiVersion,
        });
    };

    /** subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/Microsoft.App/agents/{agentName}/DataConnectors/{dataConnectorName} */
    public static putDataConnector = async (
        resourceId: string,
        dataConnector: DataConnector,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ) => {
        return MakeArmCall<ArmObj<DataConnector>>({
            resourceId,
            commandName: 'putDataConnector',
            method: 'PUT',
            body: { properties: dataConnector } as ArmObj<DataConnector>,
            apiVersion,
        });
    };

    public static deleteDataConnector = (resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmObj<DataConnector>>({
            resourceId,
            commandName: 'deleteDataConnector',
            method: 'DELETE',
            apiVersion,
        });
    };

    /**
     * subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/Microsoft.App/agents/{agentName}/DataConnectors/ListSecrets
     *
     * Currently just returns the entire data connector object but with the dataSource present/defined
     */
    public static listDataConnectorsSecrets = (agentResourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmArray<DataConnector>>({
            method: 'POST',
            resourceId: `${agentResourceId}/DataConnectors/ListSecrets`,
            commandName: 'listDataConnectorsSecrets',
            apiVersion,
        });
    };

    // Specific data connector listSecrets: // subscriptions/{subscriptionId}/resourcegroups/{resourceGroupName}/providers/Microsoft.App/agents/{agentName}/DataConnectors/{dataConnectorName}/ListSecrets

    private static delay = (time: number) => {
        return new Promise(resolve => setTimeout(resolve, time));
    };

    private static _pollOnProvisioningStateIfNeeded = (
        resourceId: string,
        previousResponse: HttpResponseObject<ArmObj<Agent>>,
        commandName: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview,
        retriesRemaining: number = 5
    ): Promise<HttpResponseObject<ArmObj<Agent>>> => {
        const previousRequestWasOriginal = !commandName.endsWith('-pollOnProvisioningState');

        // If the original request failed, we can resolve immediately with the response.
        if (previousRequestWasOriginal && !previousResponse.metadata.success) {
            return Promise.resolve(previousResponse);
        }

        // If we're out of polling retry attempts and the previous polling attempt failed,
        // we resolve immediately with a failed state.
        if (!previousRequestWasOriginal && retriesRemaining <= 0 && !previousResponse.metadata.success) {
            return Promise.resolve({
                ...previousResponse,
                metadata: {
                    ...previousResponse.metadata,
                    success: false,
                },
            });
        }

        const provisioningState = previousResponse.data?.properties?.provisioningState;

        // If the previous response was successful and the provisioning state is a terminal state,
        // we can resolve immediately with the previous response.
        if (previousResponse.metadata.success && provisioningState !== ProvisioningState.InProgress) {
            switch (provisioningState) {
                case ProvisioningState.Succeeded:
                    return Promise.resolve(previousResponse);
                case ProvisioningState.Failed:
                default:
                    return Promise.resolve({
                        ...previousResponse,
                        metadata: {
                            ...previousResponse.metadata,
                            success: false,
                        },
                    });
            }
        }

        // If the previous response was a failed polling request or the provisioning state is still in progress, we need to poll again.
        const updatedCommandName = previousRequestWasOriginal ? `${commandName}-pollOnProvisioningState` : commandName;
        const updatedRetriesRemaining = !previousResponse.metadata.success ? retriesRemaining - 1 : undefined;
        return this.delay(2000).then(() => {
            return MakeArmCall<ArmObj<Agent>>({
                resourceId,
                commandName: updatedCommandName,
                apiVersion,
            }).then((response: HttpResponseObject<ArmObj<Agent>>) => {
                return this._pollOnProvisioningStateIfNeeded(resourceId, response, updatedCommandName, apiVersion, updatedRetriesRemaining);
            });
        });
    };
}

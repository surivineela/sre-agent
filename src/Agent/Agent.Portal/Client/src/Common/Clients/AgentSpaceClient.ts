import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { AgentSpace, AgentSpaceArgItem, AgentSpaceConnector } from '../Contracts/AgentSpace';
import { ArmObj, ResponseArray } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { AgentPowerState, SreAgentArgItem } from '../Contracts/SreAgent';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from '../Hooks/useTelemetry';
import { parseArmId } from '../Utilities/ArmId';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export class AgentSpaceClient extends Client {
    private static _instance: AgentSpaceClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): AgentSpaceClient {
        if (!AgentSpaceClient._instance) {
            AgentSpaceClient._instance = new AgentSpaceClient(telemetrySource);
        }
        return AgentSpaceClient._instance;
    }

    public async getAgentSpacesFromArg(subscriptionIds?: string[], resourceGroupNames?: string[]): Promise<Response<AgentSpaceArgItem[]>> {
        try {
            const whereConditions: string[] = ["type =~ 'microsoft.app/agentspaces'"];

            if (subscriptionIds && subscriptionIds.length > 0) {
                const subIdsList = subscriptionIds.map(id => `'${id}'`).join(', ');
                whereConditions.push(`subscriptionId in~ (${subIdsList})`);
            }

            if (resourceGroupNames && resourceGroupNames.length > 0) {
                const rgNamesList = resourceGroupNames.map(name => `'${name}'`).join(', ');
                whereConditions.push(`resourceGroup in~ (${rgNamesList})`);
            }

            const whereClause = whereConditions.join(' and ');

            const content = {
                query: `
                    Resources
                    | where ${whereClause}
                    | project id, name, location, type, subscriptionId, resourceGroup
                `,
                subscriptions: subscriptionIds,
            };

            const response = await this.armClient.executeArg<AgentSpaceArgItem>(content, 'getAgentSpacesFromArg');

            if (!response.isSuccessful) {
                logTelemetryEvent({
                    action: 'fetch-agent-spaces-from-arg',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource: this.telemetrySource,
                    additionalData: {
                        error: response.error instanceof Error ? response.error.message : String(response.error),
                    },
                });
                return response;
            }

            return {
                isSuccessful: true,
                content: response.content,
            };
        } catch (error) {
            logTelemetryEvent({
                action: 'fetch-agent-spaces-from-arg',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                telemetrySource: this.telemetrySource,
                additionalData: {
                    error: error instanceof Error ? error.message : String(error),
                },
            });
            return {
                isSuccessful: false,
                error,
            };
        }
    }

    public async getAgentSpace(
        resourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<AgentSpace>>> {
        return this.armClient.makeArmCall<ArmObj<AgentSpace>>({
            resourceId,
            commandName: 'getAgentSpace',
            apiVersion,
        });
    }

    public async deleteAgentSpace(
        resourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<void>> {
        return this.armClient.makeArmCall<void>({
            resourceId,
            commandName: 'deleteAgentSpace',
            method: 'DELETE',
            apiVersion,
        });
    }

    public async updateAgentSpace(
        resourceId: string,
        properties: Partial<AgentSpace>,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<AgentSpace>>> {
        return this.armClient.makeArmCall<ArmObj<AgentSpace>, { properties: Partial<AgentSpace> }>({
            resourceId,
            commandName: 'updateAgentSpace',
            method: 'PATCH',
            body: { properties },
            apiVersion,
        });
    }

    public async getAgentsInSpace(spaceResourceId: string): Promise<Response<SreAgentArgItem[]>> {
        try {
            const content = {
                query: `
                    resources
                    | where type == "microsoft.app/agents"
                    | where properties.agentSpaceId =~ '${spaceResourceId}'
                    | project
                        id,
                        name,
                        resourceGroup = tostring(split(id, '/')[4]),
                        properties,
                        location
                `,
            };

            const response = await this.armClient.executeArg<{
                id: string;
                name: string;
                resourceGroup: string;
                properties: { agentSpaceId?: string; powerState?: AgentPowerState };
                location: string;
            }>(content, 'getAgentsInSpace');

            if (!response.isSuccessful) {
                logTelemetryEvent({
                    action: 'get-agents-in-space',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource: this.telemetrySource,
                    additionalData: {
                        error: response.error instanceof Error ? response.error.message : String(response.error),
                    },
                });
                return {
                    isSuccessful: false,
                    error: response.error,
                };
            }

            // Map ARG response to SreAgentArgItem format
            const agents: SreAgentArgItem[] = (response.content || []).map(item => {
                const parsedId = parseArmId(item.id);

                return {
                    id: item.id,
                    name: item.name,
                    location: item.location,
                    type: 'microsoft.app/agents',
                    subscriptionId: parsedId.subscription || '',
                    resourceGroup: item.resourceGroup,
                    agentSpaceId: item.properties?.agentSpaceId,
                    powerState: item.properties?.powerState,
                };
            });

            return {
                isSuccessful: true,
                content: agents,
            };
        } catch (error) {
            logTelemetryEvent({
                action: 'get-agents-in-space',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                telemetrySource: this.telemetrySource,
                additionalData: {
                    error: error instanceof Error ? error.message : String(error),
                },
            });
            return {
                isSuccessful: false,
                error,
            };
        }
    }

    public async getConnectors(
        spaceResourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ResponseArray<ArmObj<AgentSpaceConnector>>>> {
        return this.armClient.makeArmCall<ResponseArray<ArmObj<AgentSpaceConnector>>>({
            resourceId: `${spaceResourceId}/connectors`,
            commandName: 'getConnectors',
            apiVersion,
        });
    }

    public async listConnectorSecrets(
        spaceResourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ResponseArray<ArmObj<AgentSpaceConnector>>>> {
        return this.armClient.makeArmCall<ResponseArray<ArmObj<AgentSpaceConnector>>>({
            resourceId: `${spaceResourceId}/connectors/ListSecrets`,
            commandName: 'listConnectorSecrets',
            method: 'POST',
            apiVersion,
        });
    }

    public async createOrUpdateConnector(
        spaceResourceId: string,
        connectorName: string,
        connector: AgentSpaceConnector,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<AgentSpaceConnector>>> {
        return this.armClient.makeArmCall<ArmObj<AgentSpaceConnector>, { properties: AgentSpaceConnector }>({
            resourceId: `${spaceResourceId}/connectors/${connectorName}`,
            commandName: 'createOrUpdateConnector',
            method: 'PUT',
            body: { properties: connector },
            apiVersion,
        });
    }

    public async deleteConnector(
        spaceResourceId: string,
        connectorName: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<void>> {
        return this.armClient.makeArmCall<void>({
            resourceId: `${spaceResourceId}/connectors/${connectorName}`,
            commandName: 'deleteConnector',
            method: 'DELETE',
            apiVersion,
        });
    }
}

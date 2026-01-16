import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmObj } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { Agent, SreAgentArgItem } from '../Contracts/SreAgent';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from '../Hooks/useTelemetry';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

export class SreAgentClient extends Client {
    private static _instance: SreAgentClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): SreAgentClient {
        if (!SreAgentClient._instance) {
            SreAgentClient._instance = new SreAgentClient(telemetrySource);
        }
        return SreAgentClient._instance;
    }

    public async getAgent(resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) {
        return this.armClient.makeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'getAgent',
            apiVersion,
        });
    }

    /**
     * Fetches all SRE Agent resources across subscriptions using Azure Resource Graph (ARG).
     * @param subscriptionIds - Optional array of subscription IDs to filter by. Empty or undefined means all subscriptions.
     * @param resourceGroupNames - Optional array of resource group names to filter by. Empty or undefined means all resource groups.
     * @param locationFilter - Optional location to filter agents by.
     * @param excludeAgentsInSpace - When true, excludes agents that are already assigned to an agent space.
     */
    public async getAgentsFromArg(
        subscriptionIds?: string[],
        resourceGroupNames?: string[],
        locationFilter?: string,
        excludeAgentsInSpace?: boolean
    ): Promise<Response<SreAgentArgItem[]>> {
        try {
            // Build WHERE clause with filters
            const whereConditions: string[] = ["type =~ 'microsoft.app/agents'"];

            if (subscriptionIds && subscriptionIds.length > 0) {
                const subIdsList = subscriptionIds.map(id => `'${id}'`).join(', ');
                whereConditions.push(`subscriptionId in~ (${subIdsList})`);
            }

            if (resourceGroupNames && resourceGroupNames.length > 0) {
                const rgNamesList = resourceGroupNames.map(name => `'${name}'`).join(', ');
                whereConditions.push(`resourceGroup in~ (${rgNamesList})`);
            }

            if (locationFilter) {
                whereConditions.push(`location =~ '${locationFilter}'`);
            }

            if (excludeAgentsInSpace) {
                whereConditions.push(`(isnull(properties.agentSpaceId) or properties.agentSpaceId == '')`);
            }

            const whereClause = whereConditions.join(' and ');

            const content = {
                query: `
                    Resources
                    | where ${whereClause}
                    | project id, name, location, type, subscriptionId, resourceGroup, agentSpaceId = properties.agentSpaceId, powerState = properties.powerState
                `,
                subscriptions: subscriptionIds,
            };

            const response = await this.armClient.executeArg<SreAgentArgItem>(content, 'getAgentsFromArg');

            if (!response.isSuccessful) {
                logTelemetryEvent({
                    action: 'fetch-agents-from-arg',
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
                action: 'fetch-agents-from-arg',
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

    public async deleteAgent(resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview): Promise<Response<void>> {
        return this.armClient.makeArmCall<void>({
            resourceId,
            commandName: 'deleteAgent',
            method: 'DELETE',
            apiVersion,
        });
    }

    public startAgent = async (
        resourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<Agent>>> => {
        return this.armClient.makeArmCall<ArmObj<Agent>>({
            resourceId: `${resourceId}/start`,
            commandName: 'startAgent',
            method: 'POST',
            apiVersion,
        });
    };

    public stopAgent = async (
        resourceId: string,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<Agent>>> => {
        return this.armClient.makeArmCall<ArmObj<Agent>>({
            resourceId: `${resourceId}/stop`,
            commandName: 'stopAgent',
            method: 'POST',
            apiVersion,
        });
    };

    public updateAgent = async (
        resourceId: string,
        properties: Partial<Agent>,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ): Promise<Response<ArmObj<Agent>>> => {
        return this.armClient.makeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'updateAgent',
            method: 'PATCH',
            apiVersion,
            body: { properties } as ArmObj<Agent>,
        });
    };
}

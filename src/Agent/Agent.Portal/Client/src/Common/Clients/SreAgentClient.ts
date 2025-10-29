import { getCloudEndpoints } from '../Auth/cloudConfig';
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
     * Backend handles token caching and refreshing.
     */
    public async getAgentsFromArg(): Promise<Response<SreAgentArgItem[]>> {
        const tokenResponse = await this.getAccessToken('arm');

        if (!tokenResponse.isSuccessful) {
            return {
                isSuccessful: false,
                error: tokenResponse.error,
            };
        }

        try {
            const endpoints = getCloudEndpoints();
            const argUrl = `${endpoints.arm}/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01`;

            const query = {
                query: `
                    Resources
                    | where type =~ 'microsoft.app/agents'
                    | project id, name, location, type, subscriptionId, resourceGroup
                `,
            };

            const response = await fetch(argUrl, {
                method: 'POST',
                headers: {
                    Authorization: `Bearer ${tokenResponse.content}`,
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(query),
            });

            if (!response.ok) {
                const error = new Error(`Failed to fetch agents: ${response.status} ${response.statusText}`);
                logTelemetryEvent({
                    action: 'fetch-agents-from-arg',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource: this.telemetrySource,
                    additionalData: {
                        status: response.status,
                        statusText: response.statusText,
                    },
                });
                return {
                    isSuccessful: false,
                    error,
                };
            }

            const data = await response.json();
            const agentResources: SreAgentArgItem[] =
                data.data?.map((row: SreAgentArgItem) => ({
                    id: row.id,
                    name: row.name,
                    location: row.location,
                    type: row.type,
                    subscriptionId: row.subscriptionId,
                    resourceGroup: row.resourceGroup,
                })) || [];

            return {
                isSuccessful: true,
                content: agentResources,
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
}

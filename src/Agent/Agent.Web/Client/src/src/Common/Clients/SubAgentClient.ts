import axios from 'axios';
import { SubAgent } from '../Contracts/SubAgent.ts';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export class SubAgentClient extends DataPlaneClient {
    private static _instance: SubAgentClient;

    public static getInstance(sreAgentEndpoint: string): SubAgentClient {
        if (!SubAgentClient._instance) {
            SubAgentClient._instance = new SubAgentClient(sreAgentEndpoint);
        }
        return SubAgentClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getSubAgents = async (): Promise<Response<SubAgent[]>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v1/logicAppAgents`), {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data ?? [],
            };
        } catch (e: any) {
            return {
                isSuccessful: false,
                error: e?.response?.data?.message,
            };
        }
    };

    public createSubAgent = async (options: { name: string }, signal?: AbortSignal): Promise<Response<SubAgent | undefined>> => {
        const { name } = options;

        try {
            const response = await axios.post(
                this.getRequestUrl(`/api/v1/logicAppAgents`),
                {
                    ...this.getSubAgentRequestContent(name),
                },
                {
                    headers: getAgentHeaders(),
                    signal,
                }
            );
            return {
                isSuccessful: true,
                content: response?.data,
            };
        } catch (e: any) {
            return {
                isSuccessful: false,
                error: e?.response?.data?.message,
            };
        }
    };

    private getSubAgentRequestContent = (name: string) => {
        return {
            agentName: name,
            workflowConfiguration: {
                workflowDefinition: {
                    definition: {
                        $schema:
                            'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#',
                        actions: {},
                        contentVersion: '1.0.0.0',
                        outputs: {},
                        triggers: {},
                    },
                    kind: 'Agent',
                },
                connections: {
                    managedApiConnections: {},
                },
                appsettings: {},
            },
            authType: 'API-Key', //Can be “API-Key” or “Easy-auth”
            authConfig: {}, //TBD: this will be needed for easy auth for details like clientId etc
        };
    };
}

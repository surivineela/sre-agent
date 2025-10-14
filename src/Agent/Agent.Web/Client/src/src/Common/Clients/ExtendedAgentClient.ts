import axios from 'axios';
import { ExtendedAgent, PaginatedResponse } from '../../Space/Contracts/ExtendedAgentGraph.ts';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';

export class ExtendedAgentClient extends DataPlaneClient {
    private static _instance: ExtendedAgentClient;

    public static getInstance(sreAgentEndpoint: string): ExtendedAgentClient {
        if (!ExtendedAgentClient._instance) {
            ExtendedAgentClient._instance = new ExtendedAgentClient(sreAgentEndpoint);
        }
        return ExtendedAgentClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getExtendedAgents = async (): Promise<Response<PaginatedResponse<ExtendedAgent>>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v1/extendedAgent/agents?page=1&limit=200'), {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };
}

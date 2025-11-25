import axios from 'axios';
import { ResourceSearchPagniation, ResourceSearchResult } from '../../Space/Contracts/Graph';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

export class ResourceMappingClient extends DataPlaneClient {
    private static _instance: ResourceMappingClient;

    public static getInstance(sreAgentEndpoint: string): ResourceMappingClient {
        if (!ResourceMappingClient._instance) {
            ResourceMappingClient._instance = new ResourceMappingClient(sreAgentEndpoint);
        }
        return ResourceMappingClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public async searchResource(
        name: string,
        pageIndex: number,
        pageSize = 20
    ): Promise<Response<ResourceSearchPagniation<ResourceSearchResult>>> {
        const url = this.getRequestUrl(
            `/api/v1/graph/resources/search?name=${encodeURIComponent(name)}&pageIndex=${pageIndex}&pageSize=${pageSize}`
        );
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as ResourceSearchPagniation<ResourceSearchResult>,
            };
        } catch (error) {
            const errorMessage = this.getErrorMessage(error);
            return {
                isSuccessful: false,
                error: errorMessage,
            };
        }
    }
}

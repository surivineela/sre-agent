import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

export interface UploadError {
    fileName: string;
    errorMessage: string;
}

export interface UploadResponse {
    message?: string;
    error?: string;
    detail?: UploadError[];
    uploaded?: string[];
}

export class AgentMemoryClient extends DataPlaneClient {
    private static _instance: AgentMemoryClient;

    public static getInstance(sreAgentEndpoint: string): AgentMemoryClient {
        if (!AgentMemoryClient._instance) {
            AgentMemoryClient._instance = new AgentMemoryClient(sreAgentEndpoint);
        }
        return AgentMemoryClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public uploadFiles = async (files: FormData): Promise<Response<UploadResponse>> => {
        try {
            // Get headers but exclude Content-Type for FormData uploads
            const headers = getAgentHeaders();
            delete headers['Content-Type']; // Let browser set multipart/form-data with boundary

            const response = await fetch(this.getRequestUrl('/api/v1/agentmemory/upload'), {
                method: 'POST',
                headers: headers,
                body: files,
            });

            const data = await response.json().catch(() => ({ error: 'Unknown error' }));

            if (!response.ok) {
                return {
                    isSuccessful: false,
                    error: new Error(data.error || `HTTP ${response.status}: ${response.statusText}`),
                    content: data,
                };
            }

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

import axios from 'axios';
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

export interface ListFilesRequest {
    prefix?: string;
    pageSize?: number;
    continuationToken?: string;
}

export interface ListFilesResponse {
    files: string[];
    continuationToken?: string;
}

export interface DeleteDocumentRequest {
    fileNames: string[];
}

export interface DeleteResponse {
    message: string;
    deleted?: string[];
    failed?: string[];
}

export interface AgentMemoryStatus {
    enabled: boolean;
    documentRetrievalEnabled: boolean;
    trajectoryRetrievalEnabled: boolean;
    userMemoryRetrievalEnabled: boolean;
    message: string;
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
            delete headers['Content-Type']; // Let axios set multipart/form-data with boundary

            const { data } = await axios.post(this.getRequestUrl('/api/v1/agentmemory/upload'), files, {
                headers: headers,
            });

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };

    public listFiles = async (request?: ListFilesRequest): Promise<Response<ListFilesResponse>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v1/agentmemory/files'), {
                headers: getAgentHeaders(),
                params: {
                    prefix: request?.prefix,
                    pageSize: request?.pageSize,
                    continuationToken: request?.continuationToken,
                },
            });

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };

    public deleteDocument = async (fileName: string): Promise<Response<DeleteResponse>> => {
        try {
            const { data } = await axios.delete(this.getRequestUrl(`/api/v1/agentmemory/document/${encodeURIComponent(fileName)}`), {
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
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };

    public deleteDocuments = async (fileNames: string[]): Promise<Response<DeleteResponse>> => {
        try {
            const { data } = await axios.delete(this.getRequestUrl('/api/v1/agentmemory/documents'), {
                headers: getAgentHeaders(),
                data: fileNames,
            });

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };

    public getStatus = async (): Promise<Response<AgentMemoryStatus>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v1/agentmemory/status'), {
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
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };

    public getDocumentCount = async (): Promise<Response<{ count: number }>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl('/api/v1/agentmemory/files/count'), {
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
                content: axios.isAxiosError(e) ? e.response?.data : undefined,
            };
        }
    };
}

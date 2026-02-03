import axios from 'axios';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

// Request types
export interface CreateRepositoryKnowledgeRequest {
    name: string;
    displayName: string;
    description?: string;
    url: string;
    branch?: string;
}

export interface CreateTextKnowledgeRequest {
    name: string;
    displayName: string;
    description?: string;
    content: string;
}

export interface CreateWebPageKnowledgeRequest {
    name: string;
    displayName: string;
    description?: string;
    url: string;
    content?: string;
}

export interface UpdateKnowledgeItemRequest {
    displayName?: string;
    description?: string;
}

// Response types
export interface KnowledgeItemView {
    name: string;
    displayName: string;
    description?: string;
    type: 'Text' | 'File' | 'WebPage' | 'Repository';
    fileSize: number;
    sourceUrl?: string;
    metadata: Record<string, string>;
    createdBy: string;
    createdAt: string;
    lastModifiedBy: string;
    lastModifiedAt: string;
    contentType?: string;
    contentDownloadUrl: string;
}

export interface ListKnowledgeItemsResponse {
    value: KnowledgeItemView[];
    nextLink?: string;
}

export class KnowledgeApiClient extends DataPlaneClient {
    private static _instance: KnowledgeApiClient;

    public static getInstance(sreAgentEndpoint: string): KnowledgeApiClient {
        if (!KnowledgeApiClient._instance) {
            KnowledgeApiClient._instance = new KnowledgeApiClient(sreAgentEndpoint);
        }
        return KnowledgeApiClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public createRepositoryKnowledge = async (
        request: CreateRepositoryKnowledgeRequest
    ): Promise<Response<KnowledgeItemView>> => {
        try {
            const { data } = await axios.post(this.getRequestUrl('/api/v2/knowledge/repository'), request, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data.value ?? data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public createTextKnowledge = async (request: CreateTextKnowledgeRequest): Promise<Response<KnowledgeItemView>> => {
        try {
            const { data } = await axios.post(this.getRequestUrl('/api/v2/knowledge/text'), request, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data.value ?? data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public createWebPageKnowledge = async (
        request: CreateWebPageKnowledgeRequest
    ): Promise<Response<KnowledgeItemView>> => {
        try {
            const { data } = await axios.post(this.getRequestUrl('/api/v2/knowledge/webpage'), request, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data.value ?? data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public listKnowledgeItems = async (
        type?: string,
        pageSize?: number,
        continuationToken?: string
    ): Promise<Response<ListKnowledgeItemsResponse>> => {
        try {
            const params: Record<string, string | number> = {};
            if (type) params.type = type;
            if (pageSize) params.pageSize = pageSize;
            if (continuationToken) params.continuationToken = continuationToken;

            const { data } = await axios.get(this.getRequestUrl('/api/v2/knowledge'), {
                headers: getAgentHeaders(),
                params,
            });

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public getKnowledgeItem = async (name: string): Promise<Response<KnowledgeItemView>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v2/knowledge/${encodeURIComponent(name)}`), {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public updateKnowledgeItem = async (
        name: string,
        request: UpdateKnowledgeItemRequest
    ): Promise<Response<KnowledgeItemView>> => {
        try {
            const { data } = await axios.put(
                this.getRequestUrl(`/api/v2/knowledge/${encodeURIComponent(name)}`),
                request,
                {
                    headers: getAgentHeaders(),
                }
            );

            return {
                isSuccessful: true,
                content: data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };

    public deleteKnowledgeItem = async (name: string): Promise<Response<void>> => {
        try {
            await axios.delete(this.getRequestUrl(`/api/v2/knowledge/${encodeURIComponent(name)}`), {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: this.getErrorMessage(error),
            };
        }
    };
}

export const generateKnowledgeName = (displayName: string): string => {
    const slug = displayName
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/^-|-$/g, '')
        .substring(0, 50); // Limit length
    const timestamp = Date.now().toString(36);
    return `${slug}-${timestamp}`;
};

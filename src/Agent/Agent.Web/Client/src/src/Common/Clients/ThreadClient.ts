import axios from 'axios';
import { Thread, ThreadSource } from '../Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient.ts';
import { MessagePostOptions } from './MessageClient.ts';

export enum ThreadSeverity {
    Warning = 'Warning',
    Critical = 'Critical',
}

export interface ThreadsGetFilterOptions {
    searchText?: string;
    timestamps?: {
        min?: {
            timestamp: string;
            inclusive: boolean;
        };
        max?: {
            timestamp: string;
            inclusive: boolean;
        };
    };
    source?: ThreadSource;
}

export interface ThreadsGetOptions {
    skip: number;
    top: number;
    descending: boolean;
    filters?: ThreadsGetFilterOptions;
    severity?: ThreadSeverity;
}

export const getThreadsGetUrlPath = (options: ThreadsGetOptions): string => {
    const { skip, top, descending, filters, severity } = options;

    let url = `/api/v1/threads?skip=${skip}&top=${top}&orderby=modifiedTimestamp${descending ? '+desc' : ''}`;

    if (filters) {
        const filterStrings: string[] = [];

        const { searchText, timestamps, source } = filters;

        if (searchText) {
            filterStrings.push(`contains(tolower(title),'${searchText.toLowerCase()}')`);
        }

        if (timestamps) {
            const { min, max } = timestamps;
            if (min) {
                const { timestamp, inclusive } = min;
                filterStrings.push(`modifiedTimestamp ${inclusive ? 'ge' : 'gt'} ${timestamp}`);
            }
            if (max) {
                const { timestamp, inclusive } = max;
                filterStrings.push(`modifiedTimestamp ${inclusive ? 'le' : 'lt'} ${timestamp}`);
            }
        }

        if (source) {
            filterStrings.push(`source eq '${source}'`);
        }

        const filterString = filterStrings.join(' and ');
        if (filterString) {
            url += `&filter=${filterString}`;
        }
    }

    if (severity) {
        url += `&severity=${severity}`;
    }

    return url;
};

export class ThreadClient extends DataPlaneClient {
    private static _instance: ThreadClient;

    public static getInstance(sreAgentEndpoint: string): ThreadClient {
        if (!ThreadClient._instance) {
            ThreadClient._instance = new ThreadClient(sreAgentEndpoint);
        }
        return ThreadClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public getThreads = async (options: ThreadsGetOptions): Promise<Response<Thread[]>> => {
        try {
            const path = getThreadsGetUrlPath(options);

            const url = this.getRequestUrl(path);

            const { data } = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data.value ?? [],
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getThread = async (threadId: string): Promise<Response<Thread | undefined>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v1/threads/${threadId}`), {
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

    public deleteThread = async (threadId: string): Promise<Response<any>> => {
        try {
            const { data } = await axios.delete(this.getRequestUrl(`/api/v1/threads/${threadId}`), {
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

    public createThread = async (options: MessagePostOptions, signal?: AbortSignal): Promise<Response<Thread | undefined>> => {
        const url = this.getRequestUrl(`/api/v1/threads`);
        const { userId, userDisplayName, message } = options;

        const response = await axios.post(
            url,
            {
                startMessage: {
                    text: message,
                    userId: userId,
                    displayName: userDisplayName,
                },
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
    };

    public updateThreadLastReadTime = async (threadId: string): Promise<Response<Thread>> => {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/markRead`);
        try {
            const response = await axios.post(
                url,
                {},
                {
                    headers: getAgentHeaders(),
                }
            );

            return {
                isSuccessful: true,
                content: response.data as Thread,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getAvailableAgentModes = async (): Promise<Response<string[]>> => {
        const url = this.getRequestUrl('/api/v1/threads/agentModes');
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: response.data as string[],
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public updateThreadAgentMode = async (threadId: string, agentMode: string): Promise<Response<Thread>> => {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/agentMode`);
        try {
            const response = await axios.post(
                url,
                { agentMode },
                {
                    headers: getAgentHeaders(),
                }
            );

            return {
                isSuccessful: true,
                content: response.data as Thread,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };
}

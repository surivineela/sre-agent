import axios from 'axios';
import { IncidentThreadCounts, Thread, ThreadSource } from '../Contracts/DataPlane/Thread';
import { TodoPlan } from '../Contracts/DataPlane/TodoPlan';
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
    sources?: ThreadSource[];
    excludedSources?: ThreadSource[];
    unread?: boolean;
}

export interface ThreadsGetOptions {
    skip: number;
    top: number;
    orderBy: 'modifiedTimestamp' | 'createdTimestamp';
    descending: boolean;
    filters?: ThreadsGetFilterOptions;
    severity?: ThreadSeverity;
    favorite?: boolean;
}

export const getThreadsGetUrlPath = (options: ThreadsGetOptions): string => {
    const { skip, top, orderBy, descending, filters, severity, favorite } = options;

    let url = `/api/v1/threads?skip=${skip}&top=${top}&orderby=${orderBy}${descending ? '+desc' : ''}`;

    if (filters) {
        const filterStrings: string[] = [];

        const { searchText, timestamps, sources, excludedSources, unread } = filters;

        if (searchText) {
            filterStrings.push(`contains(tolower(title),'${searchText.toLowerCase()}')`);
        }

        if (timestamps) {
            const { min, max } = timestamps;
            if (min) {
                const { timestamp, inclusive } = min;
                filterStrings.push(`${orderBy} ${inclusive ? 'ge' : 'gt'} ${timestamp}`);
            }
            if (max) {
                const { timestamp, inclusive } = max;
                filterStrings.push(`${orderBy} ${inclusive ? 'le' : 'lt'} ${timestamp}`);
            }
        }

        if (sources?.length) {
            const sourcesFilter = sources.map(source => `source eq '${source}'`).join(' or ');
            filterStrings.push(sources.length > 1 ? `(${sourcesFilter})` : sourcesFilter);
        }

        if (excludedSources?.length) {
            const excludedSourcesFilter = excludedSources.map(source => `source ne '${source}'`).join(' and ');
            filterStrings.push(excludedSources.length > 1 ? `(${excludedSourcesFilter})` : excludedSourcesFilter);
        }

        if (unread) {
            filterStrings.push(`lastReadTime lt modifiedTimestamp`);
        }

        const filterString = filterStrings.join(' and ');
        if (filterString) {
            url += `&filter=${filterString}`;
        }
    }

    if (severity) {
        url += `&severity=${severity}`;
    }

    if (favorite !== undefined) {
        url += `&favorite=${favorite}`;
    }

    return url;
};

export enum IncidentThreadsSortFields {
    IncidentId = 'incidentId',
    Title = 'title',
    Status = 'status',
}

export interface IncidentThreadsGetFilterOptions {
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
    status?: string[];
    unread?: boolean;
}

export interface IncidentThreadsGetOptions {
    skip: number;
    top: number;
    filter?: string;
    orderBy?: string;
}

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

    public getIncidentThreads = async (options: IncidentThreadsGetOptions): Promise<Response<Thread[]>> => {
        try {
            let path = `/api/v1/threads?skip=${options.skip}&top=${options.top}`;

            if (options.filter) {
                path += `&filter=${options.filter}`;
            }

            if (options.orderBy) {
                path += `&orderby=${options.orderBy}`;
            }

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

    public getIncidentThreadCounts = async (filter?: string): Promise<Response<IncidentThreadCounts>> => {
        try {
            let path = '/api/v1/threads/threadsCountByStatus';

            if (filter) {
                path += `?filter=${filter}`;
            }

            const url = this.getRequestUrl(path);

            const { data } = await axios.get(url, {
                headers: getAgentHeaders(),
            });

            return {
                isSuccessful: true,
                content: data ?? {
                    incidentStatusCounts: [],
                    investigationStatusCounts: [],
                },
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

    public updateThreadFavorite = async (threadId: string, favorite: boolean): Promise<Response<Thread | undefined>> => {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/favorite`);

        try {
            const response = await axios.post(
                url,
                { favorite },
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

    public updateThreadTitle = async (threadId: string, title: string): Promise<Response<Thread | undefined>> => {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/title`);

        try {
            const response = await axios.post(
                url,
                {
                    title,
                    updateModifiedTimestamp: false,
                },
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

    public postApprovalDecision = async (
        threadId: string,
        approvalId: string,
        decision: string,
        userId: string,
        oboScope?: string
    ): Promise<Response<any>> => {
        const url = this.getRequestUrl(`/api/v1/approvals/${threadId}/${approvalId}/decision`);
        try {
            const response = await axios.post(
                url,
                {
                    Status: decision,
                    User: userId,
                    Scope: oboScope,
                },
                {
                    headers: getAgentHeaders(oboScope),
                }
            );

            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getExecutionStatus = async (
        basePath: 'azCliExecution' | 'kubectlExecution',
        threadId: string,
        executionId: string
    ): Promise<Response<any>> => {
        const url = this.getRequestUrl(`/api/v1/${basePath}/${threadId}/${executionId}/status`);
        try {
            const response = await axios.get(url, {
                headers: getAgentHeaders(),
            });
            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public postExecutionAction = async (
        basePath: 'azCliExecution' | 'kubectlExecution',
        threadId: string,
        executionId: string,
        action: 'run' | 'cancel',
        userId: string,
        oboScope?: string
    ): Promise<Response<any>> => {
        const url = this.getRequestUrl(`/api/v1/${basePath}/${threadId}/${executionId}/action`);
        try {
            const response = await axios.post(
                url,
                {
                    action,
                    user: userId,
                },
                { headers: getAgentHeaders(oboScope) }
            );
            return {
                isSuccessful: true,
                content: response.data,
            };
        } catch (e) {
            return {
                isSuccessful: false,
                error: e,
            };
        }
    };

    public getTodoPlans = async (threadId: string): Promise<Response<TodoPlan[]>> => {
        try {
            const { data } = await axios.get(this.getRequestUrl(`/api/v1/threads/${threadId}/todo-plans`), {
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
}

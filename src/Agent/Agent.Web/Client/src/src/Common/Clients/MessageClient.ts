import axios from 'axios';
import { Message } from '../Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

interface MessagesGetOptions {
    skip: number;
    top: number;
    descending: boolean;
    minTimestamp?: string;
    maxTimestamp?: string;
}

export interface MessagePostOptions {
    userId: string;
    userDisplayName: string;
    message: string;
}

export class MessageClient extends DataPlaneClient {
    public static _instance: MessageClient;

    public static getInstance(sreAgentEndpoint: string): MessageClient {
        if (!MessageClient._instance) {
            MessageClient._instance = new MessageClient(sreAgentEndpoint);
        }
        return MessageClient._instance;
    }

    constructor(sreAgentEndpoint: string) {
        super(sreAgentEndpoint);
    }

    public async getMessages(threadId: string, options: MessagesGetOptions, signal?: AbortSignal): Promise<Response<Message[]>> {
        const url = this._getMessagesGetUrl(threadId, options);

        try {
            const { data } = await axios.get(url, {
                headers: getAgentHeaders(),
                signal,
            });
            return {
                isSuccessful: true,
                content: data.value ?? [],
            };
        } catch (e) {
            // ToDo: handle error
            return {
                isSuccessful: false,
                error: e,
                content: [],
            };
        }
    }

    public async postMessage(threadId: string, options: MessagePostOptions, signal?: AbortSignal): Promise<Response<Message>> {
        const url = this.getRequestUrl(`/api/v1/threads/${threadId}/messages`);

        const { userId, userDisplayName, message } = options;

        const response = await axios.post(
            url,
            {
                text: message,
                role: 'User',
                displayName: userDisplayName,
                userId: userId,
            },
            {
                headers: getAgentHeaders(),
                signal,
            }
        );

        return {
            isSuccessful: true,
            content: response.data as Message,
        };
    }

    private _getMessagesGetUrl(threadId: string, options: MessagesGetOptions): string {
        const { skip, top, descending, minTimestamp, maxTimestamp } = options;

        let path = `/api/v1/threads/${threadId}/messages?skip=${skip}&top=${top}&orderby=timestamp${descending ? '+desc' : ''}`;

        const timestampFilters: string[] = [];

        if (minTimestamp) {
            timestampFilters.push(`timeStamp gt ${minTimestamp}`);
        }

        if (maxTimestamp) {
            timestampFilters.push(`timeStamp lt ${maxTimestamp}`);
        }

        const filterString = timestampFilters.join(' and ');

        if (filterString) {
            path += `&filter=${filterString}`;
        }

        return this.getRequestUrl(path);
    }
}

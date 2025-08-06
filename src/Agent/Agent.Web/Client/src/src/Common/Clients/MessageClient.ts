import axios from 'axios';
import { Message } from '../Contracts/DataPlane/Message';
import { getAgentHeaders } from '../Helpers/headers';
import { DataPlaneClient, Response } from './DataPlaneClient';

interface MessagesGetOptions {
    skip: number;
    top: number;
    descending: boolean;
    minTimestamp?: string;
    maxTimestamp?: string;
    maxTimestampInclusive?: boolean;
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

    // If throwError is true, the caller will get the error and handle it accordingly. This is useful when clicking cancel button which will abort
    // all the ongoing requests in sendMessageHandler and all the ongoing requests will throw an abort error to indicate the cancellation.
    public async getMessages(
        threadId: string,
        options: MessagesGetOptions,
        signal?: AbortSignal,
        throwError?: boolean
    ): Promise<Response<Message[]>> {
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
            if (throwError) {
                throw e;
            } else {
                return {
                    isSuccessful: false,
                    error: e,
                    content: [],
                };
            }
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
        const { skip, top, descending, minTimestamp, maxTimestamp, maxTimestampInclusive } = options;

        let path = `/api/v1/threads/${threadId}/messages?skip=${skip}&top=${top}&orderby=timestamp${descending ? '+desc' : ''}`;

        const timestampFilters: string[] = [];

        if (minTimestamp) {
            timestampFilters.push(`timeStamp gt ${minTimestamp}`);
        }

        if (maxTimestamp) {
            if (maxTimestampInclusive) {
                timestampFilters.push(`timeStamp le ${maxTimestamp}`);
            } else {
                timestampFilters.push(`timeStamp lt ${maxTimestamp}`);
            }
        }

        const filterString = timestampFilters.join(' and ');

        if (filterString) {
            path += `&filter=${filterString}`;
        }

        return this.getRequestUrl(path);
    }
}

export type StreamingMessageType = 'chart' | 'image' | 'mermaid' | 'azcli' | 'kubectl' | 'approval' | null;

export enum MessageRequestType {
    CreateMessage = 'CreateMessage',
    CreateThread = 'CreateThread',
    CancelThread = 'CancelThread',
}

export enum MessageResponseType {
    MessageUpdate = 'MessageUpdate',
    ThreadUpdate = 'ThreadUpdate',
}

export interface StreamingMessage {
    finishReason?: 'stop' | 'tool_calls' | 'length' | null;
    authorName?: string | null;
    role?: 'user' | 'assistant' | 'tool' | null;
    contents?: StreamingMessageContent[] | null;
    createdAt?: string | null;
    additionalProperties?: {
        actionName?: MessageRequestType | null;
        connectionId?: string | null;
        threadId?: string | null;
        messageId?: string | null;
        streamMessageType?: StreamingMessageType;
        isCancelled?: boolean | null;
    } | null;
}

export interface StreamingMessageContent {
    $type: 'text' | 'functionCall' | null;
    text?: string | null;
    name?: string | null;
    additionalProperties?: {
        userDescription?: string | null;
        functionCallDescription?: string | null;
    } | null;
}

import { MessageType } from './Message';

export enum MessageRequestType {
    CreateMessage = 'CreateMessage',
    CreateThread = 'CreateThread',
    CancelThread = 'CancelThread',
    SubmitUserQuestionResponse = 'SubmitUserQuestionResponse',
}

export enum MessageResponseType {
    MessageUpdate = 'MessageUpdate',
    ThreadUpdate = 'ThreadUpdate',
    TaskUpdate = 'TaskUpdate',
    TodoPlanUpdate = 'TodoPlanUpdate',
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
        streamMessageType?: MessageType;
        isCancelled?: boolean | null;
        userId?: string;
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

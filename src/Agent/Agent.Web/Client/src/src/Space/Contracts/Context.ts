import { createContext } from 'react';
import { MessageRequestType, StreamingMessage } from '../../Common/Contracts/Azure/Streaming';
import { AgentContextProps, ChatMessage } from './Activities';

type SreAgentContextProps = {
    grafana: {
        isGrafanaUpdating: boolean;
        deploymentId: string;
        notificationId: string;
        setNotificationId: React.Dispatch<React.SetStateAction<string>>;
        setIsGrafanaUpdating: React.Dispatch<React.SetStateAction<boolean>>;
        setDeploymentId: React.Dispatch<React.SetStateAction<string>>;
    };
    incidentManagement: {
        isIncidentManagementConnected: boolean;
        setIsIncidentManagementConnected: React.Dispatch<React.SetStateAction<boolean>>;
        hasFilters: boolean;
        setHasFilters: React.Dispatch<React.SetStateAction<boolean>>;
    };
    agent: {
        mode: string;
        setMode: React.Dispatch<React.SetStateAction<string>>;
    };
};

type StreamingContextProps = {
    sendMessage: (message: MessageRequestType, ...args: any[]) => void;
    subscribeChatStreaming: (
        threadId: string,
        existingStreamingMessageHandler: (streamingMessages: StreamingMessage[] | null | undefined) => void,
        messageUpdateHandler: (...args: any[]) => void,
        threadUpdateHandler: (...args: any[]) => void
    ) => () => void;
    deleteStreamingMessages: (threadId: string) => void;
    isConnecting: boolean;
    isConnected: boolean;
};

type ChatBoxContextProps = {
    getGroupedChatMessages: (message: ChatMessage, isStreamingMessage?: boolean) => ChatMessage[];
};

export const SreAgentContext = createContext<SreAgentContextProps>({
    grafana: {
        isGrafanaUpdating: false,
        deploymentId: '',
        notificationId: '',
        setNotificationId: () => {},
        setIsGrafanaUpdating: () => {},
        setDeploymentId: () => {},
    },
    incidentManagement: {
        isIncidentManagementConnected: false,
        setIsIncidentManagementConnected: () => {},
        hasFilters: false,
        setHasFilters: () => {},
    },
    agent: {
        mode: '',
        setMode: () => {},
    },
});

export const AgentContext = createContext<AgentContextProps>({
    threadContentAndActionKey: '',
    activeThreadId: '',
});

export const StreamingContext = createContext<StreamingContextProps>({
    sendMessage: (_message: MessageRequestType, ..._args: any[]) => {},
    subscribeChatStreaming:
        (
            _threadId: string,
            _existingStreamingMessageHandler: (streamingMessages: StreamingMessage[] | null | undefined) => void,
            _messageUpdateHandler: (...args: any[]) => void,
            _threadUpdateHandler: (...args: any[]) => void
        ) =>
        () => {},
    deleteStreamingMessages: (_threadId: string) => {},
    isConnecting: true,
    isConnected: false,
});

export const ChatBoxContext = createContext<ChatBoxContextProps>({
    getGroupedChatMessages: (_message: ChatMessage, _isStreamingMessage?: boolean) => [],
});

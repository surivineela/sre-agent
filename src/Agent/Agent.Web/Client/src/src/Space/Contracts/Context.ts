import { createContext } from 'react';
import { AgentAccessLevel } from '../../Common/Contracts/Azure/SreAgent';
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
        accessLevel: AgentAccessLevel;
        setAccessLevel: React.Dispatch<React.SetStateAction<AgentAccessLevel>>;
    };
};

type StreamingContextProps = {
    sendMessage: (message: MessageRequestType, ...args: any[]) => void;
    subscribeChatStreaming: (
        threadId: string,
        latestStreamingMessageHandler: (latestStreamingMessage: StreamingMessage | null | undefined) => void,
        messageUpdateHandler: (...args: any[]) => void,
        threadUpdateHandler: (...args: any[]) => void
    ) => () => void;
    isConnecting: boolean;
    isConnected: boolean;
    isReconnecting: boolean;
    noPermission: boolean;
};

type ChatBoxContextProps = {
    getGroupedChatMessages: (message: ChatMessage, isStreamingMessage?: boolean) => ChatMessage[];
};

type ThreadAgentModeContextProps = {
    threadAgentMode?: string;
    threadAgentModeToDisplay?: string;
    isLoadingThreadAgentMode: boolean;
    isFetchingThreadAgentMode: boolean;
    fetchThreadAgentModeError?: Error | null;
    invalidateThreadAgentModeDataCache: () => void;
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
        accessLevel: AgentAccessLevel.low,
        setAccessLevel: () => {},
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
            _latestStreamingMessageHandler: (latestStreamingMessage: StreamingMessage | null | undefined) => void,
            _messageUpdateHandler: (...args: any[]) => void,
            _threadUpdateHandler: (...args: any[]) => void
        ) =>
        () => {},
    isConnecting: true,
    isConnected: false,
    isReconnecting: false,
    noPermission: false,
});

export const ChatBoxContext = createContext<ChatBoxContextProps>({
    getGroupedChatMessages: (_message: ChatMessage, _isStreamingMessage?: boolean) => [],
});

export const ThreadAgentModeContext = createContext<ThreadAgentModeContextProps>({
    threadAgentMode: undefined,
    threadAgentModeToDisplay: undefined,
    isLoadingThreadAgentMode: false,
    isFetchingThreadAgentMode: false,
    fetchThreadAgentModeError: null,
    invalidateThreadAgentModeDataCache: () => {},
});

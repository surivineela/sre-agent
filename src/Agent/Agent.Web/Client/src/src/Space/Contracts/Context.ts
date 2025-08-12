import { createContext } from 'react';
import { HttpResponseObject } from '../../Common/ArmHelper.types';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { Agent, AgentAccessLevel } from '../../Common/Contracts/Azure/SreAgent';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
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
        checkingConnectivity: boolean;
        refreshConnectivity: () => void;
    };
    agent: {
        mode: string;
        setMode: React.Dispatch<React.SetStateAction<string>>;
        accessLevel: AgentAccessLevel;
        setAccessLevel: React.Dispatch<React.SetStateAction<AgentAccessLevel>>;
    };
    agentObj: ArmObj<Agent> | undefined;
    agentLoading: boolean;
    agentLoaded: boolean;
    agentLoadFailure: string;
    agentPatching: boolean;
    agentPatched: boolean;
    agentPatchFailure: string;
    patchAgent: (agentPayload: Partial<ArmObj<Partial<Agent>>>) => Promise<HttpResponseObject<ArmObj<Agent>>>;
    refresh: () => void;
};

type StreamingContextProps = {
    startMessageStreamingOnNewThread: (newThreadId: string, threadCreateRequest: any) => void;
    startMessageStreamingOnExistingThread: (threadId: string, messageCreateRequest: any) => void;
    cancelMessageStreaming: (threadId: string) => void;
    subscribeMessageUpdateEvent: (input: {
        handler: (message: StreamingMessage) => void;
        threadId?: string;
        latestStreamingMessageHandler?: (latestStreamingMessage: StreamingMessage | null | undefined) => void;
    }) => () => void;
    subscribeThreadUpdateEvent: (handler: (message: StreamingMessage) => void) => () => void;
    subscribeTaskUpdateEvent: (handler: (message: StreamingMessage) => void) => () => void;
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
        checkingConnectivity: false,
        refreshConnectivity: () => {},
    },
    agent: {
        mode: '',
        setMode: () => {},
        accessLevel: AgentAccessLevel.low,
        setAccessLevel: () => {},
    },
    agentObj: undefined,
    agentLoading: false,
    agentLoaded: false,
    agentLoadFailure: '',
    agentPatching: false,
    agentPatched: false,
    agentPatchFailure: '',
    patchAgent: () => Promise.resolve({} as HttpResponseObject<ArmObj<Agent>>),
    refresh: () => {},
});

export const AgentContext = createContext<AgentContextProps>({
    threadContentAndActionKey: '',
    activeThreadId: '',
});

export const StreamingContext = createContext<StreamingContextProps>({
    startMessageStreamingOnNewThread: (_newThreadId: string, _threadCreateRequest: any) => {},
    startMessageStreamingOnExistingThread: (_threadId: string, _messageCreateRequest: any) => {},
    cancelMessageStreaming: (_threadId: string) => {},
    subscribeMessageUpdateEvent:
        (_: {
            handler: (message: StreamingMessage) => void;
            threadId?: string;
            latestStreamingMessageHandler?: (latestStreamingMessage: StreamingMessage | null | undefined) => void;
        }) =>
        () => {},
    subscribeThreadUpdateEvent: (_handler: (message: StreamingMessage) => void) => () => {},
    subscribeTaskUpdateEvent: (_handler: (message: StreamingMessage) => void) => () => {},
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

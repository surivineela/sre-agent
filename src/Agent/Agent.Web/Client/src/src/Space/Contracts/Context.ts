import { DrawerProps } from '@fluentui/react-drawer';
import { createContext, Dispatch, SetStateAction } from 'react';
import { HttpResponseObject } from '../../Common/ArmHelper.types';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { Agent, AgentAccessLevel, IncidentManagementType, MonthlyUsage } from '../../Common/Contracts/Azure/SreAgent';
import { AgentTaskMetaData, InvestigationTreeNodeStatus } from '../../Common/Contracts/DataPlane/AgentTask';
import { KnowledgeGraphSearchResult, MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { UserQuestionResponse } from '../../Common/Contracts/DataPlane/UserQuestion';
import { ChatBoxSidePanelData } from './Activities';

export type IncidentManagementConnectionState = 'connected' | 'notConnected' | 'waiting';

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
        incidentPlatformType: IncidentManagementType | undefined;
        incidentPlatformTypeLoading: boolean | undefined;
        incidentPlatformTypeLoaded: boolean | undefined;
        incidentPlatformTypeLoadFailure: string | undefined;
        refreshIncidentPlatformType: () => void;
        incidentManagementConnectionState: IncidentManagementConnectionState | undefined;
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
    agentLastUpdatedTime: number | undefined;
    patchAgent: (agentPayload: Partial<ArmObj<Partial<Agent>>>) => Promise<HttpResponseObject<ArmObj<Agent>>>;
    refresh: () => void;
    startAgent: () => Promise<HttpResponseObject<ArmObj<Agent>>>;
    stopAgent: () => Promise<HttpResponseObject<ArmObj<Agent>>>;
};

type StreamingContextProps = {
    startMessageStreamingOnNewThread: (newThreadId: string, threadCreateRequest: any) => void;
    startMessageStreamingOnExistingThread: (threadId: string, messageCreateRequest: any) => void;
    cancelMessageStreaming: (threadId: string) => void;
    submitUserQuestionResponse: (threadId: string, questionId: string, response: UserQuestionResponse) => void;
    subscribeMessageUpdateEvent: (input: {
        handler: (message: StreamingMessage) => void;
        threadId?: string;
        latestStreamingMessageHandler?: (latestStreamingMessage: StreamingMessage | null | undefined) => void;
    }) => () => void;
    subscribeThreadUpdateEvent: (handler: (message: StreamingMessage) => void) => () => void;
    subscribeTaskUpdateEvent: (handler: (message: StreamingMessage) => void) => () => void;
    subscribeTodoPlanUpdateEvent: (handler: (message: StreamingMessage) => void) => () => void;
    isConnecting: boolean;
    isConnected: boolean;
    isReconnecting: boolean;
    noPermission: boolean;
};

type ChatBoxSidePanelProps = {
    openAgentTask: (agentTask: AgentTaskMetaData) => void;
    openTodoPlan: (todoPlan: TodoInfo) => void;
    openMemorySearchResult: (result: MemorySearchResult) => void;
    openKnowledgeGraphSearchResult: (result: KnowledgeGraphSearchResult) => void;
};

type ThreadAgentModeContextProps = {
    threadAgentMode?: string;
    threadAgentModeToDisplay?: string;
    isLoadingThreadAgentMode: boolean;
    isFetchingThreadAgentMode: boolean;
    fetchThreadAgentModeError?: Error | null;
    invalidateThreadAgentModeDataCache: () => void;
};

type AgentTaskContextProps = {
    toggleNode: (nodeId: string) => void;
    getNodeStatus: (nodeId: string) => InvestigationTreeNodeStatus | null;
};

type AgentTaskGraphContextProps = {
    selectNode: (nodeId: string | null) => void;
    selectedNodeId: string | null;
};

type IncidentsOverviewContextProps = {
    onInitialSidePanelDataChanged: (threadId: string, data: ChatBoxSidePanelData | undefined | null) => void;
    initialSidePanelDataMap: Map<string, ChatBoxSidePanelData>;
};

type AgentWarningContextProps = {
    // Rbac context
    showRbacWarning: boolean;
    handleAddAdminClick: () => void;
    handleDismissRbacWarning: () => void;
    isCheckingRbac: boolean;

    // Usage context
    showUsageWarning: boolean;
    approachingLimit: boolean;
    reachedLimit: boolean;
    handleDismissUsageWarning: () => void;
    onUsageUpdate: (newUsages: MonthlyUsage | null | undefined) => void;
    isCheckingUsage: boolean;
};

type SreAgentSpaceContextProps = {
    isNavOpen: boolean;
    navBarTypeWhenOpen: DrawerProps['type'];
    onExpandOrCollapseNavBar: (newState: boolean) => void;
    threadsRenderKey: string;
    addThread: (threadId: string) => void;
    selectThread: (threadId: string | null) => void;
    updateThreadLastReadTime: (threadId: string) => Promise<void>;
    deleteThread: (thread: Thread) => Promise<void>;
    updateThreadTitle: (threadId: string, newTitle: string) => void;
    updateThreadFavorite: (threadId: string, isFavorite: boolean) => void;
    subscribeThreadFavoriteUpdate: (listener: (threadId: string, isFavorite: boolean) => void) => () => void;
    subscribeThreadTitleUpdate: (listener: (threadId: string, newTitle: string) => void) => () => void;
};

type ThreadNavContextProps = {
    unreadThreadIds: Set<string>;
    updateThreadTitle: (threadId: string, newTitle: string) => void;
    updateThreadFavorite: (threadId: string, isFavorite: boolean) => void;
    selectThread: (threadId: string | null) => void;
    deleteThread: (thread: Thread) => void;
    assignThreadItemDivRef: (threadId: string, el: HTMLDivElement) => void;
};

type DirtyStateContextProps = {
    isDirty: boolean;
    setIsDirty: Dispatch<SetStateAction<boolean>>;
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
        incidentPlatformType: undefined,
        incidentPlatformTypeLoading: undefined,
        incidentPlatformTypeLoaded: false,
        incidentPlatformTypeLoadFailure: '',
        refreshIncidentPlatformType: () => {},
        incidentManagementConnectionState: undefined,
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
    agentLastUpdatedTime: undefined,
    patchAgent: () => Promise.resolve({} as HttpResponseObject<ArmObj<Agent>>),
    refresh: () => {},
    startAgent: () => Promise.resolve({} as HttpResponseObject<ArmObj<Agent>>),
    stopAgent: () => Promise.resolve({} as HttpResponseObject<ArmObj<Agent>>),
});

export const StreamingContext = createContext<StreamingContextProps>({
    startMessageStreamingOnNewThread: (_newThreadId: string, _threadCreateRequest: any) => {},
    startMessageStreamingOnExistingThread: (_threadId: string, _messageCreateRequest: any) => {},
    cancelMessageStreaming: (_threadId: string) => {},
    submitUserQuestionResponse: (_threadId: string, _questionId: string, _response: UserQuestionResponse) => {},
    subscribeMessageUpdateEvent:
        (_: {
            handler: (message: StreamingMessage) => void;
            threadId?: string;
            latestStreamingMessageHandler?: (latestStreamingMessage: StreamingMessage | null | undefined) => void;
        }) =>
        () => {},
    subscribeThreadUpdateEvent: (_handler: (message: StreamingMessage) => void) => () => {},
    subscribeTaskUpdateEvent: (_handler: (message: StreamingMessage) => void) => () => {},
    subscribeTodoPlanUpdateEvent: (_handler: (message: StreamingMessage) => void) => () => {},
    isConnecting: true,
    isConnected: false,
    isReconnecting: false,
    noPermission: false,
});

export const ChatBoxSidePanelContext = createContext<ChatBoxSidePanelProps>({
    openAgentTask: (_agentTask: AgentTaskMetaData) => {},
    openTodoPlan: (_todoPlan: TodoInfo) => {},
    openMemorySearchResult: (_result: MemorySearchResult) => {},
    openKnowledgeGraphSearchResult: (_result: KnowledgeGraphSearchResult) => {},
});

export const ThreadAgentModeContext = createContext<ThreadAgentModeContextProps>({
    threadAgentMode: undefined,
    threadAgentModeToDisplay: undefined,
    isLoadingThreadAgentMode: false,
    isFetchingThreadAgentMode: false,
    fetchThreadAgentModeError: null,
    invalidateThreadAgentModeDataCache: () => {},
});

export const AgentTaskContext = createContext<AgentTaskContextProps>({
    toggleNode: (_: string) => {},
    getNodeStatus: (_: string) => null,
});

export const AgentTaskGraphContext = createContext<AgentTaskGraphContextProps>({
    selectNode: (_: string | null) => {},
    selectedNodeId: null,
});

export const IncidentsOverviewContext = createContext<IncidentsOverviewContextProps>({
    onInitialSidePanelDataChanged: (_threadId: string, _data: ChatBoxSidePanelData | undefined | null) => {},
    initialSidePanelDataMap: new Map<string, ChatBoxSidePanelData>(),
});

export const AgentWarningContext = createContext<AgentWarningContextProps>({
    // Rbac context
    showRbacWarning: false,
    handleAddAdminClick: () => {},
    handleDismissRbacWarning: () => {},
    isCheckingRbac: true,

    // Usage context
    showUsageWarning: false,
    approachingLimit: false,
    reachedLimit: false,
    handleDismissUsageWarning: () => {},
    onUsageUpdate: (_newUsages: MonthlyUsage | null | undefined) => {},
    isCheckingUsage: true,
});

export const SreAgentSpaceContext = createContext<SreAgentSpaceContextProps>({
    isNavOpen: true,
    navBarTypeWhenOpen: 'inline',
    onExpandOrCollapseNavBar: (_newState: boolean) => {},
    addThread: (_threadId: string) => {},
    selectThread: (_threadId: string | null) => {},
    threadsRenderKey: '',
    updateThreadLastReadTime: async (_threadId: string) => {},
    deleteThread: async (_thread: Thread) => {},
    updateThreadTitle: (_threadId: string, _newTitle: string) => {},
    updateThreadFavorite: (_threadId: string, _isFavorite: boolean) => {},
    subscribeThreadFavoriteUpdate: (_listener: (threadId: string, isFavorite: boolean) => void) => () => {},
    subscribeThreadTitleUpdate: (_listener: (threadId: string, newTitle: string) => void) => () => {},
});

export const ThreadNavContext = createContext<ThreadNavContextProps>({
    unreadThreadIds: new Set<string>(),
    updateThreadTitle: (_threadId: string, _newTitle: string) => {},
    updateThreadFavorite: (_threadId: string, _isFavorite: boolean) => {},
    selectThread: (_threadId: string | null) => {},
    deleteThread: (_thread: Thread) => {},
    assignThreadItemDivRef: (_threadId: string, _el: HTMLDivElement) => {},
});

export const DirtyStateContext = createContext<DirtyStateContextProps>({
    isDirty: false,
    setIsDirty: () => {},
});

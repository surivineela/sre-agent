import { Edge, Node } from '@xyflow/react';
import { ReactNode } from 'react';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { AgentTaskMetaData, InvestigationTreeNode, InvestigationTreeState } from '../../Common/Contracts/DataPlane/AgentTask';
import {
    Approval,
    AzCliExecution,
    ChatMessageError,
    KnowledgeGraphSearchResult,
    KubectlExecution,
    MemorySearchResult,
    Message,
    MessageRole,
    PsqlExecution,
} from '../../Common/Contracts/DataPlane/Message';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { TodoInfo } from '../../Common/Contracts/DataPlane/TodoPlan';
import { ChatBoxSidePanelStyleProps, ChatBoxStyleProps } from '../Styles/Activities.styles';

export interface IActivitiesProps {
    resourceId: string;
}

export interface IThreadsMenuProps {
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

export interface IThreadContentProps {
    thread?: Thread | null;
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
    deleteThread: (thread: Thread) => void;
    updateThreadLastReadTime: (threadId: string) => void;
}

export interface IThreadActivitiesProps {
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
    thread?: Thread | null;
}

export type ThreadMenuHandle = {
    removeThreadFromList: (threadId: string) => void;
    updateThreadLastReadTime: (threadId: string) => void;
    updateThreadWithNewTitle: (threadId: string, newTitle: string) => void;
    updateThreadFavoriteProperty: (threadId: string, isFavorite: boolean) => Promise<void>;
};

export interface ThreadListState {
    threads: Thread[];
    // A temporary storage for threads that have their 'favorite' property changed and whose modifiedTimestamp is older than the oldest thread in the current threads list and there are more threads to load.
    threadsThatHaveFavoritePropertyChanged: Thread[];
    moreThreadsToLoad: boolean;
}

export interface ThreadListsState {
    favoriteThreadListState: ThreadListState;
    regularThreadListState: ThreadListState;
    isLoadingInitialThreads: boolean;
    // A temporary storage for threads that have their modifiedTimestamp updated and waiting to be moved to the top of the threads list while isLoadingInitialThreads is true and no threads are presented yet.
    threadsThatHaveModifiedTimestampUpdated: Thread[];
}

export interface ChatTelemetryMessageSnapshot {
    id: string;
    authorRole: 'SREAgent' | 'User';
    text: string;
    timeStamp?: string;
    hasError?: boolean;
}

export interface ChatTelemetrySnapshot {
    messages: ChatTelemetryMessageSnapshot[];
}

export interface ChatBoxSidePanelData {
    agentTask?: AgentTaskMetaData;
    todoInfo?: TodoInfo;
    memorySearchResult?: MemorySearchResult;
    knowledgeGraphSearchResult?: KnowledgeGraphSearchResult;
}

export interface IChatBoxProps {
    selectThread: (threadId: string | null) => void;
    addThread: (threadId: string) => void;
    updateThreadLastReadTime: (threadId: string) => void;
    threadId: string | null | undefined;
    threadSource?: string;
    stylesProps?: ChatBoxStyleProps;
    sidePanelStylesProps?: ChatBoxSidePanelStyleProps;
    initialSidePanelData?: ChatBoxSidePanelData | null;
    canOpenSidePanel: boolean;
    onOpenSidePanel?: (panelType: ChatBoxSidePanelType, sidePanelData: ChatBoxSidePanelData) => void; // Pass this callback to trigger the side effects when any side panel is opened
    onCloseSidePanel?: (panelType: ChatBoxSidePanelType) => void; // Pass this callback to trigger the side effects when any side panel is closed
    expandOrCollapseNavBar?: (state: boolean) => void;
    setHasToDoPlans?: (val: boolean) => void;
    forcedAgentName?: string;
    lockAgentSelection?: boolean;
    onTelemetryUpdate?: (snapshot: ChatTelemetrySnapshot) => void;
    renderEmptyState?: (options: { sendMessage: (message: string) => Promise<void>; forcedAgentName?: string }) => ReactNode;
    inputDisabledMessage?: string;
    initialRetroModeEnabled?: boolean;
}

export enum ChatBoxSidePanelType {
    AgentTask = 'agentTask',
    ToDoPlan = 'todoPlan',
    MemorySearchResult = 'memorySearchResult',
    KnowledgeGraphSearchResult = 'knowledgeGraphSearchResult',
}

export interface ChatBoxHandleRef {
    openTodoPlanFromOutside: () => any;
    closeTodoPlanFromOutside: () => any;
}

export interface ChatMessageGroup {
    id: string;
    userMessages: ChatMessage[];
    agentMessages: ChatMessage[];
}

export interface IChatMessageProps {
    componentId?: string;
    messages: ChatMessage[];
    role: MessageRole;
    isTyping?: boolean;
    threadId: string;
    isStreamingMessage?: boolean;
    toolCallText?: string | null;
    isWaitingForStreamingMessages?: boolean;
    threadSource?: string;
    messagesToCopy?: string;
    sendMessage?: (message: string) => Promise<void>;
    updateApprovalOrCliMessageInStreamingMessage?: (approvalOrCliMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        psqlExecution?: PsqlExecution;
    }) => void;
}

export interface IChatMessageGroupProps {
    messageGroup: ChatMessageGroup;
    isTyping?: boolean;
    threadId: string;
    threadSource?: string;
    isStreamingMessage?: boolean;
    toolCallText?: string | null;
    isWaitingForStreamingMessages?: boolean;
    sendMessage?: (message: string) => Promise<void>;
    updateApprovalOrCliMessageInStreamingMessage?: (approvalOrCliMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        psqlExecution?: PsqlExecution;
    }) => void;
}

export interface ReasoningItem {
    messageId: string;
    content: string;
}

export interface Reasoning {
    active: boolean;
    items: ReasoningItem[];
}

export interface ChatMessage extends Message {
    isImage: boolean | null | undefined;
    deepInvestigationStatus?: {
        enabled: boolean;
    };
    error?: ChatMessageError;
    reasoning: Reasoning | undefined | null;
}

export interface IAgentMessageProps {
    message: ChatMessage;
    messageId: string;
    timeStamp: string;
    isTyping?: boolean;
    threadId: string;
    sendMessage?: (message: string) => Promise<void>;
    updateApprovalOrCliMessageInStreamingMessage?: (approvalOrCliMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        psqlExecution?: PsqlExecution;
    }) => void;
}

export interface IActionsProps {
    threadId?: string;
}

export interface IChatBoxFooterProps {
    sendMessage: (message: string, options?: SendMessageOptions) => Promise<void>;
    isLoading: boolean;
    downButtonState: { visible: boolean; flash: boolean };
    onClickDownButton: () => void;
    prompts: string[];
    messagePromptsUsed: string[];
    cancelStreaming: () => void;
    isTyping: boolean;
    isCancellingStreaming: boolean;
    threadId?: string | null;
    threadSource?: string;
    isDeepInvestigationButtonEnabled: boolean;
    isDeepInvestigationTurnedOn: boolean;
    onClickDeepInvestigationButton: () => void;
    postSystemMessage?: (text: string) => void;
    forcedAgentName?: string;
    lockAgentSelection?: boolean;
    inputDisabledMessage?: string;
    isIncidentRetroModeTurnedOn?: boolean;
    toggleIncidentRetroMode?: () => void;
}

export interface SendMessageOptions {
    starterAgentName?: string;
    commandId?: string;
}

export class ThreadLoadingCounts {
    public static readonly default = 20;
}

export class ThreadPollingCounts {
    public static readonly default = 5;
}

export class MessageLoadingCounts {
    public static readonly default = 20;
    public static readonly active = 10;
}

export interface IAgentModeInfo {
    name: AgentMode;
    displayName: string;
    description: string;
    isRestricted?: boolean;
    restrictionReason?: string;
}

export interface IAgentModeSelectorProps {
    id: string;
    threadId: string;
    disabled?: boolean;
}

export class AgentMessageRegex {
    // Check for markdown image syntax with base64 data
    public static readonly imageRegex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;
    // Check for mermaid code blocks
    public static readonly mermaidRegex = /```mermaid\n([\s\S]*?)\n```/g;
    // Check for chart data blocks
    public static readonly chartRegex = /```chart-data[\r\n]+([\s\S]*?)[\r\n]+```/g;
    // Check if the entire message is just a incident-alert block
    public static readonly incidentAlertRegex = /```incident-alert\s+([\s\S]*?)```/;
    public static readonly changeDiffRegex = /```change-diff\n([\s\S]*?)\n```/g;
    // Check for investigation summary formats
    public static readonly investigationSummaryRegex = /<investigation-summary>([\s\S]*?)<\/investigation-summary>/;
    public static readonly investigationSummariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/;
}

export enum ThreadFilter {
    Incidents,
    Unread,
}

export interface AgentTaskGraphHandle {
    centerGraph: () => void;
}

export interface IAgentTaskProps {
    threadId?: string;
    userDefinedThreadId: string;
}

export type InvestigationGraphFlowNode = InvestigationTreeNode & {
    index?: number;
    isChild?: boolean;
    hasChildren?: boolean;
    showInitialInvestigationSummary?: boolean;
    showInitialInvestigationSteps?: boolean;
};

export enum InvestigationGraphFlowEdgeType {
    Parents = 'parents',
    Children = 'children',
}

export type InvestigationGraphFlowEdge = {
    fromInitialInvestigation?: boolean;
    targetId: string;
};

export type GraphFlowNode = Node<InvestigationGraphFlowNode>;
export type GraphFlowEdge = Edge<InvestigationGraphFlowEdge>;

export interface IAgentTaskGraphProps {
    isLoading: boolean;
    treeStateValue: TreeStateValue | null;
}

export interface TreeStateValue {
    taskId: string;
    treeState: InvestigationTreeState | null;
    changeIdentifier: string;
}

export enum AgentTaskPhaseNodeIdSuffix {
    InitialInvestigation = 'initial-investigation',
    FormingHypothesis = 'forming-hypothesis',
    Conclusion = 'conclusion',
}

export class AgentTaskNodeSize {
    public static readonly GroupNode = {
        width: 600,
        height: 300,
    };

    public static readonly InitialInvestigationNode = {
        width: 400,
        height: 240,
    };

    public static readonly ConclusionNode = {
        width: 500,
        height: 300,
    };

    public static readonly HypothesisNode = {
        width: 400,
        height: 180,
    };
}

export enum Shortcut {
    Agent = 'agent',
    Clear = 'clear',
    Compact = 'compact',
    Incident = 'incident',
    Resource = 'resource',
    Remember = 'remember',
    Retrieve = 'retrieve',
    IncidentRetroMode = 'incidentRetroMode',
}

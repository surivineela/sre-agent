import { Edge, Node } from '@xyflow/react';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { InvestigationTreeNode, InvestigationTreeState } from '../../Common/Contracts/DataPlane/AgentTask';
import {
    Approval,
    AzCliExecution,
    ChatMessageError,
    KubectlExecution,
    MemorySearchResult,
    Message,
    MessageContent,
    MessageMetaData,
    PsqlExecution,
} from '../../Common/Contracts/DataPlane/Message';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { AgentTaskStyleProps, ChatBoxV2StyleProps } from '../Styles/Activities.styles';

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
    collapseResizables: () => void;
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

export interface IChatBoxProps {
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
    updateThreadLastReadTime: (threadId: string) => void;
    isAgentTaskEnabled: boolean;
    threadId?: string;
    threadSource?: string;
    stylesProps?: ChatBoxV2StyleProps;
    agentTaskStyleProps?: AgentTaskStyleProps;
    collapseResizables?: () => void;
    todoPlanDrawer?: any; // Using any for now - could be strongly typed later
}

export interface IChatMessageProps {
    message: ChatMessage;
    previousMessage?: ChatMessage;
    nextMessage?: ChatMessage;
    isTyping?: boolean;
    threadId: string;
    threadSource?: string;
    isStreamingMessage?: boolean;
    toolCallText?: string | null;
    isWaitingForStreamingMessages?: boolean;
    updateSpecialMessageInStreamingMessage?: (specialMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        psqlExecution?: PsqlExecution;
        memorySearchResult?: MemorySearchResult;
    }) => void;
}

export interface ChatMessageContent extends MessageContent {
    isImage?: boolean;
    deepInvestigationStatus?: {
        enabled: boolean;
    };
    error?: ChatMessageError;
}

export interface ChatMessage extends MessageMetaData {
    contents: ChatMessageContent[];
}

export interface ChatMessageComponentInput
    extends Omit<Message, 'text' | 'approval' | 'azCliExecution' | 'kubectlExecution' | 'psqlExecution' | 'isDailyReport'> {
    text: string;
    approval?: Approval;
    azCliExecution?: AzCliExecution;
    kubectlExecution?: KubectlExecution;
    isDailyReport?: boolean;
    isImage?: boolean;
}

export interface IAgentMessageProps {
    messageContent: ChatMessageContent;
    messageId: string;
    timeStamp: string;
    isTyping?: boolean;
    threadId: string;
    updateSpecialMessageInStreamingMessage?: (specialMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
        memorySearchResult?: MemorySearchResult;
        psqlExecution?: PsqlExecution;
    }) => void;
}

export interface IChatProps {
    messages: Message[];
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
    showDeepInvestigationButton: boolean;
    isDeepInvestigationButtonEnabled: boolean;
    isDeepInvestigationTurnedOn: boolean;
    onClickDeepInvestigationButton: () => void;
    postSystemMessage: (text: string) => void;
}

export interface SendMessageOptions {
    starterAgentName?: string;
    forceNewThread?: boolean;
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

export const MessageTypingSpeedInMilliseconds = 10;
export const MessageTypingCharactersPer10Ms = 5;

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
    public static readonly chartRegex = /```chart-data\n([\s\S]*?)\n```/g;
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
}

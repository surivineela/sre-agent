import { Edge, Node } from '@xyflow/react';
import { AgentMode } from '../../Common/Contracts/Azure/SreAgent';
import { AgentTaskMetaData, InvestigationTreeNode, InvestigationTreeState } from '../../Common/Contracts/DataPlane/AgentTask';
import {
    Approval,
    AzCliExecution,
    ChatMessageError,
    KubectlExecution,
    Message,
    MessageContent,
    MessageMetaData,
} from '../../Common/Contracts/DataPlane/Message';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { ChatBoxV2StyleProps } from '../Styles/Activities.styles';

export interface IActivitiesProps {
    resourceId: string;
}

export interface AgentContextProps {
    threadContentAndActionKey: string;
    activeThreadId: string;
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
    removeThreadFromList: (thread: Thread) => void;
    updateThreadLastReadTime: (threadId: string) => void;
};

export interface IChatBoxProps {
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
    updateThreadLastReadTime: (threadId: string) => void;
    threadId?: string;
    threadSource?: string;
    stylesProps?: ChatBoxV2StyleProps;
    collapseResizables?: () => void;
}

export interface IChatMessageProps {
    message: ChatMessage;
    previousMessage?: ChatMessage;
    nextMessage?: ChatMessage;
    isTyping?: boolean;
    threadId: string;
    isStreamingMessage?: boolean;
    toolCallText?: string | null;
    isWaitingForStreamingMessages?: boolean;
    updateSpecialMessageInStreamingMessage?: (specialMessageProperties: {
        approval?: Approval;
        azCliExecution?: AzCliExecution;
        kubectlExecution?: KubectlExecution;
    }) => void;
}

export interface ChatMessageContent extends MessageContent {
    isImage?: boolean;
    error?: ChatMessageError;
}

export interface ChatMessage extends MessageMetaData {
    contents: ChatMessageContent[];
}

export interface ChatMessageComponentInput
    extends Omit<Message, 'text' | 'approval' | 'azCliExecution' | 'kubectlExecution' | 'isDailyReport'> {
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
    }) => void;
}

export interface IChatProps {
    messages: Message[];
}

export interface IActionsProps {
    threadId?: string;
}

export interface IChatBoxFooterProps {
    sendMessage: (message: string) => Promise<void>;
    isLoading: boolean;
    downButtonState: { visible: boolean; flash: boolean };
    onClickDownButton: () => void;
    prompts: string[];
    messagePromptsUsed: string[];
    cancelStreaming: () => void;
    isTyping: boolean;
    isCancellingStreaming: boolean;
    threadId?: string | null;
    openAgentTask: (task: AgentTaskMetaData | null) => void;
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
    // Check for investigation summary formats
    public static readonly investigationSummaryRegex = /<investigation-summary>([\s\S]*?)<\/investigation-summary>/;
    public static readonly investigationSummariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/;
}

export enum ThreadFilter {
    Incidents,
    Unread,
}

export interface IAgentTaskProps {
    threadId?: string;
    userDefinedThreadId: string;
    task: AgentTaskMetaData | null;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

export type InvestigationGraphFlowNode = InvestigationTreeNode & {
    index?: number;
    isChild?: boolean;
    hasChildren?: boolean;
};

export enum InvestigationGraphFlowEdgeType {
    PhaseToHypothesis = 'phase-to-hypothesis',
    HypothesisToHypothesis = 'hypothesis-to-hypothesis',
    HypothesisToConclusion = 'hypothesis-to-conclusion',
}

export type InvestigationGraphFlowEdge = {
    edgeType: InvestigationGraphFlowEdgeType;
    sourceId: string;
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

    public static readonly PhaseNode = {
        width: 400,
        height: 180,
    };

    public static readonly HypothesisNode = {
        width: 400,
        height: 180,
    };
}

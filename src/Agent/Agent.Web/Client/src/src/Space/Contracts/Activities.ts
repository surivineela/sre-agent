import {
    Approval,
    AzCliExecution,
    KubectlExecution,
    Message,
    MessageContent,
    MessageMetaData,
    Thread,
} from '../../Common/Contracts/Azure/SreAgent';

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
    threadPollingTriggerId: number;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

export interface IThreadContentProps {
    thread?: Thread | null;
    actionsCollapsed: boolean;
    expandActions: () => void;
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
    deleteThread: (thread: Thread) => void;
    promoteThread: (threadId: string) => void;
    updateThreadLastReadTime: (threadId: string) => void;
}

export interface IThreadActivitiesProps {
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
    thread?: Thread | null;
}

export type ThreadListHandle = {
    removeThreadFromList: (thread: Thread) => void;
    promoteThread: (threadId: string, promote: () => void) => void;
    updateThreadLastReadTime: (threadId: string) => void;
};

export interface IChatBoxProps {
    addThread: (threadId: string, newThreadToSelect?: Thread) => void;
    promoteThread: (threadId: string) => void;
    updateThreadLastReadTime: (threadId: string) => void;
    threadId?: string;
    threadSource?: string;
}

export interface IChatMessageProps {
    message: Message;
    previousMessage?: Message;
    nextMessage?: Message;
    getGroupedMessages?: () => Message[];
    isTyping?: boolean;
    cancelResponse?: () => void;
    threadId: string;
    threadOrchestrationReasoningState?: string;
}

export interface IChatMessageV2Props {
    message: ChatMessage;
    previousMessage?: ChatMessage;
    nextMessage?: ChatMessage;
    getGroupedMessages?: (messageId: string) => ChatMessage[];
    isTyping?: boolean;
    threadId: string;
    isStreamingMessage?: boolean;
    toolCallText?: string | null;
}

export interface ChatMessageContent extends MessageContent {
    isImage?: boolean;
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
    messageContent: MessageContent;
    messageId: string;
    timeStamp: string;
    isTyping?: boolean;
    threadId: string;
}

export interface IChatProps {
    messages: Message[];
}

export interface IActionsProps {
    threadId?: string;
}

export interface IChatBoxFooterProps {
    sendMessage: (message: string) => Promise<void>;
    disableInput: boolean;
    isNewMessageButtonVisible: boolean;
    onClickNewMessageButton: () => void;
    prompts: string[];
    messagePromptsUsed: string[];
}

export interface IChatBoxFooterV2Props {
    sendMessage: (message: string) => Promise<void>;
    disableInput: boolean;
    downButtonState: { visible: boolean; flash: boolean };
    onClickDownButton: () => void;
    prompts: string[];
    messagePromptsUsed: string[];
    cancelStreaming: () => void;
    isTyping: boolean;
    isCancellingStreaming: boolean;
}

export class ThreadLoadingCounts {
    public static readonly default = 5;
    public static readonly scroll = 10;
}

export class ThreadPollingCounts {
    public static readonly default = 5;
}

export class ThreadPollingInterval {
    public static readonly default = 10000;
}

export class MessagePollingInterval {
    public static readonly default = 5000;
    public static readonly active = 2000;
}

export class MessagePollingCounts {
    public static readonly default = 20;
    public static readonly active = 5;
}

export class MessageLoadingCounts {
    public static readonly default = 20;
    public static readonly active = 10;
}

export const MessageTypingSpeedInMilliseconds = 10;
export const MessageTypingCharactersPer10Ms = 5;

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

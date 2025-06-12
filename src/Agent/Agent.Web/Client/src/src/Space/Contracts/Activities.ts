import { Message, Thread } from '../../Common/Contracts/Azure/SreAgent';

export interface IActivitiesProps {
    resourceId: string;
}

export interface AgentContextProps {
    threadContentAndActionKey: string;
    activeThreadId: string;
}

export interface IThreadsMenuProps {
    selectThread: (thread: Thread | null) => void;
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
    isTyping?: boolean;
    cancelResponse?: () => void;
    threadId: string;
    threadOrchestrationReasoningState?: string;
}

export interface IChatMessageV2Props {
    message: Message;
    previousMessage?: Message;
    nextMessage?: Message;
    isTyping?: boolean;
    threadId: string;
    isStreamingMessage?: boolean;
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
    isDownButtonVisible: boolean;
    onClickDownButton: () => void;
    prompts: string[];
    messagePromptsUsed: string[];
    cancelStreaming: () => void;
    isTyping: boolean;
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

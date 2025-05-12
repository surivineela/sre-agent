import { Message, Thread } from '../../Common/Contracts/Azure/SreAgent';

export interface IActivitiesProps {
    resourceId: string;
}

export interface AgentContextProps {
    threadContentAndActionKey: string;
    threadsInitialized: boolean;
    activeThreadId: string;
}

export interface IThreadsMenuProps {
    threads: Thread[];
    selectThread: (thread: Thread | null) => void;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

export interface IThreadContentProps {
    thread?: Thread | null;
    actionsCollapsed: boolean;
    expandActions: () => void;
    addThread: (thread: Thread) => void;
    deleteThread: (thread: Thread) => void;
}

export interface IThreadActivitiesProps {
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
    thread?: Thread | null;
}

export interface IChatBoxProps {
    addThread: (thread: Thread) => void;
    threadId?: string;
}

export interface IChatMessageProps {
    message: Message;
    isTyping?: boolean;
    cancelResponse?: () => void;
    threadId: string;
    threadOrchestrationReasoningState?: string;
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
    public static readonly active = 1;
}

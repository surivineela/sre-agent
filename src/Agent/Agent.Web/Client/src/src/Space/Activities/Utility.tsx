import {
    Approval,
    ApprovalDecision,
    AzCliExecution,
    IncidentStatus,
    Message,
    MessageAuthor,
    MessageMetaData,
    SREAgentUserId,
    Thread,
    ThreadSource,
} from '../../Common/Contracts/Azure/SreAgent';
import { StreamingMessage, StreamingMessageType } from '../../Common/Contracts/Azure/Streaming';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { ChatMessage, ChatMessageContent, ThreadFilter } from '../Contracts/Activities';
import { DefaultUserIdAndDisplayName } from '../Hooks/useAuthenticatedUserInfo';

/**
 * Add additional threads to the existing threads list and update existing threads if they have been modified.
 * @param prevThreads existing threads sorted in descending order by modifiedTimestamp
 * @param threads additional threads sorted in descending order by modifiedTimestamp
 * @param reverse
 * @returns
 */
export const processThreads = (prevThreads: Thread[], threads: Thread[], areThreadsNew: boolean) => {
    if (threads.length === 0) {
        return {
            threads: prevThreads,
            addedThreads: [],
        };
    }

    const threadIdsToRemoveFromPrevThreads: Set<string> = new Set<string>();

    const threadsMap: Map<string, Thread> = new Map();
    threads.forEach(thread => threadsMap.set(thread.id, thread));

    for (let i = 0; i < prevThreads.length; i++) {
        const prevThreadId = prevThreads[i].id;
        const duplicatedThread = threadsMap.get(prevThreadId);
        if (duplicatedThread) {
            if (areThreadsNew && duplicatedThread.modifiedTimestamp > prevThreads[i].modifiedTimestamp) {
                // if the threads are new and the modified time is greter than the existing duplicated one from prev threads, then remove it from the prev threads
                threadIdsToRemoveFromPrevThreads.add(prevThreadId);
            } else {
                // Remove thread out of the threadsMap because the thread is already in the existing threads and has not been modified
                threadsMap.delete(prevThreadId);
            }
        }
    }

    const threadsToAdd: Thread[] = Array.from(threadsMap.values());
    threadsToAdd.sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    const updatedExistingThreads = [...prevThreads].filter(thread => {
        return !threadIdsToRemoveFromPrevThreads.has(thread.id);
    });

    const existingThreads = threadIdsToRemoveFromPrevThreads.size > 0 ? updatedExistingThreads : prevThreads;

    if (threadsToAdd.length === 0) {
        return {
            threads: existingThreads,
            addedThreads: [],
        };
    }

    return {
        threads: areThreadsNew ? [...threadsToAdd, ...existingThreads] : [...existingThreads, ...threadsToAdd],
        addedThreads: threadsToAdd,
    };
};

/**
 * @param prevMessages existing messages sorted in ascending order by timestamp
 * @param newMessages new messages sorted in descending order by timestamp
 * @returns messages combined in ascending order by timestamp
 */
export const processNewMessages = (prevMessages: Message[], newMessages: Message[]) => {
    if (newMessages.length === 0) return prevMessages;

    const updatedPrevMessages = [...prevMessages];
    let isPrevMessagesUpdated = false;

    const newMessagesMap: Map<string, Message> = new Map<string, Message>();
    newMessages.forEach((msg: Message) => newMessagesMap.set(msg.id, msg));

    for (let i = 0; i < updatedPrevMessages.length; i++) {
        const message = newMessagesMap.get(updatedPrevMessages[i].id);
        if (message) {
            if (message.text !== updatedPrevMessages[i].text) {
                // Update existing message
                updatedPrevMessages[i] = message;
                isPrevMessagesUpdated = true;
            }
            newMessagesMap.delete(updatedPrevMessages[i].id);
        }
    }

    const messagesToAdd: Message[] = Array.from(newMessagesMap.values());
    messagesToAdd.sort((a, b) => getSafeDateTime(a.timeStamp).getTime() - getSafeDateTime(b.timeStamp).getTime());

    const existingMessages = isPrevMessagesUpdated ? updatedPrevMessages : prevMessages;

    if (messagesToAdd.length === 0) {
        // Do not return copied old messages as it will introduce unnecessary re-renders
        return existingMessages;
    }

    return [...existingMessages, ...messagesToAdd];
};

/**
 * @param prevMessages existing messages sorted in ascending order by timestamp
 * @param oldMessages older messages sorted in descending order by timestamp
 * @returns messages sorted in ascending order by timestamp
 */
export const processOldMessages = (prevMessages: Message[], oldMessages: Message[]) => {
    if (oldMessages.length === 0) {
        return prevMessages;
    }

    // Copy oldMessages as reverse() will mutate the original array and return the same reference
    const oldMessagesInAscendingOrder = [...oldMessages].reverse();

    return [...oldMessagesInAscendingOrder, ...prevMessages];
};

/**
 * @param prevMessages existing messages sorted in ascending order by timestamp
 * @param oldMessages older messages sorted in descending order by timestamp
 * @returns messages sorted in ascending order by timestamp
 */
export const processOldMessagesV2 = (prevMessages: ChatMessage[], oldMessages: ChatMessage[]) => {
    if (oldMessages.length === 0) {
        return prevMessages;
    }

    // Copy oldMessages as reverse() will mutate the original array and return the same reference
    const oldMessagesInAscendingOrder = [...oldMessages].reverse();

    return [...oldMessagesInAscendingOrder, ...prevMessages];
};

/**
 * Update the text of the existing messages if they have been updated.
 * @param prevMessages
 * @param updatedMessages
 */
export const updateOldMessagesText = (prevMessages: ChatMessage[] | undefined, updatedMessages: ChatMessage[]) => {
    //Do not update the messages if the prevMessages is undefined or empty or updatedMessages is empty
    if (prevMessages === undefined || prevMessages.length === 0 || updatedMessages.length === 0) return prevMessages;

    const updatedPrevMessages = [...prevMessages];
    let isPrevMessagesUpdated = false;

    const messagesMap: Map<string, ChatMessage> = new Map<string, ChatMessage>();
    updatedMessages.forEach((msg: ChatMessage) => messagesMap.set(msg.id, msg));

    for (let i = prevMessages.length - 1; i >= 0; i--) {
        const message = messagesMap.get(prevMessages[i].id);
        if (message && message.contents[0].text !== prevMessages[i].contents[0].text) {
            updatedPrevMessages[i] = { ...prevMessages[i], contents: [{ ...prevMessages[i].contents[0], text: message.contents[0].text }] };
            isPrevMessagesUpdated = true;

            messagesMap.delete(prevMessages[i].id);
            if (messagesMap.size === 0) {
                break;
            }
        }
    }

    return isPrevMessagesUpdated ? updatedPrevMessages : prevMessages;
};

export const getDefaultSREAgentAuthor = (): MessageAuthor => {
    return {
        role: 'SREAgent',
        userId: SREAgentUserId,
        displayName: '',
    };
};

export const isAgentMessage = (message: ChatMessage): boolean => {
    return equals(message.author.role, 'SREAgent', AntUxStringComparison.IgnoreCase);
};

export const getMessageMetaDataFromChatMessage = (
    streamingMessage: StreamingMessage,
    userId: string,
    displayName: string
): MessageMetaData => {
    const { additionalProperties, createdAt } = streamingMessage;

    const author: MessageAuthor = isUserStreamingMessage(streamingMessage)
        ? {
              role: 'User',
              userId,
              displayName: displayName,
          }
        : getDefaultSREAgentAuthor();

    return {
        id: additionalProperties?.messageId || '',
        timeStamp: createdAt || '',
        author,
    };
};

/**
 * Update the current contents of the streaming message. If the new streaming message chunk is azure cli or kubectl execution, it will replace the existing azure cli or kubectl execution content with the same execution id in the current contents if it exists.
 * @param currentContents existing chat message contents
 * @param streamingMessage new streaming message chunk to be added to the current contents
 * @returns
 */
export const processChatMessageContents = (
    currentContents: ChatMessageContent[],
    streamingMessage: StreamingMessage
): ChatMessageContent[] => {
    const messageContent = streamingMessage.contents?.[0];

    let approval = getSpecialMessageContentFromStreamingMessage<Approval>(streamingMessage, 'approval');
    const azCliExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'azcli');
    const kubectlExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'kubectl');
    const text = messageContent?.text && !approval && !azCliExecution && !kubectlExecution ? messageContent.text : '';
    const isImage = isImageStreamingMessageType(streamingMessage);

    if (approval && approval.status !== null && approval.status !== undefined && typeof approval.status === 'number') {
        approval = {
            ...approval,
            status:
                approval.status === 0
                    ? ApprovalDecision.Pending
                    : approval.status === 1
                      ? ApprovalDecision.Approved
                      : approval.status === 2
                        ? ApprovalDecision.Cancelled
                        : approval.status === 3
                          ? ApprovalDecision.PendingAuthorization
                          : ApprovalDecision.Authorized,
        };
    }

    const chatMessageContent: ChatMessageContent = {
        text,
        isImage,
        approval,
        azCliExecution,
        kubectlExecution,
        isDailyReport: false,
    };

    const executionId = chatMessageContent.azCliExecution?.id || chatMessageContent.kubectlExecution?.id;

    if (executionId) {
        const existingContentIndexThatHasSameExecutionId = currentContents.findIndex(content => {
            const id = content.azCliExecution?.id || content.kubectlExecution?.id;
            return id && id === executionId;
        });

        if (existingContentIndexThatHasSameExecutionId !== -1) {
            currentContents.splice(existingContentIndexThatHasSameExecutionId, 1);
        }
    }

    return [...currentContents, chatMessageContent];
};

export const getSpecialMessageContentFromStreamingMessage = <T,>(
    streamingMessage: StreamingMessage,
    streamingMessageType: StreamingMessageType
): T | undefined => {
    const additionalProperties = streamingMessage.additionalProperties;
    const messageType = additionalProperties?.streamMessageType || '';
    const text = getStreamingMessageText(streamingMessage);

    if (
        additionalProperties &&
        messageType &&
        streamingMessageType &&
        equals(messageType, streamingMessageType, AntUxStringComparison.IgnoreCase) &&
        text
    ) {
        try {
            return JSON.parse(text) as T;
        } catch (error) {
            return undefined;
        }
    }
};

export const isDefaultStreamingMessageType = (streamingMessage: StreamingMessage): boolean => {
    return !streamingMessage.additionalProperties?.streamMessageType;
};

export const isImageStreamingMessageType = (streamingMessage: StreamingMessage): boolean => {
    const streamingMessageType = streamingMessage.additionalProperties?.streamMessageType || '';
    return (
        equals(streamingMessageType, 'image', AntUxStringComparison.IgnoreCase) ||
        equals(streamingMessageType, 'chart', AntUxStringComparison.IgnoreCase) ||
        equals(streamingMessageType, 'mermaid', AntUxStringComparison.IgnoreCase)
    );
};

export const getStreamingMessageText = (streamingMessage: StreamingMessage) => {
    return streamingMessage.contents?.[0]?.text || '';
};

export const getToolCallText = (streamingMessage: StreamingMessage): string | null => {
    const messageContent = streamingMessage.contents?.[0];
    if (messageContent && equals(messageContent.$type || '', 'functionCall', AntUxStringComparison.IgnoreCase)) {
        return messageContent.additionalProperties?.userDescription || messageContent.additionalProperties?.functionCallDescription || null;
    }
    return null;
};

export const isFinalStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    const { finishReason, additionalProperties } = streamingMessage;

    return (
        equals(finishReason || '', 'stop', AntUxStringComparison.IgnoreCase) ||
        equals(finishReason || '', 'length', AntUxStringComparison.IgnoreCase) ||
        !!additionalProperties?.isCancelled
    );
};

export const isUserStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    return equals(streamingMessage.role || '', 'user', AntUxStringComparison.IgnoreCase);
};

export const constructUserMessageFromStreamingMessage = (streamingMessage: StreamingMessage): ChatMessage => {
    const { additionalProperties, authorName, createdAt } = streamingMessage;
    const { messageId, userId } = additionalProperties || {};

    return {
        id: messageId || Guid.newGuid(),
        timeStamp: createdAt || new Date().toISOString(),
        author: {
            role: 'User',
            userId: userId || DefaultUserIdAndDisplayName.userId,
            displayName: authorName || DefaultUserIdAndDisplayName.displayName,
        },
        contents: [{ text: getStreamingMessageText(streamingMessage) }],
    };
};

export const composeUserMessage = (userId: string, userDisplayName: string, message: string): ChatMessage => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: {
            role: 'User',
            userId: userId,
            displayName: userDisplayName,
        },
        contents: [{ text: message }],
    };
};

export const composeDefaultAgentMessage = (): ChatMessage => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: getDefaultSREAgentAuthor(),
        contents: [],
    };
};

export const isChatMessageContentNonImageText = (chatMessageContent: ChatMessageContent): boolean => {
    return (
        !chatMessageContent.approval &&
        !chatMessageContent.azCliExecution &&
        !chatMessageContent.kubectlExecution &&
        !chatMessageContent.isImage
    );
};

export const noGapBetweenNewMessagesAndExistingMessages = (messages: Message[], currentLatestMessage?: Message) => {
    if (messages.length === 0 || !currentLatestMessage) {
        return true;
    }
    return (
        getSafeDateTime(messages[0].timeStamp).getTime() < getSafeDateTime(currentLatestMessage.timeStamp).getTime() ||
        messages.some((message: Message) => message.id === currentLatestMessage.id)
    );
};

/**
 * Group messages if current message and the previous messages are from the same author and within 5 minutes of each other
 * @param currentMessage
 * @param previousMessage
 * @returns
 */
export const shouldGroupWithPreviousMessage = (currentMessage?: Message, previousMessage?: Message) => {
    return (
        !!previousMessage &&
        !!currentMessage &&
        currentMessage.author.userId === previousMessage.author.userId &&
        getSafeDateTime(currentMessage.timeStamp).getTime() - getSafeDateTime(previousMessage.timeStamp).getTime() <= 5 * 60 * 1000
    );
};

export const shouldGroupWithPreviousMessageV2 = (currentChatMessage?: ChatMessage, previousMessage?: ChatMessage) => {
    return (
        !!previousMessage &&
        !!currentChatMessage &&
        currentChatMessage.author.userId === previousMessage.author.userId &&
        getSafeDateTime(currentChatMessage.timeStamp).getTime() - getSafeDateTime(previousMessage.timeStamp).getTime() <= 5 * 60 * 1000
    );
};

/** Returns the messages to be considered grouped (starting from the current and only checking prior) */
export const getGroupedMessages = (messages: Message[], currentMessageIndex: number): Message[] => {
    if (currentMessageIndex < 0 || currentMessageIndex >= messages.length) {
        return [];
    }

    const currentMessage = messages[currentMessageIndex];
    const groupedMessages: Message[] = [currentMessage];

    for (let i = currentMessageIndex - 1; i >= 0; i--) {
        const previousMessage = messages[i];
        if (shouldGroupWithPreviousMessage(currentMessage, previousMessage)) {
            groupedMessages.unshift(previousMessage);
        } else {
            break;
        }
    }

    return groupedMessages;
};

export const getFilteredThreads = (threads: Thread[], threadFilters?: Set<ThreadFilter>, searchText?: string): Thread[] => {
    return threads.filter(thread => {
        let match = true;

        if (searchText) {
            match = thread.title.toLocaleLowerCase().includes(searchText.toLocaleLowerCase());
        }

        if (threadFilters?.has(ThreadFilter.Incidents)) {
            match = thread.source === ThreadSource.incident;
        }

        if (threadFilters?.has(ThreadFilter.Unread)) {
            match = isThreadUnread(thread);
        }

        return match;
    });
};

/**
 * @param exponentialBackoffDepth
 * @returns The time in millseconds to wait before issuing a next request. The max interval is 15 minutes.
 */
export const getIntervalBetweenLoading = (exponentialBackoffDepth: number) => {
    const base = 100;
    const maxInterval = 15 * 60 * 1000; // 15 minutes in milliseconds
    return Math.min(base + Math.floor(Math.pow(2, exponentialBackoffDepth)) * 1000, maxInterval);
};

export const isThreadUnread = (thread: Thread): boolean => {
    if (thread.lastReadTime && thread.modifiedTimestamp) {
        return getSafeDateTime(thread.lastReadTime).getTime() < getSafeDateTime(thread.modifiedTimestamp).getTime();
    }

    // If lastReadTime is null, return true if welcome message and false if not (so we don't suddenly mark all old threads as unread)
    return thread.source === ThreadSource.welcomeMessage;
};

export const getUpdatedUnreadThreadIds = (unreadThreadsIds: Set<string>, addedThreads: Thread[]): Set<string> => {
    const updatedUnreadThreadIds = new Set(unreadThreadsIds);
    let isUnreadThreadsUpdated = false;

    for (const thread of addedThreads) {
        if (isThreadUnread(thread)) {
            updatedUnreadThreadIds.add(thread.id);
            isUnreadThreadsUpdated = true;
        }
    }

    if (isUnreadThreadsUpdated) {
        return updatedUnreadThreadIds;
    }

    return unreadThreadsIds;
};

export const removeThreadIdsFromUnreadThreads = (unreadThreadsIds: Set<string>, threadIdToRemove: string): Set<string> => {
    const updatedUnreadThreadIds = new Set(unreadThreadsIds);

    if (updatedUnreadThreadIds.has(threadIdToRemove)) {
        updatedUnreadThreadIds.delete(threadIdToRemove);
        return updatedUnreadThreadIds;
    }

    return unreadThreadsIds;
};

/**
 * Check if the incident thread is close, resolved or mitigated.
 * @param thread
 * @returns
 */
export const isIncidentThreadCompleted = (thread?: Thread | null): boolean => {
    if (!thread || thread.source !== ThreadSource.incident) {
        return true;
    }

    const status = thread.status?.incidentStatus?.status?.toLowerCase();
    return status === IncidentStatus.resolved || status === IncidentStatus.closed || status === IncidentStatus.mitigated;
};

export const isChatMessageEmpty = (message?: ChatMessage | null): boolean => {
    const messageContents = message?.contents || [];

    return !messageContents.some(content => {
        return !!content.text || !!content.approval || !!content.azCliExecution || !!content.kubectlExecution;
    });
};

export const convertMessageToChatMessage = (message: Message): ChatMessage => {
    return {
        id: message.id,
        timeStamp: message.timeStamp,
        author: {
            role: message.author.role,
            userId: message.author.userId,
            displayName: message.author.displayName,
        },
        title: message.title,
        contents: [
            {
                text: message.text,
                approval: message.approval,
                azCliExecution: message.azCliExecution,
                kubectlExecution: message.kubectlExecution,
                isDailyReport: message.isDailyReport,
            },
        ],
    };
};

export const parseThreadFromStreamingText = (text: string) => {
    const thread = JSON.parse(text) as Thread;
    if (thread && thread.id && thread.startMessage && thread.title && thread.lastMessage && thread.modifiedTimestamp) {
        return thread;
    } else {
        throw new Error('Invalid thread data received from streaming message');
    }
};

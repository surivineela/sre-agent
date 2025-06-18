import { ThreadSeverity } from '../../Common/Clients/ThreadClient';
import {
    Approval,
    AzCliExecution,
    IncidentStatus,
    Message,
    SREAgentUserId,
    StreamingMessage,
    StreamingMessageType,
    Thread,
    ThreadSource,
} from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { ThreadLoadingCounts } from '../Contracts/Activities';
import { ThreadItemHeightInPx, ThreadItemPaddingTopBottomInPx } from '../Styles/Activities.styles';
import { SelectedTimes } from './TimeDropdown';

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
 * Update the text of the existing messages if they have been updated.
 * @param prevMessages
 * @param updatedMessages
 */
export const updateOldMessagesText = (prevMessages: Message[], updatedMessages: Message[]) => {
    const updatedPrevMessages = [...prevMessages];
    let isPrevMessagesUpdated = false;

    const messagesMap: Map<string, Message> = new Map<string, Message>();
    updatedMessages.forEach((msg: Message) => messagesMap.set(msg.id, msg));

    for (let i = prevMessages.length - 1; i >= 0; i--) {
        const message = messagesMap.get(prevMessages[i].id);
        if (message && message.text !== prevMessages[i].text) {
            updatedPrevMessages[i] = { ...prevMessages[i], text: message.text };
            isPrevMessagesUpdated = true;

            messagesMap.delete(prevMessages[i].id);
            if (messagesMap.size === 0) {
                break;
            }
        }
    }

    return isPrevMessagesUpdated ? updatedPrevMessages : prevMessages;
};

/**
 * Get a new message based on the previous message and the streaming message input. Do not append message content to the previous message if doNotAppendMessage is true.
 * @param prev
 * @param streamingMessage
 * @param doNotAppendMessage
 * @returns
 */
export const processStreamingMessage = (
    prev: Message | null,
    streamingMessage: StreamingMessage,
    doNotAppendMessage?: boolean
): Message | null => {
    const { additionalProperties, contents, createdAt } = streamingMessage;
    const messageContent = contents?.[0];
    const { messageId } = additionalProperties || {};

    const id = messageId || prev?.id || '';

    const prevText = prev?.text || '';
    const newText = doNotAppendMessage ? '' : messageContent?.text || '';
    const updatedText = prevText + newText;

    const isToolCall = equals(messageContent?.$type ?? '', 'functionCall', AntUxStringComparison.IgnoreCase);
    const toolCallText = isToolCall
        ? messageContent?.additionalProperties?.userDescription || messageContent?.additionalProperties?.functionCallDescription || ''
        : '';

    const timeStamp = createdAt || new Date().toISOString();

    const updatedStreamingMessage: Message = {
        id,
        timeStamp,
        text: updatedText,
        toolCallText,
        author: {
            role: 'SREAgent',
            userId: SREAgentUserId,
            displayName: '',
        },
        approval: getSpecialMessageContentFromStreamingMessage<Approval>(streamingMessage, 'approval'),
        azCliExecution: getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'azcli'),
        kubectlExecution: getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'kubectl'),
        isDailyReport: false,
    };

    return updatedStreamingMessage;
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

export const getStreamingMessageText = (streamingMessage: StreamingMessage) => {
    return streamingMessage.contents?.[0]?.text || '';
};

export const isFinalStreamingMessage = (streamingMessage: StreamingMessage): boolean => {
    const { finishReason } = streamingMessage;

    return (
        equals(finishReason || '', 'stop', AntUxStringComparison.IgnoreCase) ||
        equals(finishReason || '', 'length', AntUxStringComparison.IgnoreCase)
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

export const getUTCTimestampBasedOnSelectedThreadCutoffTime = (selectedCutOffModifiedTime: SelectedTimes): string => {
    const days = selectedCutOffModifiedTime === SelectedTimes.OneDay ? 1 : selectedCutOffModifiedTime === SelectedTimes.SevenDays ? 7 : 30;
    const cutoff = new Date(Date.now() - days * 24 * 60 * 60 * 1000);

    return cutoff.toISOString();
};

export const getFilteredThreads = (
    threads: Thread[],
    filterOptions: {
        selectedCutoffTime: string;
        threadSeverity?: ThreadSeverity;
        searchText?: string;
        source?: ThreadSource;
    }
): Thread[] => {
    const { selectedCutoffTime, threadSeverity, searchText, source } = filterOptions;

    return threads.filter(thread => {
        let match = getSafeDateTime(thread.modifiedTimestamp).getTime() >= getSafeDateTime(selectedCutoffTime).getTime();
        if (searchText) {
            match = thread.title.toLowerCase().includes(searchText.toLocaleLowerCase());
        }
        if (threadSeverity) {
            match =
                threadSeverity === ThreadSeverity.Critical
                    ? !!thread.status?.actionsStatus?.hasCriticalActions
                    : !!thread.status?.actionsStatus?.hasWarningActions;
        }
        if (source === ThreadSource.incident) {
            match = thread.source === ThreadSource.incident;
        }
        return match;
    });
};

/**
 * Return 1.5 times of the number of threads that can fill the threads list div to make sure the div is overflowed. Return 5 if the result is less than 5.
 * @param threadsListContainerHeight
 * @param numberOfThreadsInDiv the existing number of threads in the div
 * @returns
 */
export const getNumberOfThreadsToOverflowThreadsListDiv = (
    threadsListDivHeightInPx: number | undefined,
    numberOfThreadsInDiv: number
): number => {
    if (threadsListDivHeightInPx === undefined) return ThreadLoadingCounts.default;

    const threadItemHeightInPx = ThreadItemHeightInPx + ThreadItemPaddingTopBottomInPx * 2;

    const numberOfThreadsToLoad = Math.ceil(1.5 * (threadsListDivHeightInPx / threadItemHeightInPx)) - numberOfThreadsInDiv;

    return Math.max(numberOfThreadsToLoad, ThreadLoadingCounts.default);
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

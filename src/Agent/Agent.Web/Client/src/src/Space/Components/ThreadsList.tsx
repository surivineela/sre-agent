import { ThreadSeverity } from '../../Common/Clients/ThreadClient';
import { Message, Thread, ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
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
export const processThreads = (prevThreads: Thread[], threads: Thread[], reverse: boolean) => {
    if (threads.length === 0) return prevThreads;

    const updatedExistingThreads = prevThreads;

    const threadsMap: Map<string, Thread> = new Map();
    threads.forEach(thread => threadsMap.set(thread.id, thread));

    for (let i = 0; i < prevThreads.length; i++) {
        const duplicatedThread = threadsMap.get(prevThreads[i].id);
        if (duplicatedThread) {
            if (duplicatedThread.modifiedTimestamp !== prevThreads[i].modifiedTimestamp) {
                // Remove thread with outdated modifiedTimestamp out of the existing threads
                updatedExistingThreads.splice(i, 1);
            } else {
                // Remove thread out of the threadsMap because the thread is already in the existing threads and has not been modified
                threadsMap.delete(prevThreads[i].id);
            }
        }
    }

    const threadsToAdd: Thread[] = Array.from(threadsMap.values());
    threadsToAdd.sort((a, b) => getSafeDateTime(b.modifiedTimestamp).getTime() - getSafeDateTime(a.modifiedTimestamp).getTime());

    if (threadsToAdd.length === 0) {
        return updatedExistingThreads;
    }

    return reverse ? [...threadsToAdd, ...updatedExistingThreads] : [...updatedExistingThreads, ...threadsToAdd];
};

/**
 * @param prevMessages existing messages sorted in descending order by timestamp
 * @param newMessages new messages sorted in descending order by timestamp
 * @param reverse if true, the current messages are placed before the old messages
 * @returns
 */
export const processNewMessages = (prevMessages: Message[], newMessages: Message[]) => {
    const updatedPrevMessages = prevMessages;
    const newMessagesMap: Map<string, Message> = new Map<string, Message>();
    newMessages.forEach((msg: Message) => newMessagesMap.set(msg.id, msg));

    for (let i = 0; i < updatedPrevMessages.length; i++) {
        const message = newMessagesMap.get(updatedPrevMessages[i].id);
        if (message) {
            if (message.text !== updatedPrevMessages[i].text) {
                // Update existing message
                updatedPrevMessages[i] = message;
            } else {
                // If the text is the same, we can skip updating this message
                newMessagesMap.delete(updatedPrevMessages[i].id);
            }
        }
    }

    const messagesToAdd: Message[] = Array.from(newMessagesMap.values());
    messagesToAdd.sort((a, b) => getSafeDateTime(b.timeStamp).getTime() - getSafeDateTime(a.timeStamp).getTime());

    if (messagesToAdd.length === 0) {
        // Do not return copied old messages as it will introduce unnecessary re-renders
        return updatedPrevMessages;
    }

    return [...messagesToAdd, ...updatedPrevMessages];
};

/**
 * 
 * @param prevMessages existing messages sorted in descending order by timestamp
 * @param oldMessages older messages sorted in descending order by timestamp
 */
export const processOldMessages = (prevMessages: Message[], oldMessages: Message[]) => {
    if (oldMessages.length === 0) {
        return prevMessages;
    }

    return [...prevMessages, ...oldMessages];
}

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
}

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

import { Message, Thread } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';

export const processThreads = (prevThreads: Thread[], threads: Thread[], reverse: boolean) => {
    const threadsToAdd: Thread[] = [];
    const uniqueThreadIds = new Set<string>();

    for (let i = 0; i < threads.length; i++) {
        const thread = threads[i];
        if (!prevThreads.some((t: Thread) => t.id === thread.id) && !uniqueThreadIds.has(thread.id)) {
            threadsToAdd.push(thread);
            uniqueThreadIds.add(thread.id);
        }
    }

    if (threadsToAdd.length === 0) {
        return prevThreads;
    }

    return reverse ? [...threadsToAdd, ...prevThreads] : [...prevThreads, ...threadsToAdd];
};

export const noGapBetweenNewThreadsAndExistingThreads = (threads: Thread[], currentLatestThread?: Thread) => {
    if (threads.length === 0 || !currentLatestThread) {
        return true;
    }

    return (
        getSafeDateTime(threads[0].createdTimestamp).getTime() <= getSafeDateTime(currentLatestThread.createdTimestamp).getTime() ||
        threads.some(thread => thread.id === currentLatestThread.id)
    );
};

export const getLatestThread = (threads?: Thread[]) => {
    if (threads && threads.length > 0) {
        return threads[0];
    }
};

/**
 * @param prevMessages messages sorted in ascending order by timestamp
 * @param currentMessages messages sorted in descending order by timestamp
 * @param reverse if true, the current messages are placed before the old messages
 * @returns
 */
export const processMessages = (prevMessages: Message[], currentMessages: Message[], reverse: boolean) => {
    // Instead of using a Map, we'll use findIndex directly
    const uniqueThreadIds = new Set<string>();
    const messagesToAdd: Message[] = [];
    const updatedPrevMessages = [...prevMessages];
    let isPrevMessagesUpdated = false;

    for (let i = 0; i < currentMessages.length; i++) {
        const currentMsg = currentMessages[i];
        // Skip if we've already processed this ID
        if (uniqueThreadIds.has(currentMsg.id)) {
            continue;
        }

        uniqueThreadIds.add(currentMsg.id);
        const existingMsgIndex = prevMessages.findIndex(msg => msg.id === currentMsg.id);

        if (existingMsgIndex === -1) {
            // New message
            messagesToAdd.unshift(currentMsg);
        } else if (currentMsg.text !== prevMessages[existingMsgIndex].text) {
            // Update existing message
            updatedPrevMessages[existingMsgIndex] = currentMsg;
            isPrevMessagesUpdated = true;
        }
    }

    if (messagesToAdd.length === 0) {
        // Do not return copied old messages as it will introduce unnecessary re-renders
        return isPrevMessagesUpdated ? updatedPrevMessages : prevMessages;
    }

    return reverse
        ? [...messagesToAdd, ...(isPrevMessagesUpdated ? updatedPrevMessages : prevMessages)]
        : [...(isPrevMessagesUpdated ? updatedPrevMessages : prevMessages), ...messagesToAdd];
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

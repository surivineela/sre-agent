import { Thread } from "../../Common/Contracts/SreAgent";
import { getSafeDateTime } from "../../Common/Helpers/Date";

export const processThreads = (prevThreads: Thread[], threads: Thread[], reverse: boolean) => {
    const threadsToAdd: Thread[] = [];

    for (let i = 0; i < threads.length; i++) {
        const thread = threads[i];
        if (!prevThreads.some((t: Thread) => t.id === thread.id)) {
            threadsToAdd.push(thread);
        }
    }

    if (threadsToAdd.length === 0) {
        return prevThreads;
    }

    return reverse ? [...threadsToAdd, ...prevThreads] : [...prevThreads, ...threadsToAdd];
}

export const noGapBetweenNewThreadsAndExistingThreads = (threads: Thread[], currentLatestThread?: Thread) => {
    if (threads.length === 0 || !currentLatestThread) {
        return true;
    }

    return getSafeDateTime(threads[0].createdTimestamp).getTime() <= getSafeDateTime(currentLatestThread.createdTimestamp).getTime() || threads.some(thread => thread.id === currentLatestThread.id);
}

export const getLatestThread = (threads?: Thread[]) => {
    if (threads && threads.length > 0) {
        return threads[0];
    }
}
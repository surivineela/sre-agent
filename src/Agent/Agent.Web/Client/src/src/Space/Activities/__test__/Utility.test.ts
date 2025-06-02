import { describe, expect, it } from 'vitest';
import { ThreadSeverity } from '../../../Common/Clients/ThreadClient';
import { Message, Thread, ThreadSource } from '../../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../../Common/Helpers/Date';
import { Guid } from '../../../Common/Helpers/Guid';
import { SelectedTimes } from '../TimeDropdown';
import {
    getFilteredThreads,
    getUTCTimestampBasedOnSelectedThreadCutoffTime,
    noGapBetweenNewMessagesAndExistingMessages,
    processMessages,
    processThreads,
    shouldGroupWithPreviousMessage,
} from '../Utility';

const getDefaultThread = (
    modifiedTimestamp: string,
    id?: string,
    severity?: ThreadSeverity,
    title?: string,
    source?: ThreadSource
): Thread => {
    return {
        id: id ?? Guid.newGuid(),
        createdTimestamp: modifiedTimestamp,
        modifiedTimestamp: modifiedTimestamp,
        title: title ?? Guid.newTinyGuid(),
        startMessage: {
            id: Guid.newGuid(),
            timeStamp: modifiedTimestamp,
            author: {
                role: 'User',
                userId: 'Web-Client-User',
                displayName: 'Web Client User',
            },
            text: 'start message',
        },
        lastMessage: {
            id: Guid.newGuid(),
            timeStamp: modifiedTimestamp,
            author: {
                role: 'SREAgent',
                userId: 'SREAgent',
                displayName: 'SRE Agent',
            },
            text: 'last message',
        },
        status: {
            actionsStatus: {
                hasCriticalActions: severity === ThreadSeverity.Critical,
                hasWarningActions: severity === ThreadSeverity.Warning,
            },
        },
        source,
    };
};

const areThreadsSame = (lhs: Thread[], rhs: Thread[]) => {
    if (lhs.length !== rhs.length) return false;

    for (let i = 0; i < lhs.length; i++) {
        const lhsThread = lhs[i];
        const rhsThread = rhs[i];

        if (lhsThread.id !== rhsThread.id) return false;
    }

    return true;
};

const getDefaultMessage = (timeStamp: string, id?: string): Message => {
    return {
        id: id ?? Guid.newGuid(),
        timeStamp: timeStamp,
        author: {
            role: 'User',
            userId: 'Web-Client-User',
            displayName: 'Web Client User',
        },
        text: 'start message',
    };
};

const areMessagesSame = (lhs: Message[], rhs: Message[]) => {
    if (lhs.length !== rhs.length) return false;

    for (let i = 0; i < lhs.length; i++) {
        const lhsMessage = lhs[i];
        const rhsMessage = rhs[i];

        if (lhsMessage.id !== rhsMessage.id) return false;
    }

    return true;
};

describe('processThreads', () => {
    const areThreadsSortedDescByModifiedTimeStamp = (threads: Thread[]) => {
        let isSortedDesc = true;

        for (let i = 0; i < threads.length - 1; i++) {
            if (getSafeDateTime(threads[i].modifiedTimestamp).getTime() < getSafeDateTime(threads[i + 1].modifiedTimestamp).getTime()) {
                isSortedDesc = false;
                break;
            }
        }

        return isSortedDesc;
    };

    const areThreadsUnique = (threads: Thread[]) => {
        const threadIds: Set<string> = new Set<string>();

        for (const thread of threads) {
            if (threadIds.has(thread.id)) {
                return false;
            }
            threadIds.add(thread.id);
        }

        return true;
    };

    it('Add old threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),
        ];

        const oldThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
        ];

        const copiedOldThreads = [...oldThreads];
        copiedOldThreads.splice(3, 1); // Remove the duplicate thread

        const result = processThreads(threads, oldThreads, false);

        expect(areThreadsSame(result, [...threads, ...copiedOldThreads])).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add new threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z', '01'),
            getDefaultThread('2023-10-04T00:00:00Z', '01'),
        ];

        const copiedNewThreads = [...newThreads];
        copiedNewThreads.splice(3, 1);

        const result = processThreads(threads, newThreads, true);

        expect(areThreadsSame(result, [...copiedNewThreads, ...threads])).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add duplicated threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
        ];

        const copiedNewThreads = [...newThreads];
        copiedNewThreads.splice(3, 1);

        const result = processThreads(threads, newThreads, true);

        const expectedResult = [...copiedNewThreads, ...threads];

        expect(result.length).toBe(6);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add new threads with duplicated id but different modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-03T01:00:00Z', '03'),
        ];

        const expectedResult = [...newThreads, ...threads.slice(1)];
        const result = processThreads(threads, newThreads, true);

        expect(result.length).toBe(6);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add old threads with duplicated id but different modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const oldThreads: Thread[] = [
            getDefaultThread('2023-09-0500:00:00Z'),
            getDefaultThread('2023-09-04T00:00:00Z'),
            getDefaultThread('2023-09-03T00:00:00Z'),
            getDefaultThread('2023-09-02T01:00:00Z', '03'),
        ];

        const expectedResult = [...threads.slice(1), ...oldThreads];
        const result = processThreads(threads, oldThreads, false);

        expect(result.length).toBe(6);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });
});

describe('getUTCTimestampBasedOnSelectedThreadCutoffTime', () => {
    it('30 days cutoff', () => {
        const thirtyDaysAgo = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.ThirtyDays);
        const diff = Date.now() - getSafeDateTime(thirtyDaysAgo).getTime();
        expect(diff).toBeGreaterThan(29 * 24 * 60 * 60 * 1000);
        expect(diff).toBeLessThan(31 * 24 * 60 * 60 * 1000);
    });

    it('7 days cutoff', () => {
        const thirtyDaysAgo = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.SevenDays);
        const diff = Date.now() - getSafeDateTime(thirtyDaysAgo).getTime();
        expect(diff).toBeGreaterThan(6 * 24 * 60 * 60 * 1000);
        expect(diff).toBeLessThan(8 * 24 * 60 * 60 * 1000);
    });

    it('1 day cutoff', () => {
        const thirtyDaysAgo = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);
        const diff = Date.now() - getSafeDateTime(thirtyDaysAgo).getTime();
        expect(diff).toBeGreaterThan(23 * 60 * 60 * 1000);
        expect(diff).toBeLessThan(25 * 60 * 60 * 1000);
    });
});

describe('getFilteredThreads', () => {
    it('Filter threads based on timestamp', () => {
        const cutoffTime = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);

        let threads: Thread[] = [getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() - 1).toISOString(), '01')];
        let result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime });
        expect(result.length).toBe(0);

        threads = [getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01')];
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime });
        expect(result.length).toBe(1);
    });

    it('Filter threads based on severity', () => {
        const cutoffTime = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);

        let threads: Thread[] = [
            getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01', ThreadSeverity.Critical),
        ];
        let result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Critical });
        expect(result.length).toBe(1);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Warning });
        expect(result.length).toBe(0);

        threads = [getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01', ThreadSeverity.Warning)];
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Critical });
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Warning });
        expect(result.length).toBe(1);

        threads = [getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01')];
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Critical });
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, threadSeverity: ThreadSeverity.Warning });
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime });
        expect(result.length).toBe(1);
    });

    it('Filter threads based on search text', () => {
        const cutoffTime = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);

        const threads: Thread[] = [
            getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01', undefined, 'Thread 01'),
        ];
        let result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, searchText: '' });
        expect(result.length).toBe(1);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, searchText: 'Thread 02' });
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, searchText: 'Thread 011' });
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, searchText: 'Thread 01' });
        expect(result.length).toBe(1);
    });

    it('Filter threads based on source', () => {
        const cutoffTime = getUTCTimestampBasedOnSelectedThreadCutoffTime(SelectedTimes.OneDay);

        let threads: Thread[] = [
            getDefaultThread(
                new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(),
                '01',
                undefined,
                undefined,
                ThreadSource.incident
            ),
        ];

        let result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, source: undefined });
        expect(result.length).toBe(1);
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, source: ThreadSource.incident });
        expect(result.length).toBe(1);

        threads = [getDefaultThread(new Date(getSafeDateTime(cutoffTime).getTime() + 1).toISOString(), '01', undefined, undefined)];
        result = getFilteredThreads(threads, { selectedCutoffTime: cutoffTime, source: ThreadSource.incident });
        expect(result.length).toBe(0);
    });
});

describe('processMessages', () => {
    const areMessagesSortedAscByTimeStamp = (messages: Message[]) => {
        let isSortedAsc = true;

        for (let i = 0; i < messages.length - 1; i++) {
            if (getSafeDateTime(messages[i].timeStamp).getTime() > getSafeDateTime(messages[i + 1].timeStamp).getTime()) {
                isSortedAsc = false;
                break;
            }
        }

        return isSortedAsc;
    };

    const areMessagesUnique = (messages: Message[]) => {
        const messageIds: Set<string> = new Set<string>();

        for (const message of messages) {
            if (messageIds.has(message.id)) {
                return false;
            }
            messageIds.add(message.id);
        }

        return true;
    };

    it('Add old messages', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-04T00:00:00Z'),
            getDefaultMessage('2023-10-05T00:00:00Z'),
            getDefaultMessage('2023-10-06T00:00:00Z'),
        ];

        const oldMessages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z'),
            getDefaultMessage('2023-10-02T00:00:00Z'),
            getDefaultMessage('2023-10-01T00:00:00Z', '01'),
            getDefaultMessage('2023-10-01T00:00:00Z', '01'),
        ];

        const copiedOldMessages = [...oldMessages];
        copiedOldMessages.splice(3, 1);

        const result = processMessages(messages, oldMessages, true);

        expect(areMessagesSame(result, [...copiedOldMessages.reverse(), ...messages])).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });

    it('Add new messages', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-01T00:00:00Z'),
            getDefaultMessage('2023-10-02T00:00:00Z'),
            getDefaultMessage('2023-10-03T00:00:00Z'),
        ];

        const newMessages: Message[] = [
            getDefaultMessage('2023-10-06T00:00:00Z'),
            getDefaultMessage('2023-10-05T00:00:00Z', '01'),
            getDefaultMessage('2023-10-05T00:00:00Z', '01'),
            getDefaultMessage('2023-10-04T00:00:00Z'),
        ];

        const result = processMessages(messages, newMessages, false);

        newMessages.splice(2, 1);
        expect(areMessagesSame(result, [...messages, ...newMessages.reverse()])).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });

    it('Add duplicated messages', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-01T00:00:00Z', '01'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
        ];

        const newMessages: Message[] = [
            getDefaultMessage('2023-10-06T00:00:00Z'),
            getDefaultMessage('2023-10-05T00:00:00Z'),
            getDefaultMessage('2023-10-04T00:00:00Z'),
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-01T00:00:00Z', '01'),
        ];

        const result = processMessages(messages, newMessages, false);
        const expectedResult = [...messages, ...newMessages.reverse().slice(3)];

        expect(result.length).toBe(6);
        expect(areMessagesSame(result, expectedResult)).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });
});

describe('noGapBetweenNewMessagesAndExistingMessages', () => {
    it('No latest message', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-01T00:00:00Z'),
        ];

        expect(noGapBetweenNewMessagesAndExistingMessages(messages)).toBe(true);
    });

    it('No messages', () => {
        const latestMessage = getDefaultMessage('2023-10-03T00:00:00Z');
        expect(noGapBetweenNewMessagesAndExistingMessages([], latestMessage)).toBe(true);
    });

    it('The latest message is newer than existing messages', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-01T00:00:00Z'),
        ];

        const latestMessage = getDefaultMessage('2023-10-04T00:00:00Z');

        expect(noGapBetweenNewMessagesAndExistingMessages(messages, latestMessage)).toBe(true);
    });

    it('The oldest message is newer than the latest message', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-01T09:00:00Z'),
        ];

        const latestMessage = getDefaultMessage('2023-10-01T00:00:00Z');

        expect(noGapBetweenNewMessagesAndExistingMessages(messages, latestMessage)).toBe(false);
    });

    it('The latest message is the same as the oldest message', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z', '03'),
            getDefaultMessage('2023-10-02T00:00:00Z', '02'),
            getDefaultMessage('2023-10-01T09:00:00Z', '01'),
        ];

        const latestMessage = getDefaultMessage('2023-10-01T09:00:00Z', '01');

        expect(noGapBetweenNewMessagesAndExistingMessages(messages, latestMessage)).toBe(true);
    });
});

describe('shouldGroupWithPreviousMessage', () => {
    const getUserMessage = (timeStamp: string, userId?: string): Message => {
        return {
            id: Guid.newGuid(),
            timeStamp: timeStamp,
            author: {
                role: 'User',
                userId: userId || 'Web-Client-User',
                displayName: 'Web Client User',
            },
            text: 'start message',
        };
    };
    it('No previous message', () => {
        const currentMessage = getUserMessage('2023-10-03T00:00:00Z');
        expect(shouldGroupWithPreviousMessage(currentMessage)).toBe(false);
    });

    it('No current message', () => {
        const previousMessage = getUserMessage('2023-10-03T00:00:00Z');
        expect(shouldGroupWithPreviousMessage(undefined, previousMessage)).toBe(false);
    });

    it('Within 5 minutes but with different authors', () => {
        const currentMessage = getUserMessage('2023-10-03T00:05:00Z', 'user1');
        const previousMessage = getUserMessage('2023-10-03T00:00:00Z', 'user2');
        expect(shouldGroupWithPreviousMessage(currentMessage, previousMessage)).toBe(false);
    });

    it('Same author but outside 5 minutes', () => {
        const currentMessage = getUserMessage('2023-10-03T00:05:01Z', 'user1');
        const previousMessage = getUserMessage('2023-10-03T00:00:00Z', 'user1');
        expect(shouldGroupWithPreviousMessage(currentMessage, previousMessage)).toBe(false);
    });

    it('Same author and within 5 minutes', () => {
        const currentMessage = getUserMessage('2023-10-03T00:05:00Z', 'user1');
        const previousMessage = getUserMessage('2023-10-03T00:00:00Z', 'user1');
        expect(shouldGroupWithPreviousMessage(currentMessage, previousMessage)).toBe(true);
    });
});

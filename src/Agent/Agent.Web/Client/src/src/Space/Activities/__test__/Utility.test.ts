import { describe, expect, it } from 'vitest';
import { ThreadSeverity } from '../../../Common/Clients/ThreadClient';
import { Message, Thread, ThreadSource } from '../../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../../Common/Helpers/Date';
import { Guid } from '../../../Common/Helpers/Guid';
import { ThreadFilter } from '../../Contracts/Activities';
import {
    getFilteredThreads,
    getGroupedMessages,
    getUpdatedUnreadThreadIds,
    isThreadUnread,
    noGapBetweenNewMessagesAndExistingMessages,
    processNewMessages,
    processOldMessages,
    processThreads,
    removeThreadIdsFromUnreadThreads,
    shouldGroupWithPreviousMessage,
} from '../Utility';

const getDefaultThread = (
    modifiedTimestamp?: string,
    id?: string,
    severity?: ThreadSeverity,
    title?: string,
    source?: ThreadSource,
    lastReadTime?: string
): Thread => {
    return {
        id: id ?? Guid.newGuid(),
        createdTimestamp: modifiedTimestamp || '2023-10-06T00:00:00Z',
        modifiedTimestamp: modifiedTimestamp || '',
        title: title ?? Guid.newTinyGuid(),
        startMessage: {
            id: Guid.newGuid(),
            timeStamp: modifiedTimestamp || '2023-10-06T00:00:00Z',
            author: {
                role: 'User',
                userId: 'Web-Client-User',
                displayName: 'Web Client User',
            },
            text: 'start message',
        },
        lastMessage: {
            id: Guid.newGuid(),
            timeStamp: modifiedTimestamp || '2023-10-06T00:00:00Z',
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
        lastReadTime,
    };
};

const areThreadsSame = (lhs: Thread[], rhs: Thread[]) => {
    if (lhs.length !== rhs.length) return false;

    for (let i = 0; i < lhs.length; i++) {
        const lhsThread = lhs[i];
        const rhsThread = rhs[i];

        if (lhsThread.id !== rhsThread.id || lhsThread.modifiedTimestamp !== rhsThread.modifiedTimestamp) return false;
    }

    return true;
};

const getDefaultMessage = (timeStamp: string, id?: string, message?: string): Message => {
    return {
        id: id ?? Guid.newGuid(),
        timeStamp: timeStamp,
        author: {
            role: 'User',
            userId: 'Web-Client-User',
            displayName: 'Web Client User',
        },
        text: message ?? 'start message',
    };
};

const areMessagesSame = (lhs: Message[], rhs: Message[]) => {
    if (lhs.length !== rhs.length) return false;

    for (let i = 0; i < lhs.length; i++) {
        const lhsMessage = lhs[i];
        const rhsMessage = rhs[i];

        if (lhsMessage.id !== rhsMessage.id || lhsMessage.text !== rhsMessage.text) return false;
    }

    return true;
};

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

const areThreadIdSetSame = (lhs: Set<string>, rhs: Set<string>) => {
    if (lhs.size !== rhs.size) return false;

    for (const id of lhs) {
        if (!rhs.has(id)) {
            return false;
        }
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

        const result = processThreads(threads, oldThreads, false).threads;

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
            getDefaultThread('2023-10-04T00:00:00Z', '01'),
            getDefaultThread('2023-10-04T00:00:00Z', '01'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].slice(1).reverse();

        const result = processThreads(threads, newThreads, true).threads;

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
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].slice(1).reverse();

        const result = processThreads(threads, newThreads, true).threads;

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
            getDefaultThread('2023-10-03T01:00:01Z', '03'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].reverse();

        const expectedResult = [...copiedNewThreads, ...threads.slice(1)];
        const { threads: result, addedThreads } = processThreads(threads, newThreads, true);

        expect(result.length).toBe(6);
        expect(addedThreads.length).toBe(4);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add new threads with duplicated id and same modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].slice(1).reverse();

        const expectedResult = [...copiedNewThreads, ...threads];
        const { threads: result, addedThreads } = processThreads(threads, newThreads, true);

        expect(result.length).toBe(6);
        expect(addedThreads.length).toBe(3);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add new threads with more than one duplicated ids with different modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:01Z', '02'),
            getDefaultThread('2023-10-03T01:00:02Z'),
            getDefaultThread('2023-10-04T00:00:00Z', '03'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].reverse();

        const expectedResult = [...copiedNewThreads, ...threads.slice(2)];
        const { threads: result, addedThreads } = processThreads(threads, newThreads, true);

        expect(result.length).toBe(6);
        expect(addedThreads.length).toBe(5);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it("Add new threads with more than one duplicated ids but somehow the new thread's has older modifiedTimestamp", () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-09-24T00:00:01Z', '02'),
            getDefaultThread('2023-09-25T01:00:02Z', '03'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-06T00:00:00Z'),
        ];

        const copiedNewThreads = [...newThreads].slice(2).reverse();
        const expectedResult = [...copiedNewThreads, ...threads];
        const { threads: result, addedThreads } = processThreads(threads, newThreads, true);

        expect(result.length).toBe(6);
        expect(addedThreads.length).toBe(3);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add old threads with duplicated id and same modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
        ];

        const oldThreads: Thread[] = [
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
            getDefaultThread('2023-09-04T00:00:00Z'),
            getDefaultThread('2023-09-03T00:00:00Z'),
        ];

        const expectedResult = [...threads, ...oldThreads.slice(1)];
        const { threads: result, addedThreads } = processThreads(threads, oldThreads, false);

        expect(result.length).toBe(5);
        expect(addedThreads.length).toBe(2);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add old threads with duplicated id and different modifiedTimestamp', () => {
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

        const expectedResult = [...threads, ...oldThreads.slice(0, 3)];
        const { threads: result, addedThreads } = processThreads(threads, oldThreads, false);

        expect(result.length).toBe(6);
        expect(addedThreads.length).toBe(3);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it('Add old threads with more than one duplicated ids and different modifiedTimestamp', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z', '02'),
        ];

        const oldThreads: Thread[] = [
            getDefaultThread('2023-09-0500:00:00Z'),
            getDefaultThread('2023-09-04T00:00:00Z', '02'),
            getDefaultThread('2023-09-03T00:00:00Z'),
            getDefaultThread('2023-09-02T01:00:00Z', '03'),
        ];

        const expectedResult = [...threads, ...[oldThreads[0], oldThreads[2]]];
        const { threads: result, addedThreads } = processThreads(threads, oldThreads, false);

        expect(result.length).toBe(5);
        expect(addedThreads.length).toBe(2);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });

    it("Add old threads with more than one duplicated ids but somehow old thread's modifiedTimestamp is newer", () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z', '02'),
        ];

        const oldThreads: Thread[] = [getDefaultThread('2023-10-05T00:00:00Z', '02'), getDefaultThread('2023-10-04T00:00:00Z', '03')];

        const expectedResult = [...threads];
        const { threads: result, addedThreads } = processThreads(threads, oldThreads, false);

        expect(result.length).toBe(3);
        expect(addedThreads.length).toBe(0);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByModifiedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    });
});

describe('getFilteredThreads', () => {
    it('Filter threads based on search text', () => {
        const threads: Thread[] = [getDefaultThread(undefined, '01', undefined, 'Thread 01')];
        let result = getFilteredThreads(threads, undefined, '');
        expect(result.length).toBe(1);
        result = getFilteredThreads(threads, undefined, 'Thread 02');
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, undefined, 'Thread 011');
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, undefined, 'Thread 01');
        expect(result.length).toBe(1);
    });

    it('Filter threads based on source', () => {
        let threads: Thread[] = [getDefaultThread(undefined, '01', undefined, undefined, ThreadSource.incident)];

        let result = getFilteredThreads(threads, new Set<ThreadFilter>([ThreadFilter.Incidents]));
        expect(result.length).toBe(1);

        threads = [getDefaultThread(undefined, '01', undefined, undefined)];
        result = getFilteredThreads(threads, new Set<ThreadFilter>([ThreadFilter.Incidents]));
        expect(result.length).toBe(0);
    });

    it('Filter threads based on unread status', () => {
        let threads: Thread[] = [getDefaultThread('2023-10-06T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-05T00:00:00Z')];

        let result = getFilteredThreads(threads, new Set<ThreadFilter>([ThreadFilter.Unread]));
        expect(result.length).toBe(1);

        threads = [getDefaultThread('2023-10-06T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-07T00:00:00Z')];
        result = getFilteredThreads(threads, new Set<ThreadFilter>([ThreadFilter.Unread]));
        expect(result.length).toBe(0);
    });
});

describe('isThreadUnread', () => {
    it('No lastReadTime', () => {
        const thread = getDefaultThread('2023-10-03T00:00:00Z', '03');
        expect(isThreadUnread(thread)).toBe(false);
    });

    it('No modified timestamp', () => {
        const thread = getDefaultThread(undefined, undefined, undefined, undefined, undefined, '2023-10-03T00:00:00Z');
        expect(isThreadUnread(thread)).toBe(false);
    });

    it('Last read time is before modified timestamp', () => {
        const thread = getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-02T00:00:00Z');
        expect(isThreadUnread(thread)).toBe(true);
    });

    it('Last read time is after modified timestamp', () => {
        const thread = getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-04T00:00:00Z');
        expect(isThreadUnread(thread)).toBe(false);
    });

    it('Last read time is same as modified timestamp', () => {
        const thread = getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-03T00:00:00Z');
        expect(isThreadUnread(thread)).toBe(false);
    });

    it('Returns true for welcome threads with lastReadTime undefined', () => {
        const thread = getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, ThreadSource.welcomeMessage, undefined);
        expect(isThreadUnread(thread)).toBe(true);
    });
});

describe('getUpdatedUnreadThreadIds', () => {
    it('Empty unread thread ids', () => {
        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '02', undefined, undefined, undefined, '2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(new Set(), addedThreads);

        expect(areThreadIdSetSame(result, new Set<string>(['03']))).toBe(true);
    });

    it('All added threads are unread', () => {
        const threads: Set<string> = new Set(['05']);

        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '02', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(threads, addedThreads);
        const expectedResult = new Set<string>(addedThreads.map(thread => thread.id));
        expectedResult.add('05');
        expect(areThreadIdSetSame(result, expectedResult)).toBe(true);
    });

    it('All added threads are read', () => {
        const threads: Set<string> = new Set(['05']);

        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '02', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(threads, addedThreads);
        expect(areThreadIdSetSame(result, threads)).toBe(true);
    });

    it('Some added threads are unread', () => {
        const threads: Set<string> = new Set(['05', '06']);

        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '02', undefined, undefined, undefined, '2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(threads, addedThreads);
        const expectedResult = new Set<string>(['05', '06', '03']);
        expect(areThreadIdSetSame(result, expectedResult)).toBe(true);
    });

    it('Duplicated unread threads', () => {
        const threads: Set<string> = new Set(['05', '06']);

        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '06', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '05', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-02T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(threads, addedThreads);
        const expectedResult = new Set<string>(['05', '06', '03']);
        expect(areThreadIdSetSame(result, expectedResult)).toBe(true);
    });

    it('Duplicated read threads', () => {
        const threads: Set<string> = new Set(['05', '06']);

        const addedThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '06', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '05', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03', undefined, undefined, undefined, '2023-10-05T00:00:00Z'),
        ];

        const result = getUpdatedUnreadThreadIds(threads, addedThreads);
        const expectedResult = new Set<string>(['05', '06']);
        expect(areThreadIdSetSame(result, expectedResult)).toBe(true);
    });
});

describe('removeThreadIdsFromUnreadThreads', () => {
    it('Unread threads are empty', () => {
        const unreadThreads: Set<string> = new Set();

        const result = removeThreadIdsFromUnreadThreads(unreadThreads, '01');
        expect(areThreadIdSetSame(result, unreadThreads)).toBe(true);
    });

    it('Thread id is empty', () => {
        const unreadThreads: Set<string> = new Set(['01', '02', '03']);

        const result = removeThreadIdsFromUnreadThreads(unreadThreads, '');
        expect(areThreadIdSetSame(result, unreadThreads)).toBe(true);
    });

    it('Thread id exists in unread threads', () => {
        const unreadThreads: Set<string> = new Set(['01', '02', '03']);

        const result = removeThreadIdsFromUnreadThreads(unreadThreads, '02');
        expect(areThreadIdSetSame(result, new Set(['01', '03']))).toBe(true);
    });

    it('Thread id does not exist in unread threads', () => {
        const unreadThreads: Set<string> = new Set(['01', '02', '03']);

        const result = removeThreadIdsFromUnreadThreads(unreadThreads, '05');
        expect(areThreadIdSetSame(result, unreadThreads)).toBe(true);
    });
});

describe('processNewMessages', () => {
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

        const result = processNewMessages(messages, newMessages);

        newMessages.splice(2, 1);
        expect(areMessagesSame(result, [...messages, ...newMessages.reverse()])).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });

    it('Add duplicated messages with same text', () => {
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

        const result = processNewMessages(messages, newMessages);
        const expectedResult = [...messages, ...newMessages.reverse().slice(3)];

        expect(result.length).toBe(6);
        expect(areMessagesSame(result, expectedResult)).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });

    it('Add duplicated messages with different text', () => {
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
            getDefaultMessage('2023-10-01T00:00:00Z', '01', 'Hello'),
        ];

        const result = processNewMessages(messages, newMessages);
        messages[0].text = 'Hello'; // Update the text of the first message to match the new message
        const expectedResult = [...messages, ...newMessages.reverse().slice(3)];

        expect(result.length).toBe(6);
        expect(areMessagesSame(result, expectedResult)).toBe(true);
        expect(areMessagesSortedAscByTimeStamp(result)).toBe(true);
        expect(areMessagesUnique(result)).toBe(true);
    });
});

describe('processOldMessages', () => {
    it('Add old messages', () => {
        const messages: Message[] = [
            getDefaultMessage('2023-10-04T00:00:00Z'),
            getDefaultMessage('2023-10-05T00:00:00Z'),
            getDefaultMessage('2023-10-06T00:00:00Z'),
        ];

        const oldMessages: Message[] = [
            getDefaultMessage('2023-10-03T00:00:00Z'),
            getDefaultMessage('2023-10-02T00:00:00Z'),
            getDefaultMessage('2023-10-01T00:00:00Z'),
        ];

        const result = processOldMessages(messages, oldMessages);

        expect(areMessagesSame(result, [...oldMessages.reverse(), ...messages])).toBe(true);
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

describe('getGroupedMessages', () => {
    const makeMessage = (id: string, userId: string, timeOffsetMinutes: number, text = ''): Message => {
        const baseTime = new Date('2024-01-01T00:00:00Z').toISOString();
        return {
            id,
            timeStamp: new Date(new Date(baseTime).getTime() + timeOffsetMinutes * 60000).toISOString(),
            text,
            author: {
                role: 'SREAgent',
                userId,
                displayName: '',
            },
        };
    };

    it('returns empty array if index is out of bounds', () => {
        const messages: Message[] = [makeMessage('1', 'a', 0)];
        expect(getGroupedMessages(messages, -1)).toEqual([]);
        expect(getGroupedMessages(messages, 1)).toEqual([]);
    });

    it('returns only the current message if no previous messages', () => {
        const messages: Message[] = [makeMessage('1', 'a', 0)];
        expect(getGroupedMessages(messages, 0)).toEqual([messages[0]]);
    });

    it('groups consecutive messages from the same author within 5 minutes', () => {
        const messages: Message[] = [
            makeMessage('1', 'a', 0),
            makeMessage('2', 'a', 3), // within 5 min
            makeMessage('3', 'a', 10), // >5 min from previous
            makeMessage('4', 'a', 12), // within 5 min from previous
        ];
        // Should group only 3 and 4
        expect(getGroupedMessages(messages, 3)).toEqual([messages[2], messages[3]]);
        // Should group 1 and 2
        expect(getGroupedMessages(messages, 1)).toEqual([messages[0], messages[1]]);
    });

    it('does not group messages from different authors', () => {
        const messages: Message[] = [makeMessage('1', 'a', 0), makeMessage('2', 'b', 2), makeMessage('3', 'a', 4)];
        expect(getGroupedMessages(messages, 2)).toEqual([messages[2]]);
        expect(getGroupedMessages(messages, 1)).toEqual([messages[1]]);
    });

    it('does not group messages if time difference is more than 5 minutes', () => {
        const messages: Message[] = [makeMessage('1', 'a', 0), makeMessage('2', 'a', 6)];
        expect(getGroupedMessages(messages, 1)).toEqual([messages[1]]);
    });

    it('groups multiple prior messages if all conditions are met', () => {
        const messages: Message[] = [
            makeMessage('1', 'a', 0),
            makeMessage('2', 'a', 2),
            makeMessage('3', 'a', 4),
            makeMessage('4', 'a', 10), // >5 min from previous
            makeMessage('5', 'a', 12),
        ];
        // Should group 1,2,3 for index 2
        expect(getGroupedMessages(messages, 2)).toEqual([messages[0], messages[1], messages[2]]);
        // Should group only 4 and 5 for index 4
        expect(getGroupedMessages(messages, 4)).toEqual([messages[3], messages[4]]);
    });
});

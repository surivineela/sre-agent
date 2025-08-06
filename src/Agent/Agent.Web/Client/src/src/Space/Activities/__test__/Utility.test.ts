import { describe, expect, it } from 'vitest';
import { ThreadSeverity } from '../../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime } from '../../../Common/Helpers/Date';
import { Guid } from '../../../Common/Helpers/Guid';
import { ThreadFilter } from '../../Contracts/Activities';
import {
    getFilteredThreads,
    getUpdatedUnreadThreadIds,
    isThreadUnread,
    processThreads,
    removeThreadIdsFromUnreadThreads,
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

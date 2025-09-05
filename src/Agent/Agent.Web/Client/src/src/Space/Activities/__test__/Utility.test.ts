import { describe, expect, it } from 'vitest';
import { ThreadSeverity } from '../../../Common/Clients/ThreadClient';
import { Thread, ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime } from '../../../Common/Helpers/Date';
import { Guid } from '../../../Common/Helpers/Guid';
import { ThreadListsState, ThreadListState } from '../../Contracts/Activities';
import {
    addOldThreads,
    addThreadToThreadsThatHaveModifiedTimestampUpdated,
    getFilteredThreads,
    getUpdatedUnreadThreadIds,
    insertThreadThatHasFavoritePropertyChangedToThreadListState,
    insertThreadToThreadList,
    isThreadUnread,
    processThreads,
    pushAllThreadsThatHaveFavoritePropertyChangedToThreadLists,
    pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists,
    removeThreadFromThreadListsState,
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
        let result = getFilteredThreads(threads, undefined, undefined, undefined, '');
        expect(result.length).toBe(1);
        result = getFilteredThreads(threads, undefined, undefined, undefined, 'Thread 02');
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, undefined, undefined, undefined, 'Thread 011');
        expect(result.length).toBe(0);
        result = getFilteredThreads(threads, undefined, undefined, undefined, 'Thread 01');
        expect(result.length).toBe(1);
    });

    it('Filter threads based on source', () => {
        let threads: Thread[] = [getDefaultThread(undefined, '01', undefined, undefined, ThreadSource.incident)];

        let result = getFilteredThreads(threads, undefined, [ThreadSource.incident], undefined, undefined);
        expect(result.length).toBe(0);

        threads = [getDefaultThread(undefined, '01', undefined, undefined)];
        result = getFilteredThreads(threads, undefined, [ThreadSource.incident], undefined, undefined);
        expect(result.length).toBe(1);
    });

    it('Filter threads based on unread status', () => {
        let threads: Thread[] = [getDefaultThread('2023-10-06T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-05T00:00:00Z')];

        let result = getFilteredThreads(threads, undefined, undefined, true, undefined);
        expect(result.length).toBe(1);

        threads = [getDefaultThread('2023-10-06T00:00:00Z', '01', undefined, undefined, undefined, '2023-10-07T00:00:00Z')];
        result = getFilteredThreads(threads, undefined, undefined, true, undefined);
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

describe('addOldThreads', () => {
    it('Duplicated with both threads and threadsThatHaveFavoritePropertiesChanged', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadsThatHaveFavoritePropertiesChanged = [
            getDefaultThread('2023-09-20T00:00:00Z', '02'),
            getDefaultThread('2023-09-19T00:00:00Z', '01'),
            getDefaultThread('2023-09-18T00:00:00Z', '00'),
        ];

        const threadListState: ThreadListState = {
            threads: threads,
            threadsThatHaveFavoritePropertyChanged: threadsThatHaveFavoritePropertiesChanged,
            moreThreadsToLoad: false,
        };

        const oldThreads = [
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
            getDefaultThread('2023-09-21T00:00:00Z', '023'),
            getDefaultThread('2023-09-18T00:00:00Z', '00'),
        ];

        const result = addOldThreads(threadListState, oldThreads, true);

        const newThreadListState = result.threadListState;
        const expectedThreadListState: ThreadListState = {
            threads: [...threads, ...oldThreads.slice(2)],
            threadsThatHaveFavoritePropertyChanged: threadsThatHaveFavoritePropertiesChanged.slice(0, 2),
            moreThreadsToLoad: true,
        };

        expect(areThreadsSame(newThreadListState.threads, expectedThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                newThreadListState.threadsThatHaveFavoritePropertyChanged,
                expectedThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(newThreadListState.moreThreadsToLoad).toBe(true);
    });

    it('No duplication', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadsThatHaveFavoritePropertiesChanged = [
            getDefaultThread('2023-09-20T00:00:00Z', '02'),
            getDefaultThread('2023-09-19T00:00:00Z', '01'),
            getDefaultThread('2023-09-18T00:00:00Z', '00'),
        ];

        const threadListState: ThreadListState = {
            threads: threads,
            threadsThatHaveFavoritePropertyChanged: threadsThatHaveFavoritePropertiesChanged,
            moreThreadsToLoad: false,
        };

        const oldThreads = [
            getDefaultThread('2023-10-01T00:00:00Z', '11'),
            getDefaultThread('2023-09-22T00:00:00Z', '12'),
            getDefaultThread('2023-09-21T00:00:00Z', '13'),
        ];

        const result = addOldThreads(threadListState, oldThreads, false);

        const newThreadListState = result.threadListState;
        const expectedThreadListState: ThreadListState = {
            threads: [...threads, ...oldThreads],
            threadsThatHaveFavoritePropertyChanged: threadsThatHaveFavoritePropertiesChanged,
            moreThreadsToLoad: false,
        };

        expect(areThreadsSame(newThreadListState.threads, expectedThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                newThreadListState.threadsThatHaveFavoritePropertyChanged,
                expectedThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(newThreadListState.moreThreadsToLoad).toBe(false);
    });
});

describe('insertThreadToThreadList', () => {
    it('Odd number of threads with newest thread', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-04T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);

        expect(areThreadsSame(result, [threadToInsert, ...threads])).toBe(true);
    });

    it('Odd number of threads with oldest thread', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-09-30T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);

        expect(areThreadsSame(result, [...threads, threadToInsert])).toBe(true);
    });

    it('Odd number of threads with thread that has duplicated modifiedTimestamp', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-02T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);
        threads.splice(1, 0, threadToInsert); // Insert before the thread with same modifiedTimestamp

        expect(areThreadsSame(result, threads)).toBe(true);
    });

    it('Odd number of threads with thread that will be inserted in the middle', () => {
        const threads = [
            getDefaultThread('2023-10-05T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-03T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);
        threads.splice(1, 0, threadToInsert); // Insert before the thread with same modifiedTimestamp

        expect(areThreadsSame(result, threads)).toBe(true);
    });

    it('Even number of threads with newest thread', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
            getDefaultThread('2023-09-30T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-04T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);

        expect(areThreadsSame(result, [threadToInsert, ...threads])).toBe(true);
    });

    it('Even number of threads with oldest thread', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
            getDefaultThread('2023-09-30T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-09-29T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);

        expect(areThreadsSame(result, [...threads, threadToInsert])).toBe(true);
    });

    it('Even number of threads with thread that has duplicated modifiedTimestamp', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
            getDefaultThread('2023-09-30T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-01T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);
        threads.splice(2, 0, threadToInsert); // Insert before the thread with same modifiedTimestamp

        expect(areThreadsSame(result, threads)).toBe(true);
    });

    it('Even number of threads with thread that will be inserted in the middle', () => {
        const threads = [
            getDefaultThread('2023-10-05T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
            getDefaultThread('2023-09-30T00:00:00Z', '03'),
        ];

        const threadToInsert = getDefaultThread('2023-10-03T00:00:00Z', '07');

        const result = insertThreadToThreadList(threads, threadToInsert);
        threads.splice(1, 0, threadToInsert); // Insert before the thread with same modifiedTimestamp

        expect(areThreadsSame(result, threads)).toBe(true);
    });
});

describe('addThreadToThreadsThatHaveModifiedTimestampUpdated', () => {
    it('Add a thread that has older modifiedTimestamp', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: threads,
        };

        const threadToInsert = getDefaultThread('2023-09-30T00:00:00Z', '06');

        const result = addThreadToThreadsThatHaveModifiedTimestampUpdated(threadListsState, threadToInsert);

        expect(
            areThreadsSame(
                result.threadListsState.threadsThatHaveModifiedTimestampUpdated,
                threadListsState.threadsThatHaveModifiedTimestampUpdated
            )
        ).toBe(true);
    });

    it('Add a thread that has newer modifiedTimestamp', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: threads,
        };

        const threadToInsert = getDefaultThread('2023-10-05T10:00:00Z', '05');

        const result = addThreadToThreadsThatHaveModifiedTimestampUpdated(threadListsState, threadToInsert);

        expect(
            areThreadsSame(result.threadListsState.threadsThatHaveModifiedTimestampUpdated, [
                getDefaultThread('2023-10-05T10:00:00Z', '05'),
                getDefaultThread('2023-10-03T00:00:00Z', '06'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ])
        ).toBe(true);
    });

    it('Add an unique thread', () => {
        const threads = [
            getDefaultThread('2023-10-03T00:00:00Z', '06'),
            getDefaultThread('2023-10-02T00:00:00Z', '05'),
            getDefaultThread('2023-10-01T00:00:00Z', '03'),
        ];

        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: threads,
        };

        const threadToInsert = getDefaultThread('2023-10-05T10:00:00Z', '07');

        const result = addThreadToThreadsThatHaveModifiedTimestampUpdated(threadListsState, threadToInsert);

        expect(areThreadsSame(result.threadListsState.threadsThatHaveModifiedTimestampUpdated, [threadToInsert, ...threads])).toBe(true);
    });
});

describe('removeThreadFromThreadListsState', () => {
    it('Remove a thread that does not exist', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-03T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-20T00:00:00Z', '02')],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05')],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-19T00:00:00Z', '01')],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: [getDefaultThread('2023-10-01T00:00:00Z', '03')],
        };

        const result = removeThreadFromThreadListsState(threadListsState, '07');

        expect(areThreadsSame(result.favoriteThreadListState.threads, threadListsState.favoriteThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                result.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(areThreadsSame(result.regularThreadListState.threads, threadListsState.regularThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                result.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(
            areThreadsSame(result.threadsThatHaveModifiedTimestampUpdated, threadListsState.threadsThatHaveModifiedTimestampUpdated)
        ).toBe(true);
    });

    it('Remove a thread that exists in all lists', () => {
        const threadToRemove = getDefaultThread('2023-10-03T00:00:00Z', '06');

        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [threadToRemove],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-20T00:00:00Z', '02'), threadToRemove],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05'), threadToRemove],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-19T00:00:00Z', '01'), threadToRemove],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: [getDefaultThread('2023-10-01T00:00:00Z', '03'), threadToRemove],
        };

        const result = removeThreadFromThreadListsState(threadListsState, threadToRemove.id);

        expect(areThreadsSame(result.favoriteThreadListState.threads, [])).toBe(true);
        expect(
            areThreadsSame(result.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged, [
                getDefaultThread('2023-09-20T00:00:00Z', '02'),
            ])
        ).toBe(true);
        expect(areThreadsSame(result.regularThreadListState.threads, [getDefaultThread('2023-10-02T00:00:00Z', '05')])).toBe(true);
        expect(
            areThreadsSame(result.regularThreadListState.threadsThatHaveFavoritePropertyChanged, [
                getDefaultThread('2023-09-19T00:00:00Z', '01'),
            ])
        ).toBe(true);
        expect(areThreadsSame(result.threadsThatHaveModifiedTimestampUpdated, [getDefaultThread('2023-10-01T00:00:00Z', '03')])).toBe(true);
    });
});

describe('insertThreadThatHasFavoritePropertyChangedToThreadListState', () => {
    it('Insert that thread that exists in the thread list', () => {
        const threadListState: ThreadListState = {
            threads: [
                getDefaultThread('2023-10-03T00:00:00Z', '06'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ],
            threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-20T00:00:00Z', '02')],
            moreThreadsToLoad: false,
        };

        const threadToInsert = getDefaultThread('2023-10-02T00:00:00Z', '05');

        const result = insertThreadThatHasFavoritePropertyChangedToThreadListState(threadListState, threadToInsert);

        expect(areThreadsSame(threadListState.threads, result.threads)).toBe(true);
        expect(areThreadsSame(threadListState.threadsThatHaveFavoritePropertyChanged, result.threadsThatHaveFavoritePropertyChanged)).toBe(
            true
        );
        expect(result.moreThreadsToLoad).toBe(threadListState.moreThreadsToLoad);
    });

    it('Insert that thread that does not exist in the thread list and newer than the oldest thread in the list', () => {
        const threadListState: ThreadListState = {
            threads: [
                getDefaultThread('2023-10-05T00:00:00Z', '08'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ],
            threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-10-04T00:00:00Z', '07')],
            moreThreadsToLoad: false,
        };

        const threadToInsert = getDefaultThread('2023-10-04T00:00:00Z', '07');

        const result = insertThreadThatHasFavoritePropertyChangedToThreadListState(threadListState, threadToInsert);

        expect(
            areThreadsSame(
                [
                    getDefaultThread('2023-10-05T00:00:00Z', '08'),
                    getDefaultThread('2023-10-04T00:00:00Z', '07'),
                    getDefaultThread('2023-10-02T00:00:00Z', '05'),
                    getDefaultThread('2023-10-01T00:00:00Z', '03'),
                ],
                result.threads
            )
        ).toBe(true);
        expect(areThreadsSame([], result.threadsThatHaveFavoritePropertyChanged)).toBe(true);
        expect(result.moreThreadsToLoad).toBe(threadListState.moreThreadsToLoad);
    });

    it('Insert that thread that does not exist in the thread list, older than the oldest thread in the list and moreThreadsToLoad is true', () => {
        const threadListState: ThreadListState = {
            threads: [
                getDefaultThread('2023-10-05T00:00:00Z', '08'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ],
            threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-10-04T00:00:00Z', '07')],
            moreThreadsToLoad: true,
        };

        const threadToInsert = getDefaultThread('2023-09-20T00:00:00Z', '01');

        const result = insertThreadThatHasFavoritePropertyChangedToThreadListState(threadListState, threadToInsert);

        expect(areThreadsSame(threadListState.threads, result.threads)).toBe(true);
        expect(
            areThreadsSame(
                [getDefaultThread('2023-10-04T00:00:00Z', '07'), getDefaultThread('2023-09-20T00:00:00Z', '01')],
                result.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(result.moreThreadsToLoad).toBe(threadListState.moreThreadsToLoad);
    });

    it('Insert that thread that does not exist in the thread list, older than the oldest thread in the list and moreThreadsToLoad is false', () => {
        const threadListState: ThreadListState = {
            threads: [
                getDefaultThread('2023-10-05T00:00:00Z', '08'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ],
            threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-20T00:00:00Z', '07')],
            moreThreadsToLoad: false,
        };

        const threadToInsert = getDefaultThread('2023-09-30T00:00:00Z', '02');

        const result = insertThreadThatHasFavoritePropertyChangedToThreadListState(threadListState, threadToInsert);

        expect(
            areThreadsSame(
                [
                    getDefaultThread('2023-10-05T00:00:00Z', '08'),
                    getDefaultThread('2023-10-02T00:00:00Z', '05'),
                    getDefaultThread('2023-10-01T00:00:00Z', '03'),
                    getDefaultThread('2023-09-30T00:00:00Z', '02'),
                ],
                result.threads
            )
        ).toBe(true);
        expect(areThreadsSame(threadListState.threadsThatHaveFavoritePropertyChanged, result.threadsThatHaveFavoritePropertyChanged)).toBe(
            true
        );
        expect(result.moreThreadsToLoad).toBe(threadListState.moreThreadsToLoad);
    });

    it('Insert that thread that exists in the threadsThatHaveFavoritePropertyChanged list, older than the oldest thread in the list and moreThreadsToLoad is true ', () => {
        const threadListState: ThreadListState = {
            threads: [
                getDefaultThread('2023-10-05T00:00:00Z', '08'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ],
            threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-20T00:00:00Z', '07')],
            moreThreadsToLoad: true,
        };

        const threadToInsert = getDefaultThread('2023-09-30T00:00:00Z', '07');

        const result = insertThreadThatHasFavoritePropertyChangedToThreadListState(threadListState, threadToInsert);

        expect(areThreadsSame(threadListState.threads, result.threads)).toBe(true);
        expect(areThreadsSame(threadListState.threadsThatHaveFavoritePropertyChanged, result.threadsThatHaveFavoritePropertyChanged)).toBe(
            true
        );
        expect(result.moreThreadsToLoad).toBe(threadListState.moreThreadsToLoad);
    });
});

describe('pushAllThreadsThatHaveFavoritePropertyChangedToThreadLists', () => {
    it('threadsThatHaveFavoritePropertyChange is empty', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-03T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05')],
                threadsThatHaveFavoritePropertyChanged: [],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: [getDefaultThread('2023-10-01T00:00:00Z', '03')],
        };

        const result = pushAllThreadsThatHaveFavoritePropertyChangedToThreadLists(threadListsState);

        expect(areThreadsSame(threadListsState.favoriteThreadListState.threads, result.favoriteThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                result.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(result.favoriteThreadListState.moreThreadsToLoad).toBe(threadListsState.favoriteThreadListState.moreThreadsToLoad);
        expect(areThreadsSame(threadListsState.regularThreadListState.threads, result.regularThreadListState.threads)).toBe(true);
        expect(
            areThreadsSame(
                threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                result.regularThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(result.regularThreadListState.moreThreadsToLoad).toBe(threadListsState.regularThreadListState.moreThreadsToLoad);
        expect(
            areThreadsSame(threadListsState.threadsThatHaveModifiedTimestampUpdated, result.threadsThatHaveModifiedTimestampUpdated)
        ).toBe(true);
        expect(result.isLoadingInitialThreads).toBe(threadListsState.isLoadingInitialThreads);
    });

    it('threadsThatHaveFavoritePropertyChange has threads', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-05T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-10-04T00:00:00Z', '07')],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05')],
                threadsThatHaveFavoritePropertyChanged: [getDefaultThread('2023-09-30T00:00:00Z', '04')],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: [getDefaultThread('2023-10-01T00:00:00Z', '03')],
        };

        const result = pushAllThreadsThatHaveFavoritePropertyChangedToThreadLists(threadListsState);

        expect(
            areThreadsSame(result.favoriteThreadListState.threads, [
                getDefaultThread('2023-10-05T00:00:00Z', '06'),
                getDefaultThread('2023-10-04T00:00:00Z', '07'),
            ])
        ).toBe(true);
        expect(areThreadsSame(result.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged, [])).toBe(true);
        expect(result.favoriteThreadListState.moreThreadsToLoad).toBe(threadListsState.favoriteThreadListState.moreThreadsToLoad);
        expect(
            areThreadsSame(result.regularThreadListState.threads, [
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-09-30T00:00:00Z', '04'),
            ])
        ).toBe(true);
        expect(areThreadsSame(result.regularThreadListState.threadsThatHaveFavoritePropertyChanged, [])).toBe(true);
        expect(result.regularThreadListState.moreThreadsToLoad).toBe(threadListsState.regularThreadListState.moreThreadsToLoad);
        expect(
            areThreadsSame(threadListsState.threadsThatHaveModifiedTimestampUpdated, result.threadsThatHaveModifiedTimestampUpdated)
        ).toBe(true);
        expect(result.isLoadingInitialThreads).toBe(threadListsState.isLoadingInitialThreads);
    });
});

describe('pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists', () => {
    it('No duplications and all new threads are newer than the current newest threads', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-04T00:00:00Z', '07'), getDefaultThread('2023-10-03T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-20T00:00:00Z', '02'),
                    getDefaultThread('2023-09-19T00:00:00Z', '01'),
                ],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05'), getDefaultThread('2023-10-01T00:00:00Z', '03')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-18T00:00:00Z', '00'),
                    getDefaultThread('2023-09-17T00:00:00Z', 'ab'),
                ],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: true,
            threadsThatHaveModifiedTimestampUpdated: [],
        };

        const newThreads: Thread[] = [getDefaultThread('2023-10-07T00:00:00Z', '09'), getDefaultThread('2023-10-06T00:00:00Z', '08')];

        const { threadListsState: updatedThreadListsState } = pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists(
            threadListsState,
            newThreads
        );

        expect(
            areThreadsSame(updatedThreadListsState.favoriteThreadListState.threads, threadListsState.favoriteThreadListState.threads)
        ).toBe(true);
        expect(
            areThreadsSame(
                updatedThreadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(updatedThreadListsState.favoriteThreadListState.moreThreadsToLoad).toBe(
            threadListsState.favoriteThreadListState.moreThreadsToLoad
        );
        expect(
            areThreadsSame(updatedThreadListsState.regularThreadListState.threads, [
                getDefaultThread('2023-10-07T00:00:00Z', '09'),
                getDefaultThread('2023-10-06T00:00:00Z', '08'),
                getDefaultThread('2023-10-02T00:00:00Z', '05'),
                getDefaultThread('2023-10-01T00:00:00Z', '03'),
            ])
        ).toBe(true);
        expect(
            areThreadsSame(
                updatedThreadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(updatedThreadListsState.regularThreadListState.moreThreadsToLoad).toBe(
            threadListsState.regularThreadListState.moreThreadsToLoad
        );
        expect(areThreadsSame(updatedThreadListsState.threadsThatHaveModifiedTimestampUpdated, [])).toBe(true);
        expect(updatedThreadListsState.isLoadingInitialThreads).toBe(true);
    });

    it('Duplications on both lists but new threads are outdated', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-04T00:00:00Z', '07'), getDefaultThread('2023-10-03T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-20T00:00:00Z', '02'),
                    getDefaultThread('2023-09-19T00:00:00Z', '01'),
                ],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05'), getDefaultThread('2023-10-01T00:00:00Z', '03')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-18T00:00:00Z', '00'),
                    getDefaultThread('2023-09-17T00:00:00Z', 'ab'),
                ],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: false,
            threadsThatHaveModifiedTimestampUpdated: [],
        };

        const newThreads: Thread[] = [
            getDefaultThread('2023-09-01T00:00:00Z', '07'),
            getDefaultThread('2023-08-31T00:00:00Z', '01'),
            getDefaultThread('2023-08-30T00:00:00Z', '03'),
            getDefaultThread('2023-08-29T00:00:00Z', '00'),
        ];

        const { threadListsState: updatedThreadListsState } = pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists(
            threadListsState,
            newThreads
        );

        expect(
            areThreadsSame(updatedThreadListsState.favoriteThreadListState.threads, threadListsState.favoriteThreadListState.threads)
        ).toBe(true);
        expect(
            areThreadsSame(
                updatedThreadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(updatedThreadListsState.favoriteThreadListState.moreThreadsToLoad).toBe(
            threadListsState.favoriteThreadListState.moreThreadsToLoad
        );
        expect(
            areThreadsSame(updatedThreadListsState.regularThreadListState.threads, threadListsState.regularThreadListState.threads)
        ).toBe(true);
        expect(
            areThreadsSame(
                updatedThreadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged,
                threadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged
            )
        ).toBe(true);
        expect(updatedThreadListsState.regularThreadListState.moreThreadsToLoad).toBe(
            threadListsState.regularThreadListState.moreThreadsToLoad
        );
        expect(areThreadsSame(updatedThreadListsState.threadsThatHaveModifiedTimestampUpdated, [])).toBe(true);
        expect(updatedThreadListsState.isLoadingInitialThreads).toBe(false);
    });

    it('Duplications on both lists and new threads are all updated', () => {
        const threadListsState: ThreadListsState = {
            favoriteThreadListState: {
                threads: [getDefaultThread('2023-10-04T00:00:00Z', '07'), getDefaultThread('2023-10-03T00:00:00Z', '06')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-20T00:00:00Z', '02'),
                    getDefaultThread('2023-09-19T00:00:00Z', '01'),
                ],
                moreThreadsToLoad: false,
            },
            regularThreadListState: {
                threads: [getDefaultThread('2023-10-02T00:00:00Z', '05'), getDefaultThread('2023-10-01T00:00:00Z', '03')],
                threadsThatHaveFavoritePropertyChanged: [
                    getDefaultThread('2023-09-18T00:00:00Z', '00'),
                    getDefaultThread('2023-09-17T00:00:00Z', 'ab'),
                ],
                moreThreadsToLoad: false,
            },
            isLoadingInitialThreads: false,
            threadsThatHaveModifiedTimestampUpdated: [],
        };

        const newThreads: Thread[] = [
            getDefaultThread('2023-11-30T00:00:00Z', '07'),
            getDefaultThread('2023-11-29T00:00:00Z', '06'),
            getDefaultThread('2023-11-28T00:00:00Z', '02'),
            getDefaultThread('2023-11-27T00:00:00Z', '01'),
            getDefaultThread('2023-11-26T00:00:00Z', '05'),
            getDefaultThread('2023-11-25T00:00:00Z', '03'),
            getDefaultThread('2023-11-18T00:00:00Z', '00'),
            getDefaultThread('2023-11-17T00:00:00Z', 'ab'),
        ];

        const { threadListsState: updatedThreadListsState } = pushAllThreadsThatHaveModifiedTimestampUpdatedToThreadLists(
            threadListsState,
            newThreads
        );

        expect(
            areThreadsSame(updatedThreadListsState.favoriteThreadListState.threads, [
                getDefaultThread('2023-11-30T00:00:00Z', '07'),
                getDefaultThread('2023-11-29T00:00:00Z', '06'),
                getDefaultThread('2023-11-28T00:00:00Z', '02'),
                getDefaultThread('2023-11-27T00:00:00Z', '01'),
            ])
        ).toBe(true);
        expect(areThreadsSame(updatedThreadListsState.favoriteThreadListState.threadsThatHaveFavoritePropertyChanged, [])).toBe(true);
        expect(updatedThreadListsState.favoriteThreadListState.moreThreadsToLoad).toBe(
            threadListsState.favoriteThreadListState.moreThreadsToLoad
        );
        expect(
            areThreadsSame(updatedThreadListsState.regularThreadListState.threads, [
                getDefaultThread('2023-11-26T00:00:00Z', '05'),
                getDefaultThread('2023-11-25T00:00:00Z', '03'),
                getDefaultThread('2023-11-18T00:00:00Z', '00'),
                getDefaultThread('2023-11-17T00:00:00Z', 'ab'),
            ])
        ).toBe(true);
        expect(areThreadsSame(updatedThreadListsState.regularThreadListState.threadsThatHaveFavoritePropertyChanged, [])).toBe(true);
        expect(updatedThreadListsState.regularThreadListState.moreThreadsToLoad).toBe(
            threadListsState.regularThreadListState.moreThreadsToLoad
        );
        expect(areThreadsSame(updatedThreadListsState.threadsThatHaveModifiedTimestampUpdated, [])).toBe(true);
        expect(updatedThreadListsState.isLoadingInitialThreads).toBe(false);
    });
});

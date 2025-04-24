import { describe, it, expect } from 'vitest';
import { Thread } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { getLatestThread, noGapBetweenNewThreadsAndExistingThreads, processThreads } from './Utility';

const getDefaultThread = (createdTimestamp: string, id?: string): Thread => {
    const title = Guid.newTinyGuid();
    return {
        id: id ?? Guid.newGuid(),
        createdTimestamp: createdTimestamp,
        modifiedTimestamp: createdTimestamp,
        title: title,
        startMessage: {
            id: Guid.newGuid(),
            timestamp: createdTimestamp,
            author: {
                role: 'User',
                userId: 'Web-Client-User',
                displayName: 'Web Client User',
            },
            text: 'start message'
        },
        lastMessage: {
            id: Guid.newGuid(),
            timestamp: createdTimestamp,
            author: {
                role: 'SREAgent',
                userId: 'SREAgent',
                displayName: 'SRE Agent',
            },
            text: 'last message'
        }
    }
}

const areThreadsSame = (lhs: Thread[], rhs: Thread[]) => {
    if (lhs.length !== rhs.length) return false;

    for (let i = 0; i < lhs.length; i++) {
        const lhsThread = lhs[i];
        const rhsThread = rhs[i];

        if (lhsThread.id !== rhsThread.id) return false;
    }

    return true;
}


describe('processThreads', () => {

    const areThreadsSortedDescByCreatedTimeStamp = (threads: Thread[]) => {
        let isSortedDesc = true;

        for (let i = 0; i < threads.length - 1; i++) {
            if (getSafeDateTime(threads[i].createdTimestamp).getTime() < getSafeDateTime(threads[i + 1].createdTimestamp).getTime()) {
                isSortedDesc = false;
                break;
            }
        }

        return isSortedDesc;
    }

    const areThreadsUnique = (threads: Thread[]) => {
        const threadIds: Set<string> = new Set<string>();

        for (const thread of threads) {
            if (threadIds.has(thread.id)) {
                return false;
            }
            threadIds.add(thread.id);
        }

        return true;
    }

    it('Add old threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),
        ];

        const oldThreads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const result = processThreads(threads, oldThreads, false);

        expect(areThreadsSame(result, [...threads, ...oldThreads])).toBe(true);
        expect(areThreadsSortedDescByCreatedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    })

    it('Add new threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z'),
            getDefaultThread('2023-10-02T00:00:00Z'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),

        ];

        const result = processThreads(threads, newThreads, true);

        expect(areThreadsSame(result, [...newThreads, ...threads])).toBe(true);
        expect(areThreadsSortedDescByCreatedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    })

    it('Add duplicated threads', () => {

        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const newThreads: Thread[] = [
            getDefaultThread('2023-10-06T00:00:00Z'),
            getDefaultThread('2023-10-05T00:00:00Z'),
            getDefaultThread('2023-10-04T00:00:00Z'),
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),

        ];

        const result = processThreads(threads, newThreads, true);
        const expectedResult = [...newThreads.slice(0, 3), ...threads]

        expect(result.length).toBe(6);
        expect(areThreadsSame(result, expectedResult)).toBe(true);
        expect(areThreadsSortedDescByCreatedTimeStamp(result)).toBe(true);
        expect(areThreadsUnique(result)).toBe(true);
    })
})

describe('noGapBetweenNewThreadsAndExistingThreads', () => {
    it('No latest thread', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        expect(noGapBetweenNewThreadsAndExistingThreads(threads)).toBe(true);
    })

    it('No threads', () => {
        const latestThread = getDefaultThread('2023-10-03T00:00:00Z');
        expect(noGapBetweenNewThreadsAndExistingThreads([], latestThread)).toBe(true);
    })

    it('The latest thread is newer than existing threads', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z'),
        ];

        const latestThread = getDefaultThread('2023-10-04T00:00:00Z');

        expect(noGapBetweenNewThreadsAndExistingThreads(threads, latestThread)).toBe(true);
    });

    it('The oldest thread is newer than the latest thread', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T09:00:00Z'),
        ];

        const latestThread = getDefaultThread('2023-10-01T00:00:00Z');

        expect(noGapBetweenNewThreadsAndExistingThreads(threads, latestThread)).toBe(false);
    });

    it('The latest thread is the same as the oldest thread', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T09:00:00Z', '01'),
        ];

        const latestThread = getDefaultThread('2023-10-01T09:00:00Z', '01');

        expect(noGapBetweenNewThreadsAndExistingThreads(threads, latestThread)).toBe(true);
    });

    it('The newest thread is older than the latest thread', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
        ];

        const latestThread = getDefaultThread('2023-10-04T09:00:00Z', '99');

        expect(noGapBetweenNewThreadsAndExistingThreads(threads, latestThread)).toBe(true);
    });

    it('Threads contains the latest thread', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
        ];

        const latestThread = getDefaultThread('2023-10-03T00:00:00Z', '03');

        expect(noGapBetweenNewThreadsAndExistingThreads(threads, latestThread)).toBe(true);
    });
});

describe('getLatestThread', () => {

    it('Undefined threads', () => {
        expect(getLatestThread(undefined)).toBeUndefined();
    });

    it('Empty threads', () => {
        expect(getLatestThread([])).toBeUndefined();
    })

    it('Thread are not empty', () => {
        const threads: Thread[] = [
            getDefaultThread('2023-10-03T00:00:00Z', '03'),
            getDefaultThread('2023-10-02T00:00:00Z', '02'),
            getDefaultThread('2023-10-01T00:00:00Z', '01'),
        ];

        const latestThread = getLatestThread(threads);
        expect(latestThread).toBeDefined();
        expect(latestThread?.id).toBe('03');
    })
})
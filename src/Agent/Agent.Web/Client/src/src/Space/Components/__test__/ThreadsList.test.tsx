import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { RefObject } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Thread } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import ThreadsList from '../ThreadsList';

const getThread = (modifiedTimestamp: string, id: string, title: string): Thread => {
    return {
        id: id ?? Guid.newGuid(),
        createdTimestamp: modifiedTimestamp,
        modifiedTimestamp: modifiedTimestamp,
        title: title,
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
    };
};

describe('ThreadsList.tsx', () => {
    let mockObserve: any;
    let mockUnobserve: any;

    beforeEach(() => {
        mockObserve = vi.fn();
        mockUnobserve = vi.fn();

        vi.mock('react-intl', () => {
            return {
                useIntl: vi.fn(() => {
                    return {
                        formatMessage: vi.fn(message => {
                            return message.defaultMessage;
                        }),
                    };
                }),
                defineMessages: vi.fn(messages => messages),
            };
        });

        // Mock IntersectionObserver
        vi.stubGlobal(
            'IntersectionObserver',
            vi.fn(function (this: any, callback) {
                this.observe = mockObserve;
                this.disconnect = mockUnobserve;
                this.trigger = (isIntersecting: boolean) => {
                    callback([{ isIntersecting }]);
                };
            })
        );

        vi.stubGlobal('CSS', {
            supports: vi.fn().mockImplementation(() => {
                return true;
            }),
        });
    });

    it('ThreadsList should display items', () => {
        const threads = [getThread('2023-10-06T00:00:00Z', '01', 'Thread 1'), getThread('2023-10-05T00:00:00Z', '02', 'Thread 2')];
        const ref: RefObject<HTMLDivElement> | null = null;
        render(
            <ThreadsList
                threads={threads}
                selectThread={() => {}}
                activeThreadId="02"
                isLoadingInitialThreads={false}
                hasMoreOldThreads={false}
                loadMoreOldThreads={() => Promise.resolve(undefined)}
                ref={ref}
                unreadThreadIds={new Set([])}
            />
        );

        const thread1 = screen.getByTestId('01');
        expect(thread1).toBeInTheDocument();

        const thread2 = screen.getByTestId('02');
        expect(thread2).toBeInTheDocument();
        expect(thread2).toHaveClass(/activeThreadItem/);
    });

    it('ThreadsList shimmering status', () => {
        const threads = [getThread('2023-10-06T00:00:00Z', '01', 'Thread 1'), getThread('2023-10-05T00:00:00Z', '02', 'Thread 2')];
        const ref: RefObject<HTMLDivElement> | null = null;
        const { container: containerWithSkeleton } = render(
            <ThreadsList
                threads={threads}
                selectThread={() => {}}
                activeThreadId="02"
                isLoadingInitialThreads={false}
                hasMoreOldThreads={true}
                loadMoreOldThreads={() => Promise.resolve(undefined)}
                ref={ref}
                unreadThreadIds={new Set([])}
            />
        );

        let skeleton = containerWithSkeleton.querySelector('[class*="fui-Skeleton"]');
        expect(skeleton).toBeInTheDocument();
        let skeletonItem = containerWithSkeleton.querySelector('[class*="fui-SkeletonItem"]');
        expect(skeletonItem).toBeInTheDocument();

        const { container: containerWithNoSkeleton } = render(
            <ThreadsList
                threads={threads}
                selectThread={() => {}}
                activeThreadId="02"
                isLoadingInitialThreads={false}
                hasMoreOldThreads={false}
                loadMoreOldThreads={() => Promise.resolve(undefined)}
                ref={ref}
                unreadThreadIds={new Set([])}
            />
        );

        skeleton = containerWithNoSkeleton.querySelector('[class*="fui-Skeleton"]');
        expect(skeleton).not.toBeInTheDocument();
        skeletonItem = containerWithNoSkeleton.querySelector('[class*="fui-SkeletonItem"]');
        expect(skeletonItem).not.toBeInTheDocument();
    });
});

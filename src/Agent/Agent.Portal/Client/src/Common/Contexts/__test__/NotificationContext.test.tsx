import { act, renderHook } from '@testing-library/react';
import React from 'react';
import { IntlProvider } from 'react-intl';
import { describe, expect, it } from 'vitest';
import { NotificationProvider, useNotifications } from '../NotificationContext';

const withProviders =
    () =>
    ({ children }: React.PropsWithChildren) => (
        <IntlProvider locale="en">
            <NotificationProvider>{children}</NotificationProvider>
        </IntlProvider>
    );

describe('NotificationContext', () => {
    describe('succeed', () => {
        it('reuses original title when not provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            expect(result.current.notifications).toHaveLength(1);
            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].description).toBe('Original Description');
            expect(result.current.notifications[0].status).toBe('in-progress');

            act(() => {
                result.current.succeed(notificationId);
            });

            expect(result.current.notifications).toHaveLength(1);
            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].status).toBe('success');
        });

        it('reuses original description when not provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            expect(result.current.notifications[0].description).toBe('Original Description');

            act(() => {
                result.current.succeed(notificationId);
            });

            expect(result.current.notifications[0].description).toBe('Original Description');
        });

        it('overrides title and description when provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.succeed(notificationId, 'New Title', 'New Description');
            });

            expect(result.current.notifications).toHaveLength(1);
            expect(result.current.notifications[0].title).toBe('New Title');
            expect(result.current.notifications[0].description).toBe('New Description');
            expect(result.current.notifications[0].status).toBe('success');
        });

        it('overrides only title when only title is provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.succeed(notificationId, 'New Title');
            });

            expect(result.current.notifications[0].title).toBe('New Title');
            expect(result.current.notifications[0].description).toBe('Original Description');
        });

        it('overrides only description when only description is provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.succeed(notificationId, undefined, 'New Description');
            });

            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].description).toBe('New Description');
        });

        it('reuses original title when empty string is provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.succeed(notificationId, '', 'New Description');
            });

            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].description).toBe('New Description');
        });

        it('reuses original description when empty string is provided on success', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.succeed(notificationId, 'New Title', '');
            });

            expect(result.current.notifications[0].title).toBe('New Title');
            expect(result.current.notifications[0].description).toBe('Original Description');
        });
    });

    describe('fail', () => {
        it('reuses original title when not provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId);
            });

            expect(result.current.notifications).toHaveLength(1);
            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].status).toBe('error');
        });

        it('reuses original description when not provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId);
            });

            expect(result.current.notifications[0].description).toBe('Original Description');
        });

        it('overrides title and description when provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId, 'Error Title', 'Error Description');
            });

            expect(result.current.notifications).toHaveLength(1);
            expect(result.current.notifications[0].title).toBe('Error Title');
            expect(result.current.notifications[0].description).toBe('Error Description');
            expect(result.current.notifications[0].status).toBe('error');
        });

        it('overrides only title when only title is provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId, 'Error Title');
            });

            expect(result.current.notifications[0].title).toBe('Error Title');
            expect(result.current.notifications[0].description).toBe('Original Description');
        });

        it('overrides only description when only description is provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId, undefined, 'Error Description');
            });

            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].description).toBe('Error Description');
        });

        it('reuses original title when empty string is provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId, '', 'Error Description');
            });

            expect(result.current.notifications[0].title).toBe('Original Title');
            expect(result.current.notifications[0].description).toBe('Error Description');
        });

        it('reuses original description when empty string is provided on failure', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title', 'Original Description');
            });

            act(() => {
                result.current.fail(notificationId, 'Error Title', '');
            });

            expect(result.current.notifications[0].title).toBe('Error Title');
            expect(result.current.notifications[0].description).toBe('Original Description');
        });
    });

    describe('unread count', () => {
        it('does not increment unread count when updating notification', () => {
            const wrapper = withProviders();
            const { result } = renderHook(() => useNotifications(), { wrapper });

            let notificationId: string;

            act(() => {
                notificationId = result.current.start('Original Title');
            });

            expect(result.current.unreadCount).toBe(1);

            act(() => {
                result.current.succeed(notificationId);
            });

            // Should still be 1 - we're replacing, not adding
            expect(result.current.unreadCount).toBe(1);
            expect(result.current.notifications).toHaveLength(1);
        });
    });
});

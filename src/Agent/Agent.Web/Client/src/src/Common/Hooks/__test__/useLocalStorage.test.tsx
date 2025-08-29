import { renderHook, waitFor } from '@testing-library/react';
import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { LocalStorageFlags, useLocalStorage } from '../useLocalStorage';

describe('useLocalStorage', () => {
    const flag = LocalStorageFlags.IncidentManagementPopoverDismissed;
    const log = vi.fn();
    const wrapper: React.FC<React.PropsWithChildren> = ({ children }) => (
        <AzPortalContext.Provider value={{ log } as any}>{children}</AzPortalContext.Provider>
    );

    beforeEach(() => {
        localStorage.clear();
        log.mockClear();
    });

    afterEach(() => {
        localStorage.clear();
    });

    it('reads initial value from localStorage on mount', async () => {
        localStorage.setItem(flag, 'yes');
        const { result } = renderHook(() => useLocalStorage(flag), { wrapper });

        await waitFor(() => expect(result.current.item).toBe('yes'));
        expect(log).toHaveBeenCalled();
    });

    it('setItem updates state and localStorage and logs', async () => {
        const { result } = renderHook(() => useLocalStorage(flag), { wrapper });

        result.current.setItem('no');

        await waitFor(() => expect(result.current.item).toBe('no'));
        expect(localStorage.getItem(flag)).toBe('no');
        expect(log).toHaveBeenCalled();
    });

    it('removeItem removes from localStorage, sets state to null, and logs', async () => {
        localStorage.setItem(flag, 'temp');
        const { result } = renderHook(() => useLocalStorage(flag), { wrapper });

        // Ensure initial read happened
        await waitFor(() => expect(result.current.item).toBe('temp'));

        result.current.removeItem();

        await waitFor(() => expect(result.current.item).toBeNull());
        expect(localStorage.getItem(flag)).toBeNull();
        expect(log).toHaveBeenCalled();
    });
});

import { renderHook } from '@testing-library/react';
import React from 'react';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { SettingNames, useConfigSetting } from '../ConfigSettings';

const withRouter =
    (initialEntries: string[]) =>
    ({ children }: React.PropsWithChildren) => (
        <MemoryRouter initialEntries={initialEntries}>
            <Routes>
                <Route path="*" element={<>{children}</>} />
            </Routes>
        </MemoryRouter>
    );

describe('useConfigSetting', () => {
    const originalHref = window.location.href;
    const originalHostname = window.location.hostname;
    const originalPort = window.location.port;
    const setLocation = (url: string) => {
        // jsdom allows reassignment of href via defineProperty
        Object.defineProperty(window, 'location', {
            value: new URL(url),
            writable: true,
        });
    };

    beforeEach(() => {
        // Default to localhost to pick up localhost config
        setLocation('https://localhost:5173/?');
    });

    afterEach(() => {
        setLocation(originalHref);
        // Restore hostname/port in case
        Object.defineProperty(window.location, 'hostname', { value: originalHostname });
        Object.defineProperty(window.location, 'port', { value: originalPort });
    });

    it('returns environment default for ForUnitTests on localhost', () => {
        const wrapper = withRouter(['/']);
        const { result } = renderHook(() => useConfigSetting(SettingNames.ForUnitTests), { wrapper });
        expect(result.current).toBe(true);
    });

    it('query parameter overrides ForUnitTests to false (even if env default is true)', () => {
        const wrapper = withRouter(['/?forunittests=false']);
        const { result } = renderHook(() => useConfigSetting(SettingNames.ForUnitTests), { wrapper });
        expect(result.current).toBe(false);
    });

    it('query parameter overrides ForUnitTests to true', () => {
        const wrapper = withRouter(['/?forunittests=true']);
        const { result } = renderHook(() => useConfigSetting(SettingNames.ForUnitTests), { wrapper });
        expect(result.current).toBe(true);
    });
});

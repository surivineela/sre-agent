import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import AzPortalProxy from '../../AzPortalProxy/AzPortalProxy';
import { getAgentHeaders } from '../headers';

describe('headers', () => {
    describe('getAgentHeaders', () => {
        const originalEnv = { ...AzPortalProxy.envInfo };
        let originalGetter: PropertyDescriptor | undefined;

        beforeEach(() => {
            // Reset token/env between tests
            AzPortalProxy.envInfo = { ...originalEnv };
            // Capture original inStandaloneMode getter
            originalGetter = Object.getOwnPropertyDescriptor(AzPortalProxy, 'inStandaloneMode');
        });

        afterEach(() => {
            // Restore getter if we changed it
            if (originalGetter) {
                Object.defineProperty(AzPortalProxy, 'inStandaloneMode', originalGetter);
            }
            AzPortalProxy.envInfo = originalEnv;
        });

        it('always includes Content-Type and optional OBO scope', () => {
            const h1 = getAgentHeaders();
            expect(h1['Content-Type']).toBe('application/json');
            expect(h1['x-sreagent-obo-scope']).toBeUndefined();
            expect(h1['Authorization']).toBeUndefined();

            const h2 = getAgentHeaders('api://scope/.default');
            expect(h2['x-sreagent-obo-scope']).toBe('api://scope/.default');
        });

        it('adds Authorization when not in standalone mode and token present', () => {
            // Force inStandaloneMode => false
            Object.defineProperty(AzPortalProxy, 'inStandaloneMode', { get: () => false, configurable: true });
            AzPortalProxy.envInfo = { ...AzPortalProxy.envInfo, sreAgentToken: 'tok' } as any;

            const h = getAgentHeaders();
            expect(h['Authorization']).toBe('Bearer tok');
        });
    });
});

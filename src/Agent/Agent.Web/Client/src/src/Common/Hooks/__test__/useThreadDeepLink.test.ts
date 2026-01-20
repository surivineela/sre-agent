import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import AzPortalProxy from '../../AzPortalProxy/AzPortalProxy';
import { azurePortalUrl, sreaPortalUrl, standaloneReactEndpoint } from '../../Constants/Uri';
import { useThreadDeepLink } from '../useThreadDeepLink';

describe('useThreadDeepLink', () => {
    const originalDescriptor = Object.getOwnPropertyDescriptor(AzPortalProxy, 'inStandaloneMode');
    const restoreStandaloneGetter = () => {
        if (originalDescriptor) {
            Object.defineProperty(AzPortalProxy, 'inStandaloneMode', originalDescriptor);
        }
    };

    beforeEach(() => {
        // Reset env flags before each test
        AzPortalProxy.envInfo = { ...(AzPortalProxy.envInfo || {}), isCrossTenantPortalMode: false } as any;
        AzPortalProxy.isHostedInSreaPortal = false;
        restoreStandaloneGetter();
    });

    afterEach(() => {
        restoreStandaloneGetter();
        AzPortalProxy.isHostedInSreaPortal = false;
    });

    it('returns standalone deep link when running standalone', () => {
        const threadId = 'thread-123';
        const link = useThreadDeepLink(
            threadId,
            '/subscriptions/abc/resourceGroups/rg/providers/Microsoft.Foo/bar',
            'https://myagent.contoso.com'
        );
        expect(link).toBe(`${standaloneReactEndpoint}#/views/thread/${threadId}`);
    });

    it('returns cross-tenant deep link when in portal cross-tenant mode', () => {
        // Force non-standalone
        Object.defineProperty(AzPortalProxy, 'inStandaloneMode', { get: () => false });
        AzPortalProxy.envInfo = { ...(AzPortalProxy.envInfo || {}), isCrossTenantPortalMode: true } as any;

        const threadId = 'thread-xyz';
        const agentEndpoint = 'https://myagent.contoso.com';
        const displayName = 'myagent';

        const link = useThreadDeepLink(threadId, '/rsc-id', agentEndpoint);
        const expected = `${azurePortalUrl}#view/Microsoft_Azure_PaasServerless/FirstPartyAgentFrameBlade.ReactView/agentDisplayName/${encodeURIComponent(
            displayName
        )}/agentUrl/${encodeURIComponent(agentEndpoint)}/sreDeepLink/${encodeURIComponent(`views/thread/${threadId}`)}`;
        expect(link).toBe(expected);
    });

    it('returns single-tenant deep link when in portal single-tenant mode', () => {
        // Force non-standalone
        Object.defineProperty(AzPortalProxy, 'inStandaloneMode', { get: () => false });
        AzPortalProxy.envInfo = { ...(AzPortalProxy.envInfo || {}), isCrossTenantPortalMode: false } as any;

        const threadId = 't-1';
        const resourceId = '/subscriptions/0000/resourceGroups/rg/providers/Microsoft.Sample/agents/foo';

        const link = useThreadDeepLink(threadId, resourceId, 'https://myagent.contoso.com');
        const expected = `${azurePortalUrl}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/${encodeURIComponent(
            resourceId
        )}/sreLink/${encodeURIComponent(`views/thread/${threadId}`)}`;
        expect(link).toBe(expected);
    });

    it('returns sre.azure.com deep link when hosted in SREA Portal', () => {
        // Force non-standalone and SREA Portal mode
        Object.defineProperty(AzPortalProxy, 'inStandaloneMode', { get: () => false });
        AzPortalProxy.envInfo = { ...(AzPortalProxy.envInfo || {}), isCrossTenantPortalMode: false } as any;
        AzPortalProxy.isHostedInSreaPortal = true;

        const threadId = 't-srea';
        const resourceId = '/subscriptions/1111/resourceGroups/rg/providers/Microsoft.Sample/agents/sreaAgent';

        const link = useThreadDeepLink(threadId, resourceId, 'https://myagent.contoso.com');
        const expected = `${sreaPortalUrl}/agents/${encodeURIComponent(resourceId)}/views/thread/${threadId}`;
        expect(link).toBe(expected);
    });

    it('returns sre.azure.com cross-tenant deep link when hosted in SREA Portal', () => {
        // Force non-standalone, cross-tenant, and SREA Portal mode
        Object.defineProperty(AzPortalProxy, 'inStandaloneMode', { get: () => false });
        AzPortalProxy.envInfo = { ...(AzPortalProxy.envInfo || {}), isCrossTenantPortalMode: true } as any;
        AzPortalProxy.isHostedInSreaPortal = true;

        const threadId = 'ct-srea';
        const agentEndpoint = 'https://sreaagent.contoso.com';
        const displayName = 'sreaagent';

        const link = useThreadDeepLink(threadId, '/rsc-id', agentEndpoint);
        const expected = `${sreaPortalUrl}/externalagents/${encodeURIComponent(displayName)}/${encodeURIComponent(agentEndpoint)}/views/thread/${threadId}`;
        expect(link).toBe(expected);
    });
});

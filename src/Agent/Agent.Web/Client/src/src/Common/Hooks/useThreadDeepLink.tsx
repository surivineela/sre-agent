import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { azurePortalUrl, sreaPortalUrl, standaloneReactEndpoint } from '../Constants/Uri';

/**
 * Generates a deep link URL for a thread based on the current portal context.
 *
 * Azure Portal format:
 *   - Single-tenant: `{azurePortalUrl}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/{resourceId}/sreLink/views/thread/{threadId}`
 *   - Cross-tenant: `{azurePortalUrl}#view/Microsoft_Azure_PaasServerless/FirstPartyAgentFrameBlade.ReactView/agentDisplayName/{name}/agentUrl/{url}/sreDeepLink/views/thread/{threadId}`
 *
 * SREA Portal format:
 *   - Single-tenant: `{sreaPortalUrl}/agents/{resourceId}/views/thread/{threadId}`
 *   - Cross-tenant: `{sreaPortalUrl}/externalagents/{displayName}/{agentUrl}/views/thread/{threadId}`
 */
export const useThreadDeepLink = (threadId: string, resourceId: string, agentEndpoint: string) => {
    const isStandaloneMode = AzPortalProxy.inStandaloneMode;
    const isCrossTenantMode = AzPortalProxy.envInfo.isCrossTenantPortalMode;
    const isHostedInSreaPortal = AzPortalProxy.isHostedInSreaPortal;
    const agentSiteDeepLink = `views/thread/${threadId}`;
    const crossTenantDisplayName = agentEndpoint.split('.')[0].replace('https://', '');

    if (isStandaloneMode) {
        return `${standaloneReactEndpoint}#/${agentSiteDeepLink}`;
    }

    // SREA Portal uses path-based routing (resource ID is part of the URL path)
    if (isHostedInSreaPortal) {
        if (isCrossTenantMode) {
            return `${sreaPortalUrl}/externalagents/${encodeURIComponent(crossTenantDisplayName)}/${encodeURIComponent(agentEndpoint)}/${agentSiteDeepLink}`;
        }
        // Resource ID becomes part of the path directly (no encoding needed)
        return `${sreaPortalUrl}/agents${resourceId}/${agentSiteDeepLink}`;
    }

    // Azure Portal uses blade-based routing with hash fragments
    if (isCrossTenantMode) {
        return `${azurePortalUrl}#view/Microsoft_Azure_PaasServerless/FirstPartyAgentFrameBlade.ReactView/agentDisplayName/${encodeURIComponent(crossTenantDisplayName)}/agentUrl/${encodeURIComponent(agentEndpoint)}/sreDeepLink/${encodeURIComponent(agentSiteDeepLink)}`;
    }

    return `${azurePortalUrl}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/${encodeURIComponent(resourceId)}/sreLink/${encodeURIComponent(agentSiteDeepLink)}`;
};

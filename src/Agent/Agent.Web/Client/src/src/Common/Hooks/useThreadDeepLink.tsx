import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { sreAgentPortalAkaLink, standaloneReactEndpoint } from '../Constants/Uri';

/**
 * Deep link format (needs encoding): `#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/<rsc-id>/sreLink/views/activities/threads/<thread-id>`
 */
export const useThreadDeepLink = (threadId: string, resourceId: string, agentEndpoint: string) => {
    const isStandaloneMode = AzPortalProxy.inStandaloneMode;
    const isCrossTenantMode = AzPortalProxy.envInfo.isCrossTenantPortalMode;
    const agentSiteDeepLink = `views/activities/threads/${threadId}`;

    if (isStandaloneMode) {
        return `${standaloneReactEndpoint}#/${agentSiteDeepLink}`;
    }

    if (isCrossTenantMode) {
        const displayName = agentEndpoint.split('.')[0].replace('https://', '');
        return `${sreAgentPortalAkaLink}#view/Microsoft_Azure_PaasServerless/FirstPartyAgentFrameBlade.ReactView/agentDisplayName/${encodeURIComponent(displayName)}/agentUrl/${encodeURIComponent(agentEndpoint)}/sreLink/${encodeURIComponent(agentSiteDeepLink)}`;
    }

    return `${sreAgentPortalAkaLink}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/${encodeURIComponent(resourceId)}/sreLink/${encodeURIComponent(agentSiteDeepLink)}`;
};

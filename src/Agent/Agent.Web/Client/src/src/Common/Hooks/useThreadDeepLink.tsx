import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { sreAgentPortalAkaLink, standaloneReactEndpoint } from '../Constants/Uri';

/**
 * Deep link format (needs encoding): `#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/<rsc-id>/sreLink/views/activities/threads/<thread-id>`
 */
export const useThreadDeepLink = (resourceId: string, threadId: string) => {
    const isStandaloneMode = AzPortalProxy.inStandaloneMode;

    if (isStandaloneMode) {
        return `${standaloneReactEndpoint}#/views/activities/threads/${threadId}`;
    }

    const agentSiteDeepLink = `views/activities/threads/${threadId}`;
    return `${sreAgentPortalAkaLink}#view/Microsoft_Azure_PaasServerless/AgentFrameBlade.ReactView/id/${encodeURIComponent(resourceId)}/sreLink/${encodeURIComponent(agentSiteDeepLink)}`;
};

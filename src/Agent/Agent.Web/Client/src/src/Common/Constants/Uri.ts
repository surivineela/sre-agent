import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';

export const standaloneAgentEndpoint = 'https://localhost:7023';
/** 7023 still works - I believe 5173 is for `npm run watch` */
export const standaloneReactPort = '5173';
export const standaloneReactEndpoint = `https://localhost:${standaloneReactPort}/static/`;

export const azurePortalUrl = 'https://portal.azure.com';
export const sreaPortalUrl = 'https://sre.azure.com';

/**
 * Returns the appropriate portal URL based on the current hosting context.
 *
 * @warning Do NOT use this for features that are specific to or only supported by
 * the Azure Portal (e.g., identity blade links). Use `azurePortalUrl` instead.
 */
export const getCurrentPortalLink = (): string => {
    return AzPortalProxy.isHostedInSreaPortal ? sreaPortalUrl : azurePortalUrl;
};

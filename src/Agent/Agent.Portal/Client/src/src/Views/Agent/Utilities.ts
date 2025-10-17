/* TODO: Some portal conversion to be done here
// Query strings passed into iframe looks like this in Ibiza: "?Microsoft_Azure_PaasServerless_ext=foo~bar;abc~xyz"

import { addPathToHostname } from '../../Common/Utilities/Url';

// This gets converted into a query string which looks like "&foo=bar&abc=xyz"
const getQueryStringForIFrame = () => {
    const ext = getFeatureValue('ext');
    if (ext) {
        return `&${ext.replaceAll('~', '=').replaceAll(';', '&')}`;
    }

    return '';
};

const getDeeplinkHash = (sreLink?: string): string => {
    const deepLink = sreLink || getFeatureValue('srelink');
    if (!deepLink) {
        return '';
    }

    return deepLink.startsWith('/') ? `#${deepLink}` : `#/${deepLink}`;
};

const getOrigin = (url: string): string | undefined => {
    try {
        return new URL(url).origin;
    } catch {
        return undefined; // Invalid URL
    }
};

export const buildAgentUxUrl = (uxEndpoint: string, deepLink?: string): string => {
    const queryString = getQueryStringForIFrame();
    const deepLinkHash = getDeeplinkHash(deepLink);

    const baseUxUrl = addPathToHostname(uxEndpoint, staticPath);

    return `${baseUxUrl}?trustedAuthority=${window.location.origin}&shellUrl=${portalOrigin}${queryString}${deepLinkHash}`;
};

export const resolveAgentSiteUrl = (resourceId: string, sreDeepLink?: string) => {
    let { uxEndpoint, dataplaneEndpoint } = getEndpointsFromFlags();

    return SreAgentClient.getInstance()
        .getAgent(resourceId)
        .then(response => {
            if (response) {
                if (!response.isSuccessful) {
                    throw Error('Failed to get agent: ' + response.error?.message);
                }

                if (!dataplaneEndpoint) {
                    dataplaneEndpoint = response.content.properties.agentEndpoint;
                }

                if (!uxEndpoint) {
                    uxEndpoint = response.content.properties.agentEndpoint;
                }
            }

            const uxUrl = buildAgentUxUrl(uxEndpoint, sreDeepLink);

            return {
                uxUrl,
                uxOrigin: getOrigin(uxUrl),
                agentUrl: dataplaneEndpoint,
            };
        });
};
*/

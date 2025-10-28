import { IPublicClientApplication } from '@azure/msal-browser';
import { SreAgentClient } from '../../Common/Clients/SreAgentClient';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { getFeatureFlag, SettingNames } from '../../Common/Hooks/useConfigSettings';
import { addPathToHostname } from '../../Common/Utilities/Url';

/** `npm run watch` server - PORT 7023 still/always hosts the `npm run dev` output */
const uxLocalhostEndpoint = 'https://localhost:5173';
const localhostEndpoint = 'https://localhost:7023';
const staticPath = '/static/';

// Query strings passed into iframe looks like this in Ibiza: "?Microsoft_Azure_PaasServerless_ext=foo~bar;abc~xyz"
// This gets converted into a query string which looks like "&foo=bar&abc=xyz"
const getQueryStringForIFrame = () => {
    const ext = getFeatureFlag(SettingNames.Ext);
    if (ext && typeof ext === 'string') {
        return `&${ext.replaceAll('~', '=').replaceAll(';', '&')}`;
    }

    return '';
};

const getDeeplinkHash = (sreLink?: string): string => {
    const deepLink = sreLink || getFeatureFlag(SettingNames.SreLink);
    if (!deepLink || typeof deepLink !== 'string') {
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

    return `${baseUxUrl}?trustedAuthority=${window.location.origin}${queryString}${deepLinkHash}`;
};

const parseFlagValue = (value: string): { isTruthy: boolean; uxPort?: number } => {
    switch (value) {
        case 'build-port':
            return { isTruthy: true, uxPort: 7023 };
        case 'watch-port':
        case 'true':
            return { isTruthy: true, uxPort: 5173 };
        default:
            return { isTruthy: false };
    }
};

const getEndpointsFromFlags = () => {
    const sreLocalParsed = parseFlagValue(String(getFeatureFlag(SettingNames.SreLocal)));
    if (sreLocalParsed.isTruthy) {
        return {
            uxEndpoint: sreLocalParsed.uxPort === 7023 ? localhostEndpoint : uxLocalhostEndpoint,
            dataplaneEndpoint: localhostEndpoint,
        };
    }

    const sreUxLocalParsed = parseFlagValue(String(getFeatureFlag(SettingNames.SreUxLocal)));
    if (sreUxLocalParsed.isTruthy) {
        return {
            uxEndpoint: sreUxLocalParsed.uxPort === 7023 ? localhostEndpoint : uxLocalhostEndpoint,
            dataplaneEndpoint: '',
        };
    }

    return {
        uxEndpoint: '',
        dataplaneEndpoint: '',
    };
};

export const resolveAgentSiteUrl = (msalInstance: IPublicClientApplication,resourceId: string, sreDeepLink?: string) => {
    let { uxEndpoint, dataplaneEndpoint } = getEndpointsFromFlags();

    return SreAgentClient.getInstance(msalInstance, TelemetrySource.AgentIFrameView)
        .getAgent(resourceId)
        .then(response => {
            if (response) {
                if (!response.isSuccessful || !response.content) {
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

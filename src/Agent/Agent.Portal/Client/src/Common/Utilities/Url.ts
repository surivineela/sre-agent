import { getCloudEndpoints } from '../Auth/cloudConfig';
import { parseArmId } from './ArmId';

/** `https://foo.com/` + `/path` -> `https://foo.com/path` */
export const addPathToHostname = (origin: string, path: string): string => {
    const url = new URL(origin);
    return new URL(path, url.origin).href;
};

export const appendQueryString = (url: string, queryString: string): string => {
    if (!queryString) {
        return url;
    }

    if (url.includes('?')) {
        return `${url}&${queryString}`;
    }
    return `${url}?${queryString}`;
};

export const getParameterByName = (url: string | null, name: string): string | null => {
    let urlFull = url;
    if (urlFull === null) {
        urlFull = window.location.href;
    }

    if (!name) {
        return null;
    }

    const sanitizedName = name.replace(/[[\]]/g, '\\$&');
    const regex = new RegExp(`[?&]${sanitizedName}(=([^&#]*)|&|#|$)`, 'i');
    const results = regex.exec(urlFull);

    if (!results) {
        return null;
    }

    if (!results[2]) {
        return '';
    }

    return decodeURIComponent(results[2].replace(/\+/g, ' '));
};

export interface IOpenBladeInfo {
    detailBlade: string;
    detailBladeInputs: any;
    extension: string;
    asContextBlade?: boolean;
    asSubJourney?: boolean;
    operationId?: string;
}

/**
 * Constructs an Azure Portal blade URL from the openBlade parameters
 * Format: https://portal.azure.com#view/<extension>/<blade>/<param-name>/<param-value>/...
 *
 * @param bladeInfo - The blade information from the iframe message
 * @returns The constructed portal URL
 */
export const buildBladeUrl = (bladeInfo: IOpenBladeInfo): string => {
    const { portal } = getCloudEndpoints();
    const { extension, detailBlade, detailBladeInputs } = bladeInfo;

    // Start with the base portal URL and view path
    let url = `${portal}#view/${extension}/${detailBlade}`;

    // Add blade inputs as path segments
    if (detailBladeInputs && typeof detailBladeInputs === 'object') {
        for (const [key, value] of Object.entries(detailBladeInputs)) {
            if (value !== undefined && value !== null) {
                // Encode the parameter name and value for URL safety
                const encodedKey = encodeURIComponent(key);
                const encodedValue = encodeURIComponent(String(value));
                url += `/${encodedKey}/${encodedValue}`;
            }
        }
    }

    return url;
};

export const openResourceGroupOverviewInNewTab = (rscId: string) => {
    const { portal } = getCloudEndpoints();
    const armId = parseArmId(rscId);
    const portalUrl = `${portal}#resource/subscriptions/${armId.subscription}/resourceGroups/${armId.resourceGroup}/overview`;

    window.open(portalUrl, '_blank', 'noopener,noreferrer');
};

export const openSubscriptionOverviewInNewTab = (subscriptionId: string) => {
    if (!subscriptionId) {
        return;
    }
    const { portal } = getCloudEndpoints();

    const portalUrl = `${portal}#resource/subscriptions/${subscriptionId}/overview`;

    window.open(portalUrl, '_blank', 'noopener,noreferrer');
};

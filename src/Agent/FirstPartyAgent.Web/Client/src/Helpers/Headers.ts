import AzPortalProxy from "../Common/AzPortalProxy/AzPortalProxy";

export const getAgentHeaders = () => {
    const headers: { [key: string]: string } = {
        'Content-Type': 'application/json',
    };

    if (!AzPortalProxy.inStandaloneMode) {
        headers['Authorization'] = `Bearer ${AzPortalProxy.envInfo.sreAgentToken as string}`;
    }

    return headers;
};

const getCookie = (name: string): string | undefined => {
    if (typeof document === 'undefined') {
        return undefined;
    }
    const cookies = document.cookie.split(';');
    for (let i = 0; i < cookies.length; i++) {
        let cookie = cookies[i].trim();
        // Does this cookie string begin with the name we want?
        if (cookie.startsWith(name + '=')) {
            return cookie.substring(name.length + 1);
        }
    }
    return undefined;
};

export const getArmHeaders = () => {
    const headers: { [key: string]: string } = {
        'Content-Type': 'application/json',
    };

    if (!AzPortalProxy.inStandaloneMode) {
        if (AzPortalProxy.envInfo.armToken) {
            headers['Authorization'] = `Bearer ${AzPortalProxy.envInfo.armToken}`;
        }
    } else {
        const armTokenFromCookie = getCookie("armToken");
        if (armTokenFromCookie) {
            headers['Authorization'] = `Bearer ${armTokenFromCookie}`;
        } else {
            alert("ARM token is not set in cookie");
        }
    }

    return headers;
};
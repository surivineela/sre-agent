import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';

export const getAgentHeaders = () => {
    const headers: { [key: string]: string } = {
        'Content-Type': 'application/json',
    };

    if (!AzPortalProxy.inStandaloneMode) {
        headers['Authorization'] = `Bearer ${AzPortalProxy.envInfo.sreAgentToken as string}`;
    }

    return headers;
};

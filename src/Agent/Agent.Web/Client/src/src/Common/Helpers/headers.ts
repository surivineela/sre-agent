import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';

export const getAgentHeaders = (oboScope?: string) => {
    const headers: { [key: string]: string } = {
        'Content-Type': 'application/json',
    };

    if (oboScope) {
        headers['x-sreagent-obo-scope'] = oboScope;
    }

    if (!AzPortalProxy.inStandaloneMode) {
        headers['Authorization'] = `Bearer ${AzPortalProxy.envInfo.sreAgentToken as string}`;
    }

    return headers;
};

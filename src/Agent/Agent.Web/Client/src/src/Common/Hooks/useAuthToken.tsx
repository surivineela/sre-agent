import { useContext, useEffect, useState } from 'react';
import { TokenTypes } from '../AzPortalProxy/Models/ITokenInfo';
import { useAzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';

/**
 * Gets a portal auth token
 *
 * If token not already tracked by portal and available, request it
 */
export const useAuthToken = (tokenType: TokenTypes) => {
    const { requestAuthToken } = useAzPortalContext();
    const { additionalTokens } = useContext(EnvironmentContext);

    const [token, setToken] = useState<string | null>(null);

    useEffect(() => {
        const portalProxyToken = additionalTokens?.get(tokenType);
        if (!portalProxyToken) {
            requestAuthToken(tokenType);
        } else if (token !== portalProxyToken) {
            setToken(portalProxyToken);
        }
    }, [additionalTokens, token, tokenType, requestAuthToken]);

    return token;
};

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
    const [isLoading, setIsLoading] = useState<boolean>(true);

    useEffect(() => {
        const portalProxyToken = additionalTokens?.get(tokenType);
        if (!portalProxyToken) {
            requestAuthToken(tokenType);
        } else if (token !== portalProxyToken) {
            setToken(portalProxyToken);
            setIsLoading(false);
        }
    }, [additionalTokens, token, tokenType, requestAuthToken]);

    return { token, isLoading };
};

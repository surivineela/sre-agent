import { useContext, useMemo } from 'react';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageAuthor } from '../../Common/Contracts/Azure/SreAgent';

const DefaultUserIdAndDisplayName: Omit<MessageAuthor, 'role'> = {
    displayName: 'Web Client User',
    userId: 'web-client-user',
};

export const useAuthenticatedUserInfo = () => {
    const { armToken } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const userIdAndDisplayName = useMemo<Omit<MessageAuthor, 'role'>>(() => {
        if (!armToken) {
            return DefaultUserIdAndDisplayName;
        }

        try {
            const base64Url = armToken.split('.')[1];
            if (!base64Url) {
                throw new Error('Invalid token format');
            }
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(
                window
                    .atob(base64)
                    .split('')
                    .map(function (c) {
                        return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
                    })
                    .join('')
            );

            const { puid, name } = JSON.parse(jsonPayload);

            if (!puid || !name) {
                return DefaultUserIdAndDisplayName;
            }

            return {
                userId: puid,
                displayName: name,
            };
        } catch (e) {
            proxy.log({
                action: 'GetUserIdAndName',
                actionModifier: 'failed',
                data: e?.toString() || 'Failed to parse token',
            });

            return DefaultUserIdAndDisplayName;
        }
    }, [armToken, proxy.log]);

    return {
        userIdAndDisplayName,
    };
};

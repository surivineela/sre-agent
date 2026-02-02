import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';

interface UseOAuthPopupProps {
    authUrl: string;
    popupName: string;
    messageType: string;
    onSuccess: () => void;
    onError: (error: string) => void;
    checkAuthStatus: () => Promise<void>;
}

interface UseOAuthPopupResult {
    openPopup: () => void;
    isAuthenticating: boolean;
}

export const useOAuthPopup = (props: UseOAuthPopupProps): UseOAuthPopupResult => {
    const { authUrl, popupName, messageType, onSuccess, onError, checkAuthStatus } = props;
    const azPortalProxy = useContext(AzPortalContext);
    const [isAuthenticating, setIsAuthenticating] = useState(false);
    const messageHandlerRef = useRef<((event: MessageEvent) => void) | null>(null);
    const intervalRef = useRef<NodeJS.Timeout | null>(null);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            if (messageHandlerRef.current) {
                window.removeEventListener('message', messageHandlerRef.current);
            }
            if (intervalRef.current) {
                clearInterval(intervalRef.current);
            }
        };
    }, []);

    const openPopup = useCallback(() => {
        setIsAuthenticating(true);

        azPortalProxy.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: `signInTo${popupName}`,
            targetFriendlyName: `Sign in to ${popupName}`,
            valueObjectName: 'oauth-signin',
            valueObjectFriendlyName: 'OAuth Sign In',
        });

        const popup = window.open(authUrl, popupName, 'width=600,height=700,scrollbars=yes');

        if (!popup) {
            const errorMsg = 'Failed to open OAuth popup. Please allow popups for this site.';
            onError(errorMsg);
            setIsAuthenticating(false);
            azPortalProxy.log({
                action: 'openOAuthPopup',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { error: errorMsg, provider: popupName },
            });
            return;
        }

        // Listen for postMessage from OAuth callback
        const messageHandler = (event: MessageEvent) => {
            // Validate origin
            if (event.origin !== window.location.origin) return;

            // Validate message structure
            if (!event.data || typeof event.data.type !== 'string') return;

            if (event.data.type === messageType && event.data.success) {
                setIsAuthenticating(false);
                popup.close();
                window.removeEventListener('message', messageHandler);
                onSuccess();

                azPortalProxy.log({
                    action: 'oauthAuthentication',
                    actionModifier: 'succeeded',
                    logLevel: 'info',
                    data: { provider: popupName },
                });
            }
        };

        messageHandlerRef.current = messageHandler;
        window.addEventListener('message', messageHandler);

        // Poll for popup close as fallback
        const popupCheckInterval = setInterval(() => {
            if (popup.closed) {
                clearInterval(popupCheckInterval);
                if (messageHandlerRef.current) {
                    window.removeEventListener('message', messageHandlerRef.current);
                }

                // Check if authentication succeeded
                setTimeout(() => {
                    checkAuthStatus();
                    setIsAuthenticating(false);
                }, 1000);
            }
        }, 500);

        intervalRef.current = popupCheckInterval;
    }, [authUrl, popupName, messageType, onSuccess, onError, checkAuthStatus, azPortalProxy]);

    return { openPopup, isAuthenticating };
};

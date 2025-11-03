import { useCallback, useEffect, useRef } from 'react';
import { AzPortalToAgentSiteVerbs } from '../../Views/Agent/AgentIFrameContracts';
import { tokenCache } from '../Clients/TokenCache';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { JWTToken } from '../Utilities/JWTToken';
import { logTelemetryEvent } from './useTelemetry';

/**
 * Authentication scope identifiers for different Azure services.
 * Used to map token requests to the appropriate backend endpoints.
 */
export type AuthScopeIdentifier = 'arm' | 'graph' | 'sreAgent' | 'appInsights';

interface AuthTokenManagerOptions {
    telemetrySource: TelemetrySource;
    resourceId?: string;
    postMessage: (verb: string, data: object) => void;
    initialTokenTypes: AuthScopeIdentifier[];
}

interface AuthTokenManagerOptions {
    telemetrySource: TelemetrySource;
    resourceId?: string;
    postMessage: (verb: string, data: object) => void;
    initialTokenTypes: AuthScopeIdentifier[];
}

/**
 * Hook for managing authentication tokens in iframe scenarios where we need to proactively
 * send refreshed tokens to the iframe. Tokens are acquired and refreshed automatically by TokenCache.
 */
export const useAuthTokenManager = ({ telemetrySource, resourceId, postMessage, initialTokenTypes }: AuthTokenManagerOptions) => {
    const subscriptionsRef = useRef<Map<AuthScopeIdentifier, (token: JWTToken) => void>>(new Map());

    const sendToken = useCallback(
        (token: JWTToken, tokenType: AuthScopeIdentifier) => {
            // Map AuthScopeIdentifier to TokenTypes for Agent.Web compatibility
            const tokenTypeMap: Record<AuthScopeIdentifier, string> = {
                arm: 'arm',
                sreAgent: 'sreagent',
                graph: 'graph',
                appInsights: 'applicationinsightapi',
            };

            const tokenMessage = {
                token: token.raw,
                type: tokenTypeMap[tokenType],
            };

            logTelemetryEvent({
                action: 'token-send',
                actionModifier: 'to-iframe',
                telemetrySource,
                additionalData: {
                    resourceId,
                    tokenType,
                    tokenExpiration: token.expiration?.toISOString(),
                    timestamp: new Date().toISOString(),
                },
            });

            postMessage(AzPortalToAgentSiteVerbs.sendToken, tokenMessage);
        },
        [postMessage, telemetrySource, resourceId]
    );

    const handleInitialTokenSetup = useCallback(() => {
        initialTokenTypes.forEach(async tokenType => {
            try {
                // Get token from cache (will fetch if not cached)
                const token = await tokenCache.getAccessToken(tokenType);
                sendToken(token, tokenType);

                // Subscribe to token updates
                const callback = (updatedToken: JWTToken) => {
                    sendToken(updatedToken, tokenType);
                };
                subscriptionsRef.current.set(tokenType, callback);
                tokenCache.subscribeToTokenUpdate(tokenType, callback);

                logTelemetryEvent({
                    action: 'token-setup',
                    actionModifier: 'success',
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        tokenExpiration: token.expiration?.toISOString(),
                        timestamp: new Date().toISOString(),
                    },
                });
            } catch (error) {
                logTelemetryEvent({
                    action: 'token-setup',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        error: error instanceof Error ? error.message : String(error),
                        timestamp: new Date().toISOString(),
                    },
                });
            }
        });
    }, [initialTokenTypes, sendToken, telemetrySource, resourceId]);

    const handleTokenRequest = useCallback(
        async (tokenType: AuthScopeIdentifier) => {
            // Check if we're already managing this token type
            if (subscriptionsRef.current.has(tokenType)) {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'already-subscribed',
                    logLevel: LogLevel.Warning,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        timestamp: new Date().toISOString(),
                    },
                });
                return;
            }

            try {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'new-token-type',
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        timestamp: new Date().toISOString(),
                    },
                });

                // Get token from cache (will fetch if not cached)
                const token = await tokenCache.getAccessToken(tokenType);
                sendToken(token, tokenType);

                // Subscribe to token updates
                const callback = (updatedToken: JWTToken) => {
                    sendToken(updatedToken, tokenType);
                };
                subscriptionsRef.current.set(tokenType, callback);
                tokenCache.subscribeToTokenUpdate(tokenType, callback);

                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'success',
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        tokenExpiration: token.expiration?.toISOString(),
                        timestamp: new Date().toISOString(),
                    },
                });
            } catch (error) {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        error: error instanceof Error ? error.message : String(error),
                        timestamp: new Date().toISOString(),
                    },
                });
            }
        },
        [sendToken, telemetrySource, resourceId]
    );

    // Cleanup subscriptions on unmount
    useEffect(() => {
        return () => {
            subscriptionsRef.current.forEach((callback, tokenType) => {
                tokenCache.unsubscribeToTokenUpdate(tokenType, callback);
            });
            subscriptionsRef.current.clear();

            logTelemetryEvent({
                action: 'token-cleanup',
                actionModifier: 'unmount',
                telemetrySource,
                additionalData: {
                    resourceId,
                    timestamp: new Date().toISOString(),
                },
            });
        };
    }, [telemetrySource, resourceId]);

    return {
        handleInitialTokenSetup,
        handleTokenRequest,
    };
};

import { useCallback, useEffect, useRef } from 'react';
import { AzPortalToAgentSiteVerbs } from '../../Views/Agent/AgentIFrameContracts';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { acquireAccessToken } from '../Utilities/Client';
import { logTelemetryEvent } from './useTelemetry';

/**
 * Authentication scope identifiers for different Azure services.
 * Used to map token requests to the appropriate backend endpoints.
 */
export type AuthScopeIdentifier = 'api' | 'arm' | 'graph' | 'sreAgent' | 'appInsights';

const REFRESH_BUFFER_MS = 5 * 60 * 1000; // 5 minutes before expiry

interface AuthTokenManagerOptions {
    telemetrySource: TelemetrySource;
    resourceId?: string;
    postMessage: (verb: string, data: object) => void;
    initialTokenTypes: AuthScopeIdentifier[];
}

export const useAuthTokenManager = ({ telemetrySource, resourceId, postMessage, initialTokenTypes }: AuthTokenManagerOptions) => {
    const refreshTimeoutsRef = useRef<Map<AuthScopeIdentifier, number>>(new Map());
    const scheduleTokenRefreshRef = useRef<(tokenType: AuthScopeIdentifier, expiresOn: Date) => void>();

    const sendToken = useCallback(
        (tokenString: string, tokenType: AuthScopeIdentifier, expiresOn?: Date) => {
            // Map AuthScopeIdentifier to TokenTypes for Agent.Web compatibility
            const tokenTypeMap: Record<AuthScopeIdentifier, string> = {
                api: 'api',
                arm: 'arm',
                sreAgent: 'sreagent',
                graph: 'graph',
                appInsights: 'applicationinsightapi',
            };

            const tokenMessage = {
                token: tokenString,
                type: tokenTypeMap[tokenType],
            };

            logTelemetryEvent({
                action: 'token-send',
                actionModifier: 'to-iframe',
                telemetrySource,
                additionalData: {
                    resourceId,
                    tokenType,
                    tokenExpiration: expiresOn?.toISOString(),
                    timestamp: new Date().toISOString(),
                },
            });

            postMessage(AzPortalToAgentSiteVerbs.sendToken, tokenMessage);
        },
        [postMessage, telemetrySource, resourceId]
    );

    const acquireAndSendToken = useCallback(
        async (tokenType: AuthScopeIdentifier, action: string, forceRefresh = false) => {
            try {
                logTelemetryEvent({
                    action,
                    actionModifier: 'start',
                    logLevel: LogLevel.Info,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        forceRefresh,
                        timestamp: new Date().toISOString(),
                    },
                });

                const { accessToken, expiresOn } = await acquireAccessToken(tokenType, telemetrySource, forceRefresh);

                if (!accessToken) {
                    return;
                }

                sendToken(accessToken, tokenType, expiresOn);

                if (expiresOn && scheduleTokenRefreshRef.current) {
                    scheduleTokenRefreshRef.current(tokenType, expiresOn);
                }

                logTelemetryEvent({
                    action,
                    actionModifier: 'success',
                    logLevel: LogLevel.Info,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        tokenExpiration: expiresOn?.toISOString(),
                        timestamp: new Date().toISOString(),
                    },
                });
            } catch (error) {
                logTelemetryEvent({
                    action,
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
                throw error;
            }
        },
        [sendToken, telemetrySource, resourceId]
    );

    const scheduleTokenRefresh = useCallback(
        (tokenType: AuthScopeIdentifier, expiresOn: Date) => {
            const existingTimeout = refreshTimeoutsRef.current.get(tokenType);
            if (existingTimeout) {
                window.clearTimeout(existingTimeout);
            }

            const now = Date.now();
            const expiresAt = expiresOn.getTime();
            const refreshAt = expiresAt - REFRESH_BUFFER_MS;
            const delay = refreshAt - now;

            if (delay <= 0) {
                // Token is already close to expiry, refresh immediately
                acquireAndSendToken(tokenType, 'token-refresh', true);
                return;
            }

            logTelemetryEvent({
                action: 'token-refresh',
                actionModifier: 'scheduled',
                logLevel: LogLevel.Info,
                telemetrySource,
                additionalData: {
                    resourceId,
                    tokenType,
                    refreshAt: new Date(refreshAt).toISOString(),
                    expires: expiresOn.toISOString(),
                    delayMs: delay,
                    timestamp: new Date().toISOString(),
                },
            });

            const timeoutId = window.setTimeout(() => {
                acquireAndSendToken(tokenType, 'token-refresh', true);
            }, delay);

            refreshTimeoutsRef.current.set(tokenType, timeoutId);
        },
        [acquireAndSendToken, telemetrySource, resourceId]
    );

    // Update the ref after scheduleTokenRefresh is defined
    scheduleTokenRefreshRef.current = scheduleTokenRefresh;

    const handleInitialTokenSetup = useCallback(() => {
        initialTokenTypes.forEach(tokenType => {
            acquireAndSendToken(tokenType, 'token-setup');
        });
    }, [initialTokenTypes, acquireAndSendToken]);

    const handleTokenRequest = useCallback(
        async (tokenType: AuthScopeIdentifier) => {
            // Check if we're already managing this token type
            if (refreshTimeoutsRef.current.has(tokenType)) {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'already-managed',
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

            await acquireAndSendToken(tokenType, 'token-request');
        },
        [acquireAndSendToken, telemetrySource, resourceId]
    );

    // Cleanup refresh timeouts on unmount
    useEffect(() => {
        const timeouts = refreshTimeoutsRef.current;

        return () => {
            timeouts.forEach(timeoutId => {
                window.clearTimeout(timeoutId);
            });
            timeouts.clear();

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

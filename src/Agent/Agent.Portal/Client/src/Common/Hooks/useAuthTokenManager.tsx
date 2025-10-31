import { useCallback, useEffect, useRef, useState } from 'react';
import { AzPortalToAgentSiteVerbs } from '../../Views/Agent/AgentIFrameContracts';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { JWTToken } from '../Utilities/JWTToken';
import { logTelemetryEvent } from './useTelemetry';

/**
 * Authentication scope identifiers for different Azure services.
 * Used to map token requests to the appropriate backend endpoints.
 */
export type AuthScopeIdentifier = 'arm' | 'graph' | 'sreAgent' | 'appInsights';

interface TokenState {
    currentAuthToken?: string;
    expiresAt?: Date;
    timeoutId?: ReturnType<typeof setTimeout>;
    initialPromise?: Promise<{ token: string; expiresAt: Date }>;
    isRefreshing?: boolean;
}

interface AuthTokenManagerOptions {
    telemetrySource: TelemetrySource;
    resourceId?: string;
    postMessage: (verb: string, data: object) => void;
    initialTokenTypes: AuthScopeIdentifier[];
}

const prettyPrintDuration = (ms: number): string => {
    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);

    if (hours > 0) {
        return `${hours}h ${minutes % 60}m`;
    } else if (minutes > 0) {
        return `${minutes}m ${seconds % 60}s`;
    } else {
        return `${seconds}s`;
    }
};

/**
 * Hook for managing authentication tokens in iframe scenarios where we need to proactively
 * send refreshed tokens to the iframe. Tokens are acquired from the backend API.
 */
export const useAuthTokenManager = ({ telemetrySource, resourceId, postMessage, initialTokenTypes }: AuthTokenManagerOptions) => {
    const tokenStates = useRef<Map<AuthScopeIdentifier, TokenState>>(new Map());
    const [tokenNeedsRefreshMap, setTokenNeedsRefreshMap] = useState<Map<AuthScopeIdentifier, boolean>>(new Map());

    const getTokenEndpoint = useCallback((tokenType: AuthScopeIdentifier): string => {
        switch (tokenType) {
            case 'arm':
                return '/api/auth/arm-token';
            case 'graph':
                return '/api/auth/graph-token';
            case 'sreAgent':
                return '/api/auth/sre-agent-token';
            case 'appInsights':
                return '/api/auth/app-insights-token';
            default:
                throw new Error(`Unknown token type: ${tokenType}`);
        }
    }, []);

    const getAuthToken = useCallback(
        async (tokenType: AuthScopeIdentifier): Promise<{ token: string; expiresAt: Date }> => {
            const endpoint = getTokenEndpoint(tokenType);
            const response = await fetch(endpoint, {
                credentials: 'include',
            });

            if (!response.ok) {
                if (response.status === 401) {
                    window.location.href = '/api/auth/login?returnUrl=' + encodeURIComponent(window.location.pathname);
                    throw new Error('User not authenticated');
                }
                throw new Error(`Failed to acquire token: ${response.statusText}`);
            }

            const data = await response.json();
            const token = data.accessToken;
            const jwtToken = new JWTToken(token);
            const expiresAt = jwtToken.expiration;

            if (!expiresAt) {
                // Default to 1 hour if we can't decode the token
                logTelemetryEvent({
                    action: 'decode-token',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Warning,
                    telemetrySource,
                    additionalData: {
                        tokenType,
                    },
                });
                return { token, expiresAt: new Date(Date.now() + 3600 * 1000) }; // 1 hour default
            }

            return { token, expiresAt };
        },
        [getTokenEndpoint, telemetrySource]
    );

    const getOrInitTokenState = useCallback(
        (tokenType: AuthScopeIdentifier): TokenState => {
            let tokenState = tokenStates.current.get(tokenType);

            if (!tokenState) {
                tokenState = {
                    initialPromise: getAuthToken(tokenType),
                };
                tokenStates.current.set(tokenType, tokenState);
            }

            return tokenState;
        },
        [getAuthToken]
    );

    const setTokenNeedsRefresh = useCallback((tokenType: AuthScopeIdentifier, needsRefresh: boolean) => {
        setTokenNeedsRefreshMap(prev => {
            const newMap = new Map(prev);
            newMap.set(tokenType, needsRefresh);
            return newMap;
        });
    }, []);

    const sendToken = useCallback(
        (token: string, tokenType: AuthScopeIdentifier, expiresAt: Date) => {
            const state = getOrInitTokenState(tokenType);
            state.currentAuthToken = token;
            state.expiresAt = expiresAt;

            // Map AuthScopeIdentifier to TokenTypes for Agent.Web compatibility
            const tokenTypeMap: Record<AuthScopeIdentifier, string> = {
                arm: 'arm',
                sreAgent: 'sreagent',
                graph: 'graph',
                appInsights: 'applicationinsightapi',
            };

            const tokenMessage = {
                token,
                type: tokenTypeMap[tokenType],
            };

            postMessage(AzPortalToAgentSiteVerbs.sendToken, tokenMessage);
        },
        [getOrInitTokenState, postMessage]
    );

    const clearTokenTimeout = useCallback(
        (tokenType: AuthScopeIdentifier) => {
            const state = getOrInitTokenState(tokenType);
            if (state.timeoutId) {
                clearTimeout(state.timeoutId);
                state.timeoutId = undefined;
            }
        },
        [getOrInitTokenState]
    );

    const clearAllTokenTimeouts = useCallback(() => {
        for (const [tokenType] of tokenStates.current) {
            clearTokenTimeout(tokenType);
        }
    }, [clearTokenTimeout]);

    const setTimerToUpdateToken = useCallback(
        (_token: string, expiresAt: Date, tokenType: AuthScopeIdentifier) => {
            // Refresh token 5 minutes before expiration to avoid timing issues
            const refreshBuffer = 5 * 60 * 1000; // 5 minutes in milliseconds
            let timeout = expiresAt.getTime() - new Date().getTime() - refreshBuffer;
            timeout = Math.max(timeout, 0); // If token is already near expiration, refresh immediately

            const expirationString = expiresAt.toISOString();
            const loggedTimeString = new Date().toISOString();

            logTelemetryEvent({
                action: 'token-timer',
                actionModifier: 'set-timeout',
                telemetrySource,
                additionalData: {
                    resourceId,
                    tokenType,
                    timeout,
                    timeoutFormatted: prettyPrintDuration(timeout),
                    expiration: expirationString,
                    loggedTime: loggedTimeString,
                    refreshBufferMinutes: 5,
                },
            });

            const state = getOrInitTokenState(tokenType);
            clearTokenTimeout(tokenType);
            const timeoutId = setTimeout(() => setTokenNeedsRefresh(tokenType, true), timeout);
            state.timeoutId = timeoutId;
        },
        [getOrInitTokenState, resourceId, setTokenNeedsRefresh, telemetrySource, clearTokenTimeout]
    );

    const updateTokenIfExpired = useCallback(
        async (tokenType: AuthScopeIdentifier, retryCount: number = 0) => {
            const currentTokenState = getOrInitTokenState(tokenType);

            // Prevent multiple simultaneous refresh attempts
            if (currentTokenState.isRefreshing) {
                return;
            }

            currentTokenState.isRefreshing = true;

            try {
                const { token, expiresAt } = await getAuthToken(tokenType);

                const expirationString = expiresAt.toISOString();
                const loggedTimeString = new Date().toISOString();

                if (currentTokenState.currentAuthToken !== token) {
                    logTelemetryEvent({
                        action: 'token-refresh',
                        actionModifier: 'token-changed',
                        telemetrySource,
                        additionalData: {
                            resourceId,
                            tokenType,
                            expiration: expirationString,
                            loggedTime: loggedTimeString,
                        },
                    });

                    sendToken(token, tokenType, expiresAt);
                    setTimerToUpdateToken(token, expiresAt, tokenType);
                } else {
                    logTelemetryEvent({
                        action: 'token-refresh',
                        actionModifier: 'token-unchanged',
                        telemetrySource,
                        additionalData: {
                            resourceId,
                            tokenType,
                            expiration: expirationString,
                            loggedTime: loggedTimeString,
                        },
                    });
                    setTimerToUpdateToken(token, expiresAt, tokenType);
                }

                currentTokenState.isRefreshing = false;
            } catch (error) {
                currentTokenState.isRefreshing = false;

                const maxRetries = 3;
                const retryDelay = Math.min(1000 * Math.pow(2, retryCount), 10000); // Exponential backoff, max 10s

                logTelemetryEvent({
                    action: 'token-refresh',
                    actionModifier: retryCount < maxRetries ? 'failed-will-retry' : 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        retryCount,
                        retryDelay,
                        error: error instanceof Error ? error.message : String(error),
                    },
                });

                if (retryCount < maxRetries) {
                    // Schedule retry with exponential backoff
                    setTimeout(() => {
                        updateTokenIfExpired(tokenType, retryCount + 1);
                    }, retryDelay);
                }
            }
        },
        [getAuthToken, getOrInitTokenState, resourceId, sendToken, setTimerToUpdateToken, telemetrySource]
    );

    const getTokenNeedsRefresh = useCallback(
        (tokenType: AuthScopeIdentifier): boolean => {
            return !!tokenNeedsRefreshMap.get(tokenType);
        },
        [tokenNeedsRefreshMap]
    );

    const handleInitialTokenSetup = useCallback(() => {
        initialTokenTypes.forEach(tokenType => {
            const state = getOrInitTokenState(tokenType);
            state.initialPromise?.then(({ token, expiresAt }) => {
                sendToken(token, tokenType, expiresAt);
                updateTokenIfExpired(tokenType);
            });
        });
    }, [initialTokenTypes, getOrInitTokenState, sendToken, updateTokenIfExpired]);

    const handleTokenRequest = useCallback(
        (tokenType: AuthScopeIdentifier) => {
            if (!tokenStates.current.has(tokenType)) {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'add-new',
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                    },
                });

                getAuthToken(tokenType)
                    .then(({ token, expiresAt }) => {
                        getOrInitTokenState(tokenType);
                        sendToken(token, tokenType, expiresAt);
                        updateTokenIfExpired(tokenType);
                    })
                    .catch(error => {
                        logTelemetryEvent({
                            action: 'token-request',
                            actionModifier: 'failed',
                            logLevel: LogLevel.Error,
                            telemetrySource,
                            additionalData: {
                                resourceId,
                                tokenType,
                                error: error instanceof Error ? error.message : String(error),
                            },
                        });
                    });
            } else {
                logTelemetryEvent({
                    action: 'token-request',
                    actionModifier: 'already-exists',
                    logLevel: LogLevel.Warning,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                    },
                });
            }
        },
        [getAuthToken, getOrInitTokenState, sendToken, updateTokenIfExpired, telemetrySource, resourceId]
    );

    // Init initial token types
    useEffect(() => {
        initialTokenTypes.forEach(tokenType => {
            getOrInitTokenState(tokenType);
        });
    }, [initialTokenTypes, getOrInitTokenState]);

    // Refresh any tokens that need to be when tokenNeedsRefreshMap updates
    useEffect(() => {
        initialTokenTypes.forEach(tokenType => {
            const needsRefresh = getTokenNeedsRefresh(tokenType);
            if (needsRefresh) {
                updateTokenIfExpired(tokenType).then(() => setTokenNeedsRefresh(tokenType, false));
            }
        });
    }, [initialTokenTypes, getTokenNeedsRefresh, updateTokenIfExpired, setTokenNeedsRefresh, tokenNeedsRefreshMap]);

    useEffect(() => {
        return () => {
            clearAllTokenTimeouts();
        };
    }, [clearAllTokenTimeouts]);

    return {
        handleInitialTokenSetup,
        handleTokenRequest,
    };
};

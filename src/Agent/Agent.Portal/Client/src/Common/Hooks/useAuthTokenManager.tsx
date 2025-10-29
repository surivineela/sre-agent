import { useCallback, useEffect, useRef, useState } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from './useTelemetry';

/**
 * Authentication scope identifiers for different Azure services.
 * Used to map token requests to the appropriate backend endpoints.
 */
export type AuthScopeIdentifier = 'arm' | 'graph' | 'sreAgent' | 'appInsights';

interface TokenState {
    currentAuthToken?: string;
    timeoutId?: ReturnType<typeof setTimeout>;
    initialPromise?: Promise<string>;
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
        async (tokenType: AuthScopeIdentifier): Promise<string> => {
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
            return data.accessToken;
        },
        [getTokenEndpoint]
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
        (token: string, tokenType: AuthScopeIdentifier) => {
            const state = getOrInitTokenState(tokenType);
            state.currentAuthToken = token;

            const tokenMessage = {
                token,
                type: tokenType,
            };

            postMessage('sendToken', tokenMessage);
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
            let timeout = expiresAt.getTime() - new Date().getTime();
            timeout = Math.max(timeout, 0); // If token has already expired then poll immediately for new one

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
        async (tokenType: AuthScopeIdentifier) => {
            const currentTokenState = getOrInitTokenState(tokenType);

            try {
                const endpoint = getTokenEndpoint(tokenType);
                const response = await fetch(`${endpoint}?forceRefresh=true`, {
                    credentials: 'include',
                });

                if (!response.ok) {
                    if (response.status === 401) {
                        window.location.href = '/api/auth/login?returnUrl=' + encodeURIComponent(window.location.pathname);
                        throw new Error('User not authenticated');
                    }
                    throw new Error(`Failed to refresh token: ${response.statusText}`);
                }

                const data = await response.json();
                const token = data.accessToken;
                const expiresAt = data.expiresOn ? new Date(data.expiresOn) : new Date(Date.now() + 3600 * 1000);

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

                    sendToken(token, tokenType);
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
            } catch (error) {
                logTelemetryEvent({
                    action: 'token-refresh',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource,
                    additionalData: {
                        resourceId,
                        tokenType,
                        error: error instanceof Error ? error.message : String(error),
                    },
                });
            }
        },
        [getOrInitTokenState, getTokenEndpoint, resourceId, sendToken, setTimerToUpdateToken, telemetrySource]
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
            state.initialPromise?.then(token => {
                sendToken(token, tokenType);
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
                    .then(token => {
                        getOrInitTokenState(tokenType);
                        sendToken(token, tokenType);
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

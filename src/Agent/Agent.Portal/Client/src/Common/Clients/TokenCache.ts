import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { AuthScopeIdentifier } from '../Hooks/useAuthTokenManager';
import { logTelemetryEvent } from '../Hooks/useTelemetry';
import { JWTToken } from '../Utilities/JWTToken';

interface CachedToken {
    jwt: JWTToken;
    refreshTimeoutId?: number;
    inFlightPromise?: Promise<JWTToken>;
}

type TokenUpdateCallback = (token: JWTToken) => void;

/**
 * Manages authentication token caching with automatic refresh and subscription support.
 * Tokens are automatically refreshed 1 minute before expiry.
 */
export class TokenCache {
    private cache: Map<AuthScopeIdentifier, CachedToken> = new Map();
    private subscribers: Map<AuthScopeIdentifier, Set<TokenUpdateCallback>> = new Map();
    private readonly REFRESH_BUFFER_MS = 60 * 1000; // 1 minute before expiry
    private readonly MAX_RETRIES = 3;
    private readonly BASE_RETRY_DELAY_MS = 1000; // Start with 1 second

    /**
     * Gets an access token for the specified scope identifier.
     * Returns cached token if valid, otherwise fetches from backend.
     */
    async getAccessToken(scopeIdentifier: AuthScopeIdentifier): Promise<JWTToken> {
        // Check cache first
        const cached = this.cache.get(scopeIdentifier);

        console.log(`type: ${scopeIdentifier}, cached == ${!!cached}, inFlightPromise == ${!!cached?.inFlightPromise}`);

        // If there's an in-flight request, wait for it (check this FIRST to catch concurrent requests)
        if (cached?.inFlightPromise) {
            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'in-flight-wait',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    expires: cached.jwt?.expiration?.toISOString(),
                    timestamp: new Date().toISOString(),
                },
            });
            return await cached.inFlightPromise;
        }

        // If we have a valid cached token, return it
        if (cached?.jwt && !cached.jwt.isExpired(this.REFRESH_BUFFER_MS)) {
            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'cache-hit',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    expires: cached.jwt.expiration?.toISOString(),
                    timestamp: new Date().toISOString(),
                },
            });
            return cached.jwt;
        }

        // Fetch from backend
        return await this.fetchAndCacheToken(scopeIdentifier);
    }

    /**
     * Subscribes to token updates for a specific scope identifier.
     * The callback will be invoked whenever the token is updated (either from getAccessToken or background refresh).
     */
    subscribeToTokenUpdate(scopeIdentifier: AuthScopeIdentifier, callback: TokenUpdateCallback): void {
        if (!this.subscribers.has(scopeIdentifier)) {
            this.subscribers.set(scopeIdentifier, new Set());
        }
        this.subscribers.get(scopeIdentifier)!.add(callback);

        logTelemetryEvent({
            action: 'token-subscription',
            actionModifier: 'subscribe',
            logLevel: LogLevel.Info,
            telemetrySource: TelemetrySource.AuthTokenCache,
            additionalData: {
                scopeIdentifier,
                subscriberCount: this.subscribers.get(scopeIdentifier)!.size,
                timestamp: new Date().toISOString(),
            },
        });
    }

    /**
     * Unsubscribes from token updates for a specific scope identifier.
     */
    unsubscribeToTokenUpdate(scopeIdentifier: AuthScopeIdentifier, callback: TokenUpdateCallback): void {
        const callbacks = this.subscribers.get(scopeIdentifier);
        if (callbacks) {
            callbacks.delete(callback);
            if (callbacks.size === 0) {
                this.subscribers.delete(scopeIdentifier);
            }

            logTelemetryEvent({
                action: 'token-subscription',
                actionModifier: 'unsubscribe',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    subscriberCount: callbacks.size,
                    timestamp: new Date().toISOString(),
                },
            });
        }
    }

    /**
     * Fetches a token from the backend and caches it.
     */
    private async fetchAndCacheToken(scopeIdentifier: AuthScopeIdentifier, retryCount = 0): Promise<JWTToken> {
        try {
            const tokenEndpoint = `/api/auth/get-token?type=${encodeURIComponent(scopeIdentifier)}`;

            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'fetch-start',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    retryCount,
                    timestamp: new Date().toISOString(),
                },
            });

            // Create the fetch promise
            const fetchPromise = fetch(tokenEndpoint, {
                credentials: 'include',
            }).then(async response => {
                if (!response.ok) {
                    if (response.status === 401) {
                        // User is not authenticated, redirect to login
                        window.location.href = '/api/auth/login?returnUrl=' + encodeURIComponent(window.location.pathname);

                        logTelemetryEvent({
                            action: 'token-acquisition',
                            actionModifier: 'unauthenticated',
                            logLevel: LogLevel.Error,
                            telemetrySource: TelemetrySource.AuthTokenCache,
                            additionalData: {
                                scopeIdentifier,
                                timestamp: new Date().toISOString(),
                            },
                        });

                        throw new Error('User not authenticated');
                    }

                    throw new Error(`Failed to acquire token: ${response.statusText}`);
                }

                const data = await response.json();
                const token = data.accessToken;

                // Parse and cache the token
                const parsedToken = new JWTToken(token);
                this.setToken(scopeIdentifier, parsedToken);

                logTelemetryEvent({
                    action: 'token-acquisition',
                    actionModifier: 'fetch-success',
                    logLevel: LogLevel.Info,
                    telemetrySource: TelemetrySource.AuthTokenCache,
                    additionalData: {
                        scopeIdentifier,
                        expiresAt: parsedToken.expiration?.toISOString(),
                        timestamp: new Date().toISOString(),
                    },
                });

                return parsedToken;
            });

            // Store the in-flight promise in cache immediately (before awaiting)
            const cached = this.cache.get(scopeIdentifier);
            if (cached) {
                cached.inFlightPromise = fetchPromise;
            } else {
                this.cache.set(scopeIdentifier, {
                    jwt: undefined as any, // Will be set by setToken when fetch completes
                    inFlightPromise: fetchPromise,
                });
            }

            // Now await the response
            const result = await fetchPromise;

            // Clean up the in-flight promise
            const current = this.cache.get(scopeIdentifier);
            if (current) {
                delete current.inFlightPromise;
            }

            return result;
        } catch (error) {
            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'fetch-failed',
                logLevel: LogLevel.Error,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    retryCount,
                    error: error instanceof Error ? error.message : String(error),
                    timestamp: new Date().toISOString(),
                },
            });

            // Retry with exponential backoff
            if (retryCount < this.MAX_RETRIES) {
                const delay = this.BASE_RETRY_DELAY_MS * Math.pow(2, retryCount);
                await new Promise(resolve => setTimeout(resolve, delay));
                return this.fetchAndCacheToken(scopeIdentifier, retryCount + 1);
            }

            throw error;
        }
    }

    /**
     * Sets a token in cache, schedules automatic refresh, and notifies subscribers.
     */
    private setToken(scopeIdentifier: AuthScopeIdentifier, token: JWTToken): void {
        // Clear existing refresh timeout if any
        const existing = this.cache.get(scopeIdentifier);
        if (existing?.refreshTimeoutId) {
            window.clearTimeout(existing.refreshTimeoutId);
        }

        // Schedule automatic refresh
        const refreshTimeoutId = this.scheduleTokenRefresh(scopeIdentifier, token);

        // Cache the token
        this.cache.set(scopeIdentifier, {
            jwt: token,
            refreshTimeoutId,
        });

        // Notify subscribers
        this.notifySubscribers(scopeIdentifier, token);
    }

    /**
     * Schedules automatic token refresh 1 minute before expiry.
     */
    private scheduleTokenRefresh(scopeIdentifier: AuthScopeIdentifier, token: JWTToken): number | undefined {
        if (!token.expiration) {
            logTelemetryEvent({
                action: 'token-refresh',
                actionModifier: 'no-expiration',
                logLevel: LogLevel.Error,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    timestamp: new Date().toISOString(),
                },
            });
            throw new Error('Token does not have an expiration date');
        }

        const now = Date.now();
        const expiresAt = token.expiration.getTime();
        const refreshAt = expiresAt - this.REFRESH_BUFFER_MS;
        const delay = refreshAt - now;

        if (delay <= 0) {
            // Token is already expired or about to expire, refresh immediately
            this.refreshToken(scopeIdentifier);
            return undefined;
        }

        logTelemetryEvent({
            action: 'token-refresh',
            actionModifier: 'scheduled',
            logLevel: LogLevel.Info,
            telemetrySource: TelemetrySource.AuthTokenCache,
            additionalData: {
                scopeIdentifier,
                refreshAt: new Date(refreshAt).toISOString(),
                expires: token.expiration.toISOString(),
                delayMs: delay,
                timestamp: new Date().toISOString(),
            },
        });

        return window.setTimeout(() => {
            this.refreshToken(scopeIdentifier);
        }, delay);
    }

    /**
     * Refreshes a token in the background.
     */
    private async refreshToken(scopeIdentifier: AuthScopeIdentifier): Promise<void> {
        try {
            logTelemetryEvent({
                action: 'token-refresh',
                actionModifier: 'start',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    timestamp: new Date().toISOString(),
                },
            });

            await this.fetchAndCacheToken(scopeIdentifier);

            logTelemetryEvent({
                action: 'token-refresh',
                actionModifier: 'success',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    timestamp: new Date().toISOString(),
                },
            });
        } catch (error) {
            logTelemetryEvent({
                action: 'token-refresh',
                actionModifier: 'failed',
                logLevel: LogLevel.Error,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    error: error instanceof Error ? error.message : String(error),
                    timestamp: new Date().toISOString(),
                },
            });
        }
    }

    /**
     * Notifies all subscribers for a specific scope identifier.
     */
    private notifySubscribers(scopeIdentifier: AuthScopeIdentifier, token: JWTToken): void {
        const callbacks = this.subscribers.get(scopeIdentifier);
        if (callbacks && callbacks.size > 0) {
            logTelemetryEvent({
                action: 'token-subscription',
                actionModifier: 'notify',
                logLevel: LogLevel.Info,
                telemetrySource: TelemetrySource.AuthTokenCache,
                additionalData: {
                    scopeIdentifier,
                    subscriberCount: callbacks.size,
                    expires: token.expiration?.toISOString(),
                    timestamp: new Date().toISOString(),
                },
            });

            callbacks.forEach(callback => {
                try {
                    callback(token);
                } catch (error) {
                    logTelemetryEvent({
                        action: 'token-subscription',
                        actionModifier: 'callback-error',
                        logLevel: LogLevel.Error,
                        telemetrySource: TelemetrySource.AuthTokenCache,
                        additionalData: {
                            scopeIdentifier,
                            error: error instanceof Error ? error.message : String(error),
                            expires: token.expiration?.toISOString(),
                            timestamp: new Date().toISOString(),
                        },
                    });
                }
            });
        }
    }

    /**
     * Clears all cached tokens and cancels all refresh timers.
     * Useful for cleanup or logout scenarios.
     */
    clear(): void {
        // Clear all refresh timeouts
        this.cache.forEach(cached => {
            if (cached.refreshTimeoutId) {
                window.clearTimeout(cached.refreshTimeoutId);
            }
        });

        this.cache.clear();
        this.subscribers.clear();

        logTelemetryEvent({
            action: 'token-cache',
            actionModifier: 'cleared',
            logLevel: LogLevel.Info,
            telemetrySource: TelemetrySource.AuthTokenCache,
            additionalData: {
                timestamp: new Date().toISOString(),
            },
        });
    }
}

// Export a singleton instance
export const tokenCache = new TokenCache();

import { TelemetrySource } from '../Constants/Telemetry';
import { Response } from '../Contracts/Response';
import { LogLevel } from '../Contracts/Telemetry';
import { AuthScopeIdentifier } from '../Hooks/useAuthTokenManager';
import { logTelemetryEvent } from '../Hooks/useTelemetry';

export class Client {
    protected telemetrySource: TelemetrySource;

    constructor(telemetrySource: TelemetrySource) {
        this.telemetrySource = telemetrySource;
    }

    /**
     * Acquires an access token for the specified API scope via backend API.
     * The backend handles token caching and refreshing.
     */
    protected async getAccessToken(scopeIdentifier: AuthScopeIdentifier): Promise<Response<string>> {
        try {
            // Map scope identifiers to backend endpoints
            const tokenEndpoint = this.getTokenEndpoint(scopeIdentifier);

            const response = await fetch(tokenEndpoint, {
                credentials: 'include', // Include cookies for authentication
            });

            if (!response.ok) {
                if (response.status === 401) {
                    // User is not authenticated, redirect to login
                    window.location.href = '/api/auth/login?returnUrl=' + encodeURIComponent(window.location.pathname);

                    logTelemetryEvent({
                        action: 'token-acquisition',
                        actionModifier: 'unauthenticated',
                        logLevel: LogLevel.Error,
                        telemetrySource: this.telemetrySource,
                        additionalData: {
                            scopeIdentifier,
                        },
                    });

                    throw new Error('User not authenticated');
                }

                throw new Error(`Failed to acquire token: ${response.statusText}`);
            }

            const data = await response.json();

            return {
                isSuccessful: true,
                content: data.accessToken,
            };
        } catch (error) {
            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'failed',
                logLevel: LogLevel.Error,
                telemetrySource: this.telemetrySource,
                additionalData: {
                    scopeIdentifier,
                    error: error instanceof Error ? error.message : String(error),
                },
            });
            return {
                isSuccessful: false,
                error,
            };
        }
    }

    /**
     * Maps scope identifiers to backend token endpoints
     */
    private getTokenEndpoint(scopeIdentifier: AuthScopeIdentifier): string {
        switch (scopeIdentifier) {
            case 'arm':
                return '/api/auth/arm-token';
            case 'graph':
                return '/api/auth/graph-token';
            case 'sreAgent':
                return '/api/auth/sre-agent-token';
            case 'appInsights':
                return '/api/auth/app-insights-token';
            default:
                throw new Error(`Unknown scope identifier: ${scopeIdentifier}`);
        }
    }
}

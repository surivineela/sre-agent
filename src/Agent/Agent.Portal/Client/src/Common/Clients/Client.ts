import { IPublicClientApplication } from '@azure/msal-browser';
import { getScopesForApi } from '../Auth/cloudConfig';
import { AuthScopeIdentifier } from '../Auth/msalConfig';
import { TelemetrySource } from '../Constants/Telemetry';
import { Response } from '../Contracts/Response';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from '../Hooks/useTelemetry';

export class Client {
    protected instance: IPublicClientApplication;
    protected telemetrySource: TelemetrySource;

    constructor(instance: IPublicClientApplication, telemetrySource: TelemetrySource) {
        this.instance = instance;
        this.telemetrySource = telemetrySource;
    }

    /**
     * Acquires an access token for the specified API scope.
     * MSAL automatically handles token caching and refreshing - it will:
     * - Return cached token if valid
     * - Automatically refresh if expired or about to expire
     * - Use refresh token to get new access token silently
     */
    protected async getAccessToken(scopeIdentifier: AuthScopeIdentifier): Promise<Response<string>> {
        const account = this.instance.getActiveAccount();

        if (!account) {
            const error = new Error('No active account available for token acquisition');
            logTelemetryEvent({
                action: 'token-acquisition',
                actionModifier: 'failed',
                logLevel: LogLevel.Error,
                telemetrySource: this.telemetrySource,
                additionalData: {
                    scopeIdentifier,
                    error: error.message,
                },
            });
            return {
                isSuccessful: false,
                error,
            };
        }

        try {
            const scopes = getScopesForApi(scopeIdentifier);
            const response = await this.instance.acquireTokenSilent({
                scopes,
                account,
            });

            return {
                isSuccessful: true,
                content: response.accessToken,
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
}

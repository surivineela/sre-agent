import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { getScopesForApi } from '../Auth/cloudConfig';
import { msalInstance } from '../Auth/msalConfig';
import { TelemetrySource } from '../Constants/Telemetry';
import { KeyValue } from '../Contracts/Arm';
import { LogLevel } from '../Contracts/Telemetry';
import { AuthScopeIdentifier } from '../Hooks/useAuthTokenManager';
import { logTelemetryEvent } from '../Hooks/useTelemetry';

/**
 * Acquires an access token for the specified scope identifier using MSAL.
 * Handles caching, automatic refresh, and interactive authentication fallback.
 *
 * @param scopeIdentifier - The API scope identifier ('arm', 'graph', 'sreAgent', 'appInsights')
 * @param telemetrySource - The source of the telemetry event for tracking. `null` only for TelemetryClient
 * @param forceRefresh - If true, bypasses cache and requests a new token from the server
 * @returns An object containing the access token and optional expiration date
 * @throws Error if token acquisition fails
 */
export const acquireAccessToken = async (
    scopeIdentifier: AuthScopeIdentifier,
    telemetrySource: TelemetrySource | null,
    forceRefresh: boolean = false
): Promise<{ accessToken: string; expiresOn?: Date }> => {
    const account = msalInstance.getActiveAccount();

    if (!account) {
        if (telemetrySource) {
            logTelemetryEvent({
                action: 'acquire-access-token',
                actionModifier: 'no-active-account',
                logLevel: LogLevel.Error,
                telemetrySource,
                additionalData: {
                    scopeIdentifier,
                    timestamp: new Date().toISOString(),
                },
            });
        }

        return { accessToken: '' };
    }

    const scopes = getScopesForApi(scopeIdentifier);

    try {
        const response = await msalInstance.acquireTokenSilent({
            scopes,
            account,
            forceRefresh,
        });

        return {
            accessToken: response.accessToken,
            expiresOn: response.expiresOn || undefined,
        };
    } catch (error) {
        console.error(error);
        if (telemetrySource) {
            logTelemetryEvent({
                action: 'acquire-access-token',
                actionModifier: 'failed',
                logLevel: LogLevel.Error,
                telemetrySource,
                additionalData: {
                    isInteractionRequiredAuthError: error instanceof InteractionRequiredAuthError,
                    scopeIdentifier,
                    error: error instanceof Error ? error.message : String(error),
                    timestamp: new Date().toISOString(),
                },
            });
        }

        return { accessToken: '' };
    }
};

export const delay = async (func: () => Promise<any>, ms = 3000) => {
    await new Promise(resolve => setTimeout(resolve, ms));
    return await func();
};

export const getHeader = (headerToFind: string, headers: KeyValue<string>) => {
    for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === headerToFind.toLowerCase()) {
            return headers[key];
        }
    }
};

export const getArmErrorMessage = (error: any, recursionLimit: number = 1): string => {
    if (!error) {
        return '';
    }

    if (Object(error) !== error) {
        // The error is a primative type, not an object.
        // If it is a string, just return the value. Otherwise, return any empty string because there's nothing to extract.
        return typeof error === 'string' ? (error as string) : '';
    }

    // Check if a "message" property is present on the error object.
    if (error.message || error.ExceptionMessage || error.Message) {
        return error.message || error.ExceptionMessage || error.Message;
    }

    // No "message" property was present, so check if there is an inner error object with a "message" property.
    return recursionLimit ? getArmErrorMessage(error.error, recursionLimit - 1) : '';
};

export const getDeploymentOperationErrorMessage = (statusMessage: any): string | null => {
    if (!statusMessage) return null;

    if (typeof statusMessage === 'string') {
        return statusMessage;
    }

    // Try to extract error message from statusMessage object
    if (typeof statusMessage === 'object') {
        if (statusMessage.error?.details?.length && statusMessage.error?.details?.length > 0) {
            return statusMessage.error.details[0].message;
        }

        // Check for error.message pattern
        if (statusMessage.error?.message && typeof statusMessage.error?.message === 'string') {
            return statusMessage.error.message;
        }

        return JSON.stringify(statusMessage, null, 2);
    }

    return null;
};

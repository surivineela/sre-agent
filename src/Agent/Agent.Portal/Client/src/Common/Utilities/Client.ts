import { AuthError, InteractionRequiredAuthError } from '@azure/msal-browser';
import { getScopesForApi } from '../Auth/cloudConfig';
import { msalInstance } from '../Auth/msalConfig';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmBatchObject, KeyValue } from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { LogLevel } from '../Contracts/Telemetry';
import { AuthScopeIdentifier } from '../Hooks/useAuthTokenManager';
import { logTelemetryEvent } from '../Hooks/useTelemetry';

/**
 * Custom event name for session expiry notification.
 * AuthContext listens for this event to show the session expired dialog.
 */
export const SESSION_EXPIRED_EVENT = 'sreagent:session-expired';

/**
 * Dispatches a session expired event that AuthContext listens for.
 * This allows non-React code to trigger the session expired dialog.
 */
const dispatchSessionExpiredEvent = () => {
    window.dispatchEvent(new CustomEvent(SESSION_EXPIRED_EVENT));
};

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
    // Use the account's tenant-specific authority to ensure tokens are acquired
    // from the correct tenant, not the user's home tenant
    const authority = `https://login.microsoftonline.com/${account.tenantId}`;

    try {
        const response = await msalInstance.acquireTokenSilent({
            scopes,
            account,
            authority,
            forceRefresh,
        });

        return {
            accessToken: response.accessToken,
            expiresOn: response.expiresOn || undefined,
        };
    } catch (error) {
        const errorName = error instanceof Error ? error.constructor.name : typeof error;
        // Use errorCode instead of message to avoid potential PII (e.g., user email in error messages)
        const errorCode = error instanceof AuthError ? error.errorCode : 'unknown';

        logTelemetryEvent({
            telemetrySource: TelemetrySource.Auth,
            action: 'acquireAccessToken',
            actionModifier: 'failed',
            logLevel: LogLevel.Error,
            additionalData: {
                scopeIdentifier,
                errorName,
                errorCode,
                isInteractionRequired: error instanceof InteractionRequiredAuthError,
            },
        });

        // In local dev, attempt popup fallback to more easily catch missing API perms and consent to them for the app registration
        const isLocalDev = window.location.hostname === 'localhost';
        if (isLocalDev) {
            try {
                const popupResponse = await msalInstance.acquireTokenPopup({
                    scopes,
                    account,
                    authority,
                });

                return {
                    accessToken: popupResponse.accessToken,
                    expiresOn: popupResponse.expiresOn || undefined,
                };
            } catch (popupError) {
                console.error('acquireTokenPopup failed:', popupError);
            }
        }

        // If the error requires user interaction (e.g., refresh token expired),
        // dispatch an event so AuthContext can show the session expired dialog
        if (error instanceof InteractionRequiredAuthError) {
            dispatchSessionExpiredEvent();
        }

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

export const delay = async <T>(func: () => Promise<T>, ms = 3000): Promise<T> => {
    await new Promise(resolve => setTimeout(resolve, ms));
    return await func();
};

export const getHeader = (headerToFind: string, headers: KeyValue<string | undefined>) => {
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

    // Check for "content" property (ARM batch response format where error content is a string)
    if (typeof error.content === 'string') {
        return error.content;
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

export const convertFetchResponseToResponseObject = async <T>(response: globalThis.Response): Promise<Response<T>> => {
    const status = response.status;
    let data: T;
    try {
        data = await response.json();
    } catch {
        data = null as any; // If response is not JSON, set data to null
    }
    const headers: Record<string, string> = {};
    response.headers.forEach((value, key) => {
        headers[key] = value;
    });

    const responseSuccess = status < 300;
    return {
        isSuccessful: responseSuccess,
        error: responseSuccess ? null : data,
        content: responseSuccess ? data : (null as T),
        metadata: { status, headers },
    };
};

export const convertArmBatchResponseToResponseObject = <T>(response: ArmBatchObject): Response<T> => {
    const { status, data, headers } = { status: response.httpStatusCode, data: response.content as T, headers: response.headers };
    const responseSuccess = status < 300;
    return {
        isSuccessful: responseSuccess,
        error: responseSuccess ? null : data,
        content: responseSuccess ? data : (null as T),
        metadata: { status, headers },
    };
};

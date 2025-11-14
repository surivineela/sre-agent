import { msalInstance } from '../Auth/msalConfig';
import { getCurrentUserPreferences } from '../Contexts/UserPreferencesContext';
import { LogLevel, TelemetryEvent } from '../Contracts/Telemetry';
import { acquireAccessToken } from '../Utilities/Client';
import { getSessionId } from '../Utilities/SessionManager';

enum ApiLogLevel {
    Trace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    None = 6,
}

const LogLevelMap = {
    [LogLevel.Verbose]: ApiLogLevel.Trace,
    [LogLevel.Debug]: ApiLogLevel.Debug,
    [LogLevel.Info]: ApiLogLevel.Information,
    [LogLevel.Warning]: ApiLogLevel.Warning,
    [LogLevel.Error]: ApiLogLevel.Error,
};

interface LogRequestBody {
    /** Defaults to ApiLogLevel.Information */
    logLevel?: ApiLogLevel;
    eventName?: string;
    exception?: string;
    /** Defaults to an empty string */
    message?: string;
    args?: any[];
}

const authenticatedLogEndpoint = '/api/Log';
const unauthenticatedLogEndpoint = '/api/Log/anonymous';

export class TelemetryClient {
    public static async logEvent(event: TelemetryEvent): Promise<void> {
        // Don't bother logging if local-dev (yet at least)
        if (window.location.hostname === 'localhost') {
            return;
        }

        // Empty string if not signed in or error fetching token - use unauthenticated endpoint
        const { accessToken } = await acquireAccessToken('api', null);
        const activeAccount = msalInstance.getActiveAccount();
        const { theme, systemTheme, locale } = getCurrentUserPreferences();

        const eventWithBaselineData = {
            version: import.meta.env.SRE_AGENT_PORTAL_VERSION,
            hostname: window.location.hostname,
            pathname: window.location.pathname,
            sessionId: getSessionId(),
            // Portal uses the legacy hexadecimal cross-tenant PUID, but this is a potential privacy concern due to cross-tenant user tracking
            userId: activeAccount?.homeAccountId || 'anonymous',
            tenantId: activeAccount?.tenantId || 'anonymous',
            userAgent: navigator.userAgent,
            screenDimensions: { width: window.screen.width, height: window.screen.height },
            theme,
            systemTheme,
            locale,
            clientTimezone: Intl.DateTimeFormat().resolvedOptions().timeZone,
            // Potential additions: ScrubbedClientIP, ServerId, JourneyId, Region/DataCenterId, BrowserId
            ...event,
        };

        const requestHeaders: Record<string, string> = {
            'Content-Type': 'application/json',
            'x-ms-client-session-id': getSessionId(),
        };

        const requestBody: LogRequestBody = {
            eventName: event.name,
            logLevel: LogLevelMap[event.logLevel],
            args: [eventWithBaselineData],
        };

        let uri = unauthenticatedLogEndpoint;

        if (accessToken) {
            uri = authenticatedLogEndpoint;
            requestHeaders['Authorization'] = `Bearer ${accessToken}`;
        }

        try {
            const response = await fetch(uri, {
                method: 'POST',
                headers: requestHeaders,
                body: JSON.stringify(requestBody),
            });

            if (!response.ok) {
                throw new Error(`Telemetry logging failed with status ${response.status} - ${JSON.stringify(await response.json())}`);
            }
        } catch (error) {
            console.error('Failed to log telemetry event:', error);
        }
    }
}

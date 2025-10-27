import { useCallback } from 'react';
import { TelemetryClient } from '../Clients/TelemetryClient';
import { TelemetrySource } from '../Constants/Telemetry';
import { ILogEvent, LogLevel } from '../Contracts/Telemetry';
import { getSanitizedLogData, sanitizeMessageString } from '../Utilities/Sanitization';
import { logTelemetryLogToConsole } from '../Utilities/Telemetry';

/** Non-hook version - recommended to use the hook where possible */
export const logTelemetryEvent = (event: ILogEvent & { telemetrySource: TelemetrySource }) => {
    const sanitizedAction = sanitizeMessageString(event.action);
    const sanitizedData = getSanitizedLogData(event.additionalData ?? {});

    TelemetryClient.logEvent({
        name: 'SREA Portal',
        source: event.telemetrySource,
        action: sanitizedAction,
        actionModifier: event.actionModifier,
        data: sanitizedData,
        timestamp: new Date().getUTCSeconds(),
    });

    logTelemetryLogToConsole(event.telemetrySource, event.actionModifier, event.logLevel ?? LogLevel.Info, event.action, sanitizedData);
};

export const useTelemetry = (telemetrySource: TelemetrySource, resourceId: string | undefined) => {
    const logEvent = useCallback(
        (event: ILogEvent) => {
            const finalData = {
                resourceId,
                ...event.additionalData,
            };

            logTelemetryEvent({
                ...event,
                telemetrySource,
                additionalData: finalData,
            });
        },
        [telemetrySource, resourceId]
    );

    return { logEvent };
};

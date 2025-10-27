import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { getFeatureFlag, SettingNames } from '../Hooks/useConfigSettings';
import { AdditionalLogData } from './Sanitization';

export const logTelemetryLogToConsole = (
    telemetrySource: TelemetrySource,
    actionModifier: string,
    logLevel: LogLevel,
    action: string,
    logData?: AdditionalLogData
) => {
    const urlLogLevel = getFeatureFlag(SettingNames.LogLevel);
    const urlLogSources = getFeatureFlag(SettingNames.LogSource);

    const desiredLogLevelInt = LogLevel[urlLogLevel as keyof typeof LogLevel] ?? LogLevel.Info;

    if (logLevel > desiredLogLevelInt || (typeof urlLogSources === 'string' && !urlLogSources.split(',').includes(telemetrySource))) {
        return;
    }

    // Color starts as verbose
    let color = window.matchMedia?.('(prefers-color-scheme: dark)').matches ? '#6a0dad' : '#000';
    switch (logLevel) {
        case LogLevel.Error:
            color = '#a30000';
            break;
        case LogLevel.Warning:
            color = '#ff8c00';
            break;
        case LogLevel.Info:
            color = '#0058ad';
            break;
        default:
            break;
    }

    const time = new Date().toISOString();
    let message = `[TELEMETRY 📎] ${time} [${telemetrySource}][${actionModifier} - ${action}]`;
    message = logData?.message ? `${message} - ${logData.message}` : message;

    console.log(`%c${message}`, `color: ${color}`);
};

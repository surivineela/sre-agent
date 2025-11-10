import { TelemetrySource } from '../Constants/Telemetry';

/** Portal's TelemetryEvent contract - the final format to be sent off */
export type TelemetryEvent = {
    readonly timestamp: number;
    readonly source: TelemetrySource;
    readonly action: string;
    readonly logLevel: LogLevel;
    readonly actionModifier?: string;
    /** The elapsed time in milliseconds for the event being recorded (optional) */
    readonly duration?: number;
    readonly name?: string;
    /** Additional data */
    readonly data?: Record<string, any>;
};

export type ILogEvent = {
    /** The action being performed:  e.g. "initializing", "updatingConfig", "refreshingToken", etc. */
    action: string;
    /** The status of that action: e.g. "started", "stopped", "succeeded", etc. */
    actionModifier: string;
    additionalData?: Record<string, any>;
    /** Defaults to `Info` */
    logLevel?: LogLevel;
};

export enum LogLevel {
    // Start at 1 to avoid any falsy check confusion
    Error = 1,
    Warning,
    Info,
    /** Events that we want logged to Kusto, but not console, by default (Ex: Client.ts GETs) */
    Debug,
    /** Super nitty-gritty events that we only want logged to console, not Kusto */
    Verbose,
}

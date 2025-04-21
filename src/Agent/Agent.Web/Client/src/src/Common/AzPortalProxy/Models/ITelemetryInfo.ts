export type LogLevel = 'error' | 'warning' | 'info' | 'verbose';

export interface ILogData {
  [key: string]: any;
  message?: string;
}

export interface ITelemetryInfo {
    action: string; // The action being performed:  e.g. "initializing", "updatingConfig", "refreshingToken", etc...
    actionModifier: string; // The status of that action: e.g. "started", "stopped", "succeeded", etc...
    resourceId?: string; // The resourceId of the resource you're logging for.
    logLevel?: LogLevel; // If unspecified, this is defaulted to "info"
    data?: string | ILogData; // If a string, will get set as a LogData message property before logging
  }
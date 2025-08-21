import { DeploymentError } from '../../Contracts/Azure/Deployment';

export type LogLevel = 'error' | 'warning' | 'info' | 'verbose';

export interface ILogData {
    [key: string]: any;
    message?: string;
}

export enum ErrorCode {
    ResourceNotFound = 'ResourceNotFound',
    ResourceGroupNotFound = 'ResourceGroupNotFound',
    DeploymentNotFound = 'DeploymentNotFound',
    ScopeLocked = 'ScopeLocked',
    PrincipalNotFound = 'PrincipalNotFound',
    LinkedRepoNotFound = 'LinkedRepoNotFound',
    Unauthorized = 'Unauthorized',
    Forbidden = 'Forbidden',
}

export type ArmError = {
    code: ErrorCode;
    message: string;
    Message?: string;
};

export type BladeError = ArmError | DeploymentError | { message: unknown } | string;

export interface ILogBladeErrorInfo {
    message: string;
    /**
     * Typically `response.error`, but can be a string as well
     * (Ex: joining multiple error messages from a single operation)
     */
    error?: BladeError;
}

export interface ITelemetryInfo {
    /** The action being performed:  e.g. "initializing", "updatingConfig", "refreshingToken", etc... */
    action: string;
    /** The status of that action: e.g. "started", "stopped", "succeeded", etc... */
    actionModifier: string;
    /** The resourceId of the resource you're logging for. */
    resourceId?: string;
    /** If unspecified, this is defaulted to "info" */
    logLevel?: LogLevel;
    /** If a string, will get set as a LogData message property before logging */
    data?: string | ILogData;
}

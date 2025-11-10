export class AgentSiteToAzPortalVerbs {
    public static readonly readyForData = 'readyForData'; // Portal absorbs the first ready message, so we need our own to indicate when the iframe is ready to receive data
    public static readonly message = 'message';
    public static readonly log = 'log';
    public static readonly logAmplitudeOperationEvent = 'log-amplitude-operation-event';
    public static readonly logAmplitudeNavigationEvent = 'log-amplitude-navigation-event';
    public static readonly logAmplitudeControlEvent = 'log-amplitude-control-event';
    public static readonly updateNotification = 'update-notification';
    public static readonly openBlade = 'open-blade';
    public static readonly requestToken = 'request-token';
}

export class AzPortalToAgentSiteVerbs {
    public static readonly sendEnvironmentInfo = 'send-environment-info';
    public static readonly sendToken = 'send-token';
    public static readonly sendTheme = 'send-theme';
    public static readonly sendUserInfo = 'send-user-info';
    public static readonly bladeClosed = 'blade-closed';
}

export interface IEnvironmentInfo {
    effectiveLocale: string;
    /** `null` for cross-tenant */
    resourceId: string | null;
    /** `null` for cross-tenant */
    armEndpoint: string | null;
    sreAgentEndpoint: string;
}

export enum IFrameThemeMode {
    Light = 0,
    Dark = 1,
}

export interface IFrameTheme {
    name: 'light' | 'dark';
    mode: IFrameThemeMode;
}

export type TokenTypes = 'arm' | 'sreagent' | 'applicationinsightapi';

export interface ITokenInfo {
    token: string;
    type: TokenTypes;
}

export interface IFrameUserInfo {
    email: string;
    givenName: string;
    directoryId: string;
    objectId: string;
}

export interface IFrameTelemetryInfo {
    action: string;
    actionModifier: string;
    /** If unspecified, this is defaulted to "info" */
    logLevel?: 'error' | 'warning' | 'info' | 'verbose';
    /** If a string, will get set as a LogData message property before logging */
    data?: string | Record<string, unknown>;
}

export interface INotificationInfo {
    id: string;
    state: 'start' | 'success' | 'fail';
    title?: string;
    description?: string;
}

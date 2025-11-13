export class AgentSiteToAzPortalVerbs {
    public static readonly ready = 'ready'; // Initial ready message required by AzPortal so that they know when the blade is loaded
    public static readonly readyForData = 'readyForData'; // Portal absorbs the first ready message, so we need our own to indicate when the iframe is ready to receive data
    public static readonly message = 'message';
    public static readonly log = 'log';
    public static readonly logAmplitudeOperationEvent = 'log-amplitude-operation-event';
    public static readonly logAmplitudeNavigationEvent = 'log-amplitude-navigation-event';
    public static readonly logAmplitudeControlEvent = 'log-amplitude-control-event';
    public static readonly updateNotification = 'update-notification';
    public static readonly openBlade = 'open-blade';
    public static readonly requestToken = 'request-token';
    public static readonly userActivity = 'user-activity'; // For playground session management
}

export class AzPortalToAgentSiteVerbs {
    public static readonly sendEnvironmentInfo = 'send-environment-info';
    public static readonly sendToken = 'send-token';
    public static readonly sendTheme = 'send-theme';
    public static readonly sendUserInfo = 'send-user-info';
    public static readonly bladeClosed = 'blade-closed';
}

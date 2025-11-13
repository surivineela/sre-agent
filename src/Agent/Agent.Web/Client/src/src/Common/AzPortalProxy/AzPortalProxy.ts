import { Guid } from '../Helpers/Guid';
import Url from '../Helpers/Url';
import { AgentSiteToAzPortalVerbs, AzPortalToAgentSiteVerbs } from './AzPortalProxyVerbs';
import { AmplitudeControlEvent, AmplitudeNavigationEvent, AmplitudeOperationEvent } from './Models/IAmplitude';
import { IEnvironmentInfo } from './Models/IEnvironmentInfo';
import { IEvent } from './Models/IEvent';
import { INotificationInfo, INotificationState } from './Models/INotificationInfo';
import { IBladeClosed, IBladeClosedResult, IOpenBlade, IOpenBladeRequest } from './Models/IOpenBlade';
import { ILogBladeErrorInfo, ITelemetryInfo } from './Models/ITelemetryInfo';
import { ITokenInfo, TokenTypes } from './Models/ITokenInfo';

// TODO: Probably need to add `portalEndpoint` alongside armEndpoint, etc.
// for deep links

export const defaultSreAgentEndpoint = '..';

/**
 * NOTE: Must use arrow functions in order for destructing to work
 */
export default class AzPortalProxy {
    public shellSrc: string = '';

    private readonly portalFrameBladeSignature = 'FxFrameBlade';
    private readonly sreAgentPortalSignature = 'SreAgentPortal';
    private bladeClosedResolver?: { operationId: string; resolver: (result: IBladeClosed) => void };
    private localhostConfirmed: boolean = false;

    private setEnvironmentInfo: React.Dispatch<React.SetStateAction<IEnvironmentInfo>> = {} as React.Dispatch<
        React.SetStateAction<IEnvironmentInfo>
    >;

    private readonly acceptedSignatures = [this.portalFrameBladeSignature, this.sreAgentPortalSignature];

    private acceptedOriginsSuffix = [
        'portal.azure.com',
        'portal.microsoftazure.de',
        'portal.azure.cn',
        'portal.azure.us',
        'portal.azure.eaglex.ic.gov',
        'portal.azure.microsoft.scloud',
        'portal.azure.net',
        'sre.azure.com',
        'int.sre.azure.com',
        'stage.sre.azure.com',
        'ms.sre.azure.com',
    ];

    public static envInfo: IEnvironmentInfo = {} as IEnvironmentInfo;

    public static get inStandaloneMode() {
        return window.self === window.top;
    }

    public initialize = (setEnvironmentInfo: React.Dispatch<React.SetStateAction<IEnvironmentInfo>>) => {
        // We don't need any initialization if we're not running within an iframe
        if (AzPortalProxy.inStandaloneMode) {
            return;
        }

        const shellUrl = decodeURI(window.location.href);
        // TODO: Can probably remove the need for this query param if we just use
        // event.origin
        this.shellSrc = Url.getParameterByName(shellUrl, 'trustedAuthority') || '';

        this.setEnvironmentInfo = setEnvironmentInfo;

        window.addEventListener(AgentSiteToAzPortalVerbs.message, this.messageReceived.bind(this) as any, false);

        this.postMessage(AgentSiteToAzPortalVerbs.readyForData, null);
    };

    public log = (info: ITelemetryInfo) => {
        this.postMessage(AgentSiteToAzPortalVerbs.log, info);
    };

    public logAmplitudeOperationEvent = (
        amplitudeEvent: AmplitudeOperationEvent & { errorInfo?: ILogBladeErrorInfo } & { metadata?: Record<string, unknown> }
    ) => {
        if (this._getDontLogAmplitudeTelemetry()) {
            return;
        }

        this.postMessage(AgentSiteToAzPortalVerbs.logAmplitudeOperationEvent, amplitudeEvent);
    };

    public logAmplitudeControlEvent = (amplitudeEvent: AmplitudeControlEvent & { metadata?: Record<string, unknown> }) => {
        if (this._getDontLogAmplitudeTelemetry()) {
            return;
        }

        this.postMessage(AgentSiteToAzPortalVerbs.logAmplitudeControlEvent, amplitudeEvent);
    };

    public logAmplitudeNavigationEvent = (amplitudeEvent: AmplitudeNavigationEvent & { metadata?: Record<string, unknown> }) => {
        if (this._getDontLogAmplitudeTelemetry()) {
            return;
        }

        this.postMessage(AgentSiteToAzPortalVerbs.logAmplitudeNavigationEvent, amplitudeEvent);
    };

    public requestAuthToken = (tokenType: TokenTypes) => {
        if (AzPortalProxy.inStandaloneMode) {
            return;
        }

        this.postMessage(AgentSiteToAzPortalVerbs.requestToken, tokenType);
    };

    public reportUserActivity = (activityType: string = 'general') => {
        if (AzPortalProxy.inStandaloneMode) {
            return;
        }

        this.postMessage(AgentSiteToAzPortalVerbs.userActivity, {
            type: activityType,
            timestamp: Date.now(),
        });
    };

    public startNotification = (title: string, description: string) => {
        const notification: INotificationInfo = {
            title,
            description,
            id: Guid.newTinyGuid(),
            state: 'start',
        };

        this.postMessage(AgentSiteToAzPortalVerbs.updateNotification, notification);
        return notification.id;
    };

    public stopNotification = (id: string, success: boolean, description: string) => {
        const state: INotificationState = success ? 'success' : 'fail';

        const notification: INotificationInfo = {
            id,
            state,
            description,
            title: '',
        };

        this.postMessage(AgentSiteToAzPortalVerbs.updateNotification, notification);
    };

    public openBlade = (info: IOpenBlade) => {
        const operationId = Guid.newGuid();

        const requestInfo = {
            ...info,
            operationId,
        };

        this.postMessage<IOpenBladeRequest>(AgentSiteToAzPortalVerbs.openBlade, requestInfo);

        const bladeClosedPromise = new Promise<IBladeClosed>(resolve => {
            this.bladeClosedResolver = {
                operationId,
                resolver: resolve,
            };
        });

        return bladeClosedPromise;
    };

    private bladeClosed = (result: IBladeClosedResult) => {
        if (!this.bladeClosedResolver || !this.bladeClosedResolver.resolver) {
            throw Error('bladeClosedResolver not set!');
        }

        if (this.bladeClosedResolver.operationId !== result.operationId) {
            throw Error(
                `bladeClosed operationIds do not match.  Waiting for ${this.bladeClosedResolver.operationId} but received ${result.operationId}`
            );
        }

        this.bladeClosedResolver.resolver({
            reason: result.reason,
            data: result.data,
        });

        this.bladeClosedResolver = undefined;
    };

    private postMessage = <T>(verb: string, data: T) => {
        console.log(`Request AgentSiteToAzPortal: '${verb}'`);
        if (!AzPortalProxy.inStandaloneMode) {
            window.parent.postMessage(
                {
                    data,
                    kind: verb,
                    signature: this.portalFrameBladeSignature,
                },
                this.shellSrc
            );
        }
    };

    private isLocalhostOrigin(origin: string): boolean {
        return origin.includes('localhost');
    }

    private messageReceived = (event: IEvent): void => {
        if (!event || !event.data) {
            return;
        }

        if (!this.acceptedSignatures.find(s => event.data.signature === s)) {
            return;
        }

        // Allow local-dev SREA portal to host prod agent site
        // - notify user to mitigate slight security risk
        if (this.isLocalhostOrigin(event.origin)) {
            if (!this.localhostConfirmed) {
                this.localhostConfirmed = confirm(
                    `This application is being embedded from a local development environment (${event.origin}).\n\n` +
                        `This should only happen during development. If you are not a developer testing this application, ` +
                        `this may be a security concern.\n\n` +
                        `Do you want to allow communication with this development environment?`
                );

                if (!this.localhostConfirmed) {
                    console.error('User declined localhost communication. Closing page.');
                    window.close();
                    return;
                }
            }
        }

        if (!this.acceptedOriginsSuffix.find(o => event.origin.toLowerCase().endsWith(o.toLowerCase())) && !this.localhostConfirmed) {
            return;
        }

        const data = event.data.data;
        const methodName = event.data.kind;
        console.log(`Received AzPortalToAgentSite: '${methodName}'`);

        switch (methodName) {
            case AzPortalToAgentSiteVerbs.sendEnvironmentInfo: {
                const envInfo = data as IEnvironmentInfo;
                AzPortalProxy.envInfo = {
                    ...AzPortalProxy.envInfo,
                    effectiveLocale: envInfo.effectiveLocale,
                    // The below two will be `null` for cross-tenant
                    resourceId: envInfo.resourceId ?? '',
                    armEndpoint: envInfo.armEndpoint ?? '',
                    sreAgentEndpoint: envInfo.sreAgentEndpoint ?? defaultSreAgentEndpoint,
                    isCrossTenantPortalMode: !envInfo.resourceId && !envInfo.armEndpoint,
                    sessionId: envInfo.sessionId,
                };
                this.setEnvironmentInfo(AzPortalProxy.envInfo);
                break;
            }

            case AzPortalToAgentSiteVerbs.sendToken:
                this.updateToken(data);
                break;

            case AzPortalToAgentSiteVerbs.sendTheme:
                AzPortalProxy.envInfo = {
                    ...AzPortalProxy.envInfo,
                    theme: data,
                };
                this.setEnvironmentInfo(AzPortalProxy.envInfo);
                break;

            case AzPortalToAgentSiteVerbs.sendUserInfo:
                AzPortalProxy.envInfo = {
                    ...AzPortalProxy.envInfo,
                    userInfo: data.userInfo,
                };
                this.setEnvironmentInfo(AzPortalProxy.envInfo);
                break;

            case AzPortalToAgentSiteVerbs.bladeClosed:
                this.bladeClosed(data);
                break;

            default:
                break;
        }
    };

    private updateToken = (tokenInfo: ITokenInfo) => {
        if (tokenInfo.type === 'arm') {
            AzPortalProxy.envInfo = {
                ...AzPortalProxy.envInfo,
                armToken: tokenInfo.token,
            };
        } else if (tokenInfo.type === 'sreagent') {
            AzPortalProxy.envInfo = {
                ...AzPortalProxy.envInfo,
                sreAgentToken: tokenInfo.token,
            };
        }

        // Add armToken and sreAgentToken to additionalTokens too for ease of use
        const newTokens = new Map<TokenTypes, string>(AzPortalProxy.envInfo.additionalTokens);
        newTokens.set(tokenInfo.type, tokenInfo.token);
        AzPortalProxy.envInfo = {
            ...AzPortalProxy.envInfo,
            additionalTokens: newTokens,
        };

        this.setEnvironmentInfo(AzPortalProxy.envInfo);
    };

    private _getDontLogAmplitudeTelemetry = () => {
        return AzPortalProxy.inStandaloneMode || AzPortalProxy.envInfo.isCrossTenantPortalMode;
    };
}

import { Guid } from "../../Helpers/Guid";
import Url from "../../Helpers/Url";
import { AgentSiteToAzPortalVerbs, AzPortalToAgentSiteVerbs } from "./AzPortalProxyVerb";
import { IEnvironmentInfo } from "./Models/IEnvironments";
import { IEvent } from "./Models/IEvent";
import { INotificationInfo, INotificationState } from "./Models/INotificationinfo";
import { IBladeClosed, IBladeClosedResult, IOpenBlade, IOpenBladeRequest } from "./Models/IOpenBlade";
import { ITelemetryInfo } from "./Models/ITememetryInfo";
import { ITokenInfo } from "./Models/ITokeninfo";

export default class AzPortalProxy {
    public shellSrc: string = '';

    private readonly portalFrameBladeSignature = 'FxFrameBlade';
    private bladeClosedResolver?: { operationId: string; resolver: (result: IBladeClosed) => void };

    private setEnvironmentInfo: React.Dispatch<React.SetStateAction<IEnvironmentInfo>> = {} as React.Dispatch<
        React.SetStateAction<IEnvironmentInfo>
    >;

    private readonly acceptedSignatures = [this.portalFrameBladeSignature];

    private acceptedOriginsSuffix = [
        'portal.azure.com',
        'portal.microsoftazure.de',
        'portal.azure.cn',
        'portal.azure.us',
        'portal.azure.eaglex.ic.gov',
        'portal.azure.microsoft.scloud',
        'portal.azure.net',
    ];

    public static envInfo: IEnvironmentInfo = {} as IEnvironmentInfo;

    public static get inStandaloneMode() {
        return window.self === window.top;
    }

    public initialize(setEnvironmentInfo: React.Dispatch<React.SetStateAction<IEnvironmentInfo>>) {
        // We don't need any initialization if we're not running within an iframe
        if (AzPortalProxy.inStandaloneMode) {
            return;
        }

        const shellUrl = decodeURI(window.location.href);
        this.shellSrc = Url.getParameterByName(shellUrl, 'trustedAuthority') || '';

        this.setEnvironmentInfo = setEnvironmentInfo;

        window.addEventListener(AgentSiteToAzPortalVerbs.message, this.messageReceived.bind(this) as any, false);

        this.postMessage(AgentSiteToAzPortalVerbs.ready, null);
        this.postMessage(AgentSiteToAzPortalVerbs.readyForData, null);
    }

    public log(info: ITelemetryInfo) {
        this.postMessage(AgentSiteToAzPortalVerbs.log, info);
    }

    public startNotification(title: string, description: string) {
        const notification: INotificationInfo = {
            title,
            description,
            id: Guid.newTinyGuid(),
            state: 'start',
        };

        this.postMessage(AgentSiteToAzPortalVerbs.updateNotification, notification);
        return notification.id;
    }

    public stopNotification(id: string, success: boolean, description: string) {
        const state: INotificationState = success ? 'success' : 'fail';

        const notification: INotificationInfo = {
            id,
            state,
            description,
            title: '',
        };

        this.postMessage(AgentSiteToAzPortalVerbs.updateNotification, notification);
    }

    public openBlade(info: IOpenBlade) {
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
    }

    private bladeClosed(result: IBladeClosedResult) {
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
    }

    private postMessage<T>(verb: string, data: T) {
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
    }

    private messageReceived(event: IEvent): void {
        if (!event || !event.data) {
            return;
        }

        if (!this.acceptedOriginsSuffix.find(o => event.origin.toLowerCase().endsWith(o.toLowerCase()))) {
            return;
        }

        if (!this.acceptedSignatures.find(s => event.data.signature === s)) {
            return;
        }

        const data = event.data.data;
        const methodName = event.data.kind;
        console.log(`Received AzPortalToAgentSite: '${methodName}`);

        switch (methodName) {
            case AzPortalToAgentSiteVerbs.sendEnvironmentInfo: {
                const envInfo = data as IEnvironmentInfo;
                AzPortalProxy.envInfo = {
                    ...AzPortalProxy.envInfo,
                    effectiveLocale: envInfo.effectiveLocale,
                    resourceId: envInfo.resourceId,
                    armEndpoint: envInfo.armEndpoint,
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
    }

    private updateToken(tokenInfo: ITokenInfo) {
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
        } else {
            throw Error('Unrecognized token type: ' + tokenInfo.type);
        }

        this.setEnvironmentInfo(AzPortalProxy.envInfo);
    }
}
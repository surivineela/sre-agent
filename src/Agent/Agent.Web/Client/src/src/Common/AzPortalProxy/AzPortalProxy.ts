import Url from "../Helpers/Url";
import { AgentSiteToAzPortalVerbs, AzPortalToAgentSiteVerbs } from "./AzPortalProxyVerbs";
import { IEvent } from "./Models/IEvent";
import { IEnvironmentInfo } from "./Models/IEnvironmentInfo";
import { ITelemetryInfo } from "./Models/ITelemetryInfo";

export default class AzPortalProxy {
  public shellSrc: string = '';

  private readonly portalFrameBladeSignature = 'FxFrameBlade';

  private internalEnvInfo: IEnvironmentInfo = {} as IEnvironmentInfo;
  private setEnvironmentInfo: React.Dispatch<React.SetStateAction<IEnvironmentInfo>> = {} as React.Dispatch<React.SetStateAction<IEnvironmentInfo>>;

  private readonly acceptedSignatures = [
    this.portalFrameBladeSignature
  ];

  private acceptedOriginsSuffix = [
    'portal.azure.com',
    'portal.microsoftazure.de',
    'portal.azure.cn',
    'portal.azure.us',
    'portal.azure.eaglex.ic.gov',
    'portal.azure.microsoft.scloud',
    'portal.azure.net',
  ];

  public static get inStandaloneMode(){
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

    window.addEventListener(AgentSiteToAzPortalVerbs.message, this.iframeReceivedMsg.bind(this) as any, false);

    this.postMessage(AgentSiteToAzPortalVerbs.ready, null);
    this.postMessage(AgentSiteToAzPortalVerbs.readyForData, null);
  }

  public log(info: ITelemetryInfo) {
    this.postMessage(AgentSiteToAzPortalVerbs.log, info);
  }

  private postMessage(verb: string, data: object | null) {
    console.log(`Request AgentSiteToAzPortal: '${verb}'`);
    window.parent.postMessage(
      {
        data,
        kind: verb,
        signature: this.portalFrameBladeSignature,
      },
      this.shellSrc
    );
  }

  private iframeReceivedMsg(event: IEvent): void {
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
      case AzPortalToAgentSiteVerbs.sendEnvironmentInfo:
        const envInfo = data as IEnvironmentInfo;
        this.internalEnvInfo = {
          ...this.internalEnvInfo,
          effectiveLocale: envInfo.effectiveLocale,
          resourceId: envInfo.resourceId,
          armEndpoint: envInfo.armEndpoint
        };
        this.setEnvironmentInfo(this.internalEnvInfo);
        break;

      case AzPortalToAgentSiteVerbs.sendToken:
        this.internalEnvInfo = {
          ...this.internalEnvInfo,
          token: data.token
        };
        this.setEnvironmentInfo(this.internalEnvInfo);
        break;

      case AzPortalToAgentSiteVerbs.sendTheme:
        this.internalEnvInfo = {
          ...this.internalEnvInfo,
          theme: data
        };
        this.setEnvironmentInfo(this.internalEnvInfo);
        break;

      case AzPortalToAgentSiteVerbs.sendUserInfo:
        this.internalEnvInfo = {
          ...this.internalEnvInfo,
          userInfo: data.userInfo
        }
        this.setEnvironmentInfo(this.internalEnvInfo);
        break;

      default:
        break;
    }
  }
}
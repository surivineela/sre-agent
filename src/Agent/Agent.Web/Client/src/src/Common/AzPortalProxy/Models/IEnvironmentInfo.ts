import { ITheme } from './ITheme';
import { IUserInfo } from './IUserInfo';

export interface IEnvironmentInfo {
    effectiveLocale: string;
    /** `''` if `isCrossTenantPortalMode` */
    resourceId: string;
    /** `''` if `isCrossTenantPortalMode` */
    armEndpoint: string;
    sreAgentEndpoint: string;
    armToken?: string;
    sreAgentToken?: string;
    theme?: ITheme;
    userInfo?: IUserInfo;
    /**
     * Loading a 1P AME agent site in portal from CORP (FirstPartyAgentFrameBlade.ReactView)
     *
     * This means Control Plane (ARM) calls won't work
     */
    isCrossTenantPortalMode?: boolean;
}

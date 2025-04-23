import { ITheme } from "./ITheme";
import { IUserInfo } from "./IUserInfo";

export interface IEnvironmentInfo {
  effectiveLocale: string;
  resourceId: string;
  armEndpoint: string;
  armToken?: string;
  sreAgentToken?: string;
  theme?: ITheme;
  userInfo?: IUserInfo;
}
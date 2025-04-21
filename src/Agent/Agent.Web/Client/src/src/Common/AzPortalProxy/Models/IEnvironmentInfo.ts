import { ITheme } from "@fluentui/react";
import { IUserInfo } from "./IUserInfo";

export interface IEnvironmentInfo {
  effectiveLocale: string;
  resourceId: string;
  armEndpoint: string;
  token?: string;
  theme?: ITheme;
  userInfo?: IUserInfo;
}
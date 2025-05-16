import { useContext } from "react";
import AzPortalProxy from "../Common/AzPortalProxy/AzPortalProxy";
import { EnvironmentContext } from "../Common/AzPortalProxy/Providers/StartupInfoContext";

export default class FeatureUtils {
    private static readonly whitelistSubIds = [
        "caae90fd-95a3-4641-a523-b66894f4654c" // XIAOXHU SUB
    ];
    
    public static isFeatureEnabled(): boolean {
        if (AzPortalProxy.inStandaloneMode) {
            return true;
        }
        const resourceId = useContext(EnvironmentContext)?.resourceId ?? "";
        const subId = FeatureUtils.extractSubscriptionId(resourceId);
        const isEnabled = FeatureUtils.whitelistSubIds.some((id) => `${id}`.toLowerCase() === `${subId}`.toLowerCase());
        return isEnabled;
    }

    private static extractSubscriptionId(resourceId: string): string {
        const resourceUriRegex = new RegExp('/subscriptions/([^/]+)/(resourceGroups/([^/]+)/)?providers/([^/]+)/(.+)', "i");
        const match = resourceId.match(resourceUriRegex);
        return match && match.length > 1 ? match[1] : "";
    }
}
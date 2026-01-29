import { azurePortalUrl } from '../Constants/Uri';

export const openSubscriptionOverviewInNewTab = (subscriptionId: string): void => {
    if (!subscriptionId) {
        return;
    }
    const portalUrl = `${azurePortalUrl}#resource/subscriptions/${subscriptionId}/overview`;
    window.open(portalUrl, '_blank', 'noopener,noreferrer');
};

export const openResourceGroupOverviewInNewTab = (resourceGroupId: string): void => {
    if (!resourceGroupId) {
        return;
    }
    const portalUrl = `${azurePortalUrl}#resource${resourceGroupId}/overview`;
    window.open(portalUrl, '_blank', 'noopener,noreferrer');
};

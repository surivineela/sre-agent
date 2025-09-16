import { IntlShape } from 'react-intl';
import { IncidentManagementPlatformResources } from '../../Strings/SREAgentResources';
import { IncidentManagementType } from '../Contracts/Azure/SreAgent';

export const getLocalizedIncidentPlatformName = (platform: string, intl: IntlShape): string => {
    const lowercasePlatform = platform?.toLowerCase() ?? '';
    switch (lowercasePlatform) {
        case IncidentManagementType.AzMonitor:
            return intl.formatMessage(IncidentManagementPlatformResources.azMonitor);
        case IncidentManagementType.PagerDuty:
            return intl.formatMessage(IncidentManagementPlatformResources.pagerDuty);
        case IncidentManagementType.Icm:
            return intl.formatMessage(IncidentManagementPlatformResources.icm);
        case IncidentManagementType.ServiceNow:
            return intl.formatMessage(IncidentManagementPlatformResources.serviceNow);
        default:
            return '';
    }
};

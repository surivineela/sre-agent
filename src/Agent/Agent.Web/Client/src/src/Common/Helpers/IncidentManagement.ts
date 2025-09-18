import { IntlShape } from 'react-intl';
import { IncidentManagementPlatformResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IncidentManagementType } from '../Contracts/Azure/SreAgent';

export const getLocalizedIncidentPlatformName = (platform: string, intl: IntlShape): string => {
    switch (platform) {
        case IncidentManagementType.AzMonitor:
            return intl.formatMessage(IncidentManagementPlatformResources.azMonitor);
        case IncidentManagementType.PagerDuty:
            return intl.formatMessage(IncidentManagementPlatformResources.pagerDuty);
        case IncidentManagementType.Icm:
            return intl.formatMessage(IncidentManagementPlatformResources.icm);
        case IncidentManagementType.ServiceNow:
            return intl.formatMessage(IncidentManagementPlatformResources.serviceNow);
        default:
            return platform;
    }
};

export const getLocalizedMitigatedBy = (mitigatedBy: 'agent' | 'user' | 'inProgress', intl: IntlShape) => {
    switch (mitigatedBy) {
        case 'agent':
            return intl.formatMessage(SreAgentResources.agent);
        case 'user':
            return intl.formatMessage(SreAgentResources.user);
        case 'inProgress':
            return intl.formatMessage(SreAgentResources.inProgress);
        default:
            return mitigatedBy;
    }
};

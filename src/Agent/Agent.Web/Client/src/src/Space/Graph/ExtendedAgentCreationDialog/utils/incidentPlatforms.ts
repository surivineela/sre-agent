import { AzMonitorResources, IcMResources, PagerDutyResources, ServiceNowResources } from '../../../../Strings/SREAgentResources';
import { IncidentPriority, IncidentType } from '../types';

export interface PriorityOption {
    key: IncidentPriority;
    intlString: { id: string; defaultMessage: string };
    severity?: 'critical' | 'high' | 'medium' | 'low';
}

export interface IncidentTypeOption {
    key: IncidentType;
    label: string;
}

export const getPrioritiesForPlatform = (incidentPlatform?: string): PriorityOption[] => {
    switch (incidentPlatform) {
        case 'AzMonitor':
            return [
                { key: 'Sev0', intlString: AzMonitorResources.sev0, severity: 'critical' },
                { key: 'Sev1', intlString: AzMonitorResources.sev1, severity: 'critical' },
                { key: 'Sev2', intlString: AzMonitorResources.sev2, severity: 'high' },
                { key: 'Sev3', intlString: AzMonitorResources.sev3, severity: 'medium' },
                { key: 'Sev4', intlString: AzMonitorResources.sev4, severity: 'low' },
            ];
        case 'PagerDuty':
            return [
                { key: 'P1', intlString: PagerDutyResources.p1, severity: 'critical' },
                { key: 'P2', intlString: PagerDutyResources.p2, severity: 'high' },
                { key: 'P3', intlString: PagerDutyResources.p3, severity: 'medium' },
                { key: 'P4', intlString: PagerDutyResources.p4, severity: 'low' },
                { key: 'P5', intlString: PagerDutyResources.p5, severity: 'low' },
            ];
        case 'Icm':
            return [
                { key: '2', intlString: IcMResources.sev2, severity: 'critical' },
                { key: '2.5', intlString: IcMResources.sev2_5, severity: 'high' },
                { key: '3', intlString: IcMResources.sev3, severity: 'medium' },
                { key: '4', intlString: IcMResources.sev4, severity: 'low' },
            ];
        case 'ServiceNow':
            return [
                { key: '1', intlString: ServiceNowResources.priorityCritical, severity: 'critical' },
                { key: '2', intlString: ServiceNowResources.priorityHigh, severity: 'high' },
                { key: '3', intlString: ServiceNowResources.priorityModerate, severity: 'medium' },
                { key: '4', intlString: ServiceNowResources.priorityLow, severity: 'low' },
                { key: '5', intlString: ServiceNowResources.priorityPlanning, severity: 'low' },
            ];
        default:
            // Default to AzMonitor
            return [
                { key: 'Sev0', intlString: AzMonitorResources.sev0, severity: 'critical' },
                { key: 'Sev1', intlString: AzMonitorResources.sev1, severity: 'critical' },
                { key: 'Sev2', intlString: AzMonitorResources.sev2, severity: 'high' },
                { key: 'Sev3', intlString: AzMonitorResources.sev3, severity: 'medium' },
                { key: 'Sev4', intlString: AzMonitorResources.sev4, severity: 'low' },
            ];
    }
};

// todo: return different incident types for each platform, including user created incident types
export const getIncidentTypesForPlatform = (incidentPlatform?: string): IncidentTypeOption[] => {
    // For ICM, incident types are typically fetched from the backend
    // For other platforms, we provide common defaults
    // Note: These can be overridden with backend-provided values via useIncidentFilterFields
    switch (incidentPlatform) {
        case 'Icm':
            // ICM has specific incident types that come from the backend
            return [
                { key: 'LiveSite', label: 'LiveSite' },
                { key: 'Maintenance', label: 'Maintenance' },
                { key: 'Security', label: 'Security' },
                { key: 'Other', label: 'Other' },
            ];
        case 'AzMonitor':
        case 'PagerDuty':
            // https://developer.pagerduty.com/api-reference/0dbbd6d1b3936-list-incident-types
            return [
                { key: 'incident_default', label: 'Base Incident' },
                { key: 'major_default', label: 'Major Incident' },
                { key: 'security_default', label: 'Security Incident' },
            ];
        case 'ServiceNow':
        default:
            // Common incident types for all platforms
            return [
                { key: 'LiveSite', label: 'LiveSite' },
                { key: 'Maintenance', label: 'Maintenance' },
                { key: 'Security', label: 'Security' },
                { key: 'Other', label: 'Other' },
            ];
    }
};

export const getDefaultPriorityForPlatform = (incidentPlatform?: string): IncidentPriority => {
    const priorities = getPrioritiesForPlatform(incidentPlatform);
    // Return the third priority (medium severity) as default
    return priorities[2]?.key ?? 'Sev2';
};

export const getDefaultIncidentTypeForPlatform = (_incidentPlatform?: string): IncidentType => {
    // LiveSite is the default across all platforms
    return 'LiveSite';
};

export const getBadgeColorForPriority = (priority: IncidentPriority, incidentPlatform?: string): 'danger' | 'warning' | 'informative' => {
    const priorities = getPrioritiesForPlatform(incidentPlatform);
    const priorityOption = priorities.find(p => p.key === priority);

    switch (priorityOption?.severity) {
        case 'critical':
            return 'danger';
        case 'high':
            return 'warning';
        case 'medium':
        case 'low':
        default:
            return 'informative';
    }
};

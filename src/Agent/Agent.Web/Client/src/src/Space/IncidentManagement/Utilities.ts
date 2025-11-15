import { tokens } from '@fluentui/react-components';
import { IncidentManagementType, IncidentStatus } from '../../Common/Contracts/Azure/SreAgent';
import { InvestigationStatus, Thread } from '../../Common/Contracts/DataPlane/Thread';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import {
    AzMonitorResources,
    IcMResources,
    IncidentManagementResources,
    PagerDutyResources,
    ServiceNowResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import { IncidentsListColumnKey } from './CreateIncidentHandler/Contracts';

export const getFilterValues = (
    values: any,
    incidentPlatform?: IncidentManagementType,
    replaceAllKey?: boolean,
    allKeyReplacement?: string
) => {
    const result = {
        incidentType: values.incidentType,
        impactedService: values.impactedService,
        priority: values.priority,
        titleContains: values.titleContains,
        agentMode: values.agentMode,
        deepInvestigationEnabled: values.deepInvestigationEnabled,
        owningTeamId: values.owningTeamId,
        createdBy: values.createdBy,
        monitorId: values.monitorId,
        handlingAgent: values.handlingAgent,
    };

    if (replaceAllKey) {
        result.incidentType = result.incidentType === 'ALL' ? allKeyReplacement : result.incidentType || allKeyReplacement;
        result.impactedService = result.impactedService === 'ALL' ? allKeyReplacement : result.impactedService || allKeyReplacement;
        result.priority = result.priority === 'ALL' ? allKeyReplacement : result.priority || allKeyReplacement;
    }

    if (incidentPlatform === IncidentManagementType.AzMonitor) {
        result.incidentType = undefined;
        result.impactedService = undefined;
    }

    if (incidentPlatform !== IncidentManagementType.Icm) {
        result.owningTeamId = undefined;
        result.createdBy = undefined;
        result.monitorId = undefined;
    }

    return result;
};

export const getPlatformSpecificStrings = (incidentPlatform?: IncidentManagementType) => {
    const strings = {
        severityOrPriorityLabel: IncidentManagementResources.severity,
        severityOrPriorityLabelPlural: IncidentManagementResources.severities,
        severityOrPriorityAllOptionLabel: IncidentManagementResources.allSeverity,
        severityOrPriorityPlaceholder: IncidentManagementResources.chooseSeverity,
        incidentOrAlertIdLabel: IncidentManagementResources.incidentId,
        incidentOrAlertTitleLabel: IncidentManagementResources.incidentTitle,
        incidentOrAlertStatusLabel: IncidentManagementResources.incidentStatus,
        incidentOrAlertCreatedLabel: IncidentManagementResources.incidentCreated,
    };

    if (incidentPlatform === IncidentManagementType.PagerDuty || incidentPlatform === IncidentManagementType.ServiceNow) {
        strings.severityOrPriorityLabel = IncidentManagementResources.priority;
        strings.severityOrPriorityLabelPlural = IncidentManagementResources.priorities;
        strings.severityOrPriorityAllOptionLabel = IncidentManagementResources.allPriorities;
        strings.severityOrPriorityPlaceholder = IncidentManagementResources.choosePriority;
    }

    if (incidentPlatform === IncidentManagementType.AzMonitor) {
        strings.incidentOrAlertIdLabel = IncidentManagementResources.alertId;
        strings.incidentOrAlertTitleLabel = IncidentManagementResources.alertTitle;
        strings.incidentOrAlertStatusLabel = IncidentManagementResources.alertStatus;
        strings.incidentOrAlertCreatedLabel = IncidentManagementResources.alertCreated;
    }

    return strings;
};

export const mapEmptyStatus = (incidentPlatformType?: IncidentManagementType) => {
    switch (incidentPlatformType) {
        case IncidentManagementType.AzMonitor:
            return IncidentStatus.new;
        case IncidentManagementType.PagerDuty:
            return IncidentStatus.triggered;
        case IncidentManagementType.Icm:
        case IncidentManagementType.ServiceNow:
            return IncidentStatus.new;
        default:
            return undefined;
    }
};

export const getColumnInfo = (column: IncidentsListColumnKey | 'modifiedTimestamp') => {
    switch (column) {
        case IncidentsListColumnKey.incidentId:
            return {
                columnType: 'string',
                columnPath: 'incidentId',
                getColumnValue: (thread: Thread) => thread.status?.incidentStatus?.incidentId ?? '-',
            };
        case IncidentsListColumnKey.title:
            return {
                columnType: 'string',
                columnPath: 'IncidentDetails%2FincidentTitle',
                getColumnValue: (thread: Thread) => thread.incidentDetails?.incidentTitle ?? thread.title ?? '-',
            };
        case IncidentsListColumnKey.priority:
            return {
                columnType: 'string',
                columnPath: 'IncidentDetails%2FincidentPriority',
                getColumnValue: (thread: Thread) => thread.incidentDetails?.incidentPriority ?? '-',
            };
        case IncidentsListColumnKey.incidentStatus:
            return {
                columnType: 'string',
                columnPath: 'incidentStatus',
                getColumnValue: (thread: Thread) => thread.status?.incidentStatus?.status ?? '-',
            };
        case IncidentsListColumnKey.agentStatus:
            return {
                columnType: 'string',
                columnPath: 'IncidentDetails%2FinvestigationStatus',
                getColumnValue: (thread: Thread) => thread.incidentDetails?.investigationStatus ?? '-',
            };
        case IncidentsListColumnKey.createdTimestamp:
            return {
                columnType: 'date',
                columnPath: 'IncidentDetails%2FincidentCreatedTime',
                getColumnValue: (thread: Thread) => {
                    const original = thread.incidentDetails?.incidentCreatedTime ?? thread.createdTimestamp;
                    const safeDate = original ? getSafeDateTime(original) : undefined;
                    return !safeDate || isNaN(safeDate.getTime()) ? '-' : safeDate.toISOString();
                },
            };
        case IncidentsListColumnKey.impactedService:
            return {
                columnType: 'string',
                columnPath: 'IncidentDetails%2FimpactedService',
                getColumnValue: (thread: Thread) => thread.incidentDetails?.impactedService,
            };
        case IncidentsListColumnKey.handler:
            return {
                columnType: 'string',
                columnPath: 'IncidentDetails%2FhandlerId',
                getColumnValue: (thread: Thread) => thread.incidentDetails?.filterId ?? '-',
            };
        case 'modifiedTimestamp':
            return {
                columnType: 'date',
                columnPath: 'modifiedTimestamp',
                getColumnValue: (thread: Thread) => thread.modifiedTimestamp ?? '-',
            };
        default:
            return {
                columnType: undefined,
                columnPath: undefined,
                getColumnValue: (_: Thread) => '-',
            };
    }
};

export const getIncidentStatusIntlString = (incidentStatus: IncidentStatus | undefined) => {
    switch (incidentStatus) {
        case IncidentStatus.triggered:
            return SreAgentResources.triggered;
        case IncidentStatus.new:
            return SreAgentResources.new;
        case IncidentStatus.active:
            return SreAgentResources.active;
        case IncidentStatus.assigned:
            return SreAgentResources.assigned;
        case IncidentStatus.inProgress:
            return SreAgentResources.inProgress;
        case IncidentStatus.acknowledged:
            return SreAgentResources.acknowledged;
        case IncidentStatus.mitigated:
            return SreAgentResources.mitigated;
        case IncidentStatus.closed:
            return SreAgentResources.closed;
        case IncidentStatus.resolved:
            return SreAgentResources.resolved;
    }
    return undefined;
};

export const getIncidentStatusColor = (incidentStatus: IncidentStatus | undefined) => {
    switch (incidentStatus) {
        case IncidentStatus.triggered:
        case IncidentStatus.new:
        case IncidentStatus.active:
            return tokens.colorStatusDangerBackground3;
        case IncidentStatus.assigned:
        case IncidentStatus.inProgress:
        case IncidentStatus.acknowledged:
            return tokens.colorStatusWarningBackground3;
        case IncidentStatus.mitigated:
        case IncidentStatus.closed:
        case IncidentStatus.resolved:
            return tokens.colorPaletteGreenForeground1;
    }
};

export const getInvestigationStatusIntlString = (investigationStatus: InvestigationStatus | undefined) => {
    switch (investigationStatus) {
        case InvestigationStatus.pendingUserInput:
            return IncidentManagementResources.pendingUserInput;
        case InvestigationStatus.inProgress:
            return IncidentManagementResources.inProgress;
        case InvestigationStatus.complete:
            return IncidentManagementResources.completed;
    }
    return undefined;
};

export const getPriorities = (incidentPlatformType?: IncidentManagementType) => {
    switch (incidentPlatformType) {
        case IncidentManagementType.AzMonitor:
            return [
                { key: 'Sev0', intlString: AzMonitorResources.sev0 },
                { key: 'Sev1', intlString: AzMonitorResources.sev1 },
                { key: 'Sev2', intlString: AzMonitorResources.sev2 },
                { key: 'Sev3', intlString: AzMonitorResources.sev3 },
                { key: 'Sev4', intlString: AzMonitorResources.sev4 },
            ];
        case IncidentManagementType.PagerDuty:
            return [
                { key: 'P1', intlString: PagerDutyResources.p1 },
                { key: 'P2', intlString: PagerDutyResources.p2 },
                { key: 'P3', intlString: PagerDutyResources.p3 },
                { key: 'P4', intlString: PagerDutyResources.p4 },
                { key: 'P5', intlString: PagerDutyResources.p5 },
            ];
        case IncidentManagementType.Icm:
            return [
                { key: '2', intlString: IcMResources.sev2 },
                { key: '2.5', intlString: IcMResources.sev2_5 },
                { key: '3', intlString: IcMResources.sev3 },
                { key: '4', intlString: IcMResources.sev4 },
            ];
        case IncidentManagementType.ServiceNow:
            return [
                { key: '1', intlString: ServiceNowResources.priorityCritical },
                { key: '2', intlString: ServiceNowResources.priorityHigh },
                { key: '3', intlString: ServiceNowResources.priorityModerate },
                { key: '4', intlString: ServiceNowResources.priorityLow },
                { key: '5', intlString: ServiceNowResources.priorityPlanning },
            ];
        default:
            return undefined;
    }
};

import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';

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
        owningTeamId: values.owningTeamId,
        createdBy: values.createdBy,
        monitorId: values.monitorId,
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

export const getPriorityOrSeverityStrings = (incidentPlatform?: IncidentManagementType) => {
    return incidentPlatform === IncidentManagementType.AzMonitor || incidentPlatform === IncidentManagementType.Icm
        ? {
              fieldLabel: IncidentManagementResources.severity,
              fieldLabelPlural: IncidentManagementResources.severities,
              allOptionLabel: IncidentManagementResources.allSeverity,
              placeholder: IncidentManagementResources.chooseSeverity,
          }
        : {
              fieldLabel: IncidentManagementResources.priority,
              fieldLabelPlural: IncidentManagementResources.priorities,
              allOptionLabel: IncidentManagementResources.allPriorities,
              placeholder: IncidentManagementResources.choosePriority,
          };
};

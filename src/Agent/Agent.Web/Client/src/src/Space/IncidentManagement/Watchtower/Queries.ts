import { TimeRangeValue } from '../../../Common/Components/PillFilter/Contracts';
import { getKustoTimespan } from '../../../Common/Helpers/Date';

export const watchtowerTempAppInsightsAppId = 'bc8d1232-d691-428e-a29f-7e785bf2d016';

/*

customDimensions includes:

- IncidentMitigatedByAgent: "False"
- ResponsePlanUpdatedOn: "2025-09-11T22:02:32.0000000Z"
- ResponsePlanCreatedOn: "2025-09-11T22:02:32.0000000Z"
- IncidentUpdatedOn: "2025-09-11T22:02:14.0000000Z"
- ResponsePlanCustom: "False"
- AgentAutonomyLevel: "review"
- IncidentCreatedOn: "2025-09-11T22:01:16.0000000Z"
- IncidentTitle: "[Public] Some incident title"
- IncidentSeverity: "3"
- IncidentHandledOn: "2025-09-11T22:02:32.0000000Z" *Was IncidentHandledAt before
- IncidentId: "000000000"
- IncidentStatus: "active"
- ResponsePlanId: "watchtower-test1"

*/

export const getHandlersIncidentCoverageTrendQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | where IncidentHandledOn ${kustoTimespan}
    | project IncidentId, IncidentHandledOn, UpdatedOn
    | summarize arg_max(UpdatedOn, IncidentHandledOn) by IncidentId
    | summarize DistinctIncidentIds = dcount(IncidentId) by bin(IncidentHandledOn, timeGrain)
    | order by IncidentHandledOn asc
    `;
};

export const getHandlersIncidentSummaryTrendQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus)
    | project IncidentId, IncidentHandledOn, IsMitigatedByAgent, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | summarize arg_max(UpdatedOn, IncidentHandledOn, IsMitigatedByAgent, Status) by IncidentId
    | summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledOn, timeGrain)
    | order by IncidentHandledOn asc
    `;
};

export const getHandlersOverviewQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), RunMode = tostring(customDimensions.HandlerRunMode), InstructionType = tostring(customDimensions.HandlerInstructionType), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | summarize arg_max(UpdatedOn, HandlerId, IncidentId, IsMitigatedByAgent, RunMode, InstructionType, Status) by IncidentId
    | summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by HandlerId, RunMode, InstructionType
    | order by Incidents desc
    `;
};

/** Results not confirmed yet */
export const getHandlerIncidentOutcomeTrendQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, IsMitigatedByAgent, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, HandlerId, Status, IsMitigatedByAgent, Status, IncidentHandledOn) by IncidentId
    | summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledOn, timeGrain)
    `;
};

/** Results not confirmed yet */
export const getHandlerIncidentOverviewQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentSeverity), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, IsMitigatedByAgent, Status, Priority, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledOn, HandlerId, Status, Priority, IsMitigatedByAgent) by IncidentId
    | project IncidentId, HandlerId, UpdatedOn, Status, Priority, IsMitigatedByAgent
    | order by Priority desc, Status desc, IsMitigatedByAgent
    `;
};

/** Results not confirmed yet */
export const getIncidentRootCauseOverviewQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), RootCause= tostring(customDimensions.IncidentRootCauseCategory), Summary = tostring(customDimensions.IncidentSummary), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, RootCause, Summary, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledOn, HandlerId, RootCause, Summary) by IncidentId
    | summarize dcount(IncidentId) by RootCause`;
};

/** Results not confirmed yet */
export const getLatestIncidentInformationQuery = (handlerId: string, incidentId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    let incidentId = '${incidentId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)),
        IncidentId = tostring(customDimensions.IncidentId),
        IncidentTitle = tostring(customDimensions.IncidentTitle),
        HandlerId = tostring(customDimensions.ResponsePlanId),
        HandlerCreatedOn = todatetime(customDimensions.ResponsePlanCreatedOn),
        HandlerUpdatedOn = todatetime(customDimensions.ResponsePlanUpdatedOn),
        IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent),
        Status = tostring(customDimensions.IncidentStatus),
        Priority = tostring(customDimensions.IncidentSeverity),
        CreatedOn = todatetime(customDimensions.IncidentCreatedOn),
        UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn),
        RootCause = tostring(customDimensions.IncidentRootCauseCategory),
        Summary = tostring(customDimensions.IncidentSummary),
        ImpactedService = tostring(customDimensions.IncidentImpactedService),
        RunMode = tostring(customDimensions.AgentAutonomyLevel),
        InstructionType = tostring(customDimensions.ResponsePlanCustom)
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId and IncidentId == incidentId
    | summarize arg_max(UpdatedOn, HandlerId, CreatedOn, IncidentHandledOn, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType) by IncidentId
    | project IncidentId, HandlerId, CreatedOn, UpdatedOn, IncidentHandledOn, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType`;
};

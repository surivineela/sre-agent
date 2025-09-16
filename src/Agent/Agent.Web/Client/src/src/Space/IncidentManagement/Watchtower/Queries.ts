import { TimeRangeValue } from '../../../Common/Components/PillFilter/TimeRangePillFilter';
import { getKustoTimespan } from '../../../Common/Helpers/Date';

export const watchtowerTempAppInsightsAppId = 'bc8d1232-d691-428e-a29f-7e785bf2d016';

export const getHandlersIncidentCoverageTrendQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | where IncidentHandledAt ${kustoTimespan}
    | project IncidentId, IncidentHandledAt, UpdatedOn
    | summarize arg_max(UpdatedOn, IncidentHandledAt ) by IncidentId
    | summarize DistinctIncidentIds = dcount(IncidentId) by bin(IncidentHandledAt, timeGrain)
    `;
};

export const getHandlersIncidentSummaryTrendQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus)
    | project IncidentId, IncidentHandledAt, IsMitigatedByAgent, Status, UpdatedOn
    | where IncidentHandledAt ${kustoTimespan}
    | summarize arg_max(UpdatedOn, IncidentHandledAt, IsMitigatedByAgent, Status) by IncidentId
    | summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledAt, timeGrain)
    `;
};

export const getHandlersOverviewQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.Status), RunMode = tostring(customDimensions.HandlerRunMode), InstructionType = tostring(customDimensions.HandlerInstructionType), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, Status, UpdatedOn
    | where IncidentHandledAt ${kustoTimespan}
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
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, Status, UpdatedOn
    | where IncidentHandledAt ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, HandlerId, Status, IsMitigatedByAgent, Status, IncidentHandledAt) by IncidentId
    | summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledAt, timeGrain)
    `;
};

/** Results not confirmed yet */
export const getHandlerIncidentOverviewQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentSeverity), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, Status, Priority, UpdatedOn
    | where IncidentHandledAt ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledAt, HandlerId, Status, Priority, IsMitigatedByAgent) by IncidentId
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
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), RootCause= tostring(customDimensions.IncidentRootCauseCategory), Summary = tostring(customDimensions.IncidentSummary), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledAt, HandlerId, RootCause, Summary, UpdatedOn
    | where IncidentHandledAt ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledAt, HandlerId, RootCause, Summary) by IncidentId
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
    | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt),
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
    | where IncidentHandledAt ${kustoTimespan}
    | where HandlerId == handlerId and IncidentId == incidentId
    | summarize arg_max(UpdatedOn, HandlerId, CreatedOn, IncidentHandledAt, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType) by IncidentId
    | project IncidentId, HandlerId, CreatedOn, UpdatedOn, IncidentHandledAt, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType`;
};

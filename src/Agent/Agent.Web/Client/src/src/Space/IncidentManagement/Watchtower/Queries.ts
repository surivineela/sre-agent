import { TimeRangeValue } from '../../../Common/Components/PillFilter/Contracts';
import { getKustoTimespan } from '../../../Common/Helpers/Date';

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
- IncidentPlatform: "Icm" | "AzMonitor" | "PagerDuty" | "ServiceNow"
- Misc: IncidentSummary, IncidentRootCauseCategory, IncidentRootCauseDescription

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
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), IsAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent), Status = tostring(customDimensions.IncidentStatus)
    | project IncidentId, IncidentHandledOn, IsMitigatedByAgent, IsAssistedByAgent, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | summarize arg_max(UpdatedOn, IncidentHandledOn, IsMitigatedByAgent, IsAssistedByAgent, Status) by IncidentId
    | summarize Incidents = dcount(IncidentId), AgentAssisted = dcountif(IncidentId, IsAssistedByAgent == True), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == False and tolower(Status) != 'active'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == True), InProgress = dcountif(IncidentId, tolower(Status) == 'active') by bin(IncidentHandledOn, timeGrain)
    | order by IncidentHandledOn asc
    `;
};

export const getHandlersOverviewQuery = (timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), IsAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent), Status = tostring(customDimensions.IncidentStatus), RunMode = tostring(customDimensions.AgentAutonomyLevel), ResponsePlanCustom = tostring(customDimensions.ResponsePlanCustom), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, IsMitigatedByAgent, IsAssistedByAgent, RunMode, ResponsePlanCustom, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | summarize arg_max(UpdatedOn, HandlerId, IncidentId, IsMitigatedByAgent, IsAssistedByAgent, RunMode, ResponsePlanCustom, Status) by IncidentId
    | summarize Incidents = dcount(IncidentId), AgentAssisted = dcountif(IncidentId, IsAssistedByAgent == True), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == False and tolower(Status) != 'active'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == True), InProgress = dcountif(IncidentId, tolower(Status) == 'active') by HandlerId, RunMode, ResponsePlanCustom
    | order by Incidents desc
    `;
};

export const getHandlerIncidentSummaryTrendQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let timeGrain = 1d;
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), IsAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
    | project IncidentId, IncidentHandledOn, HandlerId, IsMitigatedByAgent, IsAssistedByAgent, Status, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, HandlerId, Status, IsMitigatedByAgent, IsAssistedByAgent, Status, IncidentHandledOn) by IncidentId
    | summarize Incidents = dcount(IncidentId), AgentAssisted = dcountif(IncidentId, IsAssistedByAgent == True), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == False and tolower(Status) != 'active'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == True), InProgress = dcountif(IncidentId, tolower(Status) == 'active') by bin(IncidentHandledOn, timeGrain)
    | order by IncidentHandledOn asc
    `;
};

export const getHandlerIncidentOverviewQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentCreatedOn = todatetime(customDimensions.IncidentCreatedOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), IsAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentSeverity), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), IncidentTitle = tostring(customDimensions.IncidentTitle)
    | project IncidentId, IncidentTitle, IncidentHandledOn, IncidentCreatedOn, HandlerId, IsMitigatedByAgent, IsAssistedByAgent, Status, Priority, UpdatedOn
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledOn, IncidentCreatedOn, IncidentTitle, HandlerId, Status, Priority, IsMitigatedByAgent, IsAssistedByAgent) by IncidentId
    | project IncidentId, IncidentTitle, Priority, IncidentCreatedOn, Status, IsMitigatedByAgent, IsAssistedByAgent
    | order by Priority desc, Status desc, IsMitigatedByAgent, IsAssistedByAgent
    `;
};

/** ImpactedService field not actually populated in the data (*test data; yet?) */
export const getIncidentRootCauseOverviewQuery = (handlerId: string, timeRange: TimeRangeValue) => {
    const kustoTimespan = getKustoTimespan(timeRange);

    return `
    let handlerId = '${handlerId}';
    customEvents
    | where name == 'IncidentActivitySnapshot'
    | extend IncidentHandledOn = todatetime(iif(isnull(customDimensions.IncidentHandledOn), customDimensions.IncidentHandledAt, customDimensions.IncidentHandledOn)), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), RootCause= tostring(customDimensions.IncidentRootCauseCategory), Summary = tostring(customDimensions.IncidentSummary), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), Status = tostring(customDimensions.IncidentStatus)
    | project IncidentId, IncidentHandledOn, HandlerId, RootCause, Summary, UpdatedOn, Status
    | where IncidentHandledOn ${kustoTimespan}
    | where tolower(Status) != 'active'
    | where HandlerId == handlerId
    | summarize arg_max(UpdatedOn, IncidentHandledOn, HandlerId, RootCause, Summary) by IncidentId
    | summarize dcount(IncidentId) by RootCause
    | order by dcount_IncidentId desc
    `;
};

/** Results not confirmed yet - don't think this had a confirmed use case; likely just there for ref */
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
        isAssistedByAgent = tostring(customDimensions.IncidentAssistedByAgent),
        Status = tostring(customDimensions.IncidentStatus),
        Priority = tostring(customDimensions.IncidentSeverity),
        CreatedOn = todatetime(customDimensions.IncidentCreatedOn),
        UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn),
        MitigatedOn = todatetime(customDimensions.IncidentMitigatedOn),
        RootCause = tostring(customDimensions.IncidentRootCauseCategory),
        RootCauseDescription = tostring(customDimensions.IncidentRootCauseDescription),
        Summary = tostring(customDimensions.IncidentSummary),
        ImpactedService = tostring(customDimensions.IncidentImpactedService),
        RunMode = tostring(customDimensions.AgentAutonomyLevel),
        CustomHandler = tostring(customDimensions.ResponsePlanCustom)
    | where IncidentHandledOn ${kustoTimespan}
    | where HandlerId == handlerId and IncidentId == incidentId
    | summarize arg_max(UpdatedOn, HandlerId, IncidentTitle, CreatedOn, IncidentHandledOn, MitigatedOn, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, isAssistedByAgent, RootCause, RootCauseDescription, Summary, ImpactedService, RunMode, CustomHandler) by IncidentId
    | project IncidentId, HandlerId, IncidentTitle, CreatedOn, UpdatedOn, IncidentHandledOn, MitigatedOn, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, isAssistedByAgent, RootCause, RootCauseDescription, Summary, ImpactedService, RunMode, CustomHandler`;
};

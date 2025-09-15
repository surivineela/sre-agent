export const watchtowerTempAppInsightsAppId = 'bc8d1232-d691-428e-a29f-7e785bf2d016';

export const getHandlersIncidentIntakeTrendQuery = () => `let formattedStartTime = ago(30d);
let formattedEndTime = now();
let timeGrain = 1d;
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt= todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| project IncidentId, IncidentHandledAt, UpdatedOn
| summarize arg_max(UpdatedOn, IncidentHandledAt ) by IncidentId
| summarize DistinctIncidentIds = dcount(IncidentId) by bin(IncidentHandledAt , timeGrain)`;

export const getHandlersIncidentOutcomeTrendQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
let timeGrain = 1d;
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt= todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus)
| project IncidentId, IncidentHandledAt, IsMitigatedByAgent, Status, UpdatedOn
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| summarize arg_max(UpdatedOn, IncidentHandledAt, IsMitigatedByAgent, Status) by IncidentId
| summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledAt, timeGrain)`;

export const getHandlersOverviewQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.Status), RunMode = tostring(customDimensions.HandlerRunMode), InstructionType = tostring(customDimensions.HandlerInstructionType), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, Status, UpdatedOn
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| summarize arg_max(UpdatedOn, HandlerId, IncidentId, IsMitigatedByAgent, RunMode, InstructionType, Status) by IncidentId
| summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by HandlerId, RunMode, InstructionType
| order by Incidents desc `;

/** Results not confirmed yet */
export const getHandlerIncidentOutcomeTrendQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
let timeGrain = 1d;
let handlerId = 'watchtower-test-filter';
customEvents
 | where name == 'IncidentActivitySnapshot'
 | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, Status, UpdatedOn
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| where HandlerId == handlerId
| summarize arg_max(UpdatedOn, HandlerId, Status, IsMitigatedByAgent, Status, IncidentHandledAt) by IncidentId
| summarize Incidents = dcount(IncidentId), UserMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'false'), AgentMitigated = dcountif(IncidentId, IsMitigatedByAgent == 'true'), InProgress = dcountif(IncidentId, Status == 'active') by bin(IncidentHandledAt, timeGrain)`;

/** Results not confirmed yet */
export const getHandlerIncidentOverviewQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
let handlerId = 'watchtower-test1';
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentSeverity), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| project IncidentId, IncidentHandledAt, HandlerId, IsMitigatedByAgent, Status, Priority, UpdatedOn
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| where HandlerId == handlerId
| summarize arg_max(UpdatedOn, IncidentHandledAt, HandlerId, Status, Priority, IsMitigatedByAgent) by IncidentId
| project IncidentId, HandlerId, UpdatedOn, Status, Priority, IsMitigatedByAgent
| order by Priority desc, Status desc, IsMitigatedByAgent`;

/** Results not confirmed yet */
export const getIncidentRootCauseOverviewQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
let handlerId = 'watchtower-test1';
customEvents
| where name == 'IncidentActivitySnapshot'
| extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), RootCause= tostring(customDimensions.IncidentRootCauseCategory), Summary = tostring(customDimensions.IncidentSummary), UpdatedOn = todatetime(customDimensions.IncidentUpdatedOn)
| project IncidentId, IncidentHandledAt, HandlerId, RootCause, Summary, UpdatedOn
| where IncidentHandledAt between (formattedStartTime .. formattedEndTime)
| where HandlerId == handlerId
| summarize arg_max(UpdatedOn, IncidentHandledAt, HandlerId, RootCause, Summary) by IncidentId
| summarize dcount(IncidentId) by RootCause`;

/** Results not confirmed yet */
export const getLatestIncidentInformationQuery = () => `let formattedStartTime = datetime('2025-08-01');
let formattedEndTime = datetime('2025-09-11');
let handlerId = 'watchtower-test1';
let incidentId = '6134314';
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
| where HandlerId == handlerId and IncidentId == incidentId
| summarize arg_max(UpdatedOn, HandlerId, CreatedOn, IncidentHandledAt, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType) by IncidentId
| project IncidentId, HandlerId, CreatedOn, UpdatedOn, IncidentHandledAt, HandlerCreatedOn, HandlerUpdatedOn, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType`;

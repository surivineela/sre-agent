// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using JsonConvert = Newtonsoft.Json.JsonConvert;

namespace Agent.Runtime.Services;

public interface IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocumentPayload> where TIncidentDocument: IIncidentDocument where TIncidentFilterDocumentPayload: IncidentFilterDocumentPayload
{
    void Ingest(IncidentAIData data);

    Task Ingest(TIncidentDocument incidentDocument);

    Task<DataTable> GetHandlersIncidentIntakeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlersIncidentOutcomeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlersOverview(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlerIncidentOutcomeTrend(string handlerId, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlerIncidentOverview(string handlerId, DateTime startTime, DateTime endTime);

    Task<DataTable> GetLatestInformation(string handlerId, string incidentId);

    Task<TIncidentDocument> AnalyzeIncident(TIncidentDocument incidentDocument, object incident);
}

// handler information to be logged
public class IncidentAIData
{
    public required string HandlerId { get; set; }
    public required string IncidentId { get; set; }
    public required string IncidentTitle { get; set; }
    public required string Priority { get; set; }
    public required DateTime HandlerCreatedAt { get; set; }
    public required DateTime IncidentCreatedAt { get; set; }
    public required DateTime HandlerUpdatedAt { get; set; }
    public required DateTime IncidentUpdatedAt { get; set; }
    public required DateTime? IncidentHandledAt { get; set; }
    public required string Status { get; set; }
    public required bool IsMitigatedByAgent { get; set; }
    public required string RootCause { get; set; }
    public required string Summary { get; set; }
    public required string ImpactedService { get; set; }
    public required DateTime? MitigatedAt { get; set; }
    public required string RunMode { get; set; }
    public required string InstructionType { get; set; }
}

public abstract class IncidentAnalysisServiceBase<TIncidentDocument, TIncidentFilterDocumentPayload>: IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocumentPayload>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    protected readonly IChatClient _client;
    protected readonly IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> _incidentManagementService;
    private readonly ILogger<IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocumentPayload>> _logger;
    private readonly CoreSettings _coreSettings;
    private readonly ArmHelper _armHelper;
    private readonly IncidentAnalysisLogger _appInsightsLogger;
    protected readonly string _incidentRootCausePrompt;
    protected readonly string _incidentGeneralSummaryPrompt;


    public IncidentAnalysisServiceBase(
        IChatClient client,
        IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> incidentManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocumentPayload>> logger)
    {
        _client = client;
        _incidentManagementService = incidentManagementService;
        _logger = logger;
        _coreSettings = coreSettings;
        _armHelper = armHelper;
        _appInsightsLogger = new IncidentAnalysisLogger(_coreSettings.Azure.AppInsights.ConnectionString);

        _incidentRootCausePrompt = @"Analyze the following incident and, from the provided details of the investigation and resolution steps, provide a generic root cause category that
            the incident falls into. THe root cause category should be a few words, 5 at most. If one of the provided root causes matches this incident, choose the most suitable one. If there is no suitable category out of the provided options, create a new one. Only provide the root cause category in the response.
            Do not include a preface or postface.";
        _incidentGeneralSummaryPrompt = @"From the provided details, analyze the following incident and provide a general summary in a few short sentences.
            In the summary, mention the context of the incident, the symptoms, and how the issue was mitigated and resolved. Only provide the summary in the response.
            Do not include a preface or postface.";
    }


    // when to ingest data: whenever there's an incident document
    public void Ingest(IncidentAIData data)
    {
        try
        {
            var payload = new Dictionary<string, string> {
            { "ResponsePlanId", data.HandlerId },
            { "IncidentId", data.IncidentId },
            { "IncidentTitle", data.IncidentTitle },
            { "ResponsePlanCreatedOn", data.HandlerCreatedAt.ToString("u") },
            { "ResponsePlanUpdatedOn", data.HandlerUpdatedAt.ToString("u") },
            { "IncidentCreatedOn", data.IncidentCreatedAt.ToString("u")  },
            { "IncidentUpdatedOn", data.IncidentUpdatedAt.ToString("u") },
            { "IncidentHandledOn", data.IncidentHandledAt?.ToString("u") ?? string.Empty },
            { "IncidentStatus", data.Status.ToLower() },
            { "IncidentSeverity", data.Priority  },
            { "IncidentMitigatedByAgent", data.IsMitigatedByAgent.ToString() },
            { "IncidentMitigatedOn", data.MitigatedAt?.ToString("u") ?? string.Empty },
            { "IncidentRootCauseCategory", data.RootCause },
            { "IncidentSummary", data.Summary },
            { "IncidentImpactedService", data.ImpactedService },
            { "AgentAutonomyLevel", data.RunMode },
            { "ResponsePlanCustom", (data.InstructionType == "Custom").ToString() }
        };
            _appInsightsLogger.LogCustomEvent("IncidentActivitySnapshot", payload);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    // to ingest into App insights the latest incident data (instances of priority changes, reactivation, etc)
    public async Task Ingest(TIncidentDocument incidentDoc)
    {
        try
        {
            string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | where customDimensions.IncidentId == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent),
                    RunMode = tostring(customDimensions.AgentAutonomyLevel), InstructionType = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn),
                    RootCauseCategory = tostring(customDimensions.IncidentRootCauseCategory), HandledAt = todatetime(customDimensions.IncidentHandledOn),
                    HandlerCreatedAt = todatetime(customDimensions.ResponsePlanCreatedOn), HandlerUpdatedAt = todatetime(customDimensions.ResponsePlanUpdatedOn)
                | summarize arg_max(UpdatedAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, RootCauseCategory, HandledAt, HandlerCreatedAt, HandlerUpdatedAt) by IncidentId
                | project IncidentId, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, RootCauseCategory, HandledAt, HandlerCreatedAt, HandlerUpdatedAt
                | top 1 by IncidentId";

            var dataTable = await Query(query);

            // In case an incident was handled before the change of column names
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | where customDimensions.IncidentId == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent),
                    RunMode = tostring(customDimensions.HandlerRunMode), InstructionType = tostring(customDimensions.HandlerInstructionType), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt),
                    RootCauseCategory = tostring(customDimensions.IncidentRootCauseCategory), HandledAt = todatetime(customDimensions.IncidentHandledAt),
                    HandlerCreatedAt = todatetime(customDimensions.HandlerCreatedAt), HandlerUpdatedAt = todatetime(customDimensions.HandlerUpdatedAt)
                | summarize arg_max(UpdatedAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, RootCauseCategory, HandledAt, HandlerCreatedAt, HandlerUpdatedAt) by IncidentId
                | project IncidentId, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, RootCauseCategory, HandledAt, HandlerCreatedAt, HandlerUpdatedAt
                | top 1 by IncidentId";

                dataTable = await Query(query);

                if (dataTable == null || dataTable.Rows.Count == 0)
                {
                    return;
                }
            }

            var results = dataTable.Rows[0];

            var data = new IncidentAIData
            {
                HandlerId = results["HandlerId"]?.ToString() ?? string.Empty,
                IncidentId = incidentDoc.Id,
                IncidentTitle = incidentDoc.Title,
                HandlerCreatedAt = DateTime.TryParse(results["HandlerCreatedAt"]?.ToString(), out DateTime handlerCreatedAt) ? handlerCreatedAt : DateTime.TryParse(results["HandledAt"].ToString(), out DateTime handledTime) ? handledTime : incidentDoc.CreatedAt,
                IncidentCreatedAt = incidentDoc.CreatedAt,
                HandlerUpdatedAt = DateTime.TryParse(results["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ? handlerUpdatedAt: incidentDoc.CreatedAt,
                IncidentUpdatedAt = incidentDoc.UpdatedAt,
                IncidentHandledAt = DateTime.TryParse(results["HandledAt"].ToString(), out DateTime incidentHandledTime) ? incidentHandledTime : handlerCreatedAt,
                MitigatedAt = IncidentMitigatedAt(incidentDoc),
                Status = incidentDoc.Status.ToString().ToLower(),
                Priority = incidentDoc.Priority,
                IsMitigatedByAgent = IsMitigatedByAgent(incidentDoc),
                RootCause = incidentDoc.RootCause,
                Summary = incidentDoc.GeneralSummary,
                ImpactedService = incidentDoc.ImpactedServiceName,
                RunMode = results["RunMode"]?.ToString() ?? string.Empty,
                InstructionType = results["InstructionType"]?.ToString() ?? string.Empty
            };

            Ingest(data);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    private async Task<DataTable> Query(string query)
    {
        string? applicationId = GetApplicationId(_coreSettings.Azure.AppInsights.ConnectionString);
        if (!string.IsNullOrWhiteSpace(applicationId))
        {
            var results = await _armHelper.QueryAppInsightsByAppId(applicationId, query);

            var dataSet = JsonConvert.DeserializeObject<DataTableResponseObjectCollection>(results);

            if (dataSet == null || dataSet.Tables == null)
            {
                return new DataTable();
            }
            else
            {
                Core.Helpers.DataTableResponseObject? dt = dataSet.Tables.FirstOrDefault();
                if (dt == null)
                {
                    return new DataTable();
                }

                foreach (var column in dt.Columns)
                {
                    column.Type = "dynamic";
                }

                var dataTable = Agent.Core.Helpers.DataTableExtensions.ToDataTable(dt);
                return dataTable;
            }
        }
        else
        {
            throw new ArgumentException("ApplicationId is not found in the connection string");
        }
    }

    public async Task<DataTable> GetHandlersIncidentIntakeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | where IncidentHandledAt between (datetime(""{startTime:yyyy-MM-ddTHH:mm:ssZ}"") .. datetime(""{endTime:yyyy-MM-ddTHH:mm:ssZ}""))
                | where HandlerId in ('{string.Join("','", handlerIds)}')
                | summarize DistinctIncidentIds=dcount(IncidentId) by bin(IncidentHandledAt, 1d)
                | order by IncidentHandledAt asc";

        var dataTable = await Query(query);
        return dataTable;
    }

    public async Task<DataTable> GetHandlersIncidentOutcomeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | where IncidentHandledAt between (datetime(""{startTime:yyyy-MM-ddTHH:mm:ssZ}"") .. datetime(""{endTime:yyyy-MM-ddTHH:mm:ssZ}""))
                | where HandlerId in ('{string.Join("','", handlerIds)}')
                | summarize arg_max(UpdatedAt, HandlerId, Status, IsMitigatedByAgent, IncidentHandledAt) by IncidentId
                | summarize TotalProcessed = dcount(IncidentId), HumanResolved = dcountif(IncidentId, IsMitigatedByAgent == ""false""), AgentResolved = dcountif(IncidentId, IsMitigatedByAgent == ""true""), InProgress = dcountif(IncidentId, Status == ""active"") by bin(IncidentHandledAt, 1d)
                | order by IncidentHandledAt asc";

        var dataTable = await Query(query);
        return dataTable;
    }


    public async Task<DataTable> GetHandlersOverview(List<string> handlerIds, DateTime startTime, DateTime endTime)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), RunMode = tostring(customDimensions.AgentAutonomyLevel), InstructionType = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | where IncidentHandledAt between (datetime(""{startTime:yyyy-MM-ddTHH:mm:ssZ}"") .. datetime(""{endTime:yyyy-MM-ddTHH:mm:ssZ}""))
                | where HandlerId in ('{string.Join("','", handlerIds)}')
                | summarize arg_max(UpdatedAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType) by IncidentId
                | summarize TotalProcessed = dcount(IncidentId), HumanResolved = dcountif(IncidentId, IsMitigatedByAgent == ""false""), AgentResolved = dcountif(IncidentId, IsMitigatedByAgent == ""true""), InProgress = dcountif(IncidentId, Status == ""active"") by HandlerId, RunMode, InstructionType
                | order by TotalProcessed desc";

        var dataTable = await Query(query);
        return dataTable;
    }

    public async Task<DataTable> GetHandlerIncidentOutcomeTrend(string handlerId, DateTime startTime, DateTime endTime)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | where IncidentHandledAt between (datetime(""{startTime:yyyy-MM-ddTHH:mm:ssZ}"") .. datetime(""{endTime:yyyy-MM-ddTHH:mm:ssZ}""))
                | where HandlerId == {handlerId}
                | summarize arg_max(UpdatedAt, HandlerId, Status, IsMitigatedByAgent, IncidentHandledAt) by IncidentId
                | summarize TotalProcessed = dcount(IncidentId), HumanResolved = dcountif(IncidentId, IsMitigatedByAgent == ""Human""), AgentResolved = dcountif(IncidentId, IsMitigatedByAgent == ""Agent""), InProgress = dcountif(IncidentId, Status == ""active"") by bin(IncidentHandledAt, 1d)
                | order by IncidentHandledAt asc";

        var dataTable = await Query(query);
        return dataTable;
    }

    public async Task<DataTable> GetHandlerIncidentOverview(string handlerId, DateTime startTime, DateTime endTime)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentSeverity), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | where IncidentHandledAt between (datetime(""{startTime:yyyy-MM-ddTHH:mm:ssZ}"") .. datetime(""{endTime:yyyy-MM-ddTHH:mm:ssZ}""))
                | where HandlerId == {handlerId}
                | summarize arg_max(UpdatedAt, HandlerId, Status, Priority, IsMitigatedByAgent) by IncidentId
                | distinct IncidentId, HandlerId, UpdatedAt, Status, Priority, IsMitigatedByAgent
                | order by Priority desc, Status desc, IsMitigatedByAgent";

        var dataTable = await Query(query);
        return dataTable;
    }

    public async Task<DataTable> GetLatestInformation(string handlerId, string incidentId)
    {
        string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledOn),
                        IncidentId = tostring(customDimensions.IncidentId),
                        IncidentTitle = tostring(customDimensions.IncidentTitle),
                        HandlerId = tostring(customDimensions.ResponsePlanId),
                        HandlerCreatedAt = todatetime(customDimensions.ResponsePlanCreatedOn),
                        HandlerUpdatedAt = todatetime(customDimensions.ResponsePlanUpdatedOn),
                        IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent),
                        Status = tostring(customDimensions.IncidentStatus),
                        Priority = tostring(customDimensions.IncidentSeverity),
                        CreatedAt = todatetime(customDimensions.IncidentCreatedOn),
                        UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn),
                        RootCause = tostring(customDimensions.IncidentRootCauseCategory),
                        Summary = tostring(customDimensions.IncidentSummary),
                        ImpactedService = tostring(customDimensions.IncidentImpactedService),
                        RunMode = tostring(customDimensions.AgentAutonomyLevel),
                        InstructionType = tostring(customDimensions.ResponsePlanCustom),
                | where HandlerId == {handlerId} and IncidentId == {incidentId}
                | summarize arg_max(UpdatedAt, HandlerId, CreatedAt, IncidentHandledAt, HandlerCreatedAt, HandlerUpdatedAt, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType) by IncidentId,
                | project IncidentId, HandlerId, CreatedAt, UpdatedAt, IncidentHandledAt, HandlerCreatedAt, HandlerUpdatedAt, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType";

        var dataTable = await Query(query);
        return dataTable;
    }

    protected async Task<string> FetchFilterFromIncident(TIncidentDocument incidentDoc)
    {
        try
        {
            string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | where customDimensions.IncidentId == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), IsMitigatedByAgent = tostring(customDimensions.IncidentMitigatedByAgent),
                    RunMode = tostring(customDimensions.AgentAutonomyLevel), InstructionType = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn),
                    RootCauseCategory = tostring(customDimensions.IncidentRootCauseCategory), HandledAt = todatetime(customDimensions.IncidentHandledOn),
                    HandlerCreatedAt = todatetime(customDimensions.ResponsePlanCreatedOn), HandlerUpdatedAt = todatetime(customDimensions.ResponsePlanUpdatedOn)
                | summarize arg_max(UpdatedAt, HandlerId, IsMitigatedByAgent, RunMode, InstructionType, RootCauseCategory, HandledAt, HandlerCreatedAt, HandlerUpdatedAt) by IncidentId
                | project HandlerId
                | top 1 by HandlerId";

            var dataTable = await Query(query);

            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return string.Empty;
            }

            var results = dataTable.Rows[0];


            return results["HandlerId"]?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Fetching the filter that handled the incident failed");
            throw;
        }
    }


    private string? GetApplicationId(string? connectionString)
    {
        if (connectionString != null)
        {
            string[] keyValues = connectionString.Split(';');

            string applicationId = string.Empty;

            foreach (var keyValue in keyValues)
            {
                string[] pair = keyValue.Split('=');
                if (pair.Length == 2 && pair[0].Trim() == "ApplicationId")
                {
                    applicationId = pair[1];
                    break;
                }
            }
            return applicationId;
        }
        return null;
    }

    public abstract Task<TIncidentDocument> AnalyzeIncident(TIncidentDocument incidentDocument, object incident);

    protected abstract bool IsMitigatedByAgent(TIncidentDocument doc);

    protected abstract DateTime? IncidentMitigatedAt(TIncidentDocument doc);
}

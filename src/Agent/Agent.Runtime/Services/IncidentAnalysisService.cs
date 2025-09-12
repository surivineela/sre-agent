using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ICM;
using Agent.Core.Models.ServiceNow;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Logging;
using Kusto.Cloud.Platform.Security;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models.Security;
using Incident = Agent.Core.Models.ICM.Incident;
using JsonConvert = Newtonsoft.Json.JsonConvert;
using PagerDutyIncident = Agent.Graph.Interfaces.PagerDutyIncident;
using Thread = Agent.Core.Models.Api.v1.Thread;


namespace Agent.Runtime.Services;

public interface IIncidentAnalysisService
{
    void Ingest(IncidentAIData data);

    Task Ingest(IIncidentDocument incidentDocument);

    // Queries App Insights to receive information about the handled incidents
    Task<DataTable> GetHandlersIncidentIntakeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlersIncidentOutcomeTrend(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlersOverview(List<string> handlerIds, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlerIncidentOutcomeTrend(string handlerId, DateTime startTime, DateTime endTime);

    Task<DataTable> GetHandlerIncidentOverview(string handlerId, DateTime startTime, DateTime endTime);

    Task<DataTable> GetLatestInformation(string handlerId, string incidentId);

    // Getting the AI-generated metrics for the handled incidents
    Task<IcmIncidentDocument> AnalyzeIncident(IcmIncidentDocument incidentDocument, Incident incident);
    Task<PagerDutyIncidentDocument> AnalyzeIncident(PagerDutyIncidentDocument incidentDocument, PagerDutyIncident incident);
    Task<ServiceNowIncidentDocument> AnalyzeIncident(ServiceNowIncidentDocument incidentDocument, ServiceNowIncident incident);
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

public class IncidentAnalysisService: IIncidentAnalysisService
{
    private readonly IChatClient _client;
    private readonly IIncidentManagementServiceFactory _incidentManagementServiceFactory;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly ILogger<IncidentAnalysisService> _logger;
    private readonly string _incidentRootCausePrompt;
    private readonly string _incidentTSGPrompt;
    private readonly string _incidentGeneralSummaryPrompt;
    private readonly string _incidentDataSourcesPrompt;
    private readonly string _incidentActionsInvokedPrompt;
    private readonly CoreSettings _coreSettings;
    private readonly ArmHelper _armHelper;
    private readonly IncidentAnalysisLogger _appInsightsLogger;


    public IncidentAnalysisService(
        IChatClient client,
        IIncidentManagementServiceFactory incidentManagementServiceFactory,
        IncidentManagementSettings incidentManagementSettings,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<IncidentAnalysisService> logger)
    {
        _client = client;
        _incidentManagementServiceFactory = incidentManagementServiceFactory;
        _incidentManagementSettings = incidentManagementSettings;
        _logger = logger;
        _coreSettings = coreSettings;
        _armHelper = armHelper;
        _appInsightsLogger = new IncidentAnalysisLogger(_coreSettings.Azure.AppInsights.ConnectionString);

        _incidentRootCausePrompt = @"Analyze the following incident and, from the provided details, provide a generic root cause category that
            the incident falls into based on the investigation and steps taken to resolve it. Only provide the root cause category in the response.
            Do not include a preface or postface.";
        _incidentGeneralSummaryPrompt = @"From the provided details, analyze the following incident and provide a general summary in a few short sentences.
            In the summary, mention the context of the incident, the symptoms, and how the issue was mitigated and resolved. Only provide the root cause
            category in the response. Do not include a preface or postface.";
        _incidentTSGPrompt = @"Analyze the following incident and provide a troubleshooting guide that describes how to solve the issue
            as stated on the incident";
        _incidentDataSourcesPrompt = @"Analyze the following incident and provide a list of data sources that can be used to investigate the issue.
        When providing the response, only return the json list of data sources. Do not include any text before or after the list.";
        _incidentActionsInvokedPrompt = @"Analyze the following incident and provide a list of actions that were invoked to resolve the issue.
        When providing the response, only return the json list of invoked actions. Do not include any text before or after the list.";
    }


    // when to ingest data: whenever there's an incident document
    public void Ingest(IncidentAIData data)
    {
        try
        {
            var payload = new Dictionary<string, string> {
            { "HandlerId", data.HandlerId },
            { "IncidentId", data.IncidentId },
            { "IncidentTitle", data.IncidentTitle },
            { "HandlerCreatedAt", data.HandlerCreatedAt.ToString("u") },
            { "HandlerUpdatedAt", data.HandlerUpdatedAt.ToString("u") },
            { "IncidentCreatedAt", data.IncidentCreatedAt.ToString("u")  },
            { "IncidentUpdatedAt", data.IncidentUpdatedAt.ToString("u") },
            { "IncidentHandledAt", data.IncidentHandledAt?.ToString("u") ?? string.Empty },
            { "IncidentStatus", data.Status.ToLower() },
            { "IncidentSeverity", data.Priority  },
            { "IsIncidentMitigatedByAgent", data.IsMitigatedByAgent.ToString() },
            { "IncidentMitigatedAt", data.MitigatedAt?.ToString("u") ?? string.Empty },
            { "IncidentRootCauseCategory", data.RootCause },
            { "IncidentSummary", data.Summary },
            { "IncidentImpactedService", data.ImpactedService },
            { "HandlerRunMode", data.RunMode },
            { "HandlerInstructionType", data.InstructionType }
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
    public async Task Ingest(IIncidentDocument incidentDoc)
    {
        try
        {
            string query = $@"
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

            var dataTable = await Query(query);
            var results = dataTable.Rows[0];

            if (results == null)
            {
                return;
            }

            var data = new IncidentAIData
            {
                HandlerId = results["HandlerId"]?.ToString() ?? string.Empty,
                IncidentId = incidentDoc.Id,
                IncidentTitle = incidentDoc.Title,
                HandlerCreatedAt = DateTime.TryParse(results["HandlerCreatedAt"]?.ToString(), out DateTime handledCreatedAt) ? handledCreatedAt : incidentDoc.HandledAt,
                IncidentCreatedAt = incidentDoc.CreatedAt,
                HandlerUpdatedAt = DateTime.TryParse(results["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ? handlerUpdatedAt: incidentDoc.CreatedAt,
                IncidentUpdatedAt = incidentDoc.UpdatedAt,
                IncidentHandledAt = !string.IsNullOrWhiteSpace(incidentDoc.HandledAt.ToString()) ? incidentDoc.HandledAt : DateTime.TryParse(results["IncidentHandledAt"].ToString(), out DateTime handledTime) ? handledTime : null,
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

    private bool IsMitigatedByAgent(IIncidentDocument doc)
    {
        bool isMitigatedByAgent = false;
        string status;
        switch (_incidentManagementSettings.Type)
        {
            case IncidentManagementType.PagerDuty:
                var pdIncident = (PagerDutyIncidentDocument)doc;
                status = pdIncident.Status.ToLower();
                isMitigatedByAgent = (status == "resolved" || status=="closed") && (pdIncident.Tags?.Contains("SREAgent_Mitigated") ?? false);
                break;
            case IncidentManagementType.Icm:
                var icmIncident = (IcmIncidentDocument)doc;
                status = icmIncident.Status.ToString().ToLower();
                isMitigatedByAgent = (status == "mitigated" || status == "resolved") && ((icmIncident.MitigateData?.MitigatedBy.Contains("agent") ?? false) ||
                    icmIncident.Tags.Contains("SREAgent_Mitigated"));
                break;
            case IncidentManagementType.ServiceNow:
                var serviceNowIncident = (ServiceNowIncidentDocument)doc;
                status = serviceNowIncident.Status.ToString().ToLower();
                isMitigatedByAgent = (status == "resolved" || status == "closed") && (serviceNowIncident.Tags?.Contains("SREAgent_Mitigated") ?? false);
                break;
            default:
                throw new NotSupportedException($"Incident management type '{_incidentManagementSettings.Type}' is not supported.");
        }

        return isMitigatedByAgent;
    }

    private DateTime? IncidentMitigatedAt(IIncidentDocument doc)
    {
        DateTime? mitigatedAt = null;

        switch (_incidentManagementSettings.Type)
        {
            case IncidentManagementType.PagerDuty:
                var pdIncident = (PagerDutyIncidentDocument)doc;
                mitigatedAt = pdIncident.ResolvedAt;
                break;
            case IncidentManagementType.Icm:
                var icmIncident = (IcmIncidentDocument)doc;
                mitigatedAt = icmIncident.MitigatedAt;
                break;
            case IncidentManagementType.ServiceNow:
                var serviceNowIncident = (ServiceNowIncidentDocument)doc;
                mitigatedAt = serviceNowIncident.ResolvedAt;
                break;
            default:
                throw new NotSupportedException($"Incident management type '{_incidentManagementSettings.Type}' is not supported.");
        }

        return mitigatedAt;
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt)
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt)
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), RunMode = tostring(customDimensions.HandlerRunMode), InstructionType = tostring(customDimensions.HandlerInstructionType), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt)
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt)
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt), IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.HandlerId), IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent), Status = tostring(customDimensions.IncidentStatus), Priority = tostring(customDimensions.IncidentPriority), UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt)
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
                | extend IncidentHandledAt = todatetime(customDimensions.IncidentHandledAt),
                        IncidentId = tostring(customDimensions.IncidentId),
                        IncidentTitle = tostring(customDimensions.IncidentTitle),
                        HandlerId = tostring(customDimensions.HandlerId),
                        HandlerCreatedAt = todatetime(customDimensions.HandlerCreatedAt),
                        HandlerUpdatedAt = todatetime(customDimensions.HandlerUpdatedAt),
                        IsMitigatedByAgent = tostring(customDimensions.IsIncidentMitigatedByAgent),
                        Status = tostring(customDimensions.IncidentStatus),
                        Priority = tostring(customDimensions.IncidentPriority),
                        CreatedAt = todatetime(customDimensions.IncidentCreatedAt),
                        UpdatedAt = todatetime(customDimensions.IncidentUpdatedAt),
                        RootCause = tostring(customDimensions.IncidentRootCauseCategory),
                        Summary = tostring(customDimensions.IncidentSummary),
                        ImpactedService = tostring(customDimensions.IncidentImpactedService),
                        RunMode = tostring(customDimensions.HandlerRunMode),
                        InstructionType = tostring(customDimensions.HandlerInstructionType),
                        HandledAt = todatetime(IncidentHandledAt)
                | where HandlerId == {handlerId} and IncidentId == {incidentId}
                | summarize arg_max(UpdatedAt, HandlerId, CreatedAt, HandledAt, HandlerCreatedAt, HandlerUpdatedAt, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType) by IncidentId,
                | project IncidentId, HandlerId, CreatedAt, UpdatedAt, HandledAt, HandlerCreatedAt, HandlerUpdatedAt, Status, Priority, IsMitigatedByAgent, RootCause, ImpactedService, RunMode, InstructionType";

        var dataTable = await Query(query);
        return dataTable;
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

    public async Task<IcmIncidentDocument> AnalyzeIncident(IcmIncidentDocument incidentDocument, Incident incident)
    {
        var rootCause = await GetRootCause(incident);
        var generalSummary = await GetGeneralSummary(incident);
        /*var tsg = await GetTSG(incident);
        var dataSources = await GetDataSources(incident);
        var actionsInvoked = await GetActionsInvoked(incident);*/

        incidentDocument.RootCause = rootCause;
        incidentDocument.GeneralSummary = generalSummary;
        /*incidentDocument.TSG = tsg;
        incidentDocument.DataSources = dataSources;
        incidentDocument.ActionsInvoked = actionsInvoked;*/
        return incidentDocument;
    }

    public async Task<PagerDutyIncidentDocument> AnalyzeIncident(PagerDutyIncidentDocument incidentDocument, PagerDutyIncident incident)
    {
        var rootCause = await GetRootCause(incident);
        var generalSummary = await GetGeneralSummary(incident);
        /*var tsg = await GetTSG(incident);
        var dataSources = await GetDataSources(incident);
        var actionsInvoked = await GetActionsInvoked(incident);*/

        incidentDocument.RootCause = rootCause;
        incidentDocument.GeneralSummary = generalSummary;
        /*incidentDocument.TSG = tsg;
        incidentDocument.DataSources = dataSources;
        incidentDocument.ActionsInvoked = actionsInvoked;*/
        return incidentDocument;
    }

    public async Task<ServiceNowIncidentDocument> AnalyzeIncident(ServiceNowIncidentDocument incidentDocument, ServiceNowIncident incident)
    {
        var rootCause = await GetRootCause(incident);
        var generalSummary = await GetGeneralSummary(incident);
        /*var tsg = await GetTSG(incident);
        var dataSources = await GetDataSources(incident);
        var actionsInvoked = await GetActionsInvoked(incident);*/

        incidentDocument.RootCause = rootCause;
        incidentDocument.GeneralSummary = generalSummary;
        /*incidentDocument.TSG = tsg;
        incidentDocument.DataSources = dataSources;
        incidentDocument.ActionsInvoked = actionsInvoked;*/
        return incidentDocument;
    }

    private async Task<string> IncidentOverview(Incident incident)
    {
        IcmIncidentDocument existingIncidentDocument = await _incidentManagementServiceFactory.GetServiceDynamic().GetIncidentDetails(incident.IncidentId);
        var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();
        var notes = existingDiscussionEntries
                .Select(entry => new IncidentDiscussion(entry.IncidentId, entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                .ToList();

        return $@"Title: {incident.Title}\n
        Mitigation Steps: {incident.MitigateData?.MitigationSteps}\n
        Summary: {incident.Summary}\n
        DiscussionEntry: {incident.DiscussionEntry}\n
        Notes: {JsonConvert.SerializeObject(notes)}";
    }

    private async Task<string> IncidentOverview(PagerDutyIncident incident)
    {
        // may need to use pagerDutyService to get most recent notes
        PagerDutyIncidentDocument existingIncidentDocument = await _incidentManagementServiceFactory.GetServiceDynamic().GetIncidentDetails(incident.IncidentId);

        var notes = existingIncidentDocument?.Notes;
        var notesContent = notes?.Select(n => n.Content).ToList();

        return $@"Title: {incident.Title}\n
        Description: {incident.Description}\n
        Details: {incident.Body?.Details ?? "N/A"}\n
        Notes: {JsonConvert.SerializeObject(notesContent)}\n
        Channel Summary: {incident.FirstTriggerLogEntry.Channel!.Summary}\n
        Channel Details: {incident.FirstTriggerLogEntry.Channel!.Details}";
    }

    private async Task<string> IncidentOverview(ServiceNowIncident incident)
    {
        // may need to use serviceapiclient to get most recent notes
        // var latestDiscussionEntries = await serviceNowApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.IncidentSystemId);

        ServiceNowIncidentDocument existingIncidentDocument = await _incidentManagementServiceFactory.GetServiceDynamic().GetIncidentDetails(incident.Number);
        var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();

        var newNotes = existingDiscussionEntries.Select(entry => entry.Text).ToList();
        return $@"Title: {incident.Title}\n
        Description: {incident.Description}\n
        Impacted Service: {incident.ImpactedServiceName}\n
        Notes: {JsonConvert.SerializeObject(newNotes)}";
    }

    private async Task<string> GetRootCause(Incident incident)
    {
        string rootCause = await GetAIResponse(_incidentRootCausePrompt, incident);
        return rootCause;
    }

    private async Task<string> GetRootCause(PagerDutyIncident incident)
    {
        string rootCause = await GetAIResponse(_incidentRootCausePrompt, incident);
        return rootCause;
    }

    private async Task<string> GetRootCause(ServiceNowIncident incident)
    {
        string rootCause = await GetAIResponse(_incidentRootCausePrompt, incident);
        return rootCause;
    }

    private async Task<string> GetTSG(Incident incident)
    {
        string tsg = await GetAIResponse(_incidentTSGPrompt, incident);
        return tsg;
    }

    private async Task<string> GetTSG(PagerDutyIncident incident)
    {
        string tsg = await GetAIResponse(_incidentTSGPrompt, incident);
        return tsg;
    }

    private async Task<string> GetTSG(ServiceNowIncident incident)
    {
        string tsg = await GetAIResponse(_incidentTSGPrompt, incident);
        return tsg;
    }

    private async Task<string> GetGeneralSummary(Incident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    private async Task<string> GetGeneralSummary(PagerDutyIncident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    private async Task<string> GetGeneralSummary(ServiceNowIncident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    private async Task<List<string>> GetDataSources(Incident incident)
    {
        try
        {
            string dataSourcesResponse = await GetAIResponse(_incidentDataSourcesPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(dataSourcesResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of data sources: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<List<string>> GetDataSources(PagerDutyIncident incident)
    {
        try
        {
            string dataSourcesResponse = await GetAIResponse(_incidentDataSourcesPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(dataSourcesResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of data sources: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<List<string>> GetDataSources(ServiceNowIncident incident)
    {
        try
        {
            string dataSourcesResponse = await GetAIResponse(_incidentDataSourcesPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(dataSourcesResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of data sources: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<List<string>> GetActionsInvoked(Incident incident)
    {
        try
        {
            string actionsResponse = await GetAIResponse(_incidentActionsInvokedPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(actionsResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of invoked actions: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<List<string>> GetActionsInvoked(PagerDutyIncident incident)
    {
        try
        {
            string actionsResponse = await GetAIResponse(_incidentActionsInvokedPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(actionsResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of invoked actions: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<List<string>> GetActionsInvoked(ServiceNowIncident incident)
    {
        try
        {
            string actionsResponse = await GetAIResponse(_incidentActionsInvokedPrompt, incident);
            var list = JsonConvert.DeserializeObject<List<string>>(actionsResponse);
            return list ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting list of invoked actions: {message}", ex.Message);
            return new List<string>();
        }
    }

    private async Task<string> GetAIResponse(string prompt, ServiceNowIncident incident)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{IncidentOverview(incident)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Text,
        };

        var reply = await _client.GetResponseAsync(messages, options);
        return reply.Text;
    }

    private async Task<string> GetAIResponse(string prompt, PagerDutyIncident incident)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{IncidentOverview(incident)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Text,
        };

        var reply = await _client.GetResponseAsync(messages, options);
        return reply.Text;
    }

    private async Task<string> GetAIResponse(string prompt, Incident incident)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{IncidentOverview(incident)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Text,
        };

        var reply = await _client.GetResponseAsync(messages, options);
        return reply.Text;
    }


}

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ServiceNow;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Drives.Item.Items.Item.Workbook.Functions.FindB;
using Microsoft.Graph.Models.Security;
using Newtonsoft.Json;
using JsonConvert = Newtonsoft.Json.JsonConvert;


namespace Agent.Runtime.Services;

public interface IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocument : IIncidentFilterDocument
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    where TIncident : class
{
    void Ingest(IncidentAIData data);

    Task Ingest(TIncidentDocument incidentDocument, TIncidentFilterDocument? filterDoc);

    Task<TIncidentDocument> AnalyzeIncident(TIncidentDocument incidentDocument, TIncident incident, TIncidentFilterDocument? filterDocument);

    Task<bool> DetermineAgentAssistanceFromNotes(TIncidentDocument incident, List<IncidentDiscussion> newNotes);
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
    public required bool IsAssistedByAgent { get; set; }
    public required string RootCause { get; set; }
    public required string RootCauseDescription { get; set; }
    public required string Summary { get; set; }
    public required string ImpactedService { get; set; }
    public required DateTime? MitigatedAt { get; set; }
    public required string RunMode { get; set; }
    public required bool IsHandlerCustom { get; set; }
    public required string IncidentPlatform { get; set; }
}

public class AgentAssistanceResponse
{
    [JsonProperty("hasMeaningfulAssistance")]
    public bool HasMeaningfulAssistance { get; set; }
}

public abstract class IncidentAnalysisServiceBase<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident> : IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident>
    where TIncidentDocument : IIncidentDocument
    where TIncidentFilterDocument : TIncidentFilterDocumentPayload, IIncidentFilterDocument, new()
    where TIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
    where TIncident : class
{
    protected readonly IChatClientProvider _chatClientProvider;
    protected readonly IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> _incidentManagementService;
    protected readonly IIncidentHandlerManagementService _incidentHandlerManagementService;
    protected readonly ILogger<IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident>> _logger;
    protected readonly CustomerLogger _appInsightsLogger;
    protected readonly Container _container;
    private readonly IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> _incidentFilterManagementService;
    private readonly CoreSettings _coreSettings;
    private readonly ArmHelper _armHelper;
    private readonly string _incidentRootCausePrompt;
    private readonly string _incidentGeneralSummaryPrompt;


    public IncidentAnalysisServiceBase(
        IChatClientProvider chatClientProvider,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<TIncidentDocument, TIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<TIncidentFilterDocument, TIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        CustomerLogger appInsightsLogger,
        ILogger<IIncidentAnalysisService<TIncidentDocument, TIncidentFilterDocument, TIncidentFilterDocumentPayload, TIncident>> logger)
    {
        _chatClientProvider = chatClientProvider;
        _incidentManagementService = incidentManagementService;
        _incidentHandlerManagementService = incidentHandlerManagementService;
        _incidentFilterManagementService = incidentFilterManagementService;
        _logger = logger;
        _coreSettings = coreSettings;
        _armHelper = armHelper;
        _appInsightsLogger = appInsightsLogger;
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);


        _incidentRootCausePrompt = @"Analyze the following incident and, from the provided details of the investigation and resolution steps, provide a generic root cause category that
            the incident falls into. The root cause category should be a few words, 5 at most. 

            IMPORTANT: Create a GENERIC description that can be reused for similar incidents. The description should:
            - Expand upon the root cause category with general circumstances and patterns
            - Avoid incident-specific details (service names, timestamps, specific error messages, etc.)
            - Focus on the underlying technical or operational pattern that caused the issue
            - Be broad enough that other incidents with the same root cause category can be categorized under it

            Example: Instead of 'Database timeout on UserService at 3:15 PM', use 'Database performance degradation causing timeout errors under high load conditions'

            If one of the provided existing root cause categories matches this incident, choose the most suitable one. If there is no suitable category out of the provided options, create a new one. 
            Return the response as a JSON object strictly in the following schema: { ""RootCause"": ""<root cause category>"", ""Description"": ""<generic description of the root cause category>"" }
            Do not include any additional text outside of the JSON response.";
        _incidentGeneralSummaryPrompt = @"From the provided details, analyze the following incident and provide a general summary in a few short sentences.
            In the summary, mention the context of the incident, the symptoms, and how the issue was mitigated and resolved. Only provide the summary in the response.
            Do not include a preface or postface.";
    }


    // when to ingest data: whenever there's an incident document
    public virtual void Ingest(IncidentAIData data)
    {
        try
        {
            var payload = new Dictionary<string, string> {
            { "ResponsePlanId", data.HandlerId },
            { "IncidentId", data.IncidentId },
            { "IncidentTitle", data.IncidentTitle },
            { "ResponsePlanCreatedOn", data.HandlerCreatedAt.ToString("O") },
            { "ResponsePlanUpdatedOn", data.HandlerUpdatedAt.ToString("O") },
            { "IncidentCreatedOn", data.IncidentCreatedAt.ToString("O")  },
            { "IncidentUpdatedOn", data.IncidentUpdatedAt.ToString("O") },
            { "IncidentHandledOn", data.IncidentHandledAt?.ToString("O") ?? string.Empty },
            { "IncidentStatus", data.Status.ToLower() },
            { "IncidentSeverity", data.Priority  },
            { "IncidentMitigatedByAgent", data.IsMitigatedByAgent.ToString() },
            { "IncidentAssistedByAgent", data.IsAssistedByAgent.ToString() },
            { "IncidentMitigatedOn", data.MitigatedAt?.ToString("O") ?? string.Empty },
            { "IncidentRootCauseCategory", data.RootCause },
            { "IncidentRootCauseDescription", data.RootCauseDescription },
            { "IncidentSummary", data.Summary },
            { "IncidentImpactedService", data.ImpactedService },
            { "AgentAutonomyLevel", data.RunMode },
            { "ResponsePlanCustom", data.IsHandlerCustom.ToString() },
            { "IncidentPlatform", data.IncidentPlatform },
        };
            _appInsightsLogger.LogCustomEvent("IncidentActivitySnapshot", payload);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    public async Task Ingest(TIncidentDocument incidentDoc, TIncidentFilterDocument? filterDoc = null)
    {
        try
        {
            if (filterDoc == null)
            {
                filterDoc = await QueryFilter(incidentDoc);
            }

            var handlers = await _incidentHandlerManagementService.ListIncidentHandlers();
            var handlerDoc = handlers.Where(h => h.Id == filterDoc?.Id).FirstOrDefault();

            string query = GetPastIncidentDataQuery(incidentDoc);
            var dataTable = await Query(query);
            DataRow? results = null;
            if (dataTable != null && dataTable.Rows.Count > 0)
            {
                results = dataTable.Rows[0];
            }

            var data = ToIncidentActivitySnapshot(filterDoc, handlerDoc, incidentDoc, results);
            Ingest(data);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    public async Task<TIncidentDocument> AnalyzeIncident(TIncidentDocument incidentDocument, TIncident incident, TIncidentFilterDocument? filterDoc)
    {
        string filterId;
        if (filterDoc == null)
        {
            filterId = await FetchFilterFromIncident(incidentDocument);
        }
        else
        {
            filterId = filterDoc.Id;
        }

        var rootCauseResponse = await GetRootCauseCategory(filterId, incident);
        var generalSummary = await GetGeneralSummary(incident);

        // Extract just the category name for backwards compatibility
        incidentDocument.AIRootCause = rootCauseResponse.RootCause;
        incidentDocument.RootCauseDescription = rootCauseResponse.Description;
        incidentDocument.GeneralSummary = generalSummary;

        return incidentDocument;
    }


    /// <summary>
    /// Determines if the agent provided meaningful assistance based on tool usage and outcomes
    /// </summary>
    /// <param name="incident">The incident document</param>
    /// <param name="newNotes">New discussion entries to analyze</param>
    /// <returns>True if meaningful agent assistance is detected</returns>
    public async Task<bool> DetermineAgentAssistanceFromNotes(TIncidentDocument incident, List<IncidentDiscussion> newNotes)
    {
        try
        {
            return await AnalyzeDiscussionsForMeaningfulAssistance(newNotes);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Error determining agent assistance for incident {incidentId}", incident.Id);
            return false;
        }
    }

    protected abstract bool IsMitigatedByAgent(TIncidentDocument doc);

    protected abstract DateTime? IncidentMitigatedAt(TIncidentDocument doc);

    protected abstract Task<string> IncidentOverview(TIncident incident);

    protected abstract Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, TIncident incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the incident platform type as a string. Override in derived classes to return the specific platform.
    /// </summary>
    protected abstract string GetIncidentPlatform();

    protected string GetPastIncidentDataQuery(TIncidentDocument incidentDoc)
    {
        string query = $@"
                customEvents
                | where timestamp > ago(60d) and name == ""IncidentActivitySnapshot""
                | where tostring(customDimensions.IncidentId) == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), 
                    RunMode = tostring(customDimensions.AgentAutonomyLevel), IsHandlerCustom = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = tostring(customDimensions.IncidentUpdatedOn),
                    HandledAt = tostring(customDimensions.IncidentHandledOn), HandlerCreatedAt = tostring(customDimensions.ResponsePlanCreatedOn), HandlerUpdatedAt = tostring(customDimensions.ResponsePlanUpdatedOn),
                    IncidentPlatform = tostring(customDimensions.IncidentPlatform)
                | summarize arg_max(UpdatedAt, HandlerId, RunMode, IsHandlerCustom, HandledAt, HandlerCreatedAt, HandlerUpdatedAt, IncidentPlatform) by IncidentId
                | project IncidentId, HandlerId, RunMode, IsHandlerCustom, HandledAt, HandlerCreatedAt, HandlerUpdatedAt, IncidentPlatform
                | top 1 by IncidentId";

        return query;
    }

    protected async Task<DataTable> Query(string query)
    {
        string applicationId = GetApplicationId(Environment.GetEnvironmentVariable("AppSettings__Core__Azure__ApplicationInsights__ConnectionString") ?? _coreSettings.Azure.AppInsights.ConnectionString);
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

    protected virtual IncidentAIData ToIncidentActivitySnapshot(TIncidentFilterDocument? filterDoc, IncidentHandlerDocument? handlerDoc, TIncidentDocument incidentDoc, DataRow? results)
    {
        string handlerId = filterDoc?.Id ?? handlerDoc?.Id ?? "No-filterid-found";
        DateTime? handlerCreatedOn = filterDoc?.CreatedAt;
        DateTime? handlerUpdatedOn = filterDoc?.UpdatedAt;
        string runMode = !string.IsNullOrWhiteSpace(filterDoc?.AgentMode) ? filterDoc.AgentMode : "review";
        bool isHandlerCustom = !string.IsNullOrWhiteSpace(handlerDoc?.CustomInstructions) ? true : false;

        var snapshot = new IncidentAIData
        {
            HandlerId = !string.IsNullOrWhiteSpace(results?["HandlerId"]?.ToString()) ? results?["HandlerId"]?.ToString()! : handlerId,
            IncidentId = incidentDoc.Id,
            IncidentTitle = incidentDoc.Title,
            HandlerCreatedAt = (DateTime)(handlerCreatedOn != null ? handlerCreatedOn : DateTime.TryParse(results?["HandlerCreatedAt"]?.ToString(), out DateTime handlerCreatedAt) ? handlerCreatedAt : DateTime.UtcNow),
            IncidentCreatedAt = incidentDoc.CreatedAt,
            HandlerUpdatedAt = (DateTime)(handlerUpdatedOn != null ? handlerUpdatedOn : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ? handlerUpdatedAt : DateTime.UtcNow),
            IncidentUpdatedAt = incidentDoc.UpdatedAt,
            IncidentHandledAt = DateTime.TryParse(results?["HandledAt"]?.ToString(), out DateTime incidentHandledTime) ?
                (incidentHandledTime <= DateTime.MinValue.AddDays(1) ? new DateTime(Math.Max(incidentDoc.CreatedAt.Ticks, handlerCreatedOn?.Ticks ?? 0)) : incidentHandledTime) : handlerCreatedOn ?? DateTime.UtcNow,
            MitigatedAt = IncidentMitigatedAt(incidentDoc),
            Status = incidentDoc.Status.ToString().ToLower(),
            Priority = incidentDoc.Priority,
            IsMitigatedByAgent = IsMitigatedByAgent(incidentDoc),
            IsAssistedByAgent = incidentDoc.IsAssistedByAgent,
            RootCause = incidentDoc.AIRootCause,
            RootCauseDescription = incidentDoc.RootCauseDescription,
            Summary = incidentDoc.GeneralSummary,
            ImpactedService = incidentDoc.ImpactedServiceName,
            RunMode = !string.IsNullOrWhiteSpace(results?["RunMode"]?.ToString()) ? results?["RunMode"]?.ToString()! : runMode,
            IsHandlerCustom = bool.TryParse(results?["IsHandlerCustom"]?.ToString(), out bool isCustom) ? isCustom : isHandlerCustom,
            IncidentPlatform = GetIncidentPlatform()
        };

        return snapshot;
    }

    protected async Task<string> FetchFilterFromIncident(TIncidentDocument incidentDoc)
    {
        try
        {
            string query = $@"
                customEvents
                | where name == ""IncidentActivitySnapshot""
                | where tostring(customDimensions.IncidentId) == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn)
                | summarize arg_max(UpdatedAt, HandlerId) by IncidentId
                | project HandlerId
                | top 1 by HandlerId";

            var dataTable = await Query(query);

            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                var filter = await QueryFilter(incidentDoc);
                if (filter == null)
                {
                    return string.Empty;
                }
                return filter.Id;
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

    protected async Task<TIncidentFilterDocument?> QueryFilter(TIncidentDocument incidentDoc)
    {
        var filters = await _incidentFilterManagementService.ListIncidentFilters();
        foreach (var filter in filters)
        {
            var incidents = await _incidentManagementService.QueryIncidents(new IncidentQueryRequest<TIncidentFilterDocumentPayload>
            {
                Filter = filter,
                DurationInDays = (DateTime.UtcNow - incidentDoc.CreatedAt).Days + 5,
                Statuses = [],
                Priorities = [],
                PageSize = 100,
                SearchTerm = incidentDoc.Id,
                Keywords = [incidentDoc.Title],
            });

            if (incidents.Items.Where(i => i.Id == incidentDoc.Id).Any())
            {
                return filter;
            }
            else
            {
                continue;
            }
        }
        return null;
    }

    protected async Task<AIRootCauseResponse> GetAIRootCause(TIncident incident, List<RootCauseCategory> existingRootCauses)
    {
        var rootCausesForPrompt = existingRootCauses.Select(rc => new { Category = rc.Category, Description = rc.Description }).ToList();

        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{_incidentRootCausePrompt}:\n\n{await IncidentOverview(incident)}"),
            new(ChatRole.User, $"Here are the existing root cause categories and their descriptions: {JsonConvert.SerializeObject(rootCausesForPrompt)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
        };

        var (response, result) = await _chatClientProvider.DefaultModel.GetResponseAsync(messages, typeof(AIRootCauseResponse), options);

        if (result is AIRootCauseResponse rootCauseResponse)
        {
            return rootCauseResponse;
        }

        _logger.LogInternalWarning("Failed to get structured response, result was null or wrong type");
        return new AIRootCauseResponse { RootCause = "Unknown", Description = "Unable to categorize incident" };
    }

    protected async Task<string> GetGeneralSummary(TIncident incident)
    {
        string summary = await GetIncidentAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    protected async Task<string> GetAIResponse(string prompt)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, prompt)
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Text,
        };

        var reply = await _chatClientProvider.DefaultModel.GetResponseAsync(messages, options);
        return reply.Text;
    }


    protected async Task<string> GetIncidentAIResponse(string prompt, TIncident incident)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{await IncidentOverview(incident)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Text,
        };

        var reply = await _chatClientProvider.DefaultModel.GetResponseAsync(messages, options);
        return reply.Text;
    }

    /// <summary>
    /// Uses AI to analyze discussions for meaningful assistance patterns
    /// </summary>
    private async Task<bool> AnalyzeDiscussionsForMeaningfulAssistance(List<IncidentDiscussion> newNotes)
    {
        try
        {
            // Construct AI prompt focused on assistance quality, not just presence
            var discussionText = string.Join("\n---\n",
                newNotes.Select(entry => $"[{entry.CreatedTimestamp:yyyy-MM-dd HH:mm}] {entry.UserId}: {entry.Message}"));

            var aiPrompt = $@"
            Analyze the following incident discussion entries to determine if an AI agent provided MEANINGFUL assistance in investigating, troubleshooting, or resolving the incident.

            Consider the following as meaningful assistance:
            1. **Investigation Actions**: Agent executed diagnostic commands, queried logs/metrics, checked service health, presented application information, or gathered relevant data
            2. **Remediation Actions**: Agent performed mitigation steps such as restarting services/apps, scaling resources, or applying configuration changes
            3. **Technical Analysis**: Agent analyzed logs/metrics, identified patterns, correlated events, or provided technical insights about the issue
            4. **Knowledge Sharing**: Agent retrieved and shared relevant documentation, runbooks, past incident learnings, or troubleshooting guidance
            5. **Diagnostic Recommendations**: Agent suggested specific investigation steps, queries to run, or areas to examine
            6. **Root Cause Analysis**: Agent identified potential or confirmed root causes based on available evidence

            DO NOT consider these as meaningful assistance:
            - Simple messages without helpful or useful information
            - Purely administrative or procedural actions that don't contribute to understanding or resolving the technical issue

            If the agent performed ANY investigative work, presented any resource information/data, provided technical information, executed diagnostic or remediation actions, or helped advance understanding of the incident, consider it meaningful assistance.
            Examples of meaningful assistance include: printing summary of logs, providing steps to troubleshoot the issue, providing metadata and details of the resource, executing steps towards resolution or mitigation, etcetera

            Incident Discussion:
            {discussionText}

            Response Rules: Provide a response in the specified response format. DO NOT include any additional text outside of the JSON response.

            Response format: {{""hasMeaningfulAssistance"": true/false}}";


            var aiResponse = await GetAIResponse(aiPrompt);
            var response = JsonConvert.DeserializeObject<AgentAssistanceResponse>(aiResponse);
            return response?.HasMeaningfulAssistance ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "[IncidentAnalysisService] Failed to retrieve AI response for assistance analysis");
            return false;
        }
    }

    private string GetApplicationId(string? connectionString)
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
        else
        {
            _logger.LogInternalError("[IncidentAnalysisService] ApplicationInsights Connection string is empty");
            throw new Exception("[IncidentAnalysisService] ApplicationInsights Connection string is empty");
        }
    }
}

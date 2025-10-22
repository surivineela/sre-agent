using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PagerDutyIncident = Agent.Graph.Interfaces.PagerDutyIncident;


namespace Agent.Runtime.Services;
public class PagerDutyIncidentAnalysisService : IncidentAnalysisServiceBase<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload, PagerDutyIncident>
{
    private readonly Container container;
    private readonly ILogger<PagerDutyIncidentAnalysisService> _logger;

    public PagerDutyIncidentAnalysisService(
        IChatClient client,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<PagerDutyIncidentFilterDocument, PagerDutyIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        CustomerLogger appInsightsLogger,
        ILogger<PagerDutyIncidentAnalysisService> logger) : base(client, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _logger = logger;
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.PagerDuty.ToString();
    }

    public override void Ingest(IncidentAIData data)
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
            { "IncidentStatus", StatusMatching(data.Status).ToLower() },
            { "IncidentSeverity", data.Priority  },
            { "IncidentMitigatedByAgent", data.IsMitigatedByAgent.ToString() },
            { "IncidentAssistedByAgent", data.IsAssistedByAgent.ToString() },
            { "IncidentMitigatedOn", data.MitigatedAt?.ToString("O") ?? string.Empty },
            { "IncidentRootCauseCategory", data.RootCause },
            { "IncidentRootCauseDescription", data.RootCauseDescription },
            { "IncidentSummary", data.Summary },
            { "IncidentImpactedService", data.ImpactedService },
            { "AgentAutonomyLevel", data.RunMode },
            { "ResponsePlanCustom", (data.InstructionType == "Custom").ToString() },
            { "IncidentPlatform", data.IncidentPlatform }
        };
            _appInsightsLogger.LogCustomEvent("IncidentActivitySnapshot", payload);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    public override async Task Ingest(PagerDutyIncidentDocument incidentDoc, PagerDutyIncidentFilterDocument? filterDoc = null)
    {
        try
        {
            if (filterDoc == null)
            {
                filterDoc = await QueryFilter(incidentDoc);
            }

            var handlers = await _incidentHandlerManagementService.ListIncidentHandlers();
            var handlerDoc = handlers.Where(h => h.Id == filterDoc?.Id).FirstOrDefault();

            string query = $@"
                customEvents
                | where timestamp > ago(60d) and name == ""IncidentActivitySnapshot""
                | where tostring(customDimensions.IncidentId) == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), 
                    RunMode = tostring(customDimensions.AgentAutonomyLevel), InstructionType = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = tostring(customDimensions.IncidentUpdatedOn),
                    HandledAt = tostring(customDimensions.IncidentHandledOn), HandlerCreatedAt = tostring(customDimensions.ResponsePlanCreatedOn), HandlerUpdatedAt = tostring(customDimensions.ResponsePlanUpdatedOn)
                | summarize arg_max(UpdatedAt, HandlerId, RunMode, InstructionType, HandledAt, HandlerCreatedAt, HandlerUpdatedAt) by IncidentId
                | project IncidentId, HandlerId, RunMode, InstructionType, HandledAt, HandlerCreatedAt, HandlerUpdatedAt
                | top 1 by IncidentId";

            var dataTable = await Query(query);
            DataRow? results = null;
            string handlerId = filterDoc?.Id ?? handlerDoc?.Id ?? string.Empty;
            DateTime? handlerCreatedOn = filterDoc?.CreatedAt;
            DateTime? handlerUpdatedOn = filterDoc?.UpdatedAt;
            string runMode = !string.IsNullOrWhiteSpace(filterDoc?.AgentMode) ? filterDoc.AgentMode : "review";
            string instructionType = !string.IsNullOrWhiteSpace(handlerDoc?.CustomInstructions) ? "Custom" : "Default";

            if (dataTable != null && dataTable.Rows.Count > 0)
            {
                results = dataTable.Rows[0];
            }

            var data = new IncidentAIData
            {
                HandlerId = !string.IsNullOrWhiteSpace(handlerId) ? handlerId : results?["HandlerId"]?.ToString() ?? "no-filter-found",
                IncidentId = incidentDoc.Id,
                IncidentTitle = incidentDoc.Title,
                HandlerCreatedAt = (DateTime)(handlerCreatedOn != null ? handlerCreatedOn : DateTime.TryParse(results?["HandlerCreatedAt"]?.ToString(), out DateTime handlerCreatedAt) ? handlerCreatedAt : DateTime.UtcNow),
                IncidentCreatedAt = incidentDoc.CreatedAt,
                HandlerUpdatedAt = (DateTime)(handlerUpdatedOn != null ? handlerUpdatedOn : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ? handlerUpdatedAt : DateTime.UtcNow),
                IncidentUpdatedAt = incidentDoc.UpdatedAt,
                IncidentHandledAt = DateTime.TryParse(results?["HandledAt"]?.ToString(), out DateTime incidentHandledTime) ?
                    (incidentHandledTime <= DateTime.MinValue.AddDays(1) ? new DateTime(Math.Max(incidentDoc.CreatedAt.Ticks, handlerCreatedOn?.Ticks ?? 0)) : incidentHandledTime) : handlerCreatedOn ?? DateTime.UtcNow,
                MitigatedAt = IncidentMitigatedAt(incidentDoc),
                Status = StatusMatching(incidentDoc.Status.ToLower()).ToLower(),
                Priority = incidentDoc.Priority,
                IsMitigatedByAgent = IsMitigatedByAgent(incidentDoc),
                IsAssistedByAgent = incidentDoc.IsAssistedByAgent,
                RootCause = incidentDoc.AIRootCause,
                RootCauseDescription = incidentDoc.RootCauseDescription,
                Summary = incidentDoc.GeneralSummary,
                ImpactedService = incidentDoc.ImpactedServiceName,
                RunMode = !string.IsNullOrWhiteSpace(results?["RunMode"]?.ToString()) ? results?["RunMode"]?.ToString()! : runMode,
                InstructionType = !string.IsNullOrWhiteSpace(results?["InstructionType"]?.ToString()) ? results?["InstructionType"]?.ToString()! : instructionType,
                IncidentPlatform = GetIncidentPlatform()
            };

            Ingest(data);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    public override async Task<PagerDutyIncidentDocument> AnalyzeIncident(PagerDutyIncidentDocument incidentDocument, PagerDutyIncident incident, PagerDutyIncidentFilterDocument? filterDocument)
    {
        string filterId;
        if (filterDocument == null)
        {
            filterId = await FetchFilterFromIncident(incidentDocument);
        }
        else
        {
            filterId = filterDocument.Id;
        }

        var rootCauseResponse = await GetRootCauseCategory(filterId, incident);
        var generalSummary = await GetGeneralSummary(incident);

        // Extract both category and description for backwards compatibility and enhanced analysis
        incidentDocument.AIRootCause = rootCauseResponse.RootCause;
        incidentDocument.RootCauseDescription = rootCauseResponse.Description;
        incidentDocument.GeneralSummary = generalSummary;
        
        return incidentDocument;
    }

    protected override bool IsMitigatedByAgent(PagerDutyIncidentDocument pdIncident)
    {
        bool isMitigatedByAgent = false;
        string status;

        status = pdIncident.Status.ToLower();
        isMitigatedByAgent = (status == "resolved" || status == "closed") && (pdIncident.Tags?.Contains("SREAgent_Mitigated") ?? false);
        
        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(PagerDutyIncidentDocument pdIncident)
    {
        DateTime? mitigatedAt = null;
        mitigatedAt = pdIncident.ResolvedAt;
        return mitigatedAt;
    }

    private async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, PagerDutyIncident incident, CancellationToken cancellationToken = default)
    {
        var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.PagerDuty));
        var existingRootCauses = filterRootCauseDocument?.RootCauses ?? new List<RootCauseCategory>();

        var aiRootCauseResponse = await GetAIRootCause(incident, existingRootCauses);
        var rootCauseCategory = new RootCauseCategory(aiRootCauseResponse.RootCause, aiRootCauseResponse.Description);

        var updatedDoc = new PagerDutyIncidentFilterAIRootCauseDocument();

        if (filterRootCauseDocument == null)
        {
            updatedDoc = new PagerDutyIncidentFilterAIRootCauseDocument
            {
                Id = string.IsNullOrWhiteSpace(filterId) ? "No-filterid-found" : filterId,
                FilterId = filterId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RootCauses = new List<RootCauseCategory> { rootCauseCategory }
            };

            updatedDoc = await container.CreateItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
        }
        else
        {
            var updatedRootCauses = new List<RootCauseCategory>(existingRootCauses);
            
            // Check if this root cause category already exists (case-insensitive comparison)
            if (!updatedRootCauses.Any(x => string.Equals(x.Category, rootCauseCategory.Category, StringComparison.OrdinalIgnoreCase)))
            {
                updatedRootCauses.Add(rootCauseCategory);
            }

            updatedDoc = filterRootCauseDocument with
            {
                UpdatedAt = DateTime.UtcNow,
                RootCauses = updatedRootCauses
            };

            updatedDoc = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
        }

        return aiRootCauseResponse;
    }

    private async Task<AIRootCauseResponse> GetAIRootCause(PagerDutyIncident incident, List<RootCauseCategory> existingRootCauses)
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

        var (response, result) = await _client.GetResponseAsync(messages, typeof(AIRootCauseResponse), options);
        
        if (result is AIRootCauseResponse rootCauseResponse)
        {
            return rootCauseResponse;
        }
        
        _logger.LogInternalWarning("Failed to get structured response, result was null or wrong type");
        return new AIRootCauseResponse { RootCause = "Unknown", Description = "Unable to categorize incident" };
    }

    private async Task<string> GetGeneralSummary(PagerDutyIncident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    private async Task<string> IncidentOverview(PagerDutyIncident incident)
    {
        // may need to use pagerDutyService to get most recent notes
        PagerDutyIncidentDocument? existingIncidentDocument = await _incidentManagementService.GetIncidentDetails(incident.IncidentId);

        var notes = existingIncidentDocument?.Notes;
        var notesContent = notes?.Select(n => n.Content).ToList();

        return $@"Title: {incident.Title}\n
        Description: {incident.Description}\n
        Details: {incident.Body?.Details ?? "N/A"}\n
        Notes: {JsonConvert.SerializeObject(notesContent)}\n
        Channel Summary: {incident.FirstTriggerLogEntry.Channel!.Summary}\n
        Channel Details: {incident.FirstTriggerLogEntry.Channel!.Details}";
    }

    private async Task<string> GetAIResponse(string prompt, PagerDutyIncident incident)
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

        var reply = await _client.GetResponseAsync(messages, options);
        return reply.Text;
    }

    private async Task<PagerDutyIncidentFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<PagerDutyIncidentFilterAIRootCauseDocument> response = await container.ReadItemAsync<PagerDutyIncidentFilterAIRootCauseDocument>(
                !string.IsNullOrWhiteSpace(id) ? id : "No-filterid-found",
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    private string StatusMatching(string status)
    {
        return status switch
        {
            "triggered" => "active",
            "acknowledged" => "active",
            "resolved" => "resolved",
            "closed" => "resolved",
            _ => "active"
        };
    }
}

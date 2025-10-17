using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Newtonsoft.Json;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Agent.Runtime.Services;

public class ServiceNowIncidentAnalysisService : IncidentAnalysisServiceBase<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload, ServiceNowIncident>
{
    private readonly Container container;
    private readonly ILogger<ServiceNowIncidentAnalysisService> _logger;
    public ServiceNowIncidentAnalysisService(
        IChatClient client,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterManagementService<ServiceNowIncidentFilterDocument, ServiceNowIncidentFilterDocumentPayload> incidentFilterManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        CustomerLogger appInsightsLogger,
        ILogger<ServiceNowIncidentAnalysisService> logger) : base(client, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _logger = logger;
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.ServiceNow.ToString();
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
        };
            _appInsightsLogger.LogCustomEvent("IncidentActivitySnapshot", payload);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[IncidentAnalysisService] Ingesting incident data into App Insights failed");
            throw;
        }
    }

    // need to overwrite method to ingest the string version of service now incident's numeric statuses
    public override async Task Ingest(ServiceNowIncidentDocument incidentDoc, ServiceNowIncidentFilterDocument? filterDoc = null)
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
                | where name == ""IncidentActivitySnapshot""
                | where tostring(customDimensions.IncidentId) == ""{incidentDoc.Id}""
                | extend IncidentId = tostring(customDimensions.IncidentId), HandlerId = tostring(customDimensions.ResponsePlanId), 
                    RunMode = tostring(customDimensions.AgentAutonomyLevel), InstructionType = tostring(customDimensions.ResponsePlanCustom), UpdatedAt = todatetime(customDimensions.IncidentUpdatedOn),
                    HandledAt = todatetime(customDimensions.IncidentHandledOn), HandlerCreatedAt = todatetime(customDimensions.ResponsePlanCreatedOn), HandlerUpdatedAt = todatetime(customDimensions.ResponsePlanUpdatedOn)
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
                HandlerUpdatedAt = (DateTime)(handlerUpdatedOn != null ? handlerUpdatedOn : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ?
                (handlerUpdatedAt <= DateTime.MinValue.AddDays(1) ? DateTime.UtcNow : handlerUpdatedAt) : DateTime.UtcNow),
                IncidentUpdatedAt = incidentDoc.UpdatedAt,
                IncidentHandledAt = DateTime.TryParse(results?["HandledAt"]?.ToString(), out DateTime incidentHandledTime) ? incidentHandledTime : handlerCreatedOn ?? DateTime.UtcNow,
                MitigatedAt = IncidentMitigatedAt(incidentDoc),
                Status = StatusMatching(incidentDoc.Status).ToLower(),
                Priority = incidentDoc.Priority,
                IsMitigatedByAgent = IsMitigatedByAgent(incidentDoc),
                IsAssistedByAgent = incidentDoc.IsAssistedByAgent,
                RootCause = incidentDoc.AIRootCause,
                RootCauseDescription = incidentDoc.RootCauseDescription,
                Summary = incidentDoc.GeneralSummary,
                ImpactedService = incidentDoc.ImpactedServiceName,
                RunMode = results?["RunMode"]?.ToString() ?? runMode,
                InstructionType = results?["InstructionType"]?.ToString() ?? instructionType,
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

    public override async Task<ServiceNowIncidentDocument> AnalyzeIncident(ServiceNowIncidentDocument incidentDocument, ServiceNowIncident incident, ServiceNowIncidentFilterDocument? filterDocument)
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

    protected override bool IsMitigatedByAgent(ServiceNowIncidentDocument serviceNowIncident)
    {
        bool isMitigatedByAgent = false;
        string status;
        
        status = serviceNowIncident.Status.ToString().ToLower();
        isMitigatedByAgent = (status == "6" || status == "7") && (serviceNowIncident.Tags?.Contains("SREAgent_Resolved") ?? false);
               
        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(ServiceNowIncidentDocument serviceNowIncident)
    {
        DateTime? mitigatedAt = null;
        if (serviceNowIncident.ResolvedAt > DateTime.MinValue.AddDays(1))
        {
            mitigatedAt = serviceNowIncident.ResolvedAt;
        }
        return mitigatedAt;
    }

    private async Task<string> IncidentOverview(ServiceNowIncident incident)
    {
        // may need to use serviceapiclient to get most recent notes
        // var latestDiscussionEntries = await serviceNowApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.IncidentSystemId);

        ServiceNowIncidentDocument? existingIncidentDocument = await _incidentManagementService.GetIncidentDetails(incident.Number);
        var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<ServiceNowDiscussionEntry>();

        var newNotes = existingDiscussionEntries.Select(entry => entry.Text).ToList();
        return $@"Title: {incident.Title}\n
        Description: {incident.Description}\n
        Impacted Service: {incident.ImpactedServiceName}\n
        Notes: {JsonConvert.SerializeObject(newNotes)}";
    }

    private async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, ServiceNowIncident incident, CancellationToken cancellationToken = default)
    {
        var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.ServiceNow));
        var existingRootCauses = filterRootCauseDocument?.RootCauses ?? new List<RootCauseCategory>();

        var aiRootCauseResponse = await GetAIRootCause(incident, existingRootCauses);
        var rootCauseCategory = new RootCauseCategory(aiRootCauseResponse.RootCause, aiRootCauseResponse.Description);

        var updatedDoc = new ServiceNowIncidentFilterAIRootCauseDocument();

        if (filterRootCauseDocument == null)
        {
            updatedDoc = new ServiceNowIncidentFilterAIRootCauseDocument
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


    private async Task<AIRootCauseResponse> GetAIRootCause(ServiceNowIncident incident, List<RootCauseCategory> existingRootCauses)
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

    private async Task<string> GetGeneralSummary(ServiceNowIncident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
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

    private async Task<ServiceNowIncidentFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<ServiceNowIncidentFilterAIRootCauseDocument> response = await container.ReadItemAsync<ServiceNowIncidentFilterAIRootCauseDocument>(
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

    private string StatusMatching(string numberStatus)
    {
        /*
            "1" or "new" => ServiceNowIncidentStatus.New,
            "2" or "active" or "in progress" or "work in progress" => ServiceNowIncidentStatus.InProgress,
            "3" or "awaiting problem" => ServiceNowIncidentStatus.AwaitingProblem,
            "4" or "awaiting user info" or "on hold" => ServiceNowIncidentStatus.OnHold,
            "5" or "awaiting evidence" => ServiceNowIncidentStatus.AwaitingEvidence,
            "6" or "resolved" => ServiceNowIncidentStatus.Resolved,
            "7" or "closed" => ServiceNowIncidentStatus.Closed,
            "8" or "cancelled" or "canceled" => ServiceNowIncidentStatus.Cancelled,
            _ => ServiceNowIncidentStatus.New
        */

        int status = int.TryParse(numberStatus, out int numStatus) ? numStatus : 0;

        switch (status)
        {
            case 0: return numberStatus; // if unable to parse the numberStatus, then it might be a string status, return as is
            case 6: return "Resolved";
            case 7: return "Closed";
            case 8: return "Cancelled";
            default: return "Active";
        };
    }

}

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
    public PagerDutyIncidentAnalysisService(
        IChatClientProvider chatClientProvider,
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
        ILogger<PagerDutyIncidentAnalysisService> logger) : base(chatClientProvider, cosmosClient, cosmosDbSettings, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
    {
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
            { "ResponsePlanCustom", data.IsHandlerCustom.ToString() },
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

    protected override async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, PagerDutyIncident incident, CancellationToken cancellationToken = default)
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

            _ = await _container.CreateItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
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

            _ = await _container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
        }

        return aiRootCauseResponse;
    }

    protected override async Task<string> IncidentOverview(PagerDutyIncident incident)
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

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.PagerDuty.ToString();
    }

    protected override IncidentAIData ToIncidentActivitySnapshot(PagerDutyIncidentFilterDocument? filterDoc, IncidentHandlerDocument? handlerDoc, PagerDutyIncidentDocument incidentDoc, DataRow? results)
    {

        string handlerId = filterDoc?.Id ?? handlerDoc?.Id ?? string.Empty;
        DateTime? handlerCreatedOn = filterDoc?.CreatedAt;
        DateTime? handlerUpdatedOn = filterDoc?.UpdatedAt;
        string runMode = !string.IsNullOrWhiteSpace(filterDoc?.AgentMode) ? filterDoc.AgentMode : "review";
        bool isHandlerCustom = !string.IsNullOrWhiteSpace(handlerDoc?.CustomInstructions) ? true : false;

        var snapshot = new IncidentAIData
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
            IsHandlerCustom = bool.TryParse(results?["IsHandlerCustom"]?.ToString(), out bool isCustom) ? isCustom : isHandlerCustom,
            IncidentPlatform = GetIncidentPlatform()
        };

        return snapshot;
    }

    private async Task<PagerDutyIncidentFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<PagerDutyIncidentFilterAIRootCauseDocument> response = await _container.ReadItemAsync<PagerDutyIncidentFilterAIRootCauseDocument>(
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

    private string StatusMatching(string incidentStatus)
    {
        string status = incidentStatus.ToLower();
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

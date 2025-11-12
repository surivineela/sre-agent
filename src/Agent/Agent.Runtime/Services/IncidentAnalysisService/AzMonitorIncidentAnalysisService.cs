// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;

public class AzMonitorIncidentAnalysisService : IncidentAnalysisServiceBase<AzMonitorAlertDocument, AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload, AlertItem>
{
    public AzMonitorIncidentAnalysisService(
        IChatClientProvider chatClientProvider,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<AzMonitorAlertDocument, AzMonitorIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentFilterManagementService<AzMonitorIncidentFilterDocument, AzMonitorIncidentFilterDocumentPayload> incidentFilterManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        CustomerLogger appInsightsLogger,
        ILogger<AzMonitorIncidentAnalysisService> logger)
        : base(chatClientProvider, cosmosClient, cosmosDbSettings, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
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

    protected override bool IsMitigatedByAgent(AzMonitorAlertDocument azMonitorIncident)
    {
        string status;
        status = azMonitorIncident.Status.ToLower();
        var isMitigatedByAgent = (status == "resolved" || status == "closed") && azMonitorIncident.Tags.Contains("SREAgent_Resolved");
        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(AzMonitorAlertDocument azMonitorIncident)
    {
        DateTime? mitigatedAt = null;
        mitigatedAt = azMonitorIncident.ResolvedAt;
        return mitigatedAt;
    }

    protected override async Task<string> IncidentOverview(AlertItem incident)
    {
        var overview = $@"Title: {incident.Name}\n
        Description: {incident.Properties.Essentials.Description}";
        return await Task.FromResult(overview);
    }

    protected override async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, AlertItem incident, CancellationToken cancellationToken = default)
    {
        try
        {
            var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.AzMonitor));
            var existingRootCauses = filterRootCauseDocument?.RootCauses ?? [];

            var aiRootCauseResponse = await GetAIRootCause(incident, existingRootCauses);
            var rootCauseCategory = new RootCauseCategory(aiRootCauseResponse.RootCause, aiRootCauseResponse.Description);

            var updatedDoc = new AzMonitorAlertFilterAIRootCauseDocument();

            if (filterRootCauseDocument == null)
            {
                updatedDoc = new AzMonitorAlertFilterAIRootCauseDocument
                {
                    Id = string.IsNullOrWhiteSpace(filterId) ? "No-filterid-found" : filterId,
                    FilterId = filterId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RootCauses = [rootCauseCategory]
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
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in GetRootCauseCategory: {Message}", ex.Message);
            throw;
        }
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.AzMonitor.ToString();
    }

    protected override IncidentAIData ToIncidentActivitySnapshot(AzMonitorIncidentFilterDocument? filterDoc, IncidentHandlerDocument? handlerDoc, AzMonitorAlertDocument incidentDoc, DataRow? results)
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
            HandlerUpdatedAt = (DateTime)(handlerUpdatedOn != null ? handlerUpdatedOn : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ?
            (handlerUpdatedAt <= DateTime.MinValue.AddDays(1) ? DateTime.UtcNow : handlerUpdatedAt) : DateTime.UtcNow),
            IncidentUpdatedAt = incidentDoc.LastModifiedTime,
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
            RunMode = !string.IsNullOrWhiteSpace(results?["RunMode"]?.ToString()) ? results["RunMode"].ToString()! : runMode,
            IsHandlerCustom = bool.TryParse(results?["IsHandlerCustom"]?.ToString(), out bool isCustom) ? isCustom : isHandlerCustom,
            IncidentPlatform = GetIncidentPlatform()
        };

        return snapshot;
    }

    private string StatusMatching(string incidentStatus)
    {
        var status = incidentStatus.ToLower();
        return status switch
        {
            "new" => "active",
            "acknowledged" => "active",
            "resolved" => "resolved",
            "closed" => "closed",
            _ => "active"
        };
    }

    private async Task<AzMonitorAlertFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<AzMonitorAlertFilterAIRootCauseDocument> response = await _container.ReadItemAsync<AzMonitorAlertFilterAIRootCauseDocument>(
                !string.IsNullOrWhiteSpace(id) ? id : "No-filterid-found",
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (Exception)
        {
            return default;
        }
    }
}

// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Data;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Runtime.Services;

public class IcmIncidentAnalysisService : IncidentAnalysisServiceBase<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, Incident>
{
    public IcmIncidentAnalysisService(
        IChatClientProvider chatClientProvider,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentManagementService,
        IIncidentHandlerManagementService incidentHandlerManagementService,
        IIncidentFilterManagementService<IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload> incidentFilterManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        CustomerLogger appInsightsLogger,
        ILogger<IcmIncidentAnalysisService> logger) : base(chatClientProvider, cosmosClient, cosmosDbSettings, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
    {
    }

    protected override bool IsMitigatedByAgent(IcmIncidentDocument icmIncident)
    {
        bool isMitigatedByAgent = false;
        string status;

        status = icmIncident.Status.ToString().ToLower();
        isMitigatedByAgent = (status == "mitigated" || status == "resolved") && ((icmIncident.MitigateData?.MitigatedBy.Contains("agent") ?? false) ||
            icmIncident.Tags.Contains("SREAgent_Mitigated"));

        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(IcmIncidentDocument icmIncident)
    {
        DateTime? mitigatedAt = null;
        mitigatedAt = icmIncident.MitigatedAt;
        return mitigatedAt;
    }

    protected override async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, Incident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.Icm));
            var existingRootCauses = filterRootCauseDocument?.RootCauses ?? new List<RootCauseCategory>();

            var aiRootCauseResponse = await GetAIRootCause(incident, existingRootCauses);
            var rootCauseCategory = new RootCauseCategory(aiRootCauseResponse.RootCause, aiRootCauseResponse.Description);

            var updatedDoc = new IcmIncidentFilterAIRootCauseDocument();

            if (filterRootCauseDocument == null)
            {
                updatedDoc = new IcmIncidentFilterAIRootCauseDocument
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
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in GetRootCauseCategory: {Message}", ex.Message);
            throw;
        }
    }

    protected override async Task<string> IncidentOverview(Incident incident)
    {
        IcmIncidentDocument? existingIncidentDocument = await _incidentManagementService.GetIncidentDetails(incident.Id.ToString());
        var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DescriptionEntry>();
        var notes = existingDiscussionEntries
                .Select(entry => new IncidentDiscussion(entry.DescriptionEntryId.ToString(), entry.Text, entry.ChangedBy, entry.ChangedBy, entry.Date))
                .ToList();

        return $@"Title: {incident.Title}\n
        Mitigation Steps: {incident.MitigateData?.MitigationSteps}\n
        Summary: {incident.Summary}\n
        Notes: {JsonConvert.SerializeObject(notes)}";
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.Icm.ToString();
    }

    protected override IncidentAIData ToIncidentActivitySnapshot(IcmIncidentFilterDocument? filterDoc, IncidentHandlerDocument? handlerDoc, IcmIncidentDocument incidentDoc, DataRow? results)
    {

        string handlerId = filterDoc?.Id ?? handlerDoc?.IncidentFilterId ?? string.Empty;
        DateTime? handlerCreatedOn = filterDoc?.CreatedAt;
        DateTime? handlerUpdatedOn = filterDoc?.UpdatedAt;
        string runMode = !string.IsNullOrWhiteSpace(filterDoc?.AgentMode) ? filterDoc.AgentMode : "review";
        bool isHandlerCustom = !string.IsNullOrWhiteSpace(handlerDoc?.CustomInstructions) ? true : false;
        DateTime? mitigatedAt = IncidentMitigatedAt(incidentDoc);

        var snapshot = new IncidentAIData
        {
            HandlerId = !string.IsNullOrWhiteSpace(handlerId) ? handlerId : results?["HandlerId"]?.ToString() ?? "no-filter-found",
            IncidentId = incidentDoc.Id,
            IncidentTitle = incidentDoc.Title,
            HandlerCreatedAt = (DateTime)(!IsMinDateTime(handlerCreatedOn) ? handlerCreatedOn! : DateTime.TryParse(results?["HandlerCreatedAt"]?.ToString(), out DateTime handlerCreatedAt) && !IsMinDateTime(handlerCreatedAt) ? handlerCreatedAt : DateTime.UtcNow),
            IncidentCreatedAt = incidentDoc.CreatedDate.UtcDateTime,
            HandlerUpdatedAt = (DateTime)(!IsMinDateTime(handlerUpdatedOn) ? handlerUpdatedOn! : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) && !IsMinDateTime(handlerUpdatedAt) ? handlerUpdatedAt : DateTime.UtcNow),
            IncidentUpdatedAt = incidentDoc.UpdatedAt,
            IncidentHandledAt = DateTime.TryParse(results?["HandledAt"]?.ToString(), out DateTime incidentHandledTime) ?
                (IsMinDateTime(incidentHandledTime) ? new DateTime(Math.Max(incidentDoc.UpdatedAt.Ticks, handlerCreatedOn?.Ticks ?? 0)) : incidentHandledTime) : DateTime.UtcNow,
            MitigatedAt = mitigatedAt,
            Status = incidentDoc.Status.ToString().ToLower(),
            Priority = incidentDoc.Priority,
            IsMitigatedByAgent = IsMitigatedByAgent(incidentDoc),
            IsAssistedByAgent = incidentDoc.IsAssistedByAgent,
            RootCause = incidentDoc.AIRootCause,
            RootCauseDescription = incidentDoc.RootCauseDescription,
            Summary = incidentDoc.GeneralSummary,
            ImpactedService = incidentDoc.ImpactedServiceName,
            RunMode = !string.IsNullOrWhiteSpace(results?["RunMode"]?.ToString()) ? results?["RunMode"]?.ToString()! : runMode,
            IsHandlerCustom = handlerDoc != null ? isHandlerCustom : bool.TryParse(results?["IsHandlerCustom"]?.ToString(), out bool isCustom) ? isCustom : false,
            IncidentPlatform = GetIncidentPlatform(),
            TimeTilMitigation = GetTimeTilMitigation(incidentDoc)
        };

        return snapshot;
    }

    protected override double? GetTimeTilMitigation(IcmIncidentDocument incidentDoc)
    {
        DateTime? mitigatedAt = IncidentMitigatedAt(incidentDoc);
        if (mitigatedAt == null)
        {
            return null;
        }

        double totalMinutes = ((DateTime)mitigatedAt).Subtract(incidentDoc.CreatedDate.UtcDateTime).TotalMinutes;
        return Math.Round(totalMinutes, 2, MidpointRounding.AwayFromZero);
    }

    private async Task<IcmIncidentFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<IcmIncidentFilterAIRootCauseDocument> response = await _container.ReadItemAsync<IcmIncidentFilterAIRootCauseDocument>(
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

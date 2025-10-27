using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Runtime.Services;

public class IcmIncidentAnalysisService : IncidentAnalysisServiceBase<IcmIncidentDocument, IcmIncidentFilterDocument, IcmIncidentFilterDocumentPayload, Incident>
{
    private readonly ILogger<IcmIncidentAnalysisService> _logger;
    private readonly Container container;
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
        ILogger<IcmIncidentAnalysisService> logger) : base(chatClientProvider, incidentManagementService, incidentFilterManagementService, incidentHandlerManagementService, repository, inboundCommunicationService, coreSettings, armHelper, appInsightsLogger, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _logger = logger;
    }

    protected override string GetIncidentPlatform()
    {
        return IncidentManagementType.Icm.ToString();
    }

    // need to overwrite method to ingest correct incident create time rather than incident document create time
    public override async Task Ingest(IcmIncidentDocument incidentDoc, IcmIncidentFilterDocument? filterDoc = null)
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
                IncidentCreatedAt = incidentDoc.CreatedDate.UtcDateTime,
                HandlerUpdatedAt = (DateTime)(handlerUpdatedOn != null ? handlerUpdatedOn : DateTime.TryParse(results?["HandlerUpdatedAt"]?.ToString(), out DateTime handlerUpdatedAt) ?
                (handlerUpdatedAt <= DateTime.MinValue.AddDays(1) ? DateTime.UtcNow : handlerUpdatedAt) : DateTime.UtcNow),
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

    public override async Task<IcmIncidentDocument> AnalyzeIncident(IcmIncidentDocument incidentDocument, Incident incident, IcmIncidentFilterDocument? filterDocument)
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

        // Extract just the category name for backwards compatibility
        incidentDocument.AIRootCause = rootCauseResponse.RootCause;
        incidentDocument.RootCauseDescription = rootCauseResponse.Description;
        incidentDocument.GeneralSummary = generalSummary;

        return incidentDocument;
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

    private async Task<AIRootCauseResponse> GetRootCauseCategory(string filterId, Incident incident, CancellationToken cancellationToken = default)
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
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in GetRootCauseCategory: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<AIRootCauseResponse> GetAIRootCause(Incident incident, List<RootCauseCategory> existingRootCauses)
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

    private async Task<string> GetGeneralSummary(Incident incident)
    {
        string summary = await GetAIResponse(_incidentGeneralSummaryPrompt, incident);
        return summary;
    }

    private async Task<string> IncidentOverview(Incident incident)
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

    private async Task<string> GetAIResponse(string prompt, Incident incident)
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

    private async Task<IcmIncidentFilterAIRootCauseDocument?> GetDocumentAsync(string id, string partitionKey)
    {
        try
        {
            ItemResponse<IcmIncidentFilterAIRootCauseDocument> response = await container.ReadItemAsync<IcmIncidentFilterAIRootCauseDocument>(
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

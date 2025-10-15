// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Runtime.Services;

public class IcmIncidentAnalysisService : IncidentAnalysisServiceBase<IcmIncidentDocument, IcmIncidentFilterDocumentPayload, Incident>
{
    private readonly ILogger<IcmIncidentAnalysisService> _logger;
    private readonly Container container;
    public IcmIncidentAnalysisService(
        IChatClient client,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<IcmIncidentDocument, IcmIncidentFilterDocumentPayload> incidentManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<IcmIncidentAnalysisService> logger) : base(client, incidentManagementService, repository, inboundCommunicationService, coreSettings, armHelper, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _logger = logger;
    }


    public override async Task<IcmIncidentDocument> AnalyzeIncident(IcmIncidentDocument incidentDocument, Incident incident)
    {
        var filterId = await FetchFilterFromIncident(incidentDocument);

        var rootCause = await GetRootCauseCategory(filterId, incident);
        var generalSummary = await GetGeneralSummary(incident);

        incidentDocument.AIRootCause = rootCause;
        incidentDocument.GeneralSummary = generalSummary;
        return incidentDocument;
    }

    protected override bool IsMitigatedByAgent(IcmIncidentDocument icmIncident)
    {
        bool isMitigatedByAgent = false;
        string status;

        status = icmIncident.Status;
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

    private async Task<string> GetRootCauseCategory(string filterId, Incident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.Icm));
            var rootCauses = filterRootCauseDocument != null ? filterRootCauseDocument.RootCauses : new List<string>();

            var incidentRootCause = await GetAIRootCause(incident, rootCauses);
            var updatedDoc = new IcmIncidentFilterAIRootCauseDocument();

            if (filterRootCauseDocument == null)
            {
                updatedDoc = new IcmIncidentFilterAIRootCauseDocument
                {
                    Id = string.IsNullOrWhiteSpace(filterId) ? "No-filterid-found" : filterId,
                    FilterId = filterId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    RootCauses = [incidentRootCause]
                };

                updatedDoc = await container.CreateItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
            }
            else
            {

                if (!rootCauses.Select(x => x.ToLower()).Contains(incidentRootCause.ToLower()))
                {
                    rootCauses.Add(incidentRootCause);
                }

                updatedDoc = filterRootCauseDocument with
                {
                    UpdatedAt = DateTime.UtcNow,
                    RootCauses = rootCauses
                };

                updatedDoc = await container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey), cancellationToken: cancellationToken);
            }

            return incidentRootCause;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in GetRootCauseCategory: {Message}", ex.Message);
            throw;
        }
    }

    private async Task<string> GetAIRootCause(Incident incident, List<string> rootCauses)
    {
        string rootCause = await GetAIRootCause(_incidentRootCausePrompt, incident, rootCauses);
        return rootCause;
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
            ResponseFormat = ChatResponseFormat.Text,
        };

        var reply = await _client.GetResponseAsync(messages, options);
        return reply.Text;
    }

    private async Task<string> GetAIRootCause(string prompt, Incident incident, List<string> rootCauses)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{await IncidentOverview(incident)}"),
            new(ChatRole.User, $"Here are the provided root causes: {JsonConvert.SerializeObject(rootCauses)}")
        };

        var options = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = ChatResponseFormat.Text,
        };

        var reply = await _client.GetResponseAsync(messages, options);
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

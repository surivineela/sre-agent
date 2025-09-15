using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PagerDutyIncident = Agent.Graph.Interfaces.PagerDutyIncident;


namespace Agent.Runtime.Services;
public class PagerDutyIncidentAnalysisService : IncidentAnalysisServiceBase<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>
{
    private readonly Container container;
    public PagerDutyIncidentAnalysisService(
        IChatClient client,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload> incidentManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<PagerDutyIncidentAnalysisService> logger) : base(client, incidentManagementService, repository, inboundCommunicationService, coreSettings, armHelper, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    }

    public override async Task<PagerDutyIncidentDocument> AnalyzeIncident(PagerDutyIncidentDocument incidentDocument, object incident)
    {
        var filterId = await FetchFilterFromIncident(incidentDocument);

        var pdIncident = (PagerDutyIncident)incident;
        var rootCause = await GetRootCauseCategory(filterId, pdIncident);
        var generalSummary = await GetGeneralSummary(pdIncident);

        incidentDocument.RootCause = rootCause;
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

    private async Task<string> GetRootCauseCategory(string filterId, PagerDutyIncident incident, CancellationToken cancellationToken = default)
    {
        var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.PagerDuty));
        var rootCauses = filterRootCauseDocument != null ? filterRootCauseDocument.RootCauses : new List<string>();

        var incidentRootCause = await GetAIRootCause(incident, rootCauses);
        var updatedDoc = new PagerDutyIncidentFilterAIRootCauseDocument();

        if (filterRootCauseDocument == null)
        {
            updatedDoc = new PagerDutyIncidentFilterAIRootCauseDocument
            {
                Id = string.IsNullOrWhiteSpace(filterId) ? "No-filterid-found" : filterId,
                FilterId = filterId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RootCauses = new List<string>() { incidentRootCause }
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

    private async Task<string> GetAIRootCause(PagerDutyIncident incident, List<string> rootCauses)
    {
        string rootCause = await GetAIRootCause(_incidentRootCausePrompt, incident, rootCauses);
        return rootCause;
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

    private async Task<string> GetAIRootCause(string prompt, PagerDutyIncident incident, List<string> rootCauses)
    {
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>
        {
            new(ChatRole.System, "You are an expert in incident analysis."),
            new(ChatRole.User, @$"{prompt}:\n\n{await IncidentOverview(incident)}"),
            new(ChatRole.User, $"Here are the provided root causes: {JsonConvert.SerializeObject(rootCauses)}")
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
}

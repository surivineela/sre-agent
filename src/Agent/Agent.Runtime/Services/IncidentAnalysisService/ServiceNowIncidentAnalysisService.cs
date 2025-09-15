using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.ICM;
using Agent.Core.Models.ServiceNow;
using Agent.Data;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Container = Microsoft.Azure.Cosmos.Container;

namespace Agent.Runtime.Services;

public class ServiceNowIncidentAnalysisService : IncidentAnalysisServiceBase<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload>
{
    private readonly Container container;
    public ServiceNowIncidentAnalysisService(
        IChatClient client,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IIncidentManagementService<ServiceNowIncidentDocument, ServiceNowIncidentFilterDocumentPayload> incidentManagementService,
        IThreadRepository repository,
        IAgentInboundCommunicationService inboundCommunicationService,
        CoreSettings coreSettings,
        ArmHelper armHelper,
        ILogger<ServiceNowIncidentAnalysisService> logger) : base(client, incidentManagementService, repository, inboundCommunicationService, coreSettings, armHelper, logger)
    {
        container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    }

    public override async Task<ServiceNowIncidentDocument> AnalyzeIncident(ServiceNowIncidentDocument incidentDocument, object incident)
    {
        var filterId = await FetchFilterFromIncident(incidentDocument);

        var pdIncident = (ServiceNowIncident)incident;
        var rootCause = await GetRootCauseCategory(filterId, pdIncident);
        var generalSummary = await GetGeneralSummary(pdIncident);

        incidentDocument.RootCause = rootCause;
        incidentDocument.GeneralSummary = generalSummary;
        return incidentDocument;
    }

    protected override bool IsMitigatedByAgent(ServiceNowIncidentDocument serviceNowIncident)
    {
        bool isMitigatedByAgent = false;
        string status;
        
        status = serviceNowIncident.Status.ToString().ToLower();
        isMitigatedByAgent = (status == "resolved" || status == "closed") && (serviceNowIncident.Tags?.Contains("SREAgent_Mitigated") ?? false);
               
        return isMitigatedByAgent;
    }

    protected override DateTime? IncidentMitigatedAt(ServiceNowIncidentDocument serviceNowIncident)
    {
        DateTime? mitigatedAt = null;
        mitigatedAt = serviceNowIncident.ResolvedAt;
        return mitigatedAt;
    }

    private async Task<string> IncidentOverview(ServiceNowIncident incident)
    {
        // may need to use serviceapiclient to get most recent notes
        // var latestDiscussionEntries = await serviceNowApiClient.GetIncidentDiscussionEntriesAsync(incidentDocument.IncidentSystemId);

        ServiceNowIncidentDocument? existingIncidentDocument = await _incidentManagementService.GetIncidentDetails(incident.Number);
        var existingDiscussionEntries = existingIncidentDocument != null ? existingIncidentDocument.DiscussionEntries : new List<DiscussionEntry>();

        var newNotes = existingDiscussionEntries.Select(entry => entry.Text).ToList();
        return $@"Title: {incident.Title}\n
        Description: {incident.Description}\n
        Impacted Service: {incident.ImpactedServiceName}\n
        Notes: {JsonConvert.SerializeObject(newNotes)}";
    }

    private async Task<string> GetRootCauseCategory(string filterId, ServiceNowIncident incident, CancellationToken cancellationToken = default)
    {
        var filterRootCauseDocument = await GetDocumentAsync(filterId, IncidentFilterAIRootCauseUtilities.GetDocumentType(IncidentManagementType.PagerDuty));
        var rootCauses = filterRootCauseDocument != null ? filterRootCauseDocument.RootCauses : new List<string>();

        var incidentRootCause = await GetAIRootCause(incident, rootCauses);
        var updatedDoc = new ServiceNowIncidentFilterAIRootCauseDocument();

        if (filterRootCauseDocument == null)
        {
            updatedDoc = new ServiceNowIncidentFilterAIRootCauseDocument
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


    private async Task<string> GetAIRootCause(ServiceNowIncident incident, List<string> rootCauses)
    {
        string rootCause = await GetAIRootCause(_incidentRootCausePrompt, incident, rootCauses);
        return rootCause;
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

    private async Task<string> GetAIRootCause(string prompt, ServiceNowIncident incident, List<string> rootCauses)
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
}

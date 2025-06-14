// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Services;

namespace Agent.Runtime.SubAgents.PagerDutyAgent;

public class PagerDutyScanner(ILogger<PagerDutyScanner> logger,
                              //   IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
                              IPagerDutyService pagerDutyService,
                              CosmosClient cosmosClient,
                              CosmosDBSettings cosmosDbSettings,
                              IChatClient chatClient,
                              IGraphDatabaseClient graphDbClient,
                              IncidentManagementSettings incidentManagementSettings,
                              IIncidentHandlingService incidentHandlingService,
                              IAgentInboundCommunicationService agentInboundCommunicationService):IIncidentScanner
{
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 10;
    private readonly static TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        if (incidentManagementSettings is null || incidentManagementSettings.Type != IncidentManagementType.PagerDuty)
        {
            logger.LogInternalInformation("PagerDuty is not configured. Skipping scanning.");
            return;
        }

        if (string.IsNullOrEmpty(incidentManagementSettings.ConnectionKey))
        {
            logger.LogInternalWarning("PagerDuty API key is not configured. Skipping scanning.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await ScannAllIncidentsAsync(cancellationToken);

            await Task.Delay(ScanInterval, cancellationToken);
        }
    }

    private async Task ScannAllIncidentsAsync(CancellationToken cancellationToken)
    {
        uint page = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInternalInformation("Cancellation requested, stopping the scanner.");
                return;
            }
            uint offset = page * PageSize;
            try
            {
                logger.LogInternalInformation("Scanning PagerDuty incidents, page {page}", page);
                var response = await pagerDutyService.GetIncidentsAsync(limit: PageSize, offset: offset);
                if (response is null || response.Incidents.Count == 0)
                {
                    logger.LogInternalInformation("No more incidents to process, stopping the scanner.");
                    return;
                }

                foreach (var incident in response.Incidents)
                {
                    var incidentDocument = await GetDocumentAsync<PagerDutyIncidentDocument>(incident.IncidentId, incident.IncidentId);

                    incidentDocument = await UpsertIncidentDocumentIfNeededAsync(incidentDocument, incident, cancellationToken);

                    var realtedResourceIds = await UpdateResourceGraph(incidentDocument, incident);

                    await NotifyUserAsync(incidentDocument, realtedResourceIds);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Error scanning PagerDuty incidents");
            }

            page++;
        }
    }

    private async Task NotifyUserAsync(PagerDutyIncidentDocument incidentDocument, List<string> relatedResourceIds)
    {
        try
        {
            if (incidentDocument is null)
            {
                logger.LogInternalWarning("Incident document is null, skipping notification.");
                return;
            }

            if (incidentDocument.Status == "resolved")
            {
                logger.LogInternalInformation("Incident {incidentId} is resolved, skipping notification.", incidentDocument.Id);
                return;
            }

            var threadDocument = await GetIncidentThread(incidentDocument.Id);
            if (threadDocument is null)
            {
                logger.LogInternalInformation("Thread doesn't exist for incident {incidentId}, skipping notification", incidentDocument.Id);
                var response = await incidentHandlingService.HandleIncidentAsync(new IncidentHandlingRequestModel()
                {
                    IncidentId = incidentDocument.Id,
                    Title = incidentDocument.Title,
                    Description = incidentDocument.Description,
                    Severity = incidentDocument.Priority
                });
            }
            else
            {
                logger.LogInternalInformation("Thread already exists for incident {incidentId}, checking whether it needs to be updated", incidentDocument.Id);
                // todo
                var iterator = container.GetItemLinqQueryable<MessageDocument>()
                    .Where(doc => doc.DocumentType == "Message" && doc.ThreadId == threadDocument.Id)
                    .Where(doc => doc.IncidentDiscussionId != null)
                    .Select(doc => doc.IncidentDiscussionId!)
                    .ToFeedIterator();
                // .ToHashSet();
                var existingNotesIds = new HashSet<string>();
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    foreach (var item in response)
                    {
                        existingNotesIds.Add(item);
                    }
                }
                logger.LogInternalInformation("Found {existingNotesCount} existing notes for incident {incidentId}", existingNotesIds.Count, incidentDocument.Id);

                var newNotes = incidentDocument.Notes
                    .Where(note => !existingNotesIds.Contains(note.Id))
                    .OrderBy(note => note.CreatedAt)
                    .Select(note => new IncidentDiscussion(note.Id, note.Content, note.CreatedBy?.Id ?? "Unknown", note.CreatedBy?.Name ?? "Unknown", note.CreatedAt))
                    .ToList();

                if (newNotes.Count > 0)
                {
                    logger.LogInternalInformation("Found {newNotesCount} new notes for incident {incidentId}", newNotes.Count, incidentDocument.Id);
                    await agentInboundCommunicationService.AddNewDiscussionsToIncidentThread(Guid.Parse(threadDocument.Id), newNotes);
                    logger.LogInternalInformation("Added {newNotesCount} new notes to incident thread {threadId}", newNotes.Count, threadDocument.Id);
                }
                else
                {
                    logger.LogInternalInformation("No new notes found for incident {incidentId}", incidentDocument.Id);
                }

            }

        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error notifying user about incident {incidentId}", incidentDocument.Id);
        }

    }

    private async Task<ThreadDocument?> GetIncidentThread(string incidentId)
    {
        var threads = container.GetItemLinqQueryable<ThreadDocument>()
            .Where(doc => doc.DocumentType == "Thread" && doc.Source == ThreadSource.Incident)
            .Where(doc => (doc.IncidentSource != null && doc.IncidentSource.IncidentType == IncidentType.PagerDuty && doc.IncidentSource.IncidentId == incidentId) || (doc.IncidentId == incidentId))
            .OrderBy(doc => doc.CreatedTimestamp)
            .ToFeedIterator();

        if (threads.HasMoreResults)
        {
            var response = await threads.ReadNextAsync();
            if (response.Count == 1)
            {
                return response.FirstOrDefault();
            }
            else if (response.Count > 1)
            {
                logger.LogInternalWarning("Multiple threads({threadIds}) found for incident {incidentId}, returning the first one.", string.Join(',', response.Select(t => t.Id)), incidentId);
                return response.FirstOrDefault();
            }
        }
        return null;
    }

    private async Task<PagerDutyIncidentDocument> UpsertIncidentDocumentIfNeededAsync(PagerDutyIncidentDocument incidentDocument, Graph.Interfaces.PagerDutyIncident incident, CancellationToken cancellationToken = default)
    {
        try
        {
            var latestDetails = await pagerDutyService.GetLatestIncidentDetails(incident.IncidentId);
            bool needsUpsert = false;
            // TODO: check latest title
            if (incidentDocument is null)
            {
                needsUpsert = true;
                logger.LogInternalInformation("Creating new incident document by id {incidentId}", incident.IncidentId);
                incidentDocument = new PagerDutyIncidentDocument(
                    Id: incident.IncidentId,
                    HtmlUrl: incident.HtmlUrl,
                    CreatedAt: incident.CreatedAt,
                    Status: incident.Status,
                    IncidentType: incident.IncidentType?.Name,
                    ImpactedServiceId: incident.ImpactedService?.Id ?? "Not set",
                    ImpactedServiceName: incident.ImpactedService?.Summary ?? "Not set",
                    Priority: incident.Priority?.Summary ?? "Not set",
                    Urgency: incident.Urgency ?? "Not set")
                {
                    Title = incident.Title,
                    // Well done PagerDuty. Took me hours to figure out where to find the real description.
                    Description = incident.FirstTriggerLogEntry.Channel?.Details ?? incident.Description,
                    UpdatedAt = DateTime.UtcNow
                };

                if (latestDetails is not null)
                {
                    incidentDocument.Notes = latestDetails.Notes;

                    if (!string.IsNullOrEmpty(latestDetails.LatestDescription) && incidentDocument.Description != latestDetails.LatestDescription)
                    {
                        incidentDocument.Description = latestDetails.LatestDescription;
                    }

                    if (!string.IsNullOrEmpty(latestDetails.LatestTitle) && incidentDocument.Title != latestDetails.LatestTitle)
                    {
                        incidentDocument.Title = latestDetails.LatestTitle;
                    }
                }

                // var titleEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Title, cancellationToken: cancellationToken);
                // TODO: try to avoid this copy
                // incidentDocument.TitleVector = titleEmbedding.Vector.ToArray();
                // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();
            }
            else
            {
                logger.LogInternalInformation("Updating existing incident document by id {incidentId}", incident.IncidentId);
                if (latestDetails is not null)
                {
                    if (!string.IsNullOrEmpty(latestDetails.LatestDescription) && incidentDocument.Description != latestDetails.LatestDescription)
                    {
                        incidentDocument.Description = latestDetails.LatestDescription;
                        needsUpsert = true;
                    }

                    if (!string.IsNullOrEmpty(latestDetails.LatestTitle) && incidentDocument.Title != latestDetails.LatestTitle)
                    {
                        incidentDocument.Title = latestDetails.LatestTitle;
                        needsUpsert = true;
                    }
                    if (incidentDocument.Notes.Count < latestDetails.Notes.Count)
                    {
                        incidentDocument.Notes = latestDetails.Notes;
                        needsUpsert = true;
                    }

                }
                // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                // TODO: try to avoid this copy
                // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();
            }

            // todo: maybe use patch instead of upsert for updating existing incidents.
            if (needsUpsert)
            {
                await container.UpsertItemAsync(incidentDocument, new PartitionKey(incident.IncidentId), cancellationToken: cancellationToken);
                logger.LogInternalInformation("Upserted incident document for PagerDuty incident {incidentId}", incident.IncidentId);
            }
            else
            {
                logger.LogInternalInformation("No changes detected for PagerDuty incident {incidentId}", incident.IncidentId);
            }
            return incidentDocument;

        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error upserting incident document for PagerDuty incident {incidentId}", incident.IncidentId);
        }
        return incidentDocument;
    }

    private async Task<List<string>> UpdateResourceGraph(PagerDutyIncidentDocument incidentDocument, Graph.Interfaces.PagerDutyIncident incident)
    {
        if (incidentDocument is null)
        {
            logger.LogInternalWarning("Incident document is null, skipping resource graph update.");
            return [];
        }

        try
        {
            var incidentNode = new PagerDutyIncidentNode
            {
                IncidentId = incident.IncidentId
            };
            var result = await graphDbClient.AddOrUpdateNodeAsync(incidentNode);
            logger.LogInternalInformation("Upserted incident node for {incidentId}", incident.IncidentId);

            if (!string.IsNullOrEmpty(incidentDocument.Description))
            {
                var relatedResourceIds = await GetRelatedResourceIdsAsync(incidentDocument.Description);
                logger.LogInternalInformation("Related resource ids to incident {incidentId}: {relatedResourceIds}", incident.IncidentId, string.Join(", ", relatedResourceIds));

                foreach (var resourceId in relatedResourceIds)
                {
                    if (string.IsNullOrEmpty(resourceId))
                    {
                        logger.LogInternalWarning("Related resource id is null or empty for incident {incidentId}", incident.IncidentId);
                        continue;
                    }
                    var nodeId = await graphDbClient.GetNodeId(resourceId);
                    if (string.IsNullOrEmpty(nodeId))
                    {
                        logger.LogInternalWarning("{resourceId} related to incident {incidentId} doesn't exist in knowledge graph", resourceId, incident.IncidentId);
                        continue;
                    }
                    var edge = new RelatedToIncidentEdge
                    {
                        SourceNodeId = nodeId,
                        TargetNodeId = incidentNode.GetNodeId(),
                    };
                    await graphDbClient.AddOrUpdateEdgeAsync(edge);
                    logger.LogInternalInformation("Added RelatedToIncidentEdge from {resourceId} to {incidentId}", resourceId, incident.IncidentId);
                }
                return relatedResourceIds;
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error updating resource graph for incident {incidentId}", incident.IncidentId);
        }
        return [];
    }

    private async Task<List<string>> GetRelatedResourceIdsAsync(string incidentDescription)
    {
        var systemPrompt = "You are pager duty incident and Azure resource expert. " +
            "You are given a pager duty incident description and you need to find all Azure resources id's that is related to the incident." +
            "Note that the resource id's are in the format of /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceProvider}/{resourceType}/{resourceName}." +
            "The resource id may not be given directly and you need to extract necessary information and assemble them to a resource id." +
            "Return the resource id's in a json array. If you cannot find any resource id's, return an empty json array.";
        var userPrompt = new ChatMessage(ChatRole.User, $"The incident description goes below:\n{incidentDescription}");
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            userPrompt
        };

        var options = new ChatOptions
        {
            Temperature = (float)0.2,
        };

        try
        {
            var response = await chatClient.GetResponseAsync<List<string>>(messages, options);
            return response.Result;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error getting related resource ids from chat client");
            return [];
        }
    }

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await container.ReadItemAsync<T>(
                id,
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

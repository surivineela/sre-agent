// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Runtime.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.PagerDutyAgent;

public class PagerDutyScanner(ILogger<PagerDutyScanner> logger,
                              //   IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
                              IPagerDutyService pagerDutyService,
                              CosmosClient cosmosClient,
                              CosmosDBSettings cosmosDbSettings,
                              IChatClient chatClient,
                              IGraphDatabaseClient graphDbClient)
{
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private const uint PageSize = 10;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PAGERDUTY_API_KEY")))
        {
            logger.LogInformation("PagerDuty API KEY not found, skipping scan.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            await ScannAllIncidentsAsync(cancellationToken);

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task ScannAllIncidentsAsync(CancellationToken cancellationToken)
    {
        uint page = 0;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Cancellation requested, stopping the scanner.");
                return;
            }
            uint offset = page * PageSize;
            try
            {
                logger.LogInformation("Scanning PagerDuty incidents, page {page}", page);
                var response = await pagerDutyService.GetIncidentsAsync(limit: PageSize, offset: offset);
                if (response is null || response.Incidents.Count == 0)
                {
                    logger.LogInformation("No more incidents to process, stopping the scanner.");
                    return;
                }

                foreach (var incident in response.Incidents)
                {
                    var incidentDocument = await GetDocumentAsync<PagerDutyIncidentDocument>(incident.IncidentId, incident.IncidentId);
                    var latestDescription = await pagerDutyService.GetLatestIncidentDescription(incident.IncidentId);
                    // TODO: check latest title
                    if (incidentDocument is null)
                    {
                        logger.LogInformation("Creating new incident document for {incidentId}", incident.IncidentId);
                        incidentDocument = new PagerDutyIncidentDocument(Id: incident.IncidentId, HtmlUrl: incident.HtmlUrl, CreatedAt: incident.CreatedAt, Status: incident.Status)
                        {
                            Title = incident.Title,
                            Description = incident.Description,
                            UpdatedAt = DateTime.UtcNow
                        };

                        if (!string.IsNullOrEmpty(latestDescription) && incident.Description != latestDescription)
                        {
                            incidentDocument.Description = latestDescription;
                        }

                        // var titleEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Title, cancellationToken: cancellationToken);
                        // TODO: try to avoid this copy
                        // incidentDocument.TitleVector = titleEmbedding.Vector.ToArray();
                        // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                        // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();
                    }
                    else
                    {
                        logger.LogInformation("Updating existing incident document for {incidentId}", incident.IncidentId);
                        if (!string.IsNullOrEmpty(latestDescription) && incidentDocument.Description != latestDescription)
                        {
                            incidentDocument.Description = latestDescription;
                            incidentDocument.UpdatedAt = DateTime.UtcNow;
                            // var descriptionEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(incident.Description, cancellationToken: cancellationToken);
                            // TODO: try to avoid this copy
                            // incidentDocument.DescriptionVector = descriptionEmbedding.Vector.ToArray();
                        }
                        // TODO: check latest title and update titleVector if needed
                    }

                    await container.UpsertItemAsync(incidentDocument, new PartitionKey(incident.IncidentId), cancellationToken: cancellationToken);
                    logger.LogInformation("Upserted incident document for {incidentId}", incident.IncidentId);

                    var incidentNode = new PagerDutyIncidentNode
                    {
                        IncidentId = incident.IncidentId
                    };
                    var result = await graphDbClient.AddOrUpdateNodeAsync(incidentNode);
                    logger.LogInformation("Upserted incident node for {incidentId}", incident.IncidentId);
                    
                    var relatedResourceIds = await GetRelatedResourceIdsAsync(incidentDocument.Description);
                    logger.LogInformation("Related resource ids to incident {incidentId}: {relatedResourceIds}", incident.IncidentId,string.Join(", ", relatedResourceIds));

                    foreach (var resourceId in relatedResourceIds)
                    {
                        if (string.IsNullOrEmpty(resourceId))
                        {
                            logger.LogWarning("Related resource id is null or empty for incident {incidentId}", incident.IncidentId);
                            continue;
                        }
                        var nodeId = await graphDbClient.GetNodeId(resourceId);
                        if (string.IsNullOrEmpty(nodeId))
                        {
                            logger.LogWarning("{resourceId} related to incident {incidentId} doesn't exist in knowledge graph", resourceId, incident.IncidentId);
                            continue;
                        }
                        var edge = new RelatedToIncidentEdge
                        {
                            SourceNodeId = nodeId,
                            TargetNodeId = incidentNode.GetNodeId(),
                        };
                        await graphDbClient.AddOrUpdateEdgeAsync(edge);
                        logger.LogInformation("Added RelatedToIncidentEdge from {resourceId} to {incidentId}", resourceId, incident.IncidentId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error scanning PagerDuty incidents");
            }

            page++;
        }
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
            var response = await chatClient.GetResponseAsync<List<string>>(messages, options, useJsonSchema: true);
            return response.Result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting related resource ids from chat client");
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
// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Agent.Plugins.Interface;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class PagerDutyIncidentPlugin(ILogger<PagerDutyIncidentPlugin> logger,
                            IGraphDatabaseClient graphDatabaseClient,
                            CosmosDBSettings cosmosDbSettings,
                            CosmosClient cosmosClient,
                            IPagerDutyService pagerDutyService) : IPagerDutyIncidentPlugin
{

    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, PagerDutyIncidentDocument.ContainerName);
    private readonly Container azMonitorContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);

    public async Task CloseAzureMonitorAlert(string alertId)
    {
        try
        {
            var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertId, alertId);

            if (alertDocument != null)
            {
                var updatedAlertDocument = alertDocument with
                {
                    Status = ServiceAlertState.Closed.ToString(),
                    UpdatedAt = DateTime.UtcNow
                };

                await azMonitorContainer.UpsertItemAsync(
                    updatedAlertDocument,
                    new PartitionKey(updatedAlertDocument.PartitionKey)
                );

                logger.LogInternalInformation($"Successfully closed AzMonitor alert document {alertId} for inactive thread.");
            }
            else
            {
                logger.LogInternalWarning($"Could not find AzMonitor alert document with ID {alertId}.");
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, $"Error closing AzMonitor alert for thread {alertId}.");
        }
    }

    public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentsAsync(string resourceId, uint maxResults = 5)
    {
        logger.LogInternalInformation("GetPagerDutyIncidentsAsync called with resourceId: {ResourceId}", resourceId);
        if (string.IsNullOrEmpty(resourceId))
        {
            logger.LogInternalWarning("ResourceId is null or empty.");
            return [];
        }
        var query = $"g.V().has('resourceId', '{resourceId}').has('isDeleted', false).out('RELATED_TO_INCIDENT').has('resourceType', '/incidents/pagerduty').has('isDeleted', false).has('incidentId').project('incidentId').by('incidentId')";
        logger.LogInternalInformation("Found {n} incidents for resourceId: {ResourceId}", query, resourceId);

        var result = await graphDatabaseClient.Query<Dictionary<string, object>>(query);
        List<string> incidentIds = result
            .Select(x => x["incidentId"]?.ToString() ?? string.Empty)
            .Where(incidentId => !string.IsNullOrEmpty(incidentId))
            .ToList();

        return await GetIncidentById(incidentIds, maxResults);
    }

    public async Task ResolvePagerDutyIncidentAsync(string incidentId)
    {
        await pagerDutyService.ResolveIncident(incidentId);
    }

    public async Task AcknowledgePagerDutyIncidentAsync(string incidentId)
    {
        await pagerDutyService.AcknowledgeIncident(incidentId);
    }

    public async Task<string> AddNoteToIncident(string incidentId, string note)
    {
        if (string.IsNullOrEmpty(incidentId) || string.IsNullOrEmpty(note))
        {
            var message = "AddNoteToIncident: IncidentId or note is null or empty.";
            logger.LogInternalWarning(message);
            return message;
        }
        try
        {
            await pagerDutyService.AddNoteToIncident(incidentId, note);
            var successResponse = $"AddNoteToIncident: Successfully added note to PagerDuty incident {incidentId}.";
            logger.LogInternalInformation(successResponse);
            return successResponse;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error adding note to PagerDuty incident {incidentId}: {ex.Message}";
            logger.LogInternalError(ex, "AddNoteToIncident: Error adding note to PagerDuty incident {IncidentId}.", incidentId);
            return errorMessage;
        }
    }

    private async Task<List<PagerDutyIncidentDocument>> GetIncidentById(List<string> incidentId, uint maxResults)
    {
        var iterator = container.GetItemLinqQueryable<PagerDutyIncidentDocument>()
            .Where(doc => doc.DocumentType == "PagerDutyIncident" && incidentId.Contains(doc.Id))
            .OrderByDescending(doc => doc.CreatedAt)
            .Take((int)maxResults)
            .ToFeedIterator();

        var incidents = new List<PagerDutyIncidentDocument>();
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                incidents.Add(item);
            }
        }

        return incidents;
    }

    private async Task<T> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await azMonitorContainer.ReadItemAsync<T>(
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

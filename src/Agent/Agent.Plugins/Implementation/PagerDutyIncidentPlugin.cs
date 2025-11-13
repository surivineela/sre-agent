// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Plugins.Interface;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Implementation;

public class PagerDutyIncidentPlugin(ILogger<PagerDutyIncidentPlugin> logger,
                            IGraphDatabaseClient graphDatabaseClient,
                            IHttpClientFactory httpClientFactory,
                            CosmosDBSettings cosmosDbSettings,
                            CosmosClient cosmosClient,
                            IPagerDutyService pagerDutyService,
                            IOptionsMonitor<IncidentManagementSettings> monitor) : IPagerDutyIncidentPlugin
{
    private readonly IncidentManagementSettings _settings = monitor.CurrentValue;
    private readonly Container container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, PagerDutyIncidentDocument.ContainerName);
    private readonly Container azMonitorContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(Constants.HttpClientForPagerDuty);

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

    public async Task<string> QueryPagerDutyIncidentChatAsync(string userQuery, string incidentId)
    {
        if (string.IsNullOrEmpty(incidentId))
        {
            throw new ArgumentException("Incident ID not found in the user query. Please include 'incident:INCIDENT_ID' in your query.");
        }

        if (_settings?.Type != IncidentManagementType.PagerDuty)
        {
            throw new InvalidOperationException("PagerDuty incident management is not configured.");
        }

        if (string.IsNullOrEmpty(_settings.ConnectionKey))
        {
            throw new InvalidOperationException("PagerDuty API key is not configured.");
        }

        var apiUrl = "https://api.pagerduty.com/advance/chat";

        string sessionId = Guid.NewGuid().ToString();
        string timestamp = DateTime.UtcNow.ToString("o");

        var payload = new
        {
            session_id = sessionId,
            timestamp = timestamp,
            message = userQuery,
            incident_id = incidentId
        };

        string jsonPayload = JsonSerializer.Serialize(payload);

        using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", $"token={_settings.ConnectionKey}");
            request.Headers.Add("X-EARLY-ACCESS", "gen_ai_api_early_access");
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"PagerDuty API error: {response.StatusCode} - {response.ReasonPhrase}\n{responseContent}");
            }

            var doc = JsonNode.Parse(responseContent);
            string agentMessage = doc?["message"]?.ToString() ?? "No message";
            return agentMessage;
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

    public async Task<List<PagerDutyIncidentDocument>> GetPagerDutyIncidentById(string incidentId)
    {
        logger.LogInternalInformation("GetPagerDutyIncidentById called with incidentId: {IncidentId}", incidentId);
        if (string.IsNullOrEmpty(incidentId))
        {
            logger.LogInternalWarning("IncidentId is null or empty.");
            return [];
        }

        return await GetIncidentById([incidentId], 1);
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

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class ServiceNowPlugin : IServiceNowPlugin
{
    private readonly IServiceNowAPIClient _serviceNowApiClient;
    private readonly ILogger<ServiceNowPlugin> _logger;
    private readonly Container _container;

    public ServiceNowPlugin(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IServiceNowAPIClient serviceNowApiClient,
        ILogger<ServiceNowPlugin> logger)
    {
        _serviceNowApiClient = serviceNowApiClient ?? throw new ArgumentNullException(nameof(serviceNowApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    }

    public async Task<ServiceNowIncident> GetServiceNowIncident(string incidentId)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(GetServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        return await _serviceNowApiClient.GetIncidentAsync(incidentId);
    }

    public async Task<string> PostServiceNowDiscussionEntry(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(PostServiceNowDiscussionEntry)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        
        var result = await _serviceNowApiClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
        return result;
    }

    public async Task<string> AcknowledgeServiceNowIncident(string incidentId)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(AcknowledgeServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        return await _serviceNowApiClient.AcknowledgeIncidentAsync(incidentId);
    }

    public async Task<string> ResolveServiceNowIncident(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(ResolveServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        try
        {
            // First add the resolution note
            await PostServiceNowDiscussionEntry(incidentId, $"Resolution: {discussionEntry}");
            
            // Then resolve the incident
            var result = await _serviceNowApiClient.ResolveIncidentAsync(incidentId, discussionEntry);

            try
            {
                var incident = await GetServiceNowIncident(incidentId);

                // Update the document in CosmosDB to have a SREAgent_Resolved tag
                var document = await GetDocumentAsync<ServiceNowIncidentDocument>(incident.Number, incident.Number);
                if (document != null)
                {
                    // do not update UpdatedAt value so that incident gets captured in next scanner iteration
                    var updatedDoc = document;
                    if (!updatedDoc.Tags.Contains("SREAgent_Resolved"))
                    {
                        updatedDoc.Tags.Add("SREAgent_Resolved");
                    }
                    updatedDoc.Status = "6";

                    _logger.LogInternalInformation("Upserting existing incident document for ServiceNow incident {incidentNumber}", incidentId);
                    _ = await _container.UpsertItemAsync(updatedDoc, new PartitionKey(updatedDoc.PartitionKey));
                }
                // if not within CosmosDB, add the record
                else
                {
                    var incidentDocument = new ServiceNowIncidentDocument(incident);
                    incidentDocument.Tags = new List<string>() { "SREAgent_Resolved" };
                    incidentDocument = await _container.CreateItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey));
                }
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error adding SREAgent_Resolved tag to ServiceNow incident document {incidentId}: {ex.Message}";
                _logger.LogInternalError(ex, errorMessage);
            }
            
            _logger.LogInternalInformation($"Successfully resolved ServiceNow incident document {incidentId}");
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error resolving ServiceNow incident {incidentId}: {ex.Message}";
            _logger.LogInternalError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(
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

// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Runtime.IncidentIndexing.Validators;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.IncidentIndexing.Services;

/// <summary>
/// Azure Monitor incident indexing configuration service.
/// Currently a stub implementation - returns "not yet supported" for save operations.
/// </summary>
public class AzMonitorIncidentIndexingConfigurationService :
    IIncidentIndexingConfigurationService<AzMonitorIncidentIndexingConfigurationDocument, AzMonitorIncidentIndexingConfigurationPayload>
{
    private readonly Container _container;
    private readonly ILogger<AzMonitorIncidentIndexingConfigurationService> _logger;

    private static readonly string DocumentId = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.AzMonitor);
    private static readonly string DocumentType = IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.AzMonitor);

    public AzMonitorIncidentIndexingConfigurationService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<AzMonitorIncidentIndexingConfigurationService> logger)
    {
        _container = cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AzMonitorIncidentIndexingConfigurationDocument?> GetConfigurationAsync()
    {
        _logger.LogInternalInformation("GetConfigurationAsync: Retrieving AzMonitor configuration");

        try
        {
            var response = await _container.ReadItemAsync<AzMonitorIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("GetConfigurationAsync: AzMonitor configuration found");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("GetConfigurationAsync: No AzMonitor configuration found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConfigurationAsync: Error retrieving AzMonitor configuration");
            throw;
        }
    }

    public Task<AzMonitorIncidentIndexingConfigurationDocument> CreateOrUpdateConfigurationAsync(
        AzMonitorIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        // Stub: AzMonitor not yet supported
        throw new NotSupportedException("Azure Monitor incident indexing configuration is not yet implemented.");
    }

    public async Task<bool> DeleteConfigurationAsync()
    {
        _logger.LogInternalInformation("DeleteConfigurationAsync: Deleting AzMonitor configuration");

        try
        {
            await _container.DeleteItemAsync<AzMonitorIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("DeleteConfigurationAsync: AzMonitor configuration deleted");
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("DeleteConfigurationAsync: AzMonitor configuration not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteConfigurationAsync: Error deleting AzMonitor configuration");
            throw;
        }
    }
}

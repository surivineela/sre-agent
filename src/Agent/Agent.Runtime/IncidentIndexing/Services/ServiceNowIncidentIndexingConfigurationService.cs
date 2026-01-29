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
/// ServiceNow incident indexing configuration service.
/// Currently a stub implementation - returns "not yet supported" for save operations.
/// </summary>
public class ServiceNowIncidentIndexingConfigurationService :
    IIncidentIndexingConfigurationService<ServiceNowIncidentIndexingConfigurationDocument, ServiceNowIncidentIndexingConfigurationPayload>
{
    private readonly Container _container;
    private readonly ILogger<ServiceNowIncidentIndexingConfigurationService> _logger;

    private static readonly string DocumentId = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.ServiceNow);
    private static readonly string DocumentType = IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.ServiceNow);

    public ServiceNowIncidentIndexingConfigurationService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<ServiceNowIncidentIndexingConfigurationService> logger)
    {
        _container = cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceNowIncidentIndexingConfigurationDocument?> GetConfigurationAsync()
    {
        _logger.LogInternalInformation("GetConfigurationAsync: Retrieving ServiceNow configuration");

        try
        {
            var response = await _container.ReadItemAsync<ServiceNowIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("GetConfigurationAsync: ServiceNow configuration found");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("GetConfigurationAsync: No ServiceNow configuration found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConfigurationAsync: Error retrieving ServiceNow configuration");
            throw;
        }
    }

    public Task<ServiceNowIncidentIndexingConfigurationDocument> CreateOrUpdateConfigurationAsync(
        ServiceNowIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        // Stub: ServiceNow not yet supported
        throw new NotSupportedException("ServiceNow incident indexing configuration is not yet implemented.");
    }

    public async Task<bool> DeleteConfigurationAsync()
    {
        _logger.LogInternalInformation("DeleteConfigurationAsync: Deleting ServiceNow configuration");

        try
        {
            await _container.DeleteItemAsync<ServiceNowIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("DeleteConfigurationAsync: ServiceNow configuration deleted");
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("DeleteConfigurationAsync: ServiceNow configuration not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteConfigurationAsync: Error deleting ServiceNow configuration");
            throw;
        }
    }
}

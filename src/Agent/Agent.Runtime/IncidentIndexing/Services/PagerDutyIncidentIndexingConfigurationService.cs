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
/// PagerDuty incident indexing configuration service.
/// Currently a stub implementation - returns "not yet supported" for save operations.
/// </summary>
public class PagerDutyIncidentIndexingConfigurationService :
    IIncidentIndexingConfigurationService<PagerDutyIncidentIndexingConfigurationDocument, PagerDutyIncidentIndexingConfigurationPayload>
{
    private readonly Container _container;
    private readonly ILogger<PagerDutyIncidentIndexingConfigurationService> _logger;

    private static readonly string DocumentId = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.PagerDuty);
    private static readonly string DocumentType = IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.PagerDuty);

    public PagerDutyIncidentIndexingConfigurationService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<PagerDutyIncidentIndexingConfigurationService> logger)
    {
        _container = cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagerDutyIncidentIndexingConfigurationDocument?> GetConfigurationAsync()
    {
        _logger.LogInternalInformation("GetConfigurationAsync: Retrieving PagerDuty configuration");

        try
        {
            var response = await _container.ReadItemAsync<PagerDutyIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("GetConfigurationAsync: PagerDuty configuration found");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("GetConfigurationAsync: No PagerDuty configuration found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConfigurationAsync: Error retrieving PagerDuty configuration");
            throw;
        }
    }

    public Task<PagerDutyIncidentIndexingConfigurationDocument> CreateOrUpdateConfigurationAsync(
        PagerDutyIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        // Stub: PagerDuty not yet supported
        throw new NotSupportedException("PagerDuty incident indexing configuration is not yet implemented.");
    }

    public async Task<bool> DeleteConfigurationAsync()
    {
        _logger.LogInternalInformation("DeleteConfigurationAsync: Deleting PagerDuty configuration");

        try
        {
            await _container.DeleteItemAsync<PagerDutyIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("DeleteConfigurationAsync: PagerDuty configuration deleted");
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("DeleteConfigurationAsync: PagerDuty configuration not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteConfigurationAsync: Error deleting PagerDuty configuration");
            throw;
        }
    }
}

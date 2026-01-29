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
/// ICM-specific incident indexing configuration service.
/// Manages ICM configuration documents in CosmosDB.
/// </summary>
public class IcmIncidentIndexingConfigurationService :
    IIncidentIndexingConfigurationService<IcmIncidentIndexingConfigurationDocument, IcmIncidentIndexingConfigurationPayload>
{
    private readonly Container _container;
    private readonly ILogger<IcmIncidentIndexingConfigurationService> _logger;

    private static readonly string DocumentId = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.Icm);
    private static readonly string DocumentType = IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.Icm);

    public IcmIncidentIndexingConfigurationService(
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        ILogger<IcmIncidentIndexingConfigurationService> logger)
    {
        _container = cosmosClient.GetContainer(
            cosmosDbSettings.Docs.Database,
            AgentDataConfiguration.ThreadContainerName);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IcmIncidentIndexingConfigurationDocument?> GetConfigurationAsync()
    {
        _logger.LogInternalInformation("GetConfigurationAsync: Retrieving ICM configuration");

        try
        {
            var response = await _container.ReadItemAsync<IcmIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("GetConfigurationAsync: ICM configuration found");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("GetConfigurationAsync: No ICM configuration found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "GetConfigurationAsync: Error retrieving ICM configuration");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IcmIncidentIndexingConfigurationDocument> CreateOrUpdateConfigurationAsync(
        IcmIncidentIndexingConfigurationPayload request,
        IncidentIndexingValidationResult validationResult)
    {
        _logger.LogInternalInformation(
            "SaveConfigurationAsync: Saving ICM configuration for teams: {TeamIds}",
            string.Join(", ", request.TeamIds));

        // Extract validated info
        var icmInfo = validationResult.ValidatedInfo as IcmValidatedInfo;
        var teamName = icmInfo?.TeamName;
        var tenantName = icmInfo?.TenantName;

        // Check if configuration already exists to preserve CreatedAt
        var existing = await GetConfigurationAsync();

        var document = new IcmIncidentIndexingConfigurationDocument
        {
            Id = DocumentId,
            TeamIds = request.TeamIds,
            MinSeverity = request.MinSeverity,
            MaxSeverity = request.MaxSeverity,
            IncidentTypes = request.IncidentTypes,
            LookbackDays = request.LookbackDays,
            MaxIncidents = request.MaxIncidents,
            TeamName = teamName,
            TenantName = tenantName,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            var response = await _container.UpsertItemAsync(
                document,
                new PartitionKey(document.PartitionKey));

            _logger.LogInternalInformation(
                "SaveConfigurationAsync: ICM configuration saved successfully for team {TeamName}",
                teamName);

            return response.Resource;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "SaveConfigurationAsync: Error saving ICM configuration for teams: {TeamIds}",
                string.Join(", ", request.TeamIds));
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConfigurationAsync()
    {
        _logger.LogInternalInformation("DeleteConfigurationAsync: Deleting ICM configuration");

        try
        {
            await _container.DeleteItemAsync<IcmIncidentIndexingConfigurationDocument>(
                DocumentId,
                new PartitionKey(DocumentType));

            _logger.LogInternalInformation("DeleteConfigurationAsync: ICM configuration deleted");
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning("DeleteConfigurationAsync: ICM configuration not found");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "DeleteConfigurationAsync: Error deleting ICM configuration");
            throw;
        }
    }
}

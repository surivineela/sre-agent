// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

/// <summary>
/// CosmosDB implementation of TSG connector repository.
/// PAT tokens are stored directly - Cosmos DB provides encryption at rest.
/// </summary>
public class CosmosDbTsgConnectorRepository : ITsgConnectorRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbTsgConnectorRepository> _logger;

    public CosmosDbTsgConnectorRepository(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbTsgConnectorRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _logger = logger;
    }

    private Container GetContainer()
    {
        return _cosmosClient.GetContainer(_databaseName, TsgConnectorDocument.ContainerName);
    }

    public async Task<TsgConnectorDocument> UpsertAsync(TsgConnectorDocument connector)
    {
        var container = GetContainer();

        // Ensure ID is set correctly
        connector.Id = TsgConnectorDocument.GetId(connector.Name);
        connector.UpdatedAt = DateTime.UtcNow;

        var response = await container.UpsertItemAsync(
            connector,
            new PartitionKey(TsgConnectorDocument.GetPartitionKey()));

        _logger.LogInternalInformation($"Successfully upserted TSG connector: {connector.Name}");
        return response.Resource;
    }

    public async Task<TsgConnectorDocument?> GetByNameAsync(string name)
    {
        try
        {
            var container = GetContainer();
            var id = TsgConnectorDocument.GetId(name);

            var response = await container.ReadItemAsync<TsgConnectorDocument>(
                id,
                new PartitionKey(TsgConnectorDocument.GetPartitionKey()));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalDebug($"TSG connector not found: {name}");
            return null;
        }
    }

    public async Task<IReadOnlyList<TsgConnectorDocument>> GetAllAsync()
    {
        var container = GetContainer();
        var query = container.GetItemQueryIterator<TsgConnectorDocument>(
            new QueryDefinition($"SELECT * FROM c WHERE c.documentType = @docType")
                .WithParameter("@docType", TsgConnectorDocument.DocumentTypeName));

        var results = new List<TsgConnectorDocument>();
        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<bool> DeleteAsync(string name)
    {
        try
        {
            var container = GetContainer();
            var id = TsgConnectorDocument.GetId(name);

            await container.DeleteItemAsync<TsgConnectorDocument>(
                id,
                new PartitionKey(TsgConnectorDocument.GetPartitionKey()));

            _logger.LogInternalInformation($"Successfully deleted TSG connector: {name}");
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalWarning($"TSG connector not found for deletion: {name}");
            return false;
        }
    }

    public async Task<string?> GetPatAsync(string name)
    {
        var connector = await GetByNameAsync(name);
        // PAT is stored directly - Cosmos DB encryption at rest handles security
        return connector?.Pat;
    }

    public async Task<TsgConnectorDocument?> UpdateStatusAsync(string name, ConnectorStatus status, string? errorMessage = null)
    {
        var connector = await GetByNameAsync(name);
        if (connector == null)
        {
            return null;
        }

        connector.Status = status;
        connector.ErrorMessage = errorMessage;
        connector.LastValidated = DateTime.UtcNow;
        connector.UpdatedAt = DateTime.UtcNow;

        return await UpsertAsync(connector);
    }

    public async Task<TsgConnectorDocument?> UpdateCloneStatusAsync(
        string name,
        CloneStatus cloneStatus,
        string? localPath = null,
        string? latestCommit = null,
        string? errorMessage = null,
        DateTime? lastSuccessfulSync = null)
    {
        var connector = await GetByNameAsync(name);
        if (connector == null)
        {
            return null;
        }

        connector.CloneStatus = cloneStatus;

        if (cloneStatus is CloneStatus.Cloning or CloneStatus.Syncing)
        {
            connector.CloneStartedAt = DateTime.UtcNow;
        }

        if (cloneStatus == CloneStatus.Ready)
        {
            connector.CloneCompletedAt = DateTime.UtcNow;
            // Use explicit lastSuccessfulSync if provided, otherwise use current time
            connector.LastSuccessfulSync = lastSuccessfulSync ?? DateTime.UtcNow;
            connector.ErrorMessage = null;
        }

        if (cloneStatus == CloneStatus.Failed)
        {
            connector.ErrorMessage = errorMessage;
        }

        if (!string.IsNullOrEmpty(localPath))
        {
            connector.LocalPath = localPath;
        }

        if (!string.IsNullOrEmpty(latestCommit))
        {
            connector.LatestCommit = latestCommit;
        }

        connector.UpdatedAt = DateTime.UtcNow;
        return await UpsertAsync(connector);
    }
}

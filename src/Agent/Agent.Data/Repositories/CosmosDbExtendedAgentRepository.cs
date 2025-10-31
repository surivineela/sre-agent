using System.Net;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Data.DataModels.Legacy;
using Agent.Data.Helpers;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;

namespace Agent.Data.Repositories;

public class CosmosDbExtendedAgentRepository : IExtendedAgentRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbExtendedAgentRepository> _logger;

    // A single container for all related documents, distinguished by 'documentType'.
    private const string ContainerName = "extendedagents";

    public CosmosDbExtendedAgentRepository(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbExtendedAgentRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _logger = logger;
    }

    #region Agent Operations

    public async Task<AgentDocumentModel> UpsertAgentAsync(AgentDocumentModel agent, string operationId)
    {
        var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(agent, new PartitionKey(agent.Name));
        _logger.LogInternalInformation("Successfully upserted agent document {AgentName}", agent.Name);
        return response.Resource;
    }

    public async Task<AgentDocumentModel?> GetAgentByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<AgentDocumentModel>(
                $"{name}",
                new PartitionKey(name));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {

            return null;
        }
    }

    public async Task<PaginatedList<AgentDocumentModel>> GetAgentsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);

            var queryAgentDocumentModelTask = Task.Run(async () =>
            {
                var query = container.GetItemLinqQueryable<AgentDocumentModel>()
                .Where(d => d.DocumentType == "ExtendedAgent" && d.Spec != null); // ensures only query new model

                if (!string.IsNullOrWhiteSpace(search))
                {
                    // Apply search filter to name and description fields
                    query = query.Where(d =>
                        d.Spec.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (d.Spec.HandoffDescription ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                using var iterator = query.Take(limit).ToFeedIterator();
                var results = new List<AgentDocumentModel>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response);
                }

                return results;
            });

            var queryAgentDocumentModelLegacyTask = Task.Run(async () =>
            {
                var query = container.GetItemLinqQueryable<AgentDocumentModelLegacy>()
                .Where(d => d.DocumentType == "ExtendedAgent" && d.Name != null); // ensures only query legacy model

                if (!string.IsNullOrWhiteSpace(search))
                {
                    // Apply search filter to name and description fields
                    query = query.Where(d =>
                        d.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (d.HandoffDescription ?? string.Empty).Contains(search, StringComparison.OrdinalIgnoreCase));
                }

                using var iterator = query.Take(limit).ToFeedIterator();
                var results = new List<AgentDocumentModel>();

                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    results.AddRange(response.Select(d => d.ToAgentDocumentModel()));
                }

                return results;
            });

            await Task.WhenAll(queryAgentDocumentModelLegacyTask, queryAgentDocumentModelTask);

            var results = new List<AgentDocumentModel>();
            results.AddRange(queryAgentDocumentModelTask.Result);
            results.AddRange(queryAgentDocumentModelLegacyTask.Result);

            _logger.LogInternalInformation("Retrieved {Count} agents with search '{Search}' (New: {NewCount}, Legacy: {LegacyCount})", results.Count, search ?? "none", queryAgentDocumentModelTask.Result.Count, queryAgentDocumentModelLegacyTask.Result.Count);
            return new PaginatedList<AgentDocumentModel>(results, results.Count, 1, results.Count);
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve agents with search '{Search}'", search ?? "none");
            throw;
        }
    }

    public async Task<bool> DeleteAgentAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);

            // Construct the item ID and partition key
            var itemId = $"{name}";
            var partitionKey = new PartitionKey(name);

            // Delete the item
            await container.DeleteItemAsync<object>(itemId, partitionKey);

            _logger.LogInternalInformation("Successfully deleted agent {AgentName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Agent {AgentName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete agent {AgentName}", name);
            throw;
        }
    }

    #endregion

    #region Tool Operations

    public async Task<ToolDocumentModel> UpsertToolAsync(ToolDocumentModel tool, string operationId)
    {
        var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(tool, new PartitionKey(tool.Name));
        _logger.LogInternalInformation("Successfully upserted tool document {ToolName}", tool.Name);
        return response.Resource;
    }

    public async Task<CommonPromptDocumentModel> UpsertCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(prompt, new PartitionKey(prompt.Name));
        _logger.LogInternalInformation("Successfully upserted common prompt document {PromptName}", prompt.Name);
        return response.Resource;
    }

    public async Task<CommonToolsListDocumentModel> UpsertCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(toolsList, new PartitionKey(toolsList.Name));
        _logger.LogInternalInformation("Successfully upserted common tools list document {ToolsListName}", toolsList.Name);
        return response.Resource;
    }
    public async Task<PlugInConfigDocumentModel> UpsertPluginConfigAsync(PlugInConfigDocumentModel config)
    {
        var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(config, new PartitionKey(config.Name));
        _logger.LogInternalInformation("Successfully upserted plugin config document {PluginConfigName}", config.Name);
        return response.Resource;
    }

    public async Task<ToolDocumentModel?> GetToolByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<ToolDocumentModel>(
                $"tool_{name}",
                new PartitionKey(name));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // ... (error handling)
            return null;
        }
    }

    public async Task<PaginatedList<ToolDocumentModel>> GetToolsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);

        var queryToolDocumentModelTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<ToolDocumentModel>()
                .Where(d => d.DocumentType == "ExtendedAgentTool" && d.Spec != null); // ensures only query new model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search) ||
                    d.Spec.Description.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<ToolDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        var queryToolDocumentModelLegacyTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<ToolDocumentModel>()
                .Where(d => d.DocumentType == "ExtendedAgentTool" && d.Name != null); // ensures only query legacy model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search) ||
                    d.Spec.Description.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<ToolDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        await Task.WhenAll(queryToolDocumentModelTask, queryToolDocumentModelLegacyTask);

        var results = new List<ToolDocumentModel>();
        results.AddRange(queryToolDocumentModelTask.Result);
        results.AddRange(queryToolDocumentModelLegacyTask.Result);

        _logger.LogInternalInformation("Retrieved {Count} tools with search '{Search}' (New: {NewCount}, Legacy: {LegacyCount})", results.Count, search ?? "none", queryToolDocumentModelTask.Result.Count, queryToolDocumentModelLegacyTask.Result.Count);
        return new PaginatedList<ToolDocumentModel>(results, results.Count, 1, results.Count);
    }


    public async Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);

        var queryCommonToolsListDocumentModelTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonToolsListDocumentModel>()
                .Where(d => d.DocumentType == "CommonToolsList" && d.Spec != null); // ensures only query new model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<CommonToolsListDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        var queryCommonToolsListDocumentModelLegacyTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonToolsListDocumentModel>()
                .Where(d => d.DocumentType == "CommonToolsList" && d.Name != null); // ensures only query legacy model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<CommonToolsListDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        await Task.WhenAll(queryCommonToolsListDocumentModelTask, queryCommonToolsListDocumentModelLegacyTask);

        var results = new List<CommonToolsListDocumentModel>();
        results.AddRange(queryCommonToolsListDocumentModelTask.Result);
        results.AddRange(queryCommonToolsListDocumentModelLegacyTask.Result);


        _logger.LogInternalInformation("Retrieved {Count} common tools lists with search '{Search}' (New: {NewCount}, Legacy: {LegacyCount})", results.Count, search ?? "none", queryCommonToolsListDocumentModelTask.Result.Count, queryCommonToolsListDocumentModelLegacyTask.Result.Count);
        return new PaginatedList<CommonToolsListDocumentModel>(results, results.Count, 1, results.Count);
    }

    public async Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);

        var queryCommonPromptDocumentModelTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonPromptDocumentModel>()
                .Where(d => d.DocumentType == "CommonPrompt" && d.Spec != null); // ensures only query new model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<CommonPromptDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        var queryCommonPromptDocumentModelLegacyTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonPromptDocumentModel>()
                .Where(d => d.DocumentType == "CommonPrompt" && d.Name != null); // ensures only query legacy model

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<CommonPromptDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            return results;
        });

        await Task.WhenAll(queryCommonPromptDocumentModelTask, queryCommonPromptDocumentModelLegacyTask);

        var results = new List<CommonPromptDocumentModel>();
        results.AddRange(queryCommonPromptDocumentModelTask.Result);
        results.AddRange(queryCommonPromptDocumentModelLegacyTask.Result);

        _logger.LogInternalInformation("Retrieved {Count} common prompts with search '{Search}' (New: {NewCount}, Legacy: {LegacyCount})", results.Count, search ?? "none", queryCommonPromptDocumentModelTask.Result.Count, queryCommonPromptDocumentModelLegacyTask.Result.Count);
        return new PaginatedList<CommonPromptDocumentModel>(results, results.Count, 1, results.Count);
    }

    public async Task<bool> DeleteToolAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ContainerName);
            // DeleteItemAsync does not require a generic type
            await container.DeleteItemAsync<object>( // Use <object> or a non-generic call if available
                $"tool_{name}",
                new PartitionKey(name));

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }


    #endregion

    #region Connector Operations
    public async Task<ConnectorDocumentModel> UpsertConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        try
        {

            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.UpsertItemAsync(connector, new PartitionKey(connector.PartitionKey));
            _logger.LogInternalInformation("Successfully upserted extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to upsert extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            throw;
        }
    }

    public async Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<ConnectorDocumentModel>(
                $"connector_{name}",
                new PartitionKey(name));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Extended agent connector {ConnectorName} not found", name);
            return null;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve extended agent connector {ConnectorName}", name);
            throw;
        }
    }

    public async Task<PaginatedList<ConnectorDocumentModel>> GetConnectorsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var query = container.GetItemLinqQueryable<ConnectorDocumentModel>()
                .Where(d => d.DocumentType == "ExtendedAgentConnector");
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Name.Contains(search) ||
                    d.Spec.Description.Contains(search));
            }
            query = query.Take(limit);
            using var iterator = query.ToFeedIterator();
            var results = new List<ConnectorDocumentModel>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }
            _logger.LogInternalInformation("Retrieved {Count} extended agent connectors with search '{Search}'", results.Count, search ?? "none");
            return new PaginatedList<ConnectorDocumentModel>(results, 50, 0, 50);
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve extended agent connectors with search '{Search}'", search ?? "none");
            throw;
        }
    }

    public async Task<bool> DeleteConnectorAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            await container.DeleteItemAsync<ConnectorDocumentModel>(
                $"connector_{name}",
                new PartitionKey(name));
            _logger.LogInternalInformation("Successfully deleted extended agent connector {ConnectorName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Extended agent connector {ConnectorName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete extended agent connector {ConnectorName}", name);
            throw;
        }
    }

    #endregion
}

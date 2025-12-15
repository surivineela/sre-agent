using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Agent.Data.DataModels.Legacy;
using Agent.Data.Json;
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
        var response = await container.UpsertItemAsync(agent, new PartitionKey(AgentDocumentModel.GetPartitionKey()));
        _logger.LogInternalInformation("Successfully upserted agent document {AgentName}", agent.Name);
        return response.Resource;
    }

    public async Task<AgentDocumentModel?> GetAgentByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<AgentDocumentModel>(
                AgentDocumentModel.GetId(name),
                new PartitionKey(AgentDocumentModel.GetPartitionKey()));

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
                .Where(d => d.DocumentType == AgentDocumentModel.DocumentTypeName && d.Spec != null); // ensures only query new model

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
                    results.AddRange(response);
                }

                return results;
            });

            var queryAgentDocumentModelLegacyTask = Task.Run(async () =>
            {
                var query = container.GetItemLinqQueryable<AgentDocumentModelLegacy>()
                .Where(d => d.DocumentType == AgentDocumentModel.DocumentTypeName && d.Name != null); // ensures only query legacy model

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
            var itemId = AgentDocumentModel.GetId(name);
            var partitionKey = new PartitionKey(AgentDocumentModel.GetPartitionKey());

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
        var response = await container.UpsertItemAsync(tool, new PartitionKey(ToolDocumentModel.GetPartitionKey()));
        _logger.LogInternalInformation("Successfully upserted tool document {ToolName}", tool.Name);
        return response.Resource;
    }

    public async Task<ToolDocumentModel?> GetToolByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<ToolDocumentModel>(
                ToolDocumentModel.GetId(name),
                new PartitionKey(ToolDocumentModel.GetPartitionKey()));

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
                .Where(d => d.DocumentType == ToolDocumentModel.DocumentTypeName && d.Spec != null); // ensures only query new model

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
                .Where(d => d.DocumentType == ToolDocumentModel.DocumentTypeName && d.Name != null); // ensures only query legacy model

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

    public async Task<bool> DeleteToolAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ContainerName);
            // DeleteItemAsync does not require a generic type
            await container.DeleteItemAsync<object>( // Use <object> or a non-generic call if available
                ToolDocumentModel.GetId(name),
                new PartitionKey(ToolDocumentModel.GetPartitionKey()));

            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    #endregion

    #region CommonPrompt Operations

    public async Task<CommonPromptDocumentModel> UpsertCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(prompt, new PartitionKey(CommonPromptDocumentModel.GetPartitionKey()));
        _logger.LogInternalInformation("Successfully upserted common prompt document {PromptName}", prompt.Name);
        return response.Resource;
    }

    public async Task<CommonPromptDocumentModel?> GetCommonPromptByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<CommonPromptDocumentModel>(
                CommonPromptDocumentModel.GetId(name),
                new PartitionKey(CommonPromptDocumentModel.GetPartitionKey()));

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);

        var queryCommonPromptDocumentModelTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonPromptDocumentModel>()
                .Where(d => d.DocumentType == CommonPromptDocumentModel.DocumentTypeName && d.Spec != null); // ensures only query new model

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
                .Where(d => d.DocumentType == CommonPromptDocumentModel.DocumentTypeName && d.Name != null); // ensures only query legacy model

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

    public async Task<bool> DeleteCommonPromptAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
            await container.DeleteItemAsync<CommonPromptDocumentModel>(
                CommonPromptDocumentModel.GetId(name),
                new PartitionKey(CommonPromptDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully deleted common prompt {PromptName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Common prompt {PromptName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete common prompt {PromptName}", name);
            throw;
        }
    }

    #endregion

    #region CommonToolsList Operations

    public async Task<CommonToolsListDocumentModel> UpsertCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(toolsList, new PartitionKey(CommonToolsListDocumentModel.GetPartitionKey()));
        _logger.LogInternalInformation("Successfully upserted common tools list document {ToolsListName}", toolsList.Name);
        return response.Resource;
    }

    public async Task<CommonToolsListDocumentModel?> GetCommonToolsListByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<CommonToolsListDocumentModel>(
                CommonToolsListDocumentModel.GetId(name),
                new PartitionKey(CommonToolsListDocumentModel.GetPartitionKey()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Common tools list {ToolsListName} not found", name);
            return null;
        }
    }

    public async Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);

        var queryCommonToolsListDocumentModelTask = Task.Run(async () =>
        {
            var query = container.GetItemLinqQueryable<CommonToolsListDocumentModel>()
                .Where(d => d.DocumentType == CommonToolsListDocumentModel.DocumentTypeName && d.Spec != null); // ensures only query new model

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
                .Where(d => d.DocumentType == CommonToolsListDocumentModel.DocumentTypeName && d.Name != null); // ensures only query legacy model

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

    public async Task<bool> DeleteCommonToolsListAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
            await container.DeleteItemAsync<CommonToolsListDocumentModel>(
                CommonToolsListDocumentModel.GetId(name),
                new PartitionKey(CommonToolsListDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully deleted common tools list {ToolsListName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Common tools list {ToolsListName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete common tools list {ToolsListName}", name);
            throw;
        }
    }

    #endregion

    #region PluginConfig Operations

    public async Task<PlugInConfigDocumentModel> UpsertPluginConfigAsync(PlugInConfigDocumentModel config)
    {
        var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(config, new PartitionKey(PlugInConfigDocumentModel.GetPartitionKey()));
        _logger.LogInternalInformation("Successfully upserted plugin config document {PluginConfigName}", config.Name);
        return response.Resource;
    }

    public async Task<PlugInConfigDocumentModel?> GetPluginConfigByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<PlugInConfigDocumentModel>(
                PlugInConfigDocumentModel.GetId(name),
                new PartitionKey(PlugInConfigDocumentModel.GetPartitionKey()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Plugin config {PluginConfigName} not found", name);
            return null;
        }
    }

    public async Task<PaginatedList<PlugInConfigDocumentModel>> GetPluginConfigsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
            var query = container.GetItemLinqQueryable<PlugInConfigDocumentModel>()
                .Where(d => d.DocumentType == PlugInConfigDocumentModel.DocumentTypeName);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d => d.Name.Contains(search));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<PlugInConfigDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            _logger.LogInternalInformation("Retrieved {Count} plugin configs with search '{Search}'", results.Count, search ?? "none");
            return new PaginatedList<PlugInConfigDocumentModel>(results, results.Count, 1, results.Count);
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve plugin configs with search '{Search}'", search ?? "none");
            throw;
        }
    }

    public async Task<bool> DeletePluginConfigAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
            await container.DeleteItemAsync<PlugInConfigDocumentModel>(
                PlugInConfigDocumentModel.GetId(name),
                new PartitionKey(PlugInConfigDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully deleted plugin config {PluginConfigName}", name);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogInternalInformation("Plugin config {PluginConfigName} not found for deletion", name);
            return false;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete plugin config {PluginConfigName}", name);
            throw;
        }
    }

    #endregion

    #region Connector Operations
    public async Task<ConnectorDocumentModel> UpsertConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        try
        {

            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.UpsertItemAsync(connector, new PartitionKey(ConnectorDocumentModel.GetPartitionKey()));
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
                ConnectorDocumentModel.GetId(name),
                new PartitionKey(ConnectorDocumentModel.GetPartitionKey()));
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
                .Where(d => d.DocumentType == ConnectorDocumentModel.DocumentTypeName);
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
                ConnectorDocumentModel.GetId(name),
                new PartitionKey(ConnectorDocumentModel.GetPartitionKey()));
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

    #region Skill Operations

    public async Task<SkillDocumentModel> UpsertSkillDocumentAsync(SkillDocumentModel skill, string operationId)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, SkillDocumentModel.ContainerName);
            var response = await container.UpsertItemAsync(skill, new PartitionKey(SkillDocumentModel.GetPartitionKey()));
            _logger.LogInternalInformation("Successfully updated skill document {SkillName} for operation {OperationId}", skill.Spec.Name, operationId);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to update skill document {SkillName} for operation {OperationId}", skill.Spec.Name, operationId);
            throw;
        }
    }

    public async Task<bool> DeleteSkillAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, SkillDocumentModel.ContainerName);

            try
            {
                await container.DeleteItemAsync<SkillDocumentModel>(
                    SkillDocumentModel.GetId(name),
                    new PartitionKey(SkillDocumentModel.GetPartitionKey()));
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to delete skill {SkillName}", name);
            throw;
        }
    }

    public async Task<SkillDocumentModel?> GetSkillByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, SkillDocumentModel.ContainerName);

            try
            {
                var response = await container.ReadItemAsync<SkillDocumentModel>(
                    SkillDocumentModel.GetId(name),
                    new PartitionKey(SkillDocumentModel.GetPartitionKey()));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve skill {SkillName}", name);
            throw;
        }
    }

    public async Task<PaginatedList<SkillDocumentModel>> GetSkillsAsync(int limit = 50, string? search = null, int pageIndex = 1)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, SkillDocumentModel.ContainerName);
            var query = container.GetItemLinqQueryable<SkillDocumentModel>()
                .Where(d => d.DocumentType == SkillDocumentModel.DocumentTypeName);

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(d =>
                    d.Spec.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    d.Spec.Description.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            using var iterator = query.Skip((pageIndex - 1) * limit).Take(limit).ToFeedIterator();
            var results = new List<SkillDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Resource);
            }

            _logger.LogInternalInformation("Retrieved {Count} skills with search '{Search}'", results.Count, search ?? "none");
            return new PaginatedList<SkillDocumentModel>(results, results.Count, pageIndex, limit);
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to retrieve skills with search '{Search}'", search ?? "none");
            throw;
        }
    }

    #endregion

    #region Migration Operations
    /// <summary>
    /// Migrates all tool documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateToolDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting ToolDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{ToolDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<ToolDocumentModel>(
            container,
            queryText,
            tool => tool.Name,
            name => ToolDocumentModel.GetId(name),
            _ => ToolDocumentModel.GetPartitionKey(),
            ToolDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "ToolDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all agent documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateAgentDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting AgentDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, AgentDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{AgentDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<AgentDocumentModel>(
            container,
            queryText,
            agent => agent.Name,
            name => AgentDocumentModel.GetId(name),
            _ => AgentDocumentModel.GetPartitionKey(),
            AgentDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "AgentDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all connector documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateConnectorDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting ConnectorDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{ConnectorDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<ConnectorDocumentModel>(
            container,
            queryText,
            connector => connector.Name,
            name => ConnectorDocumentModel.GetId(name),
            _ => ConnectorDocumentModel.GetPartitionKey(),
            ConnectorDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "ConnectorDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all common prompt documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateCommonPromptDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting CommonPromptDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{CommonPromptDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<CommonPromptDocumentModel>(
            container,
            queryText,
            prompt => prompt.Name,
            name => CommonPromptDocumentModel.GetId(name),
            _ => CommonPromptDocumentModel.GetPartitionKey(),
            CommonPromptDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "CommonPromptDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all common tools list documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateCommonToolsListDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting CommonToolsListDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{CommonToolsListDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<CommonToolsListDocumentModel>(
            container,
            queryText,
            toolsList => toolsList.Name,
            name => CommonToolsListDocumentModel.GetId(name),
            _ => CommonToolsListDocumentModel.GetPartitionKey(),
            CommonToolsListDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "CommonToolsListDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all skill documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigrateSkillDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting SkillDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, SkillDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{SkillDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<SkillDocumentModel>(
            container,
            queryText,
            skill => skill.Spec.Name,
            name => SkillDocumentModel.GetId(name),
            _ => SkillDocumentModel.GetPartitionKey(),
            SkillDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "SkillDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Migrates all plugin config documents to use the new id and partitionKey format.
    /// </summary>
    public async Task MigratePluginConfigDocumentsAsync()
    {
        _logger.LogInternalInformation("Starting PlugInConfigDocumentModel migration...");

        var container = _cosmosClient.GetContainer(_databaseName, PlugInConfigDocumentModel.ContainerName);
        var queryText = $"SELECT * FROM c WHERE c.documentType = '{PlugInConfigDocumentModel.DocumentTypeName}'";

        var (migratedCount, skippedCount, errorCount) = await MigrateDocumentsAsync<PlugInConfigDocumentModel>(
            container,
            queryText,
            config => config.Name,
            name => PlugInConfigDocumentModel.GetId(name),
            _ => PlugInConfigDocumentModel.GetPartitionKey(),
            PlugInConfigDocumentModel.DocumentTypeName);

        _logger.LogInternalInformation(
            "PlugInConfigDocumentModel migration completed. Migrated: {MigratedCount}, Skipped: {SkippedCount}, Errors: {ErrorCount}",
            migratedCount, skippedCount, errorCount);
    }

    /// <summary>
    /// Generic migration method for extended agent documents.
    /// </summary>
    private async Task<(int migratedCount, int skippedCount, int errorCount)> MigrateDocumentsAsync<T>(
        Container container,
        string queryText,
        Func<T, string> getNameFunc,
        Func<string, string> getExpectedIdFunc,
        Func<string, string> getExpectedPartitionKeyFunc,
        string documentTypeName) where T : ICosmosDocument
    {
        var migratedCount = 0;
        var skippedCount = 0;
        var errorCount = 0;

        try
        {
            var queryDefinition = new QueryDefinition(queryText);
            var queryOptions = new QueryRequestOptions { MaxItemCount = 100 };

            // Query raw JSON documents to get the actual id and partitionKey stored in Cosmos DB
            // The document model's Id/PartitionKey properties are computed and always return the new format
            using var iterator = container.GetItemQueryIterator<JsonElement>(queryDefinition, requestOptions: queryOptions);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();

                foreach (var rawDocument in response)
                {
                    try
                    {
                        // Extract the actual id and partitionKey from the raw JSON document
                        var currentId = rawDocument.GetProperty("id").GetString()!;
                        var currentPartitionKey = rawDocument.GetProperty("partitionKey").GetString()!;

                        // Deserialize to the typed model for processing
                        // this option must be same as CosmosSystemTextJsonSerializer
                        var options = new JsonSerializerOptions
                        {
                            Converters =
                            {
                                new LegacyDocumentModelConverter<AgentDocumentModel, AgentDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<ToolDocumentModel, ToolDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<ConnectorDocumentModel, ConnectorDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<PlugInConfigDocumentModel, PlugInConfigDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<CommonPromptDocumentModel, CommonPromptDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<CommonToolsListDocumentModel, CommonToolsListDocumentModelLegacy>(),
                                new LegacyDocumentModelConverter<SkillDocumentModel, SkillDocumentModelLegacy>(),
                            },
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = true,
                            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
                            UnknownTypeHandling = JsonUnknownTypeHandling.JsonElement,
                            AllowOutOfOrderMetadataProperties = true
                        };
                        var document = JsonSerializer.Deserialize<T>(rawDocument.GetRawText(), options);
                        if (document == null)
                        {
                            _logger.LogInternalWarning("Failed to migrate {DocumentType} document: Deserialization returned null. Raw: {RawDocument}", documentTypeName, rawDocument.GetRawText());
                            errorCount++;
                            continue;
                        }

                        var name = getNameFunc(document);

                        if (string.IsNullOrEmpty(name))
                        {
                            _logger.LogInternalWarning("Failed to migrate {DocumentType} document: Unable to determine name", documentTypeName);
                            errorCount++;
                            continue;
                        }

                        var expectedId = getExpectedIdFunc(name);
                        var expectedPartitionKey = getExpectedPartitionKeyFunc(name);

                        // Check if migration is needed using the actual stored values
                        if (currentId == expectedId && currentPartitionKey == expectedPartitionKey)
                        {
                            _logger.LogInternalDebug("{DocumentType} {Name} already has correct id/partitionKey, skipping", documentTypeName, name);
                            skippedCount++;
                            continue;
                        }

                        _logger.LogInternalInformation(
                            "Migrating {DocumentType} {Name}: id '{CurrentId}' -> '{NewId}', partitionKey '{CurrentPK}' -> '{NewPK}'",
                            documentTypeName, name, currentId, expectedId, currentPartitionKey, expectedPartitionKey);

                        // Create the document with correct id/partitionKey
                        await container.CreateItemAsync(
                            document,
                            new PartitionKey(expectedPartitionKey));

                        // Delete the old document from legacy location
                        try
                        {
                            await container.DeleteItemAsync<T>(
                                currentId,
                                new PartitionKey(currentPartitionKey));
                            _logger.LogInternalInformation("Deleted legacy {DocumentType} document for {Name}", documentTypeName, name);
                        }
                        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            _logger.LogInternalDebug("No legacy {DocumentType} document found at old location for {Name}", documentTypeName, name);
                        }

                        migratedCount++;
                        _logger.LogInternalInformation("Successfully migrated {DocumentType} {Name}", documentTypeName, name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to migrate {DocumentType} document", documentTypeName);
                        errorCount++;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to run migration for {DocumentType} documents", documentTypeName);
            errorCount++;
        }

        return (migratedCount, skippedCount, errorCount);
    }

    #endregion
}

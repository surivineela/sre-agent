using System.Net;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Agent.Data.Repositories;

public class CosmosDbExtendedAgentRepository : IExtendedAgentRepository
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseName;
    private readonly ILogger<CosmosDbExtendedAgentRepository> _logger;

    // A single container for all related documents, distinguished by 'documentType'.
    private const string ContainerName = "extendedagents";

    // Reusable JSON serializer for converting dictionaries back to models.
    private static readonly JsonSerializer s_serializer = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Ignore
    };

    public CosmosDbExtendedAgentRepository(
        CosmosClient cosmosClient,
        string databaseName,
        ILogger<CosmosDbExtendedAgentRepository> logger)
    {
        _cosmosClient = cosmosClient;
        _databaseName = databaseName;
        _logger = logger;
    }

    #region Conversion Helper

    /// <summary>
    /// Converts a dictionary-based document from Cosmos DB back to a strongly-typed domain model.
    /// </summary>
    private static T ConvertToModel<T>(Dictionary<string, object> document) where T : class
    {
        if (document == null)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;

        }
        var jObject = JObject.FromObject(document);
        return jObject.ToObject<T>(s_serializer);
#pragma warning restore CS8603 // Possible null reference return.
    }
    public static Dictionary<string, Type> GetToolDocumentTypeMappings() => new Dictionary<string, Type>
    {
        ["KustoTool"] = typeof(KustoToolDocumentModel),

    };


    public static Dictionary<string, Type> GetConnectorDocumentTypeMappings() => new Dictionary<string, Type>
    {
        ["Kusto"] = typeof(KustoConnectorDocumentModel),


    };

    private static ConnectorDocumentModel? ConvertConnectorToModel(Dictionary<string, object> document, string connectorType)
    {
        var targetType = GetConnectorDocumentTypeMappings()[connectorType];
        if (document == null)
        {
            return null;
        }
        var jObject = JObject.FromObject(document);
        var result = jObject.ToObject(targetType, s_serializer);
        return result as ConnectorDocumentModel;
    }
    private static ToolDocumentModel ConvertToolToModel(Dictionary<string, object> document, string toolType)
    {
        var targetType = GetToolDocumentTypeMappings()[toolType];
        if (document == null)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;

        }
        var jObject = JObject.FromObject(document);
        if (jObject == null)
        {
            return null;
        }

        return (ToolDocumentModel?)jObject.ToObject(targetType, s_serializer);
#pragma warning restore CS8603 // Possible null reference return.
    }

    #endregion

    #region Agent Operations

    public async Task<AgentDocumentModel> CreateAgentAsync(AgentDocumentModel document, string operationId)
    {
        try
        {

            var container = _cosmosClient.GetContainer(_databaseName, ExtendedAgentDocument.ContainerName);

            var response = await container.CreateItemAsync(document, new PartitionKey(document.Name));

            _logger.LogInternalInformation("Successfully created agent document {AgentName}", document.Name);

            // Convert the response back to the domain model before returning
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInternalWarning("Agent {AgentName} already exists, updating instead", document.Name);
            return await UpdateAgentAsync(document, operationId);
        }
        // ... other catches
    }

    public async Task<AgentDocumentModel> UpdateAgentAsync(AgentDocumentModel agent, string operationId)
    {
        var container = _cosmosClient.GetContainer(_databaseName, ExtendedAgentDocument.ContainerName);
        var response = await container.UpsertItemAsync(agent, new PartitionKey(agent.Name));
        _logger.LogInternalInformation("Successfully updated agent document {AgentName}", agent.Name);
        return response.Resource;
    }

    public async Task<AgentDocumentModel?> GetAgentByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ExtendedAgentDocument.ContainerName);
            // Read as a dictionary
            var response = await container.ReadItemAsync<Dictionary<string, object>>(
                $"{name}",
                new PartitionKey(name));

            // Convert to the domain model
            return ConvertToModel<AgentDocumentModel>(response.Resource);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {

            return null;
        }
    }



    #endregion

    #region Tool Operations

    public async Task<ToolDocumentModel> CreateToolAsync(ToolDocumentModel tool, string operationId)
    {
        try
        {
            // Use the factory to create a flattened dictionary

            var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);

            var response = await container.CreateItemAsync(tool, new PartitionKey(tool.Name));

            _logger.LogInternalInformation("Successfully created tool document {ToolName}", tool.Name);

            // Convert the response back to the domain model
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInternalWarning("Tool {ToolName} already exists, updating instead", tool.Name);
            return await UpdateToolAsync(tool, operationId);
        }
        // ... other catches
    }

    public async Task<ToolDocumentModel> UpdateToolAsync(ToolDocumentModel tool, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(tool, new PartitionKey(tool.Name));
        _logger.LogInternalInformation("Successfully updated tool document {ToolName}", tool.Name);
        return response.Resource;
    }

    public async Task<CommonPromptDocumentModel> UpdateCommonPromptAsync(CommonPromptDocumentModel prompt, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(prompt, new PartitionKey(prompt.Name));
        _logger.LogInternalInformation("Successfully updated common prompt document {PromptName}", prompt.Name);
        return response.Resource;
    }

    public async Task<CommonToolsListDocumentModel> UpdateCommonToolsListAsync(CommonToolsListDocumentModel toolsList, string operationId)
    {

        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(toolsList, new PartitionKey(toolsList.Name));
        _logger.LogInternalInformation("Successfully updated common tools list document {ToolsListName}", toolsList.Name);
        return response.Resource;
    }
    public async Task<PlugInConfigDocumentModel> UpdatePluginConfigAsync(PlugInConfigDocumentModel config)
    {
        var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
        var response = await container.UpsertItemAsync(config, new PartitionKey(config.Name));
        _logger.LogInternalInformation("Successfully updated tool document {ToolName}", config.Name);
        return response.Resource;
    }

    public async Task<ToolDocumentModel?> GetToolByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ToolDocumentModel.ContainerName);
            // Read as a dictionary
            var response = await container.ReadItemAsync<Dictionary<string, object>>(
                $"tool_{name}",
                new PartitionKey(name));

            // Convert to the domain model
            return ConvertToModel<ToolDocumentModel>(response.Resource);
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
        // Query for dictionaries
        var query = container.GetItemLinqQueryable<Dictionary<string, object>>()
            .Where(d => (string)d["documentType"] == "ExtendedAgentTool");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                ((string)d["name"]).Contains(search) ||
                ((string)d["description"]).Contains(search));
        }

        using var iterator = query.Take(limit).ToFeedIterator();
        var results = new List<ToolDocumentModel>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            // Convert each dictionary in the response to the domain model
            results.AddRange(response.Select(doc => ConvertToolToModel(doc, (string)doc["type"])));
        }


        return new PaginatedList<ToolDocumentModel>(results, limit, 0, 50);
    }


    public async Task<PaginatedList<CommonToolsListDocumentModel>> GetCommonToolsListsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonToolsListDocumentModel.ContainerName);
        // Query for dictionaries
        var query = container.GetItemLinqQueryable<Dictionary<string, object>>()
            .Where(d => (string)d["documentType"] == "CommonToolsList");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                ((string)d["name"]).Contains(search) ||
                ((string)d["description"]).Contains(search));
        }

        using var iterator = query.Take(limit).ToFeedIterator();
        var results = new List<CommonToolsListDocumentModel>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            // Convert each dictionary in the response to the domain model
            results.AddRange(response.Select(doc => ConvertCommonToolsListToModel(doc)));
        }


        return new PaginatedList<CommonToolsListDocumentModel>(results, limit, 0, 50);
    }

    private CommonToolsListDocumentModel ConvertCommonToolsListToModel(Dictionary<string, object> doc)
    {
        if (doc == null)
        {
            #pragma warning disable CS8603 // Possible null reference return.
            return null;
        }
        var jObject = JObject.FromObject(doc);
        return jObject.ToObject<CommonToolsListDocumentModel>(s_serializer);
#pragma warning restore CS8603 // Possible null reference return.
    }

    public async Task<PaginatedList<CommonPromptDocumentModel>> GetCommonPromptsAsync(int limit = 50, string? search = null)
    {
        var container = _cosmosClient.GetContainer(_databaseName, CommonPromptDocumentModel.ContainerName);
        // Query for dictionaries
        var query = container.GetItemLinqQueryable<Dictionary<string, object>>()
            .Where(d => (string)d["documentType"] == "CommonPrompt");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                ((string)d["name"]).Contains(search) ||
                ((string)d["description"]).Contains(search));
        }

        using var iterator = query.Take(limit).ToFeedIterator();
        var results = new List<CommonPromptDocumentModel>();

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            // Convert each dictionary in the response to the domain model
            results.AddRange(response.Select(doc => ConvertCommonPromptToModel(doc)));
        }


        return new PaginatedList<CommonPromptDocumentModel>(results, limit, 0, 50);
    }

    private CommonPromptDocumentModel ConvertCommonPromptToModel(Dictionary<string, object> doc)
    {
        if (doc == null)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return null;
        }
        var jObject = JObject.FromObject(doc);
        return jObject.ToObject<CommonPromptDocumentModel>(s_serializer);
#pragma warning restore CS8603 // Possible null reference return.
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

    public async Task<PaginatedList<AgentDocumentModel>> GetAgentsAsync(int limit = 50, string? search = null)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ExtendedAgentDocument.ContainerName);

            // Query for dictionaries where documentType is "Agent"
            var query = container.GetItemLinqQueryable<Dictionary<string, object>>()
                .Where(d => (string)d["documentType"] == "ExtendedAgent");

            if (!string.IsNullOrWhiteSpace(search))
            {
                // Apply search filter to name and description fields
                query = query.Where(d =>
                    ((string)d["name"]).Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (d.ContainsKey("handoffDescription") && ((string)d["handoffDescription"]).Contains(search, StringComparison.OrdinalIgnoreCase)));
            }

            using var iterator = query.Take(limit).ToFeedIterator();
            var results = new List<AgentDocumentModel>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                // Convert each dictionary document back to the domain model
                results.AddRange(response.Select(doc => ConvertToModel<AgentDocumentModel>(doc)));
            }

            _logger.LogInternalInformation("Retrieved {Count} agents with search '{Search}'", results.Count, search ?? "none");
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
            var container = _cosmosClient.GetContainer(_databaseName, ExtendedAgentDocument.ContainerName);

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

    #region Connector Operations
    public async Task<ConnectorDocumentModel> CreateConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        try
        {

            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.CreateItemAsync(connector, new PartitionKey(connector.PartitionKey));
            _logger.LogInternalInformation("Successfully created extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            _logger.LogInternalWarning("Extended agent connector {ConnectorName} already exists, updating instead", connector.Name);
            return await UpdateConnectorAsync(connector, operationId);
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to create extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            throw;
        }
    }

    public async Task<ConnectorDocumentModel> UpdateConnectorAsync(ConnectorDocumentModel connector, string operationId)
    {
        try
        {

            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.UpsertItemAsync(connector, new PartitionKey(connector.PartitionKey));
            _logger.LogInternalInformation("Successfully updated extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            return response.Resource;
        }
        catch (CosmosException ex)
        {
            _logger.LogInternalError(ex, "Failed to update extended agent connector document {ConnectorName} for operation {OperationId}",
                connector.Name, operationId);
            throw;
        }
    }

    public async Task<ConnectorDocumentModel?> GetConnectorByNameAsync(string name)
    {
        try
        {
            var container = _cosmosClient.GetContainer(_databaseName, ConnectorDocumentModel.ContainerName);
            var response = await container.ReadItemAsync<Dictionary<string, object>>(
                $"connector_{name}",
                new PartitionKey(name));
            return ConvertConnectorToModel(response.Resource, (string)response.Resource["type"]);
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
            var query = container.GetItemLinqQueryable<Dictionary<string, object>>()
                .Where(d => (string)d["DocumentType"] == "ConnectorDocument");
            if (!string.IsNullOrWhiteSpace(search))
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                query = query.Where(d =>
                    d["Name"].ToString().Contains(search) ||
                    d["Description"].ToString().Contains(search));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
            query = query.Take(limit);
            using var iterator = query.ToFeedIterator();
            var results = new List<ConnectorDocumentModel>();
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                results.AddRange(response.Select(doc => ConvertToModel<ConnectorDocumentModel>(doc)));
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

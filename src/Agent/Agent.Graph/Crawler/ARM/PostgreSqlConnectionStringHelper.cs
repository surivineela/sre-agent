// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Schema;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Agent.Graph.Crawler.ARM;

public class PostgreSqlConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;
    private readonly IGraphDatabaseClient _graphDbClient;
    private const string azurePostgreSqlSuffix = ".postgres.database.azure.com";

    public PostgreSqlConnectionStringHelper(ILogger logger, ArmClient armClient, IGraphDatabaseClient graphDbClient)
    {
        _logger = logger;
        _armClient = armClient;
        _graphDbClient = graphDbClient;
    }

    public async Task<ArmResourceNode> GetPostgreSqlResourceFromConnectionStringAsync(
        GraphNode workloadNode,
        string value,
        string sourceType,
        string sourceName)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(value);
            var rawServer = builder.Host;
            var database = builder.Database;

            var serverName = rawServer;
            int portIndex = serverName.IndexOf(":");
            if (portIndex > 0)
            {
                serverName = serverName.Substring(0, portIndex);
            }

            var serverBaseName = serverName;
            if (serverBaseName.EndsWith(azurePostgreSqlSuffix, StringComparison.OrdinalIgnoreCase))
            {
                serverBaseName = serverBaseName.Substring(0, serverBaseName.Length - azurePostgreSqlSuffix.Length);
            }

            _logger.LogDebug($"Parsed PostgreSQL server name: {serverName}, Database: {database}");

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.GetSubscriptionId()));
            
            // Check for flexible servers first
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/flexibleServers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, value, sourceType, sourceName, "Microsoft.DBforPostgreSQL/flexibleServers");
            }

            // Check for single servers
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/servers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, value, sourceType, sourceName, "Microsoft.DBforPostgreSQL/servers");
            }

            // Check for Cosmos DB for PostgreSQL (formerly Hyperscale)
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.DBforPostgreSQL/serverGroupsv2' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                return await CreatePostgreSqlNode(workloadNode, server, value, sourceType, sourceName, "Microsoft.DBforPostgreSQL/serverGroupsv2");
            }

            _logger.LogInternalWarning($"PostgreSQL server with name {serverName} was not found in the subscription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error processing PostgreSQL connection string: {ex.Message}");
            return null;
        }
    }

    private async Task<ArmResourceNode> CreatePostgreSqlNode(
        GraphNode workloadNode,
        Azure.ResourceManager.Resources.GenericResource server,
        string connectionString,
        string sourceType,
        string sourceName,
        string resourceType)
    {
        var postgresResourceId = new ResourceIdentifier(server.Data.Id.ToString());
        var postgresNode = new ArmResourceNode(
            resourceType: resourceType,
            resourceId: postgresResourceId,
            subscriptionId: postgresResourceId.SubscriptionId,
            resourceGroupName: postgresResourceId.ResourceGroupName,
            resourceName: postgresResourceId.Name);

        var properties = postgresNode.GetNodeProperties();
        properties["authType"] = DetermineAuthType(connectionString);
        properties["source"] = $"{sourceType}:{sourceName}";

        await _graphDbClient.AddOrUpdateNodeAsync(postgresNode);

        var edge = new ArmResourceEdge(workloadNode.GetNodeId(), postgresNode.GetNodeId(), Constants.Relationships.PostgreSqlConnected);
        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

        _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with PostgreSQL resource {postgresResourceId}");
        return postgresNode;
    }

    private string DetermineAuthType(string connectionString)
    {
        if (connectionString.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Integrated Security=true", StringComparison.OrdinalIgnoreCase))
        {
            return "managedIdentity";
        }
        else if (connectionString.Contains("SSL Mode=", StringComparison.OrdinalIgnoreCase))
        {
            return "connectionStringWithSSL";
        }
        return "connectionString";
    }

    public bool IsPostgreSqlConnectionString(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Common PostgreSQL connection string indicators
        return value.Contains(".postgres.database.azure.com", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ArmResourceNode> TryLinkPostgreSqlResourceById(GraphNode workloadNode, string possiblePostgreSqlResource, string sourceType, string sourceName)
    {
        try
        {
            var postgresId = new ResourceIdentifier(possiblePostgreSqlResource);
            var resourceType = postgresId.ResourceType.ToString();
            
            // Validate it's a PostgreSQL resource
            if (!resourceType.Contains("Microsoft.DBforPostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var postgresNode = new ArmResourceNode(
                resourceType: resourceType,
                resourceId: postgresId,
                subscriptionId: postgresId.SubscriptionId,
                resourceGroupName: postgresId.ResourceGroupName,
                resourceName: postgresId.Name);

            var properties = postgresNode.GetNodeProperties();
            properties["source"] = $"{sourceType}:{sourceName}";
            properties["authType"] = "resourceId";

            await _graphDbClient.AddOrUpdateNodeAsync(postgresNode);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), postgresNode.GetNodeId(), Constants.Relationships.PostgreSqlConnected);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with PostgreSQL resource {postgresId}");
            return postgresNode;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error linking PostgreSQL resource from value: {possiblePostgreSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }
}
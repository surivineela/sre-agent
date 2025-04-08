// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Schema;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class SqlConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;
    private readonly IGraphDatabaseClient _graphDbClient;
    private const string azureSqlSuffix = ".database.windows.net";

    public SqlConnectionStringHelper(ILogger logger, ArmClient armClient, IGraphDatabaseClient graphDbClient)
    {
        _logger = logger;
        _armClient = armClient;
        _graphDbClient = graphDbClient;
    }

    public async Task<ArmResourceNode> GetSqlResourceFromConnectionStringAsync(
        GraphNode workloadNode,
        string value,
        string sourceType,
        string sourceName)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(value);
            var rawServer = builder.DataSource;
            var database = builder.InitialCatalog;

            var serverName = rawServer;
            if (serverName.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                serverName = serverName.Substring(4);
            }
            int commaIndex = serverName.IndexOf(",");
            if (commaIndex > 0)
            {
                serverName = serverName.Substring(0, commaIndex);
            }

            var serverBaseName = serverName;
            if (serverBaseName.EndsWith(azureSqlSuffix, StringComparison.OrdinalIgnoreCase))
            {
                serverBaseName = serverBaseName.Substring(0, serverBaseName.Length - azureSqlSuffix.Length);
            }

            _logger.LogDebug($"Parsed SQL server name: {serverName}, Database: {database}");

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.GetSubscriptionId()));
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: $"resourceType eq 'Microsoft.Sql/servers' and name eq '{serverBaseName.ToLowerInvariant()}'"))
            {
                var sqlResourceId = new ResourceIdentifier(server.Data.Id.ToString());
                var sqlNode = new ArmResourceNode(
                    resourceType: "Microsoft.Sql/servers",
                    resourceId: sqlResourceId,
                    subscriptionId: sqlResourceId.SubscriptionId,
                    resourceGroupName: sqlResourceId.ResourceGroupName,
                    resourceName: sqlResourceId.ResourceGroupName);

                if (sqlNode != null)
                {
                    var properties = sqlNode.GetNodeProperties();
                    properties["authType"] = value.Contains("Authentication=Active Directory Managed Identity",
                        StringComparison.OrdinalIgnoreCase)
                            ? "managedIdentity"
                            : "connectionString";
                    properties["source"] = $"{sourceType}:{sourceName}";

                    await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

                    var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                    _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with SQL resource {sqlResourceId}");
                }

                return sqlNode;
            }

            _logger.LogWarning($"SQL server with name {serverName} was not found in the subscription.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing connection string: {ex.Message}");
            return null;
        }
    }

    public bool IsSqlConnectionString(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Common SQL connection string indicators
        return value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ArmResourceNode> TryLinkSqlResourceById(GraphNode workloadNode, string possibleSqlResource, string sourceType, string sourceName)
    {
        try
        {
            var sqlId = new ResourceIdentifier(possibleSqlResource);
            var sqlNode = new ArmResourceNode(
                resourceType: "Microsoft.Sql/servers",
                resourceId: sqlId,
                subscriptionId: sqlId.SubscriptionId,
                resourceGroupName: sqlId.ResourceGroupName,
                resourceName: sqlId.Name);

            var properties = sqlNode.GetNodeProperties();
            properties["source"] = $"{sourceType}:{sourceName}";
            properties["authType"] = "resourceId";

            await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with SQL resource {sqlId}");
            return sqlNode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }
}


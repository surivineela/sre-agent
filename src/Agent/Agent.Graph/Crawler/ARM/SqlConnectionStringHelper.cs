// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class SqlConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;
    private const string azureSqlSuffix = ".database.windows.net";

    public SqlConnectionStringHelper(ILogger logger, ArmClient armClient)
    {
        _logger = logger;
        _armClient = armClient;
    }

    public async Task<ArmResourceNode> GetSqlResourceFromConnectionStringAsync(
        IGraphDatabaseClient dbManager,
        GraphNode workloadNode,
        string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
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

                await dbManager.AddOrUpdateNodeAsync(sqlNode);

                var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                await dbManager.AddOrUpdateEdgeAsync(edge);

                _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with SQL resource {sqlResourceId}");
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
}


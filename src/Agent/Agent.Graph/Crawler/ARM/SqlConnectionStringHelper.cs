using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

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
        IGraphDatabaseManager dbManager,
        ArmResourceNode workloadNode,
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

            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/" + workloadNode.SubscriptionId));
            await foreach (var server in subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Sql/servers'"))
            {
                // Compare names (adjust for case or domain differences as needed).
                if (server.Data.Name.Equals(serverBaseName, StringComparison.OrdinalIgnoreCase))
                {
                    var sqlResourceId = server.Data.Id.ToString();
                    var sqlNode = new ArmResourceNode(
                        resourceType: "Microsoft.Sql/servers",
                        resourceId: sqlResourceId,
                        subscriptionId: workloadNode.SubscriptionId,
                        resourceGroupName: ExtractResourceGroupName(server.Data.Id),
                        resourceName: server.Data.Name);

                    await dbManager.AddOrUpdateNodeAsync(
                        sqlNode.GetNodeLabel(),
                        sqlNode.GetNodeId(),
                        sqlNode.GetResourceType(),
                        sqlNode.GetNodeProperties());

                    var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                    await dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

                    _logger.LogDebug($"Linked workload {workloadNode.ResourceId} with SQL resource {sqlResourceId}");
                    return sqlNode;
                }
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


    private string ExtractResourceGroupName(string resourceId)
    {
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }
        return string.Empty;
    }
}

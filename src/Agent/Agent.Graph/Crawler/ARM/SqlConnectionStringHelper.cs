using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

public class SqlConnectionStringHelper
{
    private readonly ILogger _logger;
    private readonly ArmClient _armClient;

    public SqlConnectionStringHelper(ILogger logger)
    {
        _logger = logger;
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    public async Task<ArmResourceNode> GetSqlResourceFromConnectionStringAsync(IGraphDatabaseManager dbManager, ArmResourceNode workloadNode, string connectionString)
    {
        try
        {
            // Use SqlConnectionStringBuilder to parse the connection string.
            var builder = new SqlConnectionStringBuilder(connectionString);
            var rawServer = builder.DataSource; // e.g. "tcp:myserver.database.windows.net,1433"
            var database = builder.InitialCatalog;

            // Normalize server name: remove "tcp:" prefix and any port numbers.
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

            _logger.LogDebug($"Parsed SQL server name: {serverName}, Database: {database}");

            // Use the Azure Resource Graph / ARM client to find the SQL Server resource.
            // For this example, we list SQL servers in the subscription of the workload node.
            var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier(workloadNode.SubscriptionId));
            var sqlServers = subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Sql/servers'");
            await foreach (var server in sqlServers)
            {
                // Compare names (you might need to adjust for case or domain differences).
                if (server.Data.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase))
                {
                    var sqlResourceId = server.Data.Id.ToString();
                    var sqlNode = new ArmResourceNode(
                        resourceType: "Microsoft.Sql/servers",
                        resourceId: sqlResourceId,
                        subscriptionId: workloadNode.SubscriptionId,
                        resourceGroupName: ExtractResourceGroupName(server.Data.Id),
                        resourceName: server.Data.Name);

                    await dbManager.AddOrUpdateNodeAsync(sqlNode.GetNodeLabel(), sqlNode.GetNodeId(), sqlNode.GetResourceType(), sqlNode.GetNodeProperties());
                    await dbManager.AddEdgeIfNotExistsAsync(workloadNode.GetNodeId(), sqlNode.GetNodeId(), "SQL_CONNECTED");

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

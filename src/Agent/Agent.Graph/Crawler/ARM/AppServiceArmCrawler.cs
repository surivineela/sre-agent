using System.Text.Json;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;

namespace Agent.Graph.Crawler.ARM
{
    public class AppServiceARMCrawler : IArmResourceCrawler
    {
        private readonly ILogger<AppServiceARMCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;

        public AppServiceARMCrawler(ILogger<AppServiceARMCrawler> logger, IGraphDatabaseManager dbManager)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            _logger.LogInformation($"Crawling App Service {node.ResourceId}");

            // Add or update the App Service node in the graph.
            await _dbManager.AddOrUpdateNodeAsync(
                node.GetNodeLabel(),
                node.GetNodeId(),
                node.GetResourceType(),
                node.GetNodeProperties());

            // Get the resource details using the generic API.
            var resourceIdentifier = new ResourceIdentifier(node.ResourceId);
            var resource = _armClient.GetGenericResource(resourceIdentifier);
            if (resource == null || !resource.HasData)
            {
                _logger.LogWarning($"Failed to get resource details for: {node.ResourceId}");
                yield break;
            }

            // Deserialize the properties (which should contain the site config)
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(resource.Data.Properties);

            // Look for configuration in "siteConfig"
            if (jsonObj.TryGetProperty("siteConfig", out JsonElement siteConfig))
            {
                // Check appSettings for SQL-related connection values.
                if (siteConfig.TryGetProperty("appSettings", out JsonElement appSettings) &&
                    appSettings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var setting in appSettings.EnumerateArray())
                    {
                        if (setting.TryGetProperty("value", out JsonElement valueElement) &&
                            valueElement.ValueKind == JsonValueKind.String)
                        {
                            var value = valueElement.GetString();
                            // If the value contains a resource ID, use the existing logic.
                            if (!string.IsNullOrEmpty(value) &&
                                value.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                            {
                                yield return await TryLinkSqlResource(node, resourceIdentifier, value);
                            }
                        }
                    }
                }

                // Also check connectionStrings (if present)
                if (siteConfig.TryGetProperty("connectionStrings", out JsonElement connectionStrings) &&
                    connectionStrings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var conn in connectionStrings.EnumerateArray())
                    {
                        if (conn.TryGetProperty("connectionString", out JsonElement connStringElement) &&
                            connStringElement.ValueKind == JsonValueKind.String)
                        {
                            var connValue = connStringElement.GetString();
                            if (!string.IsNullOrEmpty(connValue))
                            {
                                // If the connection string already has the SQL resource ID, use that.
                                if (connValue.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                                {
                                    yield return await TryLinkSqlResource(node, resourceIdentifier, connValue);
                                }
                                else
                                {
                                    // Otherwise, treat it as a standard SQL connection string.
                                    var sqlHelper = new SqlConnectionStringHelper(_logger, _armClient);
                                    var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_dbManager, node, connValue);
                                    if (sqlNode != null)
                                    {
                                        yield return sqlNode;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            yield break;
        }

        private async Task<ArmResourceNode> TryLinkSqlResource(ArmResourceNode appServiceNode, ResourceIdentifier appId, string possibleSqlResource)
        {
            try
            {
                var sqlId = new ResourceIdentifier(possibleSqlResource).ToString();
                // Create a new node for the SQL resource.
                var sqlNode = new ArmResourceNode(
                    resourceType: "Microsoft.Sql/servers",
                    resourceId: sqlId,
                    subscriptionId: appId.SubscriptionId,
                    resourceGroupName: appId.ResourceGroupName,
                    resourceName: appId.Name); // Adjust resourceName as needed

                await _dbManager.AddOrUpdateNodeAsync(
                    sqlNode.GetNodeLabel(),
                    sqlNode.GetNodeId(),
                    sqlNode.GetResourceType(),
                    sqlNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(
                    appServiceNode.GetNodeId(),
                    sqlNode.GetNodeId(),
                    "SQL_CONNECTED");
                _logger.LogInformation($"Linked App Service {appServiceNode.ResourceId} with SQL resource {sqlId}");
                return sqlNode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
                return null;
            }
        }
    }

    public class SqlConnectionStringHelper
    {
        private readonly ILogger _logger;
        private readonly ArmClient _armClient;

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

                _logger.LogInformation($"Parsed SQL server name: {serverName}, Database: {database}");

                var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier(workloadNode.SubscriptionId));
                await foreach (var server in subscription.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Sql/servers'"))
                {
                    // Compare names (adjust for case or domain differences as needed).
                    if (server.Data.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase))
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
                        await dbManager.AddEdgeIfNotExistsAsync(
                            workloadNode.GetNodeId(),
                            sqlNode.GetNodeId(),
                            "SQL_CONNECTED");

                        _logger.LogInformation($"Linked workload {workloadNode.ResourceId} with SQL resource {sqlResourceId}");
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
}


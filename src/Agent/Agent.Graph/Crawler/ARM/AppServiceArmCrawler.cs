using System.Text.Json;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.AppService;

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

            await _dbManager.AddOrUpdateNodeAsync(
                node.GetNodeLabel(),
                node.GetNodeId(),
                node.GetResourceType(),
                node.GetNodeProperties());

            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);
            var armResourceId = new ResourceIdentifier(node.ResourceId);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);
            var resourceGroup = armClient.GetResourceGroupResource(resourceGroupId);
            var siteResponse = await resourceGroup.GetWebSiteAsync(armResourceId.Name);
            var webApp = siteResponse.Value;

            // Link to App Service Plan if it exists
            if (!string.IsNullOrEmpty(webApp.Data.AppServicePlanId))
            {
                var planId = new ResourceIdentifier(webApp.Data.AppServicePlanId);
                var appServicePlanNode = new ArmResourceNode(
                    resourceType: "Microsoft.Web/serverfarms",
                    resourceId: webApp.Data.AppServicePlanId,
                    subscriptionId: planId.SubscriptionId,
                    resourceGroupName: planId.ResourceGroupName,
                    resourceName: planId.Name);

                // Add the App Service Plan node
                await _dbManager.AddOrUpdateNodeAsync(
                    appServicePlanNode.GetNodeLabel(),
                    appServicePlanNode.GetNodeId(),
                    appServicePlanNode.GetResourceType(),
                    appServicePlanNode.GetNodeProperties());

                // Create bidirectional edges
                await _dbManager.AddEdgeIfNotExistsAsync(
                    appServicePlanNode.GetNodeId(),
                    node.GetNodeId(),
                    "HOSTS");

                await _dbManager.AddEdgeIfNotExistsAsync(
                    node.GetNodeId(),
                    appServicePlanNode.GetNodeId(),
                    "HOSTED_ON");

                _logger.LogInformation($"Created bidirectional edges between App Service {node.ResourceId} and App Service Plan {webApp.Data.AppServicePlanId}");

                yield return appServicePlanNode;
            }

            var appSettingsResponse = await siteResponse.Value.GetApplicationSettingsAsync();
            var appSettings = appSettingsResponse.Value.Properties;

            foreach (var setting in appSettings)
            {
                var name = setting.Key;
                var value = setting.Value;
                if (string.IsNullOrEmpty(value)) continue;

                // Look for SQL connection strings in app settings
                if (IsSqlConnectionString(value))
                {
                    var sqlHelper = new SqlConnectionStringHelper(_logger, _armClient);
                    var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_dbManager, node, value);
                    if (sqlNode != null)
                    {
                        var properties = sqlNode.GetNodeProperties();
                        properties["authType"] = value.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase)
                            ? "managedIdentity"
                            : "connectionString";
                        properties["source"] = $"appService:appSetting:{name}";

                        await _dbManager.AddOrUpdateNodeAsync(
                            sqlNode.GetNodeLabel(),
                            sqlNode.GetNodeId(),
                            sqlNode.GetResourceType(),
                            properties);

                        await _dbManager.AddEdgeIfNotExistsAsync(
                            node.GetNodeId(),
                            sqlNode.GetNodeId(),
                            "SQL_CONNECTED");

                        yield return sqlNode;
                    }
                }
                // Look for Redis connection strings in app settings
                else if (IsRedisConnectionString(value))
                {
                    var redisHelper = new RedisConnectionStringHelper(_logger, _armClient);
                    var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_dbManager, node, value);
                    if (redisNode != null)
                    {
                        var properties = redisNode.GetNodeProperties();
                        properties["authType"] = value.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                            ? "managedIdentity"
                            : "connectionString";
                        properties["source"] = $"appService:appSetting:{name}";

                        await _dbManager.AddOrUpdateNodeAsync(
                            redisNode.GetNodeLabel(),
                            redisNode.GetNodeId(),
                            redisNode.GetResourceType(),
                            properties);

                        await _dbManager.AddEdgeIfNotExistsAsync(
                            node.GetNodeId(),
                            redisNode.GetNodeId(),
                            "REDIS_CONNECTED");

                        yield return redisNode;
                    }
                }
            }
        }

        private bool IsSqlConnectionString(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            
            // Common SQL connection string indicators
            return value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsRedisConnectionString(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            
            // Common Redis connection string indicators
            return value.Contains(".redis.cache.windows.net", StringComparison.OrdinalIgnoreCase) ||
                   value.Contains("ssl=true", StringComparison.OrdinalIgnoreCase) && 
                   (value.Contains(",abortConnect=false", StringComparison.OrdinalIgnoreCase) ||
                    value.Contains("password=", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<ArmResourceNode> TryLinkSqlResource(ArmResourceNode appServiceNode, ResourceIdentifier appId, string connValue)
        {
            try
            {
                var sqlHelper = new SqlConnectionStringHelper(_logger, _armClient);
                var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_dbManager, appServiceNode, connValue);
                if (sqlNode != null)
                {
                    var properties = sqlNode.GetNodeProperties();
                    properties["authType"] = connValue.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase) 
                        ? "managedIdentity" 
                        : "connectionString";
                    properties["source"] = "appService:connectionString";

                    await _dbManager.AddOrUpdateNodeAsync(
                        sqlNode.GetNodeLabel(),
                        sqlNode.GetNodeId(),
                        sqlNode.GetResourceType(),
                        properties);

                    await _dbManager.AddEdgeIfNotExistsAsync(
                        appServiceNode.GetNodeId(),
                        sqlNode.GetNodeId(),
                        "SQL_CONNECTED");

                    _logger.LogInformation($"Linked App Service {appServiceNode.ResourceId} with SQL resource");
                    return sqlNode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error linking SQL resource from value: {connValue}. Exception: {ex.Message}");
            }
            return null;
        }

        private async Task<ArmResourceNode> TryLinkRedisResource(ArmResourceNode appServiceNode, ResourceIdentifier appId, string connValue)
        {
            try
            {
                var redisHelper = new RedisConnectionStringHelper(_logger, _armClient);
                var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_dbManager, appServiceNode, connValue);
                if (redisNode != null)
                {
                    var properties = redisNode.GetNodeProperties();
                    properties["authType"] = connValue.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase) 
                        ? "managedIdentity" 
                        : "connectionString";
                    properties["source"] = "appService:connectionString";

                    await _dbManager.AddOrUpdateNodeAsync(
                        redisNode.GetNodeLabel(),
                        redisNode.GetNodeId(),
                        redisNode.GetResourceType(),
                        properties);

                    await _dbManager.AddEdgeIfNotExistsAsync(
                        appServiceNode.GetNodeId(),
                        redisNode.GetNodeId(),
                        "REDIS_CONNECTED");

                    _logger.LogInformation($"Linked App Service {appServiceNode.ResourceId} with Redis resource");
                    return redisNode;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error linking Redis resource from value: {connValue}. Exception: {ex.Message}");
            }
            return null;
        }
    }

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

                _logger.LogInformation($"Parsed SQL server name: {serverName}, Database: {database}");

                var subscription = _armClient.GetSubscriptionResource(new ResourceIdentifier("/subscriptions/"+workloadNode.SubscriptionId));
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



using System.Security.Principal;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AppServiceCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AppServiceCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public AppServiceCrawler(ILogger<AppServiceCrawler> logger, IGraphDatabaseClient dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient, false)
    {
        _logger = logger;
        _graphDbClient = dbManager;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var appServiceNode = (AppServiceNode)node;
        _logger.LogDebug($"Crawling App Service {appServiceNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(appServiceNode.ResourceId);
        var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);
        var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        var siteResponse = await resourceGroup.GetWebSiteAsync(armResourceId.Name);
        var webApp = siteResponse.Value;

        appServiceNode.Location = webApp.Data.Location;
        if (!string.IsNullOrEmpty(webApp.Data.VirtualNetworkSubnetId))
        {
            appServiceNode.VnetId = webApp.Data.VirtualNetworkSubnetId;
        }

        var webConfigResp = await webApp.GetWebSiteConfig().GetAsync();
        if (webConfigResp != null && webConfigResp.Value != null)
        {
            var webConfig = webConfigResp.Value;
            if (webConfig.HasData && webConfig.Data.MinTlsVersion != null)
            {
                appServiceNode.MinTlsVersion = webConfig.Data.MinTlsVersion.ToString();
            }
        }

        await _graphDbClient.AddOrUpdateNodeAsync(
            appServiceNode.GetNodeLabel(),
            appServiceNode.GetNodeId(),
            appServiceNode.GetResourceType(),
            appServiceNode.GetNodeProperties());

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

            var properties = appServicePlanNode.GetNodeProperties();
            properties["location"] = webApp.Data.Location;

            // Add the App Service Plan node
            await _graphDbClient.AddOrUpdateNodeAsync(
                appServicePlanNode.GetNodeLabel(),
                appServicePlanNode.GetNodeId(),
                appServicePlanNode.GetResourceType(),
                properties);

            // Create bidirectional edges
            var edge1 = new ArmResourceEdge(appServicePlanNode.GetNodeId(), appServiceNode.GetNodeId(), Constants.Relationships.Hosts);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge1.GetSourceNodeId(), edge1.GetTargetNodeId(), edge1.GetRelationship(), edge1.GetEdgeProperties());

            var edge2 = new ArmResourceEdge(appServiceNode.GetNodeId(), appServicePlanNode.GetNodeId(), Constants.Relationships.HostedOn);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2.GetSourceNodeId(), edge2.GetTargetNodeId(), edge2.GetRelationship(), edge2.GetEdgeProperties());

            _logger.LogDebug($"Created bidirectional edges between App Service {appServiceNode.ResourceId} and App Service Plan {webApp.Data.AppServicePlanId}");

            yield return appServicePlanNode;
        }

        if (!string.IsNullOrEmpty(webApp.Data.VirtualNetworkSubnetId))
        {
            var subnetId = new ResourceIdentifier(webApp.Data.VirtualNetworkSubnetId);
            var subnetNode = new ArmResourceNode(
                resourceType: subnetId.ResourceType,
                resourceId: webApp.Data.VirtualNetworkSubnetId,
                subscriptionId: subnetId.SubscriptionId,
                resourceGroupName: subnetId.ResourceGroupName,
                resourceName: subnetId.Name);

            var properties = subnetNode.GetNodeProperties();
            properties["location"] = webApp.Data.Location;

            await _graphDbClient.AddOrUpdateNodeAsync(subnetNode.GetNodeLabel(), subnetNode.GetNodeId(), subnetNode.GetResourceType(), properties);

            // add bidirectional edges for network connections
            var edge1 = new ArmResourceEdge(appServiceNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
            edge1.AddNetworkEgressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge1.GetSourceNodeId(), edge1.GetTargetNodeId(), edge1.GetRelationship(), edge1.GetEdgeProperties());

            var edge2 = new ArmResourceEdge(subnetNode.GetNodeId(), appServiceNode.GetNodeId(), Constants.Relationships.Connected);
            edge2.AddNetworkIngressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2.GetSourceNodeId(), edge2.GetTargetNodeId(), edge2.GetRelationship(), edge2.GetEdgeProperties());

            var vnetResourceId = subnetId.Parent;
            var vnetNode = new ArmResourceNode(vnetResourceId.ResourceType, vnetResourceId.ToString(), vnetResourceId.SubscriptionId, vnetResourceId.ResourceGroupName, vnetResourceId.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(vnetNode.GetNodeLabel(), vnetNode.GetNodeId(), vnetNode.GetResourceType(), vnetNode.GetNodeProperties());
            // crawl the whole vnet
            yield return vnetNode;
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
                var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_graphDbClient, appServiceNode, value);
                if (sqlNode != null)
                {
                    var properties = sqlNode.GetNodeProperties();
                    properties["authType"] = value.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase)
                        ? "managedIdentity"
                        : "connectionString";
                    properties["source"] = $"appService:appSetting:{name}";

                    await _graphDbClient.AddOrUpdateNodeAsync(
                        sqlNode.GetNodeLabel(),
                        sqlNode.GetNodeId(),
                        sqlNode.GetResourceType(),
                        properties);

                    var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

                    yield return sqlNode;
                }
            }
            // Look for Redis connection strings in app settings
            else if (IsRedisConnectionString(value))
            {
                var redisHelper = new RedisConnectionStringHelper(_logger, _armClient);
                var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_graphDbClient, appServiceNode, value);
                if (redisNode != null)
                {
                    var properties = redisNode.GetNodeProperties();
                    properties["authType"] = value.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                        ? "managedIdentity"
                        : "connectionString";
                    properties["source"] = $"appService:appSetting:{name}";

                    await _graphDbClient.AddOrUpdateNodeAsync(
                        redisNode.GetNodeLabel(),
                        redisNode.GetNodeId(),
                        redisNode.GetResourceType(),
                        properties);

                    var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), redisNode.GetNodeId(), Constants.Relationships.RedisConnected);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

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
            var sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(_graphDbClient, appServiceNode, connValue);
            if (sqlNode != null)
            {
                var properties = sqlNode.GetNodeProperties();
                properties["authType"] = connValue.Contains("Authentication=Active Directory Managed Identity", StringComparison.OrdinalIgnoreCase)
                    ? "managedIdentity"
                    : "connectionString";
                properties["source"] = "appService:connectionString";

                await _graphDbClient.AddOrUpdateNodeAsync(
                    sqlNode.GetNodeLabel(),
                    sqlNode.GetNodeId(),
                    sqlNode.GetResourceType(),
                    properties);

                var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

                _logger.LogDebug($"Linked App Service {appServiceNode.ResourceId} with SQL resource");
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
            var redisNode = await redisHelper.GetRedisResourceFromConnectionStringAsync(_graphDbClient, appServiceNode, connValue);
            if (redisNode != null)
            {
                var properties = redisNode.GetNodeProperties();
                properties["authType"] = connValue.Contains("Managed Identity", StringComparison.OrdinalIgnoreCase)
                    ? "managedIdentity"
                    : "connectionString";
                properties["source"] = "appService:connectionString";

                await _graphDbClient.AddOrUpdateNodeAsync(
                    redisNode.GetNodeLabel(),
                    redisNode.GetNodeId(),
                    redisNode.GetResourceType(),
                    properties);

                var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), redisNode.GetNodeId(), Constants.Relationships.RedisConnected);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

                _logger.LogDebug($"Linked App Service {appServiceNode.ResourceId} with Redis resource");
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

// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agent.Graph.Crawler.ARM;

public class AppServiceCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AppServiceCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly SqlConnectionStringHelper _sqlHelper;

    public AppServiceCrawler(ILogger<AppServiceCrawler> logger, IGraphDatabaseClient dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient, false)
    {
        _logger = logger;
        _graphDbClient = dbManager;
        _sqlHelper = new SqlConnectionStringHelper(logger, armClient, dbManager);
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

        // Extract basic metadata
        if (!string.IsNullOrEmpty(webApp.Data.VirtualNetworkSubnetId))
        {
            appServiceNode.VnetId = webApp.Data.VirtualNetworkSubnetId;
        }

        // Store the kind property
        appServiceNode.Kind = webApp.Data.Kind;

        // Get website configuration
        var webConfigResp = await webApp.GetWebSiteConfig().GetAsync();
        if (webConfigResp != null && webConfigResp.Value != null)
        {
            var webConfig = webConfigResp.Value;
            if (webConfig.HasData)
            {
                if (webConfig.Data.MinTlsVersion != null)
                {
                    appServiceNode.MinTlsVersion = webConfig.Data.MinTlsVersion.ToString();
                }

                // Get stack version from site config
                appServiceNode.StackVersion = GetStackVersion(webConfig.Data);

                // Set additional properties from site config
                appServiceNode.AlwaysOn = webConfig.Data.IsAlwaysOn;
                appServiceNode.AutoHealEnabled = webConfig.Data.IsAutoHealEnabled;
                appServiceNode.NumberOfWorkers = webConfig.Data.NumberOfWorkers;
                appServiceNode.HealthCheckEnabled = !string.IsNullOrEmpty(webConfig.Data.HealthCheckPath);
                appServiceNode.WebSocketsEnabled = webConfig.Data.IsWebSocketsEnabled;
            }
        }

        // Get app settings to check for Functions host version and App Insights
        var appSettingsResponse = await webApp.GetApplicationSettingsAsync();
        var appSettings = appSettingsResponse.Value.Properties;

        // Check for Functions host version and runtime
        if (appServiceNode.Kind?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (appSettings.TryGetValue("FUNCTIONS_EXTENSION_VERSION", out var functionsVersion))
            {
                appServiceNode.FunctionsHostVersion = functionsVersion;
            }

            // Also capture the worker runtime for Functions
            if (appSettings.TryGetValue("FUNCTIONS_WORKER_RUNTIME", out var workerRuntime))
            {
                // If we have a worker runtime, use it to enhance or set the stack version
                if (!string.IsNullOrEmpty(workerRuntime))
                {
                    // If we don't have a stack version yet, set it from the worker runtime
                    if (string.IsNullOrEmpty(appServiceNode.StackVersion))
                    {
                        appServiceNode.StackVersion = workerRuntime;
                    }

                    appServiceNode.WorkerRuntime = workerRuntime;
                }
            }
        }

        // Check for App Insights
        appServiceNode.UsesAppInsights = appSettings.Any(s =>
            s.Key.Contains("APPINSIGHTS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(s.Value));

        // Get hostnames including custom domains
        if (webApp.Data.HostNames != null && webApp.Data.HostNames.Count > 0)
        {
            appServiceNode.HostNames.AddRange(webApp.Data.HostNames);
            _logger.LogDebug($"Found {webApp.Data.HostNames.Count} hostnames for {appServiceNode.ResourceId}");
        }
        else
        {
            // Fallback to default hostname if available
            if (!string.IsNullOrEmpty(webApp.Data.DefaultHostName))
            {
                appServiceNode.HostNames.Add(webApp.Data.DefaultHostName);
                _logger.LogDebug($"Using default hostname for {appServiceNode.ResourceId}: {webApp.Data.DefaultHostName}");
            }
        }

        // Additionally, try to retrieve custom hostnames binding information
        try
        {
            var hostnameBindings = webApp.GetSiteHostNameBindings();
            var bindingsList = await hostnameBindings.GetAllAsync().ToListAsync();

            foreach (var binding in bindingsList)
            {
                string hostnameValue = null;

                // Handle the case where binding.Data.Name is in format "app-name/hostname.domain.com"
                if (!string.IsNullOrEmpty(binding.Data.Name) && binding.Data.Name.Contains("/"))
                {
                    hostnameValue = binding.Data.Name.Split('/').Last();
                }
                // Try to use hostname property directly if available
                else if (!string.IsNullOrEmpty(binding.Data.Name))
                {
                    hostnameValue = binding.Data.Name;
                }
                // Fallback to name if no slash is present
                else if (!string.IsNullOrEmpty(binding.Data.Name))
                {
                    hostnameValue = binding.Data.Name;
                }

                // Add the hostname if it's valid, unique, and not an azurewebsites.net domain
                if (!string.IsNullOrEmpty(hostnameValue) &&
                    !appServiceNode.HostNames.Contains(hostnameValue) &&
                    !hostnameValue.Contains(".azurewebsites.net", StringComparison.OrdinalIgnoreCase))
                {
                    appServiceNode.HostNames.Add(hostnameValue);
                    _logger.LogDebug($"Found custom domain binding: {hostnameValue}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to get hostname bindings for {webApp.Data.Id}: {ex.Message}");
        }

        await _graphDbClient.AddOrUpdateNodeAsync(appServiceNode);

        // Link to App Service Plan if it exists
        if (!string.IsNullOrEmpty(webApp.Data.AppServicePlanId))
        {
            var planId = new ResourceIdentifier(webApp.Data.AppServicePlanId);
            var appServicePlanNode = new AppServicePlanNode(
                resourceType: "Microsoft.Web/serverfarms",
                resourceId: webApp.Data.AppServicePlanId,
                subscriptionId: planId.SubscriptionId,
                resourceName: planId.Name,
                location: webApp.Data.Location);

            // TODO: this should be only put on appserviceplan node
            appServiceNode.PlanType = await GetAppServicePlanTypeAsync(webApp.Data.AppServicePlanId);
            await _graphDbClient.AddOrUpdateNodeAsync(appServiceNode);

            // Add the App Service Plan node
            await _graphDbClient.AddOrUpdateNodeAsync(appServicePlanNode);

            // Create bidirectional edges
            var edge1 = new ArmResourceEdge(appServicePlanNode.GetNodeId(), appServiceNode.GetNodeId(), Constants.Relationships.Hosts);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge1);

            var edge2 = new ArmResourceEdge(appServiceNode.GetNodeId(), appServicePlanNode.GetNodeId(), Constants.Relationships.HostedOn);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2);

            _logger.LogDebug($"Created bidirectional edges between App Service {appServiceNode.ResourceId} and App Service Plan {webApp.Data.AppServicePlanId}");

            yield return appServicePlanNode;
        }

        // Link to VNet if available
        if (!string.IsNullOrEmpty(webApp.Data.VirtualNetworkSubnetId))
        {
            var subnetId = new ResourceIdentifier(webApp.Data.VirtualNetworkSubnetId);
            var subnetNode = new ArmResourceNode(
                resourceType: subnetId.ResourceType,
                resourceId: webApp.Data.VirtualNetworkSubnetId,
                subscriptionId: subnetId.SubscriptionId,
                resourceGroupName: subnetId.ResourceGroupName,
                resourceName: subnetId.Name,
                location: webApp.Data.Location);

            await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);

            // add bidirectional edges for network connections
            var edge1 = new ArmResourceEdge(appServiceNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
            edge1.AddNetworkEgressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge1);

            var edge2 = new ArmResourceEdge(subnetNode.GetNodeId(), appServiceNode.GetNodeId(), Constants.Relationships.Connected);
            edge2.AddNetworkIngressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2);

            var vnetResourceId = subnetId.Parent;
            var vnetNode = new ArmResourceNode(vnetResourceId.ResourceType, vnetResourceId.ToString(), vnetResourceId.SubscriptionId, vnetResourceId.ResourceGroupName, vnetResourceId.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);
            // crawl the whole vnet
            yield return vnetNode;
        }

        // Process app settings for connection strings
        foreach (var setting in appSettings)
        {
            var name = setting.Key;
            var value = setting.Value;
            if (string.IsNullOrEmpty(value)) continue;

            // Look for SQL connection strings in app settings
            if (_sqlHelper.IsSqlConnectionString(value))
            {
                var sqlNode = await _sqlHelper.GetSqlResourceFromConnectionStringAsync(appServiceNode, value, "appService:appSetting", name);
                if (sqlNode != null)
                {
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

                    await _graphDbClient.AddOrUpdateNodeAsync(redisNode);

                    var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), redisNode.GetNodeId(), Constants.Relationships.RedisConnected);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                    yield return redisNode;
                }
            }
        }
    }

    private string GetStackVersion(SiteConfigData config)
    {
        // For Linux web apps, use LinuxFxVersion directly
        if (!string.IsNullOrEmpty(config.LinuxFxVersion))
        {
            return config.LinuxFxVersion;
        }
        // For Windows web apps, use WindowsFxVersion if available
        else if (!string.IsNullOrEmpty(config.WindowsFxVersion))
        {
            return config.WindowsFxVersion;
        }
        // Fallback to other runtime properties if needed
        else if (!string.IsNullOrEmpty(config.NetFrameworkVersion))
        {
            return $"dotnet:{config.NetFrameworkVersion}";
        }
        else if (!string.IsNullOrEmpty(config.JavaVersion))
        {
            return $"java:{config.JavaVersion}";
        }
        else if (!string.IsNullOrEmpty(config.PhpVersion))
        {
            return $"php:{config.PhpVersion}";
        }
        else if (!string.IsNullOrEmpty(config.PythonVersion))
        {
            return $"python:{config.PythonVersion}";
        }
        else if (!string.IsNullOrEmpty(config.NodeVersion))
        {
            return $"node:{config.NodeVersion}";
        }

        return null;
    }

    private async Task<string> GetAppServicePlanTypeAsync(string appServicePlanId)
    {
        try
        {
            var planResourceId = new ResourceIdentifier(appServicePlanId);
            var planResource = _armClient.GetAppServicePlanResource(planResourceId);
            var plan = await planResource.GetAsync();

            if (plan.Value != null && plan.Value.Data != null)
            {
                var sku = plan.Value.Data.Sku;
                if (sku != null)
                {
                    // Check for elastic premium (EP) or consumption (Y)
                    if (sku.Tier?.Equals("ElasticPremium", StringComparison.OrdinalIgnoreCase) == true ||
                        sku.Name?.StartsWith("EP", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return "ElasticPremium";
                    }
                    else if (sku.Tier?.Equals("Dynamic", StringComparison.OrdinalIgnoreCase) == true ||
                             sku.Name?.StartsWith("Y", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        return "FlexConsumption";
                    }
                    else
                    {
                        // Return the actual SKU tier or name if not a special case
                        return sku.Tier ?? sku.Name;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to get plan type for {appServicePlanId}: {ex.Message}");
        }

        return null;
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
}

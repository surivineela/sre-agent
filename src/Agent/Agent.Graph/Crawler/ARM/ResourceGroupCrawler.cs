using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class ResourceGroupCrawler : IArmResourceCrawler
    {
        private readonly ILogger<ResourceGroupCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;
        private readonly AzureResourceGraphClient _graphClient;

        public ResourceGroupCrawler(ILogger<ResourceGroupCrawler> logger, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
            _graphClient = graphClient;
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            var rgNode = (ResourceGroupNode)node;
            _logger.LogInformation($"Crawling resource group {rgNode.ResourceGroupName}");

            await _dbManager.AddOrUpdateNodeAsync(
                rgNode.GetNodeLabel(),
                rgNode.GetNodeId(),
                rgNode.GetResourceType(),
                rgNode.GetNodeProperties());

            // Query 1: Container App Environments
            var managedEnvsQuery = await _graphClient.Query(
                new[] { rgNode.SubscriptionId },
                $"Resources | where type =~ 'Microsoft.App/managedEnvironments' and resourceGroup =~ '{rgNode.ResourceGroupName}' | project id, type, subscriptionId, resourceGroup, name, location");
            _logger.LogInformation($"Found {managedEnvsQuery.Count} container app environments under {rgNode.ResourceGroupName}");
            var managedEnvsJson = JsonSerializer.Deserialize<JsonElement>(managedEnvsQuery.Data);
            foreach (var item in managedEnvsJson.EnumerateArray())
            {
                var envNode = CreateNodeFromJson(item, (resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) =>
                    new ContainerAppEnvironmentNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location));
                if (envNode != null)
                {
                    await _dbManager.AddOrUpdateNodeAsync(
                        envNode.GetNodeLabel(), envNode.GetNodeId(), envNode.GetResourceType(), envNode.GetNodeProperties());
                    await _dbManager.AddEdgeIfNotExistsAsync(rgNode.GetNodeId(), envNode.GetNodeId(), "CONTAINS");
                    yield return envNode;
                }
            }

            // Query 2: App Service Plans (serverFarms)
            var appServicePlansQuery = await _graphClient.Query(
                new[] { rgNode.SubscriptionId },
                $"Resources | where type =~ 'Microsoft.Web/serverFarms' and resourceGroup =~ '{rgNode.ResourceGroupName}'  | project id, type, subscriptionId, resourceGroup, name, location");
            _logger.LogInformation($"Found {appServicePlansQuery.Count} app service plans under {rgNode.ResourceGroupName}");
            var plansJson = JsonSerializer.Deserialize<JsonElement>(appServicePlansQuery.Data);
            foreach (var item in plansJson.EnumerateArray())
            {
                var planNode = CreateNodeFromJson(item, (resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) =>
                    new ArmResourceNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName));
                if (planNode != null)
                {
                    await _dbManager.AddOrUpdateNodeAsync(
                        planNode.GetNodeLabel(), planNode.GetNodeId(), planNode.GetResourceType(), planNode.GetNodeProperties());
                    await _dbManager.AddEdgeIfNotExistsAsync(rgNode.GetNodeId(), planNode.GetNodeId(), "CONTAINS");
                    yield return planNode;
                }
                Thread.Sleep(500);
            }

            // Query 3: App Services (Web Apps)
            var webAppsQuery = await _graphClient.Query(
                new[] { rgNode.SubscriptionId },
                $"Resources | where type =~ 'Microsoft.Web/sites' and resourceGroup =~ '{rgNode.ResourceGroupName}' " +
                "| extend serverFarmId = tostring(properties.serverFarmId), virtualNetworkSubnetId = tostring(properties.virtualNetworkSubnetId) " +
                "| project id, type, subscriptionId, resourceGroup, name, location, serverFarmId, virtualNetworkSubnetId");

            _logger.LogInformation($"Found {webAppsQuery.Count} web apps under {rgNode.SubscriptionId}");
            var webAppsJson = JsonSerializer.Deserialize<JsonElement>(webAppsQuery.Data);
            foreach (var item in webAppsJson.EnumerateArray())
            {
                var webAppNode = CreateNodeFromJson(item, (rt, id, subId, rg, name, loc) =>
                    new ArmResourceNode(rt, id, subId, rg, name));
                if (webAppNode != null)
                {
                    await _dbManager.AddOrUpdateNodeAsync(
                        webAppNode.GetNodeLabel(),
                        webAppNode.GetNodeId(),
                        webAppNode.GetResourceType(),
                        webAppNode.GetNodeProperties());

                    // First, create the edge from subscription to web app as a fallback
                    var subscriptionEdgeCreated = false;

                    // If a serverFarmId is present, create the edge from server farm to web app
                    if (item.TryGetProperty("serverFarmId", out var sFarmProp))
                    {
                        var serverFarmId = sFarmProp.GetString();
                        if (!string.IsNullOrEmpty(serverFarmId))
                        {
                            // Sanitize the ID for the server farm
                            var sanitizedServerFarmId = GetSanitizedCosmosDBId(serverFarmId);

                            // Create edge from server farm to web app
                            await _dbManager.AddEdgeIfNotExistsAsync(
                                sanitizedServerFarmId,
                                webAppNode.GetNodeId(),
                                "HOSTS");  // Changed relationship type to be more descriptive

                            subscriptionEdgeCreated = true;
                        }
                    }

                    // If no server farm connection was made, link directly to subscription
                    if (!subscriptionEdgeCreated)
                    {
                        await _dbManager.AddEdgeIfNotExistsAsync(
                            rgNode.GetNodeId(),
                            webAppNode.GetNodeId(),
                            "CONTAINS");
                    }

                    // Handle subnet connection if present
                    if (item.TryGetProperty("virtualNetworkSubnetId", out var subnetProp))
                    {
                        var subnetId = subnetProp.GetString();
                        if (!string.IsNullOrEmpty(subnetId))
                        {
                            var subnetNode = new ArmResourceNode(
                                resourceType: "Microsoft.Network/virtualNetworks/subnets",
                                resourceId: subnetId,
                                subscriptionId: webAppNode.SubscriptionId,
                                resourceGroupName: ExtractResourceGroupName(subnetId),
                                resourceName: ExtractResourceName(subnetId));

                            await _dbManager.AddOrUpdateNodeAsync(
                                subnetNode.GetNodeLabel(),
                                subnetNode.GetNodeId(),
                                subnetNode.GetResourceType(),
                                subnetNode.GetNodeProperties());

                            // Create edge from web app to subnet
                            await _dbManager.AddEdgeIfNotExistsAsync(
                                webAppNode.GetNodeId(),
                                subnetNode.GetNodeId(),
                                "USES_SUBNET");  // Changed relationship type to be more descriptive
                        }
                    }
                    yield return webAppNode;
                }
            }

            // --- Query 4: Kubernetes Clusters (AKS) ---
            var aksQuery = await _graphClient.Query(
                new[] { rgNode.SubscriptionId },
                $"Resources | where type =~ 'Microsoft.ContainerService/managedClusters' and resourceGroup =~ '{rgNode.ResourceGroupName}'  | project id, type, subscriptionId, resourceGroup, name, location");
            _logger.LogInformation($"Found {aksQuery.Count} AKS clusters under {rgNode.ResourceGroupName}");
            var aksJson = JsonSerializer.Deserialize<JsonElement>(aksQuery.Data);
            foreach (var item in aksJson.EnumerateArray())
            {
                var aksNode = CreateNodeFromJson(item, (rt, id, subId, rg, name, loc) =>
                    new ArmResourceNode(rt, id, subId, rg, name));

                if (aksNode != null)
                {
                    // Add the AKS cluster node and connect it to the subscription
                    await _dbManager.AddOrUpdateNodeAsync(
                        aksNode.GetNodeLabel(),
                        aksNode.GetNodeId(),
                        aksNode.GetResourceType(),
                        aksNode.GetNodeProperties());

                    await _dbManager.AddEdgeIfNotExistsAsync(
                        rgNode.GetNodeId(),
                        aksNode.GetNodeId(),
                        "CONTAINS");

                    // Yield the AKS cluster node
                    yield return aksNode;

                    // Add a small delay to prevent throttling
                    Thread.Sleep(500);
                }
            }
        }

        private string GetSanitizedCosmosDBId(string id)
        {
            return id.Replace("/", "_").Replace(":", "_").Replace(" ", "_");
        }

        private string ExtractResourceName(string resourceId)
        {
            var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments.Length > 0 ? segments[segments.Length - 1] : string.Empty;
        }

        // Helper to create an ArmResourceNode from a JSON element using the provided factory function.
        private ArmResourceNode CreateNodeFromJson(JsonElement item, Func<string, string, string, string, string, string, ArmResourceNode> factory)
        {
            try
            {
                var resourceId = item.GetProperty("id").GetString();
                var resourceType = item.GetProperty("type").GetString();
                var subscriptionId = item.GetProperty("subscriptionId").GetString();
                var resourceGroupName = item.GetProperty("resourceGroup").GetString();
                var resourceName = item.GetProperty("name").GetString();
                var location = item.GetProperty("location").GetString();
                return factory(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating node from JSON: {ex.Message}");
                return null;
            }
        }

        // Helper method to extract the resource group name from a resource ID.
        private string ExtractResourceGroupName(string resourceId)
        {
            // Expected format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/...
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

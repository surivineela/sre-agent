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
        private readonly AzureResourceGraphClient _graphClient;

        public ResourceGroupCrawler(ILogger<ResourceGroupCrawler> logger, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient)
        {
            _logger = logger;
            _dbManager = dbManager;
            _graphClient = graphClient;
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            var rgNode = (ResourceGroupNode)node;
            _logger.LogDebug($"Crawling resource group {rgNode.ResourceGroupName}");

            await _dbManager.AddOrUpdateNodeAsync(
                rgNode.GetNodeLabel(),
                rgNode.GetNodeId(),
                rgNode.GetResourceType(),
                rgNode.GetNodeProperties());

            // get all resources under resource group
            var resources = await _graphClient.Query(
                new[] { rgNode.SubscriptionId },
                $"resources | where resourceGroup =~ '{rgNode.ResourceGroupName}' | project id, type, subscriptionId, resourceGroup, name, location");
            _logger.LogDebug($"Found {resources.Count} resources under {rgNode.ResourceGroupName}");

            var resourcesJson = JsonSerializer.Deserialize<JsonElement>(resources.Data);
            foreach (var resource in resourcesJson.EnumerateArray())
            {
                var type = resource.GetProperty("type").GetString();
                // Container App Environments
                if(Constants.ContainerAppEnvironmentType.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    var envNode = CreateNodeFromJson(resource, (resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) =>
                        new ContainerAppEnvironmentNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location));
                    if (envNode != null)
                    {
                        await _dbManager.AddOrUpdateNodeAsync(
                            envNode.GetNodeLabel(), envNode.GetNodeId(), envNode.GetResourceType(), envNode.GetNodeProperties());

                        var edge = new ArmResourceEdge(rgNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Contains);
                        edge.AddRbacInheritedEdgeProperties();
                        await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                        yield return envNode;
                    }
                }
                else if(Constants.AppServicePlanType.Equals(type, StringComparison.OrdinalIgnoreCase)){
                    var planNode = CreateNodeFromJson(resource, (resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) =>
                        new ArmResourceNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName));
                    if (planNode != null)
                    {
                        await _dbManager.AddOrUpdateNodeAsync(
                            planNode.GetNodeLabel(), planNode.GetNodeId(), planNode.GetResourceType(), planNode.GetNodeProperties());

                        var edge = new ArmResourceEdge(rgNode.GetNodeId(), planNode.GetNodeId(), Constants.Relationships.Contains);
                        edge.AddRbacInheritedEdgeProperties();
                        await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                        yield return planNode;
                    }
                }
                else if(Constants.AppServiceType.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    var webAppNode = CreateNodeFromJson(resource, (rt, id, subId, rg, name, loc) =>
                        new ArmResourceNode(rt, id, subId, rg, name));
                    if (webAppNode != null)
                    {
                        await _dbManager.AddOrUpdateNodeAsync(webAppNode.GetNodeLabel(), webAppNode.GetNodeId(), webAppNode.GetResourceType(), webAppNode.GetNodeProperties());

                        var edge = new ArmResourceEdge(rgNode.GetNodeId(), webAppNode.GetNodeId(), Constants.Relationships.Contains);
                        edge.AddRbacInheritedEdgeProperties();
                        await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                        yield return webAppNode;
                    }
                }
                else if (Constants.AzureKubernetesServiceType.Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    var aksNode = CreateNodeFromJson(resource, (rt, id, subId, rg, name, loc) =>
                        new ArmResourceNode(rt, id, subId, rg, name));
                    if (aksNode != null)
                    {
                        await _dbManager.AddOrUpdateNodeAsync(aksNode.GetNodeLabel(), aksNode.GetNodeId(), aksNode.GetResourceType(), aksNode.GetNodeProperties());

                        var edge = new ArmResourceEdge(rgNode.GetNodeId(), aksNode.GetNodeId(), Constants.Relationships.Contains);
                        edge.AddRbacInheritedEdgeProperties();
                        await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                        yield return aksNode;
                    }
                }
                else
                {
                    var genericNode = CreateNodeFromJson(resource, (rt, id, subId, rg, name, loc) =>
                        new ArmResourceNode(rt, id, subId, rg, name));
                    if (genericNode != null)
                    {
                        await _dbManager.AddOrUpdateNodeAsync(genericNode.GetResourceType(), genericNode.GetNodeId(), genericNode.GetResourceType(), genericNode.GetNodeProperties());

                        var edge = new ArmResourceEdge(rgNode.GetNodeId(), genericNode.GetNodeId(), Constants.Relationships.Contains);
                        edge.AddRbacInheritedEdgeProperties();
                        await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                    }
                    // do not return node because we only crawl specific resource types here
                }
            }
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
    }
}

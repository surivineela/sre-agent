// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ResourceGroupCrawler : IResourceCrawler
{
    private readonly ILogger<ResourceGroupCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly AzureResourceGraphClient _graphClient;
    private readonly ArmClient _armClient;

    public ResourceGroupCrawler(ILogger<ResourceGroupCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _graphClient = graphClient;
        _armClient = armClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var rgNode = (ResourceGroupNode)node;
        _logger.LogDebug($"Crawling resource group {rgNode.ResourceGroupName}");

        var rgResource = _armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(rgNode.SubscriptionId, rgNode.ResourceGroupName));
        var resp = await rgResource.GetAsync();
        if (resp != null && resp.Value.HasData)
        {
            rgNode.Location = resp.Value.Data.Location;
        }
        await _graphDbClient.AddOrUpdateNodeAsync(rgNode);

        // add or update subscription node for rg subscription
        var subNode = new SubscriptionNode(rgNode.SubscriptionId);
        var subscription = _armClient.GetSubscriptions().Get(subNode.SubscriptionId);
        var subName = subscription?.Value?.Data?.DisplayName;
        var nodeProperties = subNode.GetNodeProperties();
        nodeProperties["subscriptionName"] = subName;

        await _graphDbClient.AddOrUpdateNodeAsync(
               subNode.GetNodeLabel(),
               subNode.GetNodeId(),
               subNode.GetResourceType(),
               nodeProperties);

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
            if (Constants.ContainerAppEnvironmentType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var envNode = CreateNodeFromJson(resource);
                if (envNode != null)
                {
                    envNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(envNode);

                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return envNode;
                }
            }
            else if (Constants.AppServicePlanType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var planNode = CreateNodeFromJson(resource);
                if (planNode != null)
                {
                    planNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(planNode);

                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), planNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return planNode;
                }
            }
            else if (Constants.AppServiceType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var webAppNode = CreateNodeFromJson(resource);
                if (webAppNode != null)
                {
                    webAppNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(webAppNode);

                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), webAppNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return webAppNode;
                }
            }
            else if (Constants.AzureKubernetesServiceType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var aksNode = CreateNodeFromJson(resource);
                if (aksNode != null)
                {
                    aksNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(aksNode);

                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), aksNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return aksNode;
                }
            }
            else if (Constants.StorageType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var storageNode = CreateNodeFromJson(resource);
                if (storageNode != null)
                {
                    storageNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(storageNode);
                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), storageNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return storageNode;
                }
            }
            else if (Constants.KeyVaultType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var kvNode = CreateNodeFromJson(resource);
                if (kvNode != null)
                {
                    kvNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(kvNode);
                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), kvNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return kvNode;
                }
            }
            else if (Constants.ManagedDiskType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var diskNode = CreateNodeFromJson(resource);
                if (diskNode != null)
                {
                    diskNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(diskNode);
                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), diskNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return diskNode;
                }
            }
            else if (Constants.PostgreSqlFlexServerType.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                var pgNode = CreateNodeFromJson(resource);
                if (pgNode != null)
                {
                    pgNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(pgNode);
                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), pgNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return pgNode;
                }
            }
            else
            {
                var genericNode = CreateNodeFromJson(resource);
                if (genericNode != null)
                {
                    genericNode.Location = resource.GetProperty("location").GetString();
                    await _graphDbClient.AddOrUpdateNodeAsync(genericNode);

                    var edge = new ArmResourceEdge(rgNode.GetNodeId(), genericNode.GetNodeId(), Constants.Relationships.Contains);
                    edge.AddRbacInheritedEdgeProperties();
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                }
                // do not return node because we only crawl specific resource types here
            }
        }
    }

    // Helper to create an ArmResourceNode from a JSON element using the provided factory function.
    private ArmResourceNode CreateNodeFromJson(JsonElement item)
    {
        try
        {
            var resourceId = item.GetProperty("id").GetString();

            return ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(resourceId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Error creating node from JSON: {ex.Message}");
            return null;
        }
    }
}


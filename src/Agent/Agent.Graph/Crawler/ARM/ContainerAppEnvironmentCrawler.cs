// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ContainerAppEnvironmentCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<ContainerAppEnvironmentCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly AzureResourceGraphClient _graphClient;

    public ContainerAppEnvironmentCrawler(ILogger<ContainerAppEnvironmentCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
        : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _graphClient = graphClient;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var envNode = (ContainerAppEnvironmentNode)node;
        _logger.LogInternalInformation($"Crawling container app environment: {envNode.ResourceId}");

        var rgResourceId = ResourceGroupResource.CreateResourceIdentifier(envNode.SubscriptionId, envNode.ResourceGroupName);
        var rgResource = _armClient.GetResourceGroupResource(rgResourceId);
        if (rgResource == null)
        {
            _logger.LogInternalWarning($"Failed to get container app environment: {envNode.ResourceId}");
            yield break;
        }

        var envResp = await rgResource.GetContainerAppManagedEnvironmentAsync(envNode.ResourceName);

        if (envResp == null || !envResp.Value.HasData)
        {
            _logger.LogInternalWarning($"Failed to get container app environment: {envNode.ResourceId}");
            yield break;
        }

        // update current node properties
        var env = envResp.Value.Data;
        envNode.Internal = env.VnetConfiguration?.IsInternal;
        envNode.InfrastructureSubnetId = env.VnetConfiguration?.InfrastructureSubnetId;
        envNode.StaticIp = env.StaticIP.ToString();
        envNode.LogDestination = env.AppLogsConfiguration.Destination;
        envNode.ZoneRedundant = env.IsZoneRedundant;
        envNode.CustomDomain = env.CustomDomainConfiguration?.DnsSuffix;

        if (env.WorkloadProfiles.Count > 0)
        {
            foreach (var workloadProfile in env.WorkloadProfiles)
            {
                var profile = new ContainerAppEnvironmentNode.WorkloadProfileName
                {
                    Name = workloadProfile.Name,
                    Type = workloadProfile.WorkloadProfileType.ToString(),
                    MinimumCount = workloadProfile.MinimumCount,
                    MaximumCount = workloadProfile.MaximumCount
                };
                envNode.WorkloadProfiles.Add(profile);
            }
        }

        envNode.InfrastructureResourceGroup = env.InfrastructureResourceGroup;
        envNode.PublicNetworkAccess = env.PublicNetworkAccess.ToString();

        if (!string.IsNullOrEmpty(envNode.InfrastructureSubnetId))
        {
            // TODO: load balancers for shared env
            if (string.IsNullOrEmpty(env.InfrastructureResourceGroup))
            {
                envNode.LbId = $"/subscriptions/{envNode.SubscriptionId}/resourceGroups/ME_{envNode.ResourceName}_{envNode.ResourceGroupName}_{envNode.Location}/providers/Microsoft.Network/loadBalancers/capp-svc-lb";
            }
            else
            {
                envNode.LbId = $"/subscriptions/{envNode.SubscriptionId}/resourceGroups/{env.InfrastructureResourceGroup}/providers/Microsoft.Network/loadBalancers/capp-svc-lb";
            }
        }

        await _graphDbClient.AddOrUpdateNodeAsync(envNode);

        // network
        if (env.VnetConfiguration?.InfrastructureSubnetId is not null)
        {
            var id = env.VnetConfiguration?.InfrastructureSubnetId;

            // subnet
            var subnetResourceId = new ResourceIdentifier(id);
            var subnetNode = new ArmResourceNode(subnetResourceId.ResourceType, id, subnetResourceId.SubscriptionId, subnetResourceId.ResourceGroupName, subnetResourceId.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);

            var edge1 = new ArmResourceEdge(envNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
            edge1.AddNetworkEgressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge1);

            var edge2 = new ArmResourceEdge(subnetNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Connected);
            edge2.AddNetworkIngressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2);

            var vnetResourceId = subnetResourceId.Parent;
            var vnetNode = new ArmResourceNode(vnetResourceId.ResourceType, vnetResourceId.ToString(), vnetResourceId.SubscriptionId, vnetResourceId.ResourceGroupName, vnetResourceId.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);
            // crawl the whole vnet
            yield return vnetNode;

            var lbId = envNode.LbId;
            var lbResourceId = new ResourceIdentifier(lbId);
            var lbNode = new ArmResourceNode(lbResourceId.ResourceType, lbId, lbResourceId.SubscriptionId, lbResourceId.ResourceGroupName, lbResourceId.Name);
            await _graphDbClient.AddOrUpdateNodeAsync(lbNode);

            var edge = new ArmResourceEdge(lbNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Connected);
            edge.AddNetworkEgressEdgeProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return lbNode;
        }

        // LA

        // container apps
        var queryResult = await _graphClient.Query([envNode.SubscriptionId], $"resources|where type =~ 'Microsoft.App/containerApps' and properties.environmentId =~ '{envNode.ResourceId}'| project id, type, subscriptionId, resourceGroup, name");

        _logger.LogDebug($"Find {queryResult.Count} container apps under environment");
        var jsonObj = JsonSerializer.Deserialize<JsonElement>(queryResult.Data);
        foreach (var item in jsonObj.EnumerateArray())
        {
            var resourceId = item.GetProperty("id").GetString();
            var resourceType = item.GetProperty("type").GetString();
            var subscriptionId = item.GetProperty("subscriptionId").GetString();
            var resourceGroupName = item.GetProperty("resourceGroup").GetString();
            var resourceName = item.GetProperty("name").GetString();
            var containerAppNode = new ContainerAppNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName);

            await _graphDbClient.AddOrUpdateNodeAsync(containerAppNode);

            var edge = new ArmResourceEdge(envNode.GetNodeId(), containerAppNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return containerAppNode;
        }

        yield break;
    }
}


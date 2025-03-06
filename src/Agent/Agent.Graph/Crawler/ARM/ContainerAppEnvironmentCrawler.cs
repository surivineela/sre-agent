using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class ContainerAppEnvironmentCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<ContainerAppEnvironmentCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly AzureResourceGraphClient _graphClient;

        public ContainerAppEnvironmentCrawler(ILogger<ContainerAppEnvironmentCrawler> logger, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient, ArmClient armClient)
            : base(logger, dbManager, armClient, false)
        {
            _logger = logger;
            _dbManager = dbManager;
            _graphClient = graphClient;
        }

        public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            await foreach (var n in base.Crawl(node))
            {
                yield return n;
            }

            var envNode = (ContainerAppEnvironmentNode)node;
            _logger.LogDebug($"Crawling container app environment: {envNode.ResourceId}");

            var rgResourceId = ResourceGroupResource.CreateResourceIdentifier(envNode.SubscriptionId, envNode.ResourceGroupName);
            var rgResource = _armClient.GetResourceGroupResource(rgResourceId);
            if (rgResource == null)
            {
                _logger.LogWarning($"Failed to get container app environment: {envNode.ResourceId}");
                yield break;
            }

            var env = await rgResource.GetContainerAppManagedEnvironmentAsync(envNode.ResourceName);

            if (env == null || !env.Value.HasData)
            {
                _logger.LogWarning($"Failed to get container app environment: {envNode.ResourceId}");
                yield break;
            }

            // update current node properties
            envNode.Location = env.Value.Data.Location;
            envNode.VnetId = env.Value.Data.VnetConfiguration?.InfrastructureSubnetId;
            if (!string.IsNullOrEmpty(envNode.VnetId))
            {
                if (string.IsNullOrEmpty(env.Value.Data.InfrastructureResourceGroup))
                {
                    envNode.LbId = $"/subscriptions/{envNode.SubscriptionId}/resourceGroups/ME_{envNode.ResourceName}_{envNode.ResourceGroupName}_{envNode.Location}/providers/Microsoft.Network/loadBalancers/capp-svc-lb";
                }
                else
                {
                    envNode.LbId = $"/subscriptions/{envNode.SubscriptionId}/resourceGroups/{env.Value.Data.InfrastructureResourceGroup}/providers/Microsoft.Network/loadBalancers/capp-svc-lb";
                }
            }

            await _dbManager.AddOrUpdateNodeAsync(envNode.GetNodeLabel(), envNode.GetNodeId(), envNode.GetResourceType(), envNode.GetNodeProperties());

            // network
            if (env.Value.Data.VnetConfiguration?.InfrastructureSubnetId is not null)
            {
                var id = env.Value.Data.VnetConfiguration?.InfrastructureSubnetId;

                // subnet
                var subnetResourceId = new ResourceIdentifier(id);
                var subnetNode = new ArmResourceNode(subnetResourceId.ResourceType, id, subnetResourceId.SubscriptionId, subnetResourceId.ResourceGroupName, subnetResourceId.Name);
                await _dbManager.AddOrUpdateNodeAsync(subnetNode.GetNodeLabel(), subnetNode.GetNodeId(), subnetNode.GetResourceType(), subnetNode.GetNodeProperties());

                var edge1 = new ArmResourceEdge(envNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
                edge1.AddNetworkEgressEdgeProperties();
                await _dbManager.AddOrUpdateEdgeAsync(edge1.GetSourceNodeId(), edge1.GetTargetNodeId(), edge1.GetRelationship(), edge1.GetEdgeProperties());

                var edge2 = new ArmResourceEdge(subnetNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Connected);
                edge2.AddNetworkIngressEdgeProperties();
                await _dbManager.AddOrUpdateEdgeAsync(edge2.GetSourceNodeId(), edge2.GetTargetNodeId(), edge2.GetRelationship(), edge2.GetEdgeProperties());

                var vnetResourceId = subnetResourceId.Parent;
                var vnetNode = new ArmResourceNode(vnetResourceId.ResourceType, vnetResourceId.ToString(), vnetResourceId.SubscriptionId, vnetResourceId.ResourceGroupName, vnetResourceId.Name);
                await _dbManager.AddOrUpdateNodeAsync(vnetNode.GetNodeLabel(), vnetNode.GetNodeId(), vnetNode.GetResourceType(), vnetNode.GetNodeProperties());
                // crawl the whole vnet
                yield return vnetNode;

                var lbId = envNode.LbId;
                var lbResourceId = new ResourceIdentifier(lbId);
                var lbNode = new ArmResourceNode(lbResourceId.ResourceType, lbId, lbResourceId.SubscriptionId, lbResourceId.ResourceGroupName, lbResourceId.Name);
                await _dbManager.AddOrUpdateNodeAsync(lbNode.GetNodeLabel(), lbNode.GetNodeId(), lbNode.GetResourceType(), lbNode.GetNodeProperties());

                var edge = new ArmResourceEdge(lbNode.GetNodeId(), envNode.GetNodeId(), Constants.Relationships.Connected);
                edge.AddNetworkEgressEdgeProperties();
                await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
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
                var containerAppNode = new ArmResourceNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName);

                await _dbManager.AddOrUpdateNodeAsync(containerAppNode.GetNodeLabel(), containerAppNode.GetNodeId(), containerAppNode.GetResourceType(), containerAppNode.GetNodeProperties());

                var edge = new ArmResourceEdge(envNode.GetNodeId(), containerAppNode.GetNodeId(), Constants.Relationships.Contains);
                await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                yield return containerAppNode;
            }

            yield break;
        }
    }
}

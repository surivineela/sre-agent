using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Schema;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.ManagedServiceIdentities;
using Azure.ResourceManager.Models;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class ContainerAppEnvironmentCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<ContainerAppEnvironmentCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly ArmClient _armClient;
        private readonly AzureResourceGraphClient _graphClient;

        public ContainerAppEnvironmentCrawler(ILogger<ContainerAppEnvironmentCrawler> logger, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient)
            : base(logger, dbManager, false)
        {
            _logger = logger;
            _dbManager = dbManager;
            _armClient = new ArmClient(new DefaultAzureCredential());
            _graphClient = graphClient;
        }

        public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node)
        {
            // TODO: remove
            if (node.ResourceName == "large")
            {
                yield break;
            }

            await foreach (var n in base.Crawl(node))
            {
                yield return n;
            }

            var envNode = (ContainerAppEnvironmentNode)node;
            _logger.LogInformation($"Crawling container app environment: {envNode.ResourceId}");

            var rgResourceId = ResourceGroupResource.CreateResourceIdentifier(envNode.SubscriptionId, envNode.ResourceGroupName);
            var rgResource = _armClient.GetResourceGroupResource(rgResourceId);
            if(rgResource == null)
            {
                _logger.LogWarning($"Failed to get container app environment: {envNode.ResourceId}");
                yield break;
            }

            var env = await rgResource.GetContainerAppManagedEnvironmentAsync(envNode.ResourceName);
            
            if(env == null || !env.Value.HasData)
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
                await _dbManager.AddEdgeIfNotExistsAsync(envNode.GetNodeId(), subnetNode.GetNodeId(), "SWIFT_INJECTED");
                var vnetResourceId = subnetResourceId.Parent;
                var vnetNode = new ArmResourceNode(vnetResourceId.ResourceType, vnetResourceId.ToString(), vnetResourceId.SubscriptionId, vnetResourceId.ResourceGroupName, vnetResourceId.Name);
                await _dbManager.AddOrUpdateNodeAsync(vnetNode.GetNodeLabel(), vnetNode.GetNodeId(), vnetNode.GetResourceType(), vnetNode.GetNodeProperties());
                // crawl the whole vnet
                yield return vnetNode;

                var lbId = envNode.LbId;
                var lbResourceId = new ResourceIdentifier(lbId);
                var lbNode = new ArmResourceNode(lbResourceId.ResourceType, lbId, lbResourceId.SubscriptionId, lbResourceId.ResourceGroupName, lbResourceId.Name);
                await _dbManager.AddOrUpdateNodeAsync(lbNode.GetNodeLabel(), lbNode.GetNodeId(), lbNode.GetResourceType(), lbNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(lbNode.GetNodeId(), envNode.GetNodeId(), "INGRESS_CONNECTED");
                yield return lbNode;
            }

            // LA

            // container apps
            var queryResult = await _graphClient.Query([envNode.SubscriptionId], $"resources|where type =~ 'Microsoft.App/containerApps' and properties.environmentId =~ '{envNode.ResourceId}'| project id, type, subscriptionId, resourceGroup, name");

            _logger.LogInformation($"Find {queryResult.Count} container apps under environment");
            var jsonObj = JsonSerializer.Deserialize<JsonElement>(queryResult.Data);
            foreach(var item in jsonObj.EnumerateArray())
            {
                var resourceId = item.GetProperty("id").GetString();
                var resourceType = item.GetProperty("type").GetString();
                var subscriptionId = item.GetProperty("subscriptionId").GetString();
                var resourceGroupName = item.GetProperty("resourceGroup").GetString();
                var resourceName = item.GetProperty("name").GetString();
                var containerAppNode = new ArmResourceNode(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName);

                await _dbManager.AddOrUpdateNodeAsync(containerAppNode.GetNodeLabel(), containerAppNode.GetNodeId(), containerAppNode.GetResourceType(), containerAppNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(envNode.GetNodeId(), containerAppNode.GetNodeId(), "CONTAINS");
                yield return containerAppNode;
            }

            yield break;
        }
    }

    public class AzureResourceGraphClient
    {
        private readonly ArmClient _client;
        private TenantResource _tenantResource;

        public AzureResourceGraphClient(IConfiguration configuration)
        {
            _client = new ArmClient(new DefaultAzureCredential());
            InitTenantResource(configuration["AppSettings:Core:Azure:Crawler:TenantId"]);
        }

        public void InitTenantResource(string tenantId)
        {
            foreach (var pages in _client.GetTenants().GetAll().AsPages())
            {
                foreach (var tenant in pages.Values)
                {
                    if (tenant.HasData && tenant.Data.TenantId.HasValue && tenant.Data.TenantId == new Guid(tenantId))
                    {
                        _tenantResource = tenant!;
                        return;
                    }
                }
            }
        }

        public async Task<ResourceQueryResult> Query(IList<string> subscriptions, string query)
        {
            var request = new ResourceQueryContent(query);
            foreach (var sub in subscriptions)
            {
                request.Subscriptions.Add(sub);
            }

            var result = await _tenantResource.GetResourcesAsync(request);

            return result;
        }
    }
}

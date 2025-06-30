// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ApiManagement;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class APIManagementCrawler : GenericArmResourceCrawler
    {
        private readonly ILogger<APIManagementCrawler> _logger;
        private readonly IGraphDatabaseClient _graphDbClient;
        private readonly AzureResourceGraphClient _graphClient;

        public APIManagementCrawler(ILogger<APIManagementCrawler> logger, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
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
            var apiManagementNode = (APIManagementNode)node;
            _logger.LogDebug($"Crawling API Management {apiManagementNode.ResourceId}");

            var armResourceId = new ResourceIdentifier(apiManagementNode.ResourceId);
            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);

            var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
            if (resourceGroup == null)
            {
                _logger.LogInternalWarning($"Failed to get Resource Group: {resourceGroupId} for API management instance: {apiManagementNode.ResourceId}");
                yield break;
            }

            var apiManagementResponse = await resourceGroup.GetApiManagementServiceAsync(armResourceId.Name);
            if (apiManagementResponse == null || !apiManagementResponse.Value.HasData)
            {
                _logger.LogInternalWarning($"Failed to get API management instance: {apiManagementNode.ResourceId}");
                yield break;
            }

            var apiManagementInstance = apiManagementResponse.Value.Data;
            apiManagementNode.PopulateFromApiManagementServiceData(apiManagementInstance);

            // Create or update the API Management node in the graph database
            await _graphDbClient.AddOrUpdateNodeAsync(apiManagementNode);

            await foreach (var connectedNode in ExtractNetworkConnections(apiManagementNode))
            {
                _logger.LogDebug($"Discovered network connection API Management instance: {apiManagementNode.ResourceName} | Connected node: {connectedNode}");
            }
        }

        private async IAsyncEnumerable<GraphNode> ExtractNetworkConnections(APIManagementNode apiManagementNode)
        {
            if (!string.IsNullOrWhiteSpace(apiManagementNode.SubnetResourceId))
            {
                var subnetResourceId = new ResourceIdentifier(apiManagementNode.SubnetResourceId);
                var vnetResourceId = subnetResourceId.Parent;

                if (vnetResourceId != null)
                {
                    // Create VNet node
                    var vnetNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(vnetResourceId.ToString());
                    await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);

                    // Connect APIM -> VNet
                    var apimToVnetEdge = new ArmResourceEdge(apiManagementNode.GetNodeId(), vnetNode.GetNodeId(), Constants.Relationships.Connected);
                    apimToVnetEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.ConnectionTypeNetwork);
                    await _graphDbClient.AddOrUpdateEdgeAsync(apimToVnetEdge);
                    _logger.LogDebug($"Connected API Management {apiManagementNode.ResourceName} to VNet {vnetNode.ResourceName}");
                    yield return vnetNode;

                    // Create Subnet node
                    var subnetNode = new ArmResourceNode(
                        Constants.VirtualNetworkType,
                        apiManagementNode.SubnetResourceId,
                        subnetResourceId.SubscriptionId,
                        subnetResourceId.ResourceGroupName,
                        subnetResourceId.Name,
                        apiManagementNode.Location);

                    await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);

                    // Connect VNet -> Subnet
                    var vnetToSubnetEdge = new ArmResourceEdge(vnetNode.GetNodeId(), subnetNode.GetNodeId(), Constants.Relationships.Connected);
                    vnetToSubnetEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.ConnectionTypeNetwork);
                    await _graphDbClient.AddOrUpdateEdgeAsync(vnetToSubnetEdge);
                    _logger.LogDebug($"Connected VNet {vnetNode.ResourceName} to Subnet {subnetNode.ResourceName}");
                    yield return subnetNode;
                }
            }
        }
    }
}

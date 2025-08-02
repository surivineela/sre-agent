// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
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
            _logger.LogInternalInformation($"Crawling API Management {apiManagementNode.ResourceId}");

            var armResourceId = new ResourceIdentifier(apiManagementNode.ResourceId ?? string.Empty);
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

            apiManagementNode.PopulateFromApiManagementServiceResource(apiManagementResponse.Value);

            // Create or update the API Management node in the graph database
            await _graphDbClient.AddOrUpdateNodeAsync(apiManagementNode);

            await foreach (var connectedNode in ExtractNetworkConnections(apiManagementNode))
            {
                _logger.LogDebug($"Discovered network connection API Management instance: {apiManagementNode.ResourceName} | Connected node: {connectedNode}");
                yield return connectedNode;
            }

            await foreach (var backendNode in ExtractBackendConnections(apiManagementNode))
            {
                _logger.LogDebug($"Discovered Azure backend connection API Management instance: {apiManagementNode.ResourceName} | Backend node: {backendNode}");
                yield return backendNode;
            }
        }
        private async IAsyncEnumerable<GraphNode> ExtractNetworkConnections(APIManagementNode apiManagementNode)
        {
            if (!string.IsNullOrWhiteSpace(apiManagementNode.SubnetResourceId))
            {
                var subnetResourceId = new ResourceIdentifier(apiManagementNode.SubnetResourceId);
                var vnetResourceId = subnetResourceId.Parent;

                if (vnetResourceId is not null)
                {
                    // Create VNet node
                    var vnetNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(vnetResourceId.ToString())!;
                    await _graphDbClient.AddOrUpdateNodeAsync(vnetNode);

                    // Connect APIM -> VNet
                    var apimToVnetEdge = new ArmResourceEdge(apiManagementNode.GetNodeId() ?? string.Empty, vnetNode.GetNodeId() ?? string.Empty, Constants.Relationships.Connected);
                    apimToVnetEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.ConnectionTypeNetwork);
                    await _graphDbClient.AddOrUpdateEdgeAsync(apimToVnetEdge);
                    _logger.LogDebug($"Connected API Management {apiManagementNode.ResourceName} to VNet {vnetNode.ResourceName}");
                    yield return vnetNode;

                    // Create Subnet node
                    var subnetNode = new ArmResourceNode(
                        Constants.VirtualNetworkType,
                        apiManagementNode.SubnetResourceId,
                        subnetResourceId.SubscriptionId!,
                        subnetResourceId.ResourceGroupName!,
                        subnetResourceId.Name,
                        apiManagementNode.Location);

                    await _graphDbClient.AddOrUpdateNodeAsync(subnetNode);

                    // Connect VNet -> Subnet
                    var vnetToSubnetEdge = new ArmResourceEdge(vnetNode.GetNodeId() ?? string.Empty, subnetNode.GetNodeId() ?? string.Empty, Constants.Relationships.Connected);
                    vnetToSubnetEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.ConnectionTypeNetwork);
                    await _graphDbClient.AddOrUpdateEdgeAsync(vnetToSubnetEdge);
                    _logger.LogDebug($"Connected VNet {vnetNode.ResourceName} to Subnet {subnetNode.ResourceName}");
                    yield return subnetNode;
                }
            }
        }

        private async IAsyncEnumerable<GraphNode> ExtractBackendConnections(APIManagementNode apiManagementNode)
        {
            if (apiManagementNode.BackendResourceMap != null)
            {
                foreach (var kvp in apiManagementNode.BackendResourceMap)
                {
                    var backendName = kvp.Key;
                    var backendInfo = kvp.Value;
                    if (string.IsNullOrEmpty(backendInfo.BackendResourceId))
                        continue;

                    var resourceIdObj = new ResourceIdentifier(backendInfo.BackendResourceId);
                    var apimBackendNode = new APIManagementBackendNode(
                        resourceIdObj.ResourceType,
                        backendInfo.BackendResourceId,
                        resourceIdObj.SubscriptionId!,
                        resourceIdObj.ResourceGroupName!,
                        resourceIdObj.Name,
                        resourceIdObj.Location!
                    );

                    apimBackendNode.PopulateAPIMBackendResource(backendInfo);
                    await _graphDbClient.AddOrUpdateNodeAsync(apimBackendNode);

                    // Connect APIM -> Backend
                    var apimToBackendEdge = new ArmResourceEdge(apiManagementNode.GetNodeId() ?? string.Empty, apimBackendNode.GetNodeId() ?? string.Empty, Constants.Relationships.Connected);
                    apimToBackendEdge.AddOrUpdateEdgeProperty(Constants.ConnectionType, Constants.APIManagementBackend);
                    await _graphDbClient.AddOrUpdateEdgeAsync(apimToBackendEdge);
                    _logger.LogDebug($"Connected API Management {apiManagementNode.ResourceName} to Backend {apimBackendNode.ResourceName}");
                    yield return apimBackendNode;
                }
            }
        }
    }
}
